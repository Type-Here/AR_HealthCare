using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using Unity.Collections;
using System.Reflection;

namespace ARHealthCare.Input
{   

    public enum CpuAcquisitionMode
    {
        Sync, Async
    }


    [RequireComponent(typeof(ARCameraManager))]
    /* Remember to add the ARCameraManager component to your AR Camera in the scene*/

    /**
     * ARFCameraBridge
     * Summary:
     * Bridge between AR Foundation Camera and MediaPipe Runner
     * Description:
     * This script captures the camera feed from the AR Foundation's ARCameraManager
     * and makes it available as a Texture for the MediaPipe PoseLandmarkerRunner to process.
     * 
     * Attach this script to an empty GameObject in the scene and link the ARCameraManager
     * component from your AR Camera.
     * 
     */
    public class ARFCameraBridge : MonoBehaviour
    {
        [Tooltip("Trascina qui il tuo AR Camera Manager")]
        [SerializeField] private ARCameraManager cameraManager;

        [SerializeField, Tooltip("Fattore di downscale per la CPU (1 = full res, 2 = metà, 3 = un terzo, ...).")]
        private int cpuDownscale = 2;

        [Tooltip("Se true proverà ad usare la texture GPU quando possibile (migliore performance). Not Implemented yet.")]
        [SerializeField] private bool preferGpu = false;

        [Tooltip("Modalità di acquisizione CPU: Sync (bloccante) o Async (non blocca il main).")]
        [SerializeField] private CpuAcquisitionMode cpuMode = CpuAcquisitionMode.Async;
        
        // Texture used in the CPU path
        private Texture2D cpuTexture2D;

        // Texture to be read by the MediaPipe Runner
        public Texture CurrentCameraTexture { get; private set; }

        // GPU detection state
        private bool usingGpu = false;
        private Texture gpuCameraTexture = null;
        private bool gpuDetectionLogged = false;

        // CPU path persistent buffer
        private NativeArray<byte> persistentBuffer;
        private int persistentBufferSize = 0;

        // Async conversion state
        private XRCpuImage.AsyncConversion asyncRequest;
        private bool hasAsyncRequest = false;
        private int asyncWidth;
        private int asyncHeight;

        void OnEnable()
        {   
            if (cameraManager == null)
                cameraManager = GetComponent<ARCameraManager>();

              DetectGPUReadback();

            if (cameraManager != null)
            {
                // Subscribe to the event that is called when a new frame is ready
                cameraManager.frameReceived += OnCameraFrameReceived;
            }
        }

        void OnDisable()
        {
            // Unsubscribe from the event when the script is disabled
            if (cameraManager != null)
            {
                cameraManager.frameReceived -= OnCameraFrameReceived;
            }

            // Dispose of persistent buffer if allocated
            if (persistentBuffer.IsCreated)
            {
                persistentBuffer.Dispose();
                persistentBufferSize = 0;
            }

            // Clean up CPU texture
            if (cpuTexture2D != null)
            {
                Destroy(cpuTexture2D);
                cpuTexture2D = null;
            }

            // Dispose of any pending async request if enabled
            if (hasAsyncRequest)
            {
                asyncRequest.Dispose();
                hasAsyncRequest = false;
            }
        }

        void Update()
        {
            // If we are in async mode, process the results here when ready
            if (cpuMode == CpuAcquisitionMode.Async)
            {
                ProcessAsyncConversionIfReady();
            }
        }

        #region GPU detection

