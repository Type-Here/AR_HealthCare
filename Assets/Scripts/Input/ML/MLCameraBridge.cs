using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Android;
using UnityEngine.XR.MagicLeap;
using MagicLeap.Android;
using System.Collections.Generic;

namespace ARHealthCare.Input
{
    public enum MLCameraResolution
    {
        R640x480,
        R1280x720,
        R1920x1080
    }

    /// <summary>
    /// MLCameraBridge
    /// -------------
    /// Provides a simple bridge between Magic Leap's MLCamera API and MediaPipe (or any other CV pipeline)
    /// by exposing the latest RGBA frame as a Unity Texture2D.
    ///
    /// Key points:
    /// - Uses Android runtime permission for the standard camera permission (android.permission.CAMERA).
    /// - Connects to MLCamera (Main or CV) and starts VIDEO capture in RGBA_8888 format.
    /// - Updates a reusable Texture2D on each OnRawVideoFrameAvailable callback.
    ///
    /// Notes:
    /// - If you later need Eye/Depth permissions, request those separately using the Magic Leap permission APIs,
    ///   but for RGB video capture this script should rely on the standard Android CAMERA permission.
    /// - Consider throttling or dropping frames if your CV pipeline can't keep up with the camera framerate.
    /// </summary>
    public class MLCameraBridge : MonoBehaviour
    {
        [Header("Capture Target")]
        [SerializeField] private MLCameraResolution resolution = MLCameraResolution.R640x480;
        
        private int captureWidth = 640;
        private int captureHeight = 480;

        [Tooltip("Main = RGB POV. CV = stream for Computer Vision.")]
        [SerializeField] private MLCamera.Identifier cameraId = MLCamera.Identifier.CV;
        
        [SerializeField] private MLCamera.CaptureFrameRate frameRate = MLCamera.CaptureFrameRate._30FPS;

        [Header("Runtime")]
        public Texture CurrentCameraTexture { get; private set; }
        
        public bool IsReady => CurrentCameraTexture != null;
        public bool IsCapturing => _isCapturing;
        private Texture2D _videoTextureRgba;
        private MLCamera _camera;
        private bool _cameraDeviceAvailable;
        private bool _isCapturing;

        private Dictionary<MLCameraResolution, (int, int)> _resolutionMap = new Dictionary<MLCameraResolution, (int, int)>
        {
            { MLCameraResolution.R640x480, (640, 480) },
            { MLCameraResolution.R1280x720, (1280, 720) },
            { MLCameraResolution.R1920x1080, (1920, 1080) }
        };

        private void OnEnable()
        {
            RequestCameraPermission();
            if (_resolutionMap.TryGetValue(resolution, out var dims))
            {
                captureWidth = dims.Item1;
                captureHeight = dims.Item2;
            }
            else
            {
                Debug.LogWarning($"MLCameraBridge: risoluzione non riconosciuta, uso default 640x480.");
                captureWidth = 640;
                captureHeight = 480;
            }
        }

          private void OnDisable()
        {
            StopCaptureAndDisconnect();
        }

        private void RequestCameraPermission()
        {
            if (Permissions.CheckPermission(Permission.Camera))
            {
                StartCoroutine(ConnectAndStart());
                return;
            }

            var callbacks = new PermissionCallbacks();
            callbacks.PermissionGranted += _ => StartCoroutine(ConnectAndStart());
            callbacks.PermissionDenied += _ => Debug.LogError("MLCameraBridge: CAMERA denied");
            //PermissionDeniedAndDontAskAgain is Obsolete in favor of PermissionDenied
            //callbacks.PermissionDeniedAndDontAskAgain += _ => Debug.LogError("MLCameraBridge: CAMERA denied and don't ask again");
            Permission.RequestUserPermission(Permission.Camera, callbacks);
        }

        private IEnumerator ConnectAndStart()
        {
            // Wait for camera device availability
            while (!_cameraDeviceAvailable)
            {
                MLResult result = MLCamera.GetDeviceAvailabilityStatus(cameraId, out _cameraDeviceAvailable);
                if (!result.IsOk || !_cameraDeviceAvailable)
                    yield return new WaitForSeconds(1.0f);
            }

            // Connect to camera
            var ctx = MLCamera.ConnectContext.Create();
            ctx.CamId = cameraId;
            ctx.Flags = MLCamera.ConnectFlag.CamOnly; // “camera only”
            ctx.EnableVideoStabilization = true;

            _camera = MLCamera.CreateAndConnect(ctx);
            if (_camera == null)
            {
                Debug.LogError("MLCameraBridge: CreateAndConnect ha restituito null.");
                yield break;
            }

            // Callback frame
            _camera.OnRawVideoFrameAvailable += OnRawVideoFrameAvailable;
            // TODO: TOCHECK: Thread safety: OnRawVideoFrameAvailable could come from non-main thread. 
            // If these strange crashes occur, do this: 
            // in the callback copy the bytes to a buffer and apply LoadRawTextureData/Apply in Update() on the main thread.

            // Configure and start RGBA video capture
            ConfigureAndStartVideo();
        }

        private void ConfigureAndStartVideo()
        {
            var caps = MLCamera.GetImageStreamCapabilitiesForCamera(_camera, MLCamera.CaptureType.Video);
            if (caps == null || caps.Length == 0)
            {
                Debug.LogError("MLCameraBridge: nessuna StreamCapability per Video.");
                return;
            }

            var capability = caps[0];
            if (MLCamera.TryGetBestFitStreamCapabilityFromCollection(
                    caps, captureWidth, captureHeight, MLCamera.CaptureType.Video, out var selected))
            {
                capability = selected;
            }

            var captureConfig = new MLCamera.CaptureConfig
            {
                CaptureFrameRate = frameRate,
                StreamConfigs = new[]
                {
                    MLCamera.CaptureStreamConfig.Create(capability, MLCamera.OutputFormat.RGBA_8888)
                }
            };

            var prep = _camera.PrepareCapture(captureConfig, out _);
            if (!prep.IsOk)
            {
                Debug.LogError($"MLCameraBridge: PrepareCapture fallita: {prep}");
                return;
            }

            _camera.PreCaptureAEAWB();

            var start = _camera.CaptureVideoStart();
            _isCapturing = MLResult.DidNativeCallSucceed(start.Result, nameof(_camera.CaptureVideoStart));
            if (!_isCapturing)
                Debug.LogError($"MLCameraBridge: CaptureVideoStart fallita: {start}");
        }

        private void StopCaptureAndDisconnect()
        {
            if (_camera != null)
            {
                try
                {
                    _camera.OnRawVideoFrameAvailable -= OnRawVideoFrameAvailable;

                    if (_isCapturing)
                        _camera.CaptureVideoStop();

                    _camera.Disconnect();
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"MLCameraBridge: errore in Stop/Disconnect: {e.Message}");
                }
                finally
                {
                    _isCapturing = false;
                    _camera = null;
                }
            }

            if (_videoTextureRgba != null)
            {
                Destroy(_videoTextureRgba);
                _videoTextureRgba = null;
                CurrentCameraTexture = null;
            }
        }

        private void OnRawVideoFrameAvailable(MLCamera.CameraOutput output, MLCamera.ResultExtras extras, MLCameraBase.Metadata metadata)
        {
            if (output.Format != MLCamera.OutputFormat.RGBA_8888)
                return;

            // Evita immagine capovolta
            MLCamera.FlipFrameVertically(ref output);

            var plane = output.Planes[0];
            UpdateRGBATexture(ref _videoTextureRgba, plane);

            CurrentCameraTexture = _videoTextureRgba;
        }

        private static void UpdateRGBATexture(ref Texture2D tex, MLCamera.PlaneInfo plane)
        {
            int w = (int)plane.Width;
            int h = (int)plane.Height;

            if (tex != null && (tex.width != w || tex.height != h))
            {
                UnityEngine.Object.Destroy(tex);
                tex = null;
            }

            if (tex == null)
            {
                tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
                {
                    wrapMode = TextureWrapMode.Clamp,
                    filterMode = FilterMode.Bilinear
                };
            }

            // Gestione stride/pixelStride come negli esempi ML
            int actualRowBytes = (int)(plane.Width * plane.PixelStride);
            if (plane.Stride != actualRowBytes)
            {
                var packed = new byte[actualRowBytes * plane.Height];
                for (int row = 0; row < plane.Height; row++)
                {
                    Buffer.BlockCopy(plane.Data, (int)(row * plane.Stride), packed, row * actualRowBytes, actualRowBytes);
                }
                tex.LoadRawTextureData(packed);
            }
            else
            {
                tex.LoadRawTextureData(plane.Data);
            }

            tex.Apply(false);
        }
    }
}