        /// <summary>
        /// Detects if we can use GPU readback from ARFoundation. <br/>
        /// If successful, sets the usingGpu flag and gpuCameraTexture. <br/>
        /// This method attempts to access the ARCameraBackground's material
        /// to retrieve the GPU texture used for rendering the camera feed. <br/>
        /// 
        /// If <b>successful</b>, it enables the GPU path for camera frame acquisition.
        /// </summary>
        /// 
        private void DetectGPUReadback()
        {
            usingGpu = false;
            gpuCameraTexture = null;
            gpuDetectionLogged = false;

            /* --- Preliminary checks --- */

            if(!preferGpu)
            {
                Debug.Log("ARFCameraBridge: preferGpu è disabilitato da utente, uso CPU.");
                return;
            }

            if(!SystemInfo.supportsAsyncGPUReadback)
            {
                Debug.Log("ARFCameraBridge: Async GPU Readback non supportato, uso CPU.");
                return;
            }

            if(cameraManager == null)
            {
                Debug.LogWarning("ARFCameraBridge: ARCameraManager non assegnato!");
                return;
            }

            // Try to get the Camera component
            Camera cam = cameraManager.GetComponent<Camera>();
            if (cam == null)
            {
                Debug.LogWarning("ARFCameraBridge: Nessuna Camera trovata sul GameObject di ARCameraManager.");
                return;
            }

            // Try to get the ARCameraBackground component
            var background = cam.GetComponent<ARCameraBackground>();
            if (background == null)
            {
                Debug.LogWarning("ARFCameraBridge: Nessun componente ARCameraBackground trovato. Fallback CPU.");
                return;
            }

            /* ---- Try to get the GPU texture from the ARCameraManager ---- */

            try
            {   // Using reflection to access the non-public 'material' property needed to get the texture
                var bgType = background.GetType();
                var prop = bgType.GetProperty("material", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public) 
                                ?? bgType.GetProperty("m_Material", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                var mat = prop.GetValue(background) as Material;

                if(mat == null)
                {
                    Debug.LogWarning("ARFCameraBridge: Impossibile ottenere il materiale da ARCameraBackground. Fallback CPU.");
                    return;
                }

                gpuCameraTexture = mat.mainTexture;
                // Alternative texture property names
                if(gpuCameraTexture == null)
                {
                     string[] keys = { "_MainTex", "_BaseMap", "_Tex" };
                        foreach (var k in keys)
                        {
                            var t = mat.GetTexture(k);
                            if (t != null)
                            {
                                gpuCameraTexture = t;
                                break;
                            }
                        }
                }
            } catch (System.Exception e)
            {
                Debug.LogWarning($"ARFCameraBridge: Errore durante l'accesso alla texture GPU: {e.Message}. Fallback CPU.");
                return;
            }


            if(gpuCameraTexture != null)
            {   
                // Success: GPU texture obtained
                usingGpu = true;
                Debug.Log("ARFCameraBridge: Percorso GPU abilitato.");
            } else {
                Debug.LogWarning("ARFCameraBridge: Material/texture GPU non ottenuta.");
                Debug.LogWarning("ARFCameraBridge: Impossibile ottenere la texture GPU. Fallback CPU.");
            }


            // GPU detection completed
            gpuDetectionLogged = true;
        }

        #endregion

        /// <summary>
        /// Callback for when a new camera frame is received from AR Foundation.
        /// Converts the <em>XRCameraImage</em> to a <em>Texture2D</em> that can be used by MediaPipe.
        /// </summary>
        /// <param name="eventArgs">
        /// The event arguments containing the camera frame data.
        /// </param>
        private void OnCameraFrameReceived(ARCameraFrameEventArgs eventArgs)
        {   

            if (cameraManager == null)
                return;


            if (usingGpu && gpuCameraTexture != null) ConvertViaGPU();
            else if (cpuMode == CpuAcquisitionMode.Async) HandleCpuFrameAsync(); //Async path
            else HandleCpuFrameSync(); //Sync path
        }

        #region GPU Path

        /// <summary>
        /// Converts the camera frame using GPU. <br/>
        /// FASTEST METHOD <br/>
        /// Simply assigns the GPU texture from ARFoundation to the CurrentCameraTexture property. <br/>
        /// </summary>
        private void ConvertViaGPU()
        {
            // Directly use the GPU texture (No conversion or copy needed)
            CurrentCameraTexture = gpuCameraTexture;

            // Log once that we are using GPU
            if (!gpuDetectionLogged)
            {
                Debug.Log("ARFCameraBridge: Usando il percorso GPU per la fotocamera.");
                gpuDetectionLogged = true;
            }
        }

        #endregion

        #region CPU Sync Path

        /// <summary>
        /// Converts the camera frame using CPU. <em>SLOWER METHOD</em> <br/>
        /// Acquires the latest camera image and converts it to a Texture2D using CPU methods. <br/>
        /// </summary>
        private void HandleCpuFrameSync()
        {
            // Acquire the latest camera image from the subsystem
            if (!cameraManager.TryAcquireLatestCpuImage(out XRCpuImage cpuImage))
            {
                return;
            }

            try
            {
                // Calculate the output dimensions based on downscale factor
                int outWidth  = cpuImage.width  / Mathf.Max(1, cpuDownscale);
                int outHeight = cpuImage.height / Mathf.Max(1, cpuDownscale);

                // Option 1: Synchronous copy (SLOW)
                var conversionParams = new XRCpuImage.ConversionParams
                {
                    // Area of the image to convert (entire image)
                    inputRect = new RectInt(0, 0, cpuImage.width, cpuImage.height),
                    // Dimensions of the output (same resolution)
                    outputDimensions = new Vector2Int(cpuImage.width, cpuImage.height),
                    // Output format
                    outputFormat = TextureFormat.RGBA32,
                    // Often you need at least MirrorY to align with what the camera sees
                    transformation = XRCpuImage.Transformation.MirrorY
                    //TODO: Check MirrorY is needed in our case
                    
                };

                
                int size = cpuImage.GetConvertedDataSize(conversionParams);

                EnsurePersistentBuffer(size); // Ensure buffer is allocated
                cpuImage.Convert(conversionParams, persistentBuffer); // Synchronous conversion
                UpdateCpuTextureFromBuffer(outWidth, outHeight); // Update the Texture2D from buffer

                usingGpu = false;
            }
            finally
            {
            // Dispose of the CPU image
            cpuImage.Dispose();
            }
        }

        #endregion

        
        #region CPU async path

        /// <summary>
        /// Handles the asynchronous acquisition and conversion of the camera frame using CPU. <br/>
        /// NON-BLOCKING METHOD <br/>   
        /// Initiates an async request to convert the camera image to a Texture2D. <br/>
        /// 
        /// </summary>
        private void HandleCpuFrameAsync()
        {
            // If there is already an ongoing async request, do not start a new one
            if (hasAsyncRequest)
                return;
            
            // Acquire the latest camera image from the subsystem
            if (!cameraManager.TryAcquireLatestCpuImage(out XRCpuImage cpuImage))
                return;

            // Setup conversion parameters
            int outWidth = cpuImage.width / Mathf.Max(1, cpuDownscale);
            int outHeight = cpuImage.height / Mathf.Max(1, cpuDownscale);

            var conversionParams = new XRCpuImage.ConversionParams
            {
                inputRect = new RectInt(0, 0, cpuImage.width, cpuImage.height),
                outputDimensions = new Vector2Int(outWidth, outHeight),
                outputFormat = TextureFormat.RGBA32,
                transformation = XRCpuImage.Transformation.MirrorY
            };

            // Start async request
            asyncRequest = cpuImage.ConvertAsync(conversionParams);
            hasAsyncRequest = true;
            asyncWidth = outWidth;
            asyncHeight = outHeight;

            // It is safe to dispose immediately, according to the documentation
            cpuImage.Dispose();
        }

        /// <summary>
        /// Processes the result of the asynchronous conversion if it is ready. <br/>
        /// Once the conversion is complete, updates the CurrentCameraTexture property. <br/>
        /// </summary>
        private void ProcessAsyncConversionIfReady()
        {
            if (!hasAsyncRequest)
                return;

            if (!asyncRequest.status.IsDone())
                return;

            if (asyncRequest.status != XRCpuImage.AsyncConversionStatus.Ready)
            {
                Debug.LogWarning($"ARFCameraBridge: Async conversion failed: {asyncRequest.status}");
                asyncRequest.Dispose();
                hasAsyncRequest = false;
                return;
            }

            // When ready, get the data
            var rawData = asyncRequest.GetData<byte>(); // NativeArray<byte>

            EnsurePersistentBuffer(rawData.Length);
            persistentBuffer.CopyFrom(rawData);

            asyncRequest.Dispose();
            hasAsyncRequest = false;

            UpdateCpuTextureFromBuffer(asyncWidth, asyncHeight);

            usingGpu = false;
        }

        #endregion

        #region Helpers CPU

        /// <summary>
        /// Ensures the persistent buffer is allocated and of the correct size. <br/>
        /// If not, allocates or resizes it accordingly. <br/>
        /// </summary>
        /// <param name="size">
        /// The required size of the buffer in bytes.
        /// </param>
        private void EnsurePersistentBuffer(int size)
        {
            if (!persistentBuffer.IsCreated || persistentBufferSize != size)
            {
                if (persistentBuffer.IsCreated)
                    persistentBuffer.Dispose();

                persistentBuffer = new NativeArray<byte>(size, Allocator.Persistent,
                    NativeArrayOptions.UninitializedMemory);
                persistentBufferSize = size;
            }
        }


        /// <summary>
        /// Updates or creates the CPU Texture2D from the persistent buffer data. <br/>
        /// Sets the CurrentCameraTexture property to the updated texture. <br/>
        /// </summary>
        /// <param name="width">
        /// The width of the texture.
        /// </param>
        /// <param name="height">
        /// The height of the texture.
        /// </param>
        private void UpdateCpuTextureFromBuffer(int width, int height)
        {
            if (cpuTexture2D == null || cpuTexture2D.width != width || cpuTexture2D.height != height)
            {
                if (cpuTexture2D != null)
                    Destroy(cpuTexture2D);

                cpuTexture2D = new Texture2D(width, height, TextureFormat.RGBA32, false)
                {
                    wrapMode = TextureWrapMode.Clamp,
                    filterMode = FilterMode.Bilinear
                };
            }

            cpuTexture2D.LoadRawTextureData(persistentBuffer);
            cpuTexture2D.Apply(false);

            CurrentCameraTexture = cpuTexture2D;
        }

        #endregion


    }
}