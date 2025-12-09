// This file is modified from the original file in the MediaPipeUnityPlugin
// Adapted to use Modified ARFoundationImageSource with ARFCameraBridge script
// Modified by: Type-Here

// Following notice applies to the original file:
// Copyright (c) 2021 homuler
//
// Use of this source code is governed by an MIT-style
// license that can be found in the LICENSE file or at
// https://opensource.org/licenses/MIT.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Mediapipe.Unity;
using UnityEngine.XR.ARFoundation;

#if UNITY_ANDROID
using UnityEngine.Android;
#endif

namespace ARHealthCare.Input
{
    /// <summary>
    /// ARFImageSource is an ImageSource implementation that provides camera textures from
    /// Unity's AR Foundation pipeline via an ARFCameraBridge helper.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This class adapts an AR Foundation camera feed into the ImageSource abstraction expected
    /// by the surrounding media/processing framework. It is intended for use where an application
    /// (for example, a MediaPipe/vision processing pipeline) needs a Texture representing the
    /// live AR camera image rather than a conventional webcam feed.
    /// </para>
    /// <br/>
    /// <b>Key responsibilities:</b>
    /// <br/>
    /// <list type="bullet">
    ///   <item><description>Expose the current camera Texture through GetCurrentTexture and the textureWidth/textureHeight properties.</description></item>
    ///   <item><description>Provide a simple lifecycle (Play, Resume, Pause, Stop) to coordinate enabling/disabling the ARCameraManager.</description></item>
    ///   <item><description>Provide a logical "available resolution" based on the active AR camera texture (or a safe fallback).</description></item>
    ///   <item><description>Handle permission gating in a cross-platform-friendly way (for AR Foundation, the XR runtime typically manages permissions; this implementation assumes permission is granted but uses an internal gate to preserve the original lifecycle semantics).</description></item>
    ///   <item><description>Allow the ARFCameraBridge and ARCameraManager references to be assigned via the inspector or auto-discovered at runtime.</description></item>
    /// </list>
    /// <br/>
    /// <b>Important behavior and assumptions:</b>
    /// <br/>
    /// <list type="bullet">
    ///   <item><description>This implementation assumes the AR feed is the (rear/world-facing) camera. The <see cref="isFrontFacing"/>, <see cref="isVerticallyFlipped"/>, and <see cref="rotation"/> properties reflect that assumption (false, false, Rotation0). If target devices or specific AR subsystems use different conventions, additional transformation handling may be required.</description></item>
    ///   <item><description>Texture dimensions (textureWidth/textureHeight) return 0 when the source is not prepared (no valid texture available).</description></item>
    ///   <item><description>The class uses a single logical resolution derived from the current texture. If the texture is not yet available, a default fallback resolution (1280x720 @ 30fps) is returned.</description></item>
    ///   <item><description>SelectSource only supports a single source id (0) corresponding to the AR Camera and will throw an <see cref="ArgumentException"/> for other values.</description></item>
    ///   <item><description>Play and Resume are implemented as coroutines to accommodate asynchronous initialization and to wait for the bridge texture to become available.</description></item>
    ///   <item><description>Permission handling is simplified to always allow access for AR Foundation platforms (the XR runtime typically performs actual checks).</description></item>
    ///   <item><description>The constructor parameter <paramref name="preferableDefaultWidth"/> is used by an internal resolution comparer to choose the best default resolution when multiple resolutions become available.</description></item>
    /// </list>
    /// <br/>
    /// <b>Threading and coroutine usage:</b>
    /// <br/>
    /// <list type="bullet">
    ///   <item><description>Initialize/Play/Resume are implemented as IEnumerator coroutines and must be invoked via StartCoroutine or equivalent so they can yield while waiting for permissions or texture availability.</description></item>
    ///   <item><description>Access to texture and most properties is safe on the Unity main thread. The class does not attempt to marshal calls to other threads — consume textures and properties from the main thread or follow Unity's thread-safety rules.</description></item>
    /// </list>
    /// <br/>
    /// <b>Typical usage:</b>
    /// <br/>
    /// <list type="bullet">
    ///   <item><description>Add ARFImageSource to a GameObject or construct it via code, assign an ARFCameraBridge and (optionally) an ARCameraManager. If those references are not set, they will be auto-located during initialization.</description></item>
    ///   <item><description>Start the source by calling StartCoroutine(imageSource.Play()). Wait for the coroutine to complete; when it returns, the AR bridge should be actively providing textures.</description></item>
    ///   <item><description>Poll or retrieve the current Texture with GetCurrentTexture() each frame (or pass it to downstream processing).</description></item>
    ///   <item><description>Pause() or Stop() to halt processing. Resume() can be used to re-enable the feed (also a coroutine).</description></item>
    /// </list>
    ///
    /// Example:
    /// <code>
    /// // Assume 'imageSource' is an instance of ARFImageSource assigned in the inspector or created in code.
    /// // Start playback (in a MonoBehaviour):
    /// yield return StartCoroutine(imageSource.Play());
    /// 
    /// if (imageSource.isPrepared)
    /// {
    ///     Texture current = imageSource.GetCurrentTexture();
    ///     // Use the texture for processing/rendering.
    /// }
    /// 
    /// // To pause:
    /// imageSource.Pause();
    /// 
    /// // To resume:
    /// yield return StartCoroutine(imageSource.Resume());
    /// 
    /// // To stop and disable the ARCameraManager:
    /// imageSource.Stop();
    /// </code>
    ///
    /// Exceptions:
    /// - <see cref="ArgumentException"/> is thrown by <c>SelectSource(int)</c> if an invalid source id (anything other than 0) is provided.
    /// - <see cref="InvalidOperationException"/> may be thrown by <c>Play()</c> if permission to access the camera is not available,
    ///   or by <c>Resume()</c> if the AR camera texture is not yet prepared.
    /// </remarks>
    /// <param name="preferableDefaultWidth">A hint width used when selecting a preferred resolution among candidates (keeps compatibility with prior implementations).</param>
    public class ARFImageSource : ImageSource
    {
        private readonly int _preferableDefaultWidth = 1280;

        private const string _TAG = "ARFImageSource";


        public ARFImageSource(int preferableDefaultWidth)
        {
            _preferableDefaultWidth = preferableDefaultWidth;
        }

        private static readonly object _PermissionLock = new object();
        private static bool _IsPermitted = false;

        // ADDED: Addition to support Bridge
        /* --- AR Foundation Bridge --- */
        [Header("AR Foundation")]

        [SerializeField,
        Tooltip("AR Foundation Camera Bridge da cui prendere le immagini.")]
        private ARFCameraBridge _arBridge;

        [SerializeField,
        Tooltip("AR Camera Manager usato per accedere alla fotocamera AR.")]
        private ARCameraManager _arCameraManager;

        /* --- Options --- */
        [Header("Options")]

        [SerializeField,
        Tooltip("Nome della sorgente mostrato nei menu MediaPipe.")]
        private string _sourceName = "AR Foundation Camera";

        private bool _isPlaying = false;

        // Available resolutions cache
        private ResolutionStruct[] _availableResolutions;


        // --- Internal helper to access the bridge texture ---
        private Texture CurrentTexture => _arBridge != null ? _arBridge.CurrentCameraTexture : null;

        // --- Overrides ---

        // Properties
        public override int textureWidth => !isPrepared ? 0 : CurrentTexture.width;
        public override int textureHeight => !isPrepared ? 0 : CurrentTexture.height;

        // TODO: This properties for now are simplistic, need to be improved
        [Tooltip("True per un'immagine capovolta verticalmente.")]
        public override bool isVerticallyFlipped => false;

        // Assuming rear camera for AR Foundation
        // Magic Leap 2: World-Facing camera -> Not Front-Facing (?)
        // TODO: Check

        [Tooltip("Se la fotocamera è frontale (selfie) o posteriore (world-facing).")]
        public override bool isFrontFacing => false;

        // AR Foundation should already provide the correct orientation, 
        // Eventual rotation should be handled with GetTransformationOptions.
        public override RotationAngle rotation => RotationAngle.Rotation0;

        // Source Info
        public override string sourceName => _sourceName;
        public override string[] sourceCandidateNames => new[] { _sourceName };

        // Available resolutions: for AR foundation we use a single "logical resolution"
        // based on the texture, or a fallback if not ready yet.
        public override ResolutionStruct[] availableResolutions
        {
            get
            {
                if (_availableResolutions == null || _availableResolutions.Length == 0)
                {
                    var tex = CurrentTexture;
                    if (tex != null)
                    {
                        _availableResolutions = new[]
                        {
                        new ResolutionStruct
                        {
                            width  = tex.width,
                            height = tex.height,
                            frameRate = 30, //TODO: Get real framerate if possible
                        }
                    };
                    }
                    else
                    {
                        // Use a default resolution as fallback
                        _availableResolutions = new[]
                        {
                        new ResolutionStruct
                        {
                            width  = 1280,
                            height = 720,
                            frameRate = 30,
                        }
                    };
                    }
                }

                return _availableResolutions;
            }
        }

        public override bool isPrepared => CurrentTexture != null;
        public override bool isPlaying => _isPlaying;

        /// <summary>
        /// Modified from original to support AR Foundation Bridge. 
        /// Kept as Coroutine for consistency.
        /// Initializes the AR Foundation Image Source.
        /// </summary>
        /// <returns>
        /// IEnumerator for coroutine handling initialization.
        /// </returns>
        private IEnumerator Initialize()
        {
            // Request permission if not already granted
            yield return GetPermission();

            if (!_IsPermitted) yield break;

            // Ensure references are set
            if (_arCameraManager == null)
                _arCameraManager = UnityEngine.Object.FindFirstObjectByType<ARCameraManager>();
            if (_arBridge == null)
                _arBridge = UnityEngine.Object.FindFirstObjectByType<ARFCameraBridge>();
        }

        // TODO: CHECK: Is this needed with AR Foundation?
        private IEnumerator GetPermission()
        {
            lock (_PermissionLock)
            {
                if (_IsPermitted) yield break;
                // Added: With ARFoundation, permission handling is done by the XR runtime (ML2 / Quest / etc.).
                // Here we can simply mark permission as granted.
                _IsPermitted = true;
                // Wait one frame to simulate async permission request
                yield return new WaitForEndOfFrame();
            }
        }

        /// <summary>
        /// Selects the camera source by its ID.
        /// For ARFImageSource, only source ID 0 (the AR Camera) is valid.
        /// </summary>
        /// <param name="sourceId">
        /// The ID of the camera source to select. For ARFImageSource, only 0 is valid.
        /// </param>
        /// <exception cref="ArgumentException">
        /// Thrown when an invalid source ID is provided. ARFImageSource supports only source 0 (AR Camera).
        /// </exception>
        public override void SelectSource(int sourceId)
        {
            // ARFImageSource has only one source (the AR Camera)
            if (sourceId != 0)
            {
                throw new ArgumentException(
                    $"Invalid source ID: {sourceId}. ARFImageSource supports only source 0 (AR Camera).");
            }
        }


        public override IEnumerator Play()
        {
            yield return Initialize();

            if (!_IsPermitted)
            {
                throw new InvalidOperationException("Not permitted to access cameras");
            }

            _isPlaying = true;
            if (_arCameraManager != null)
                _arCameraManager.enabled = true;

            // Wait until ARFCameraBridge is ready
            while (_arBridge == null || _arBridge.CurrentCameraTexture == null)
                yield return null;
        }

        public override IEnumerator Resume()
        {
            if (!isPrepared)
            {
                // Here isPrepared is true only if the ARFCameraBridge has a valid texture 
                // (CurrentTexture != null)
                throw new InvalidOperationException("AR camera is not prepared yet");
            }

            _isPlaying = true;

            if (_arCameraManager != null)
                _arCameraManager.enabled = true;

            // As in Play: ensure the bridge is still providing frames
            while (_arBridge == null || _arBridge.CurrentCameraTexture == null)
                yield return null;
        }

        public override void Pause()
        {
            _isPlaying = false;
        }

        public override void Stop()
        {
            _isPlaying = false;
            if (_arCameraManager != null)
                _arCameraManager.enabled = false;
        }

        public override Texture GetCurrentTexture()
        {
            // Use ARFCameraBridge to get current texture
            return _arBridge != null ? _arBridge.CurrentCameraTexture : null;
        }

        private ResolutionStruct GetDefaultResolution()
        {
            var resolutions = availableResolutions;
            return resolutions == null || resolutions.Length == 0 ? new ResolutionStruct() : resolutions.OrderBy(resolution => resolution, new ResolutionStructComparer(_preferableDefaultWidth)).First();
        }



        /* Helper class to compare resolutions based on preferable width
         * Used to select the best matching resolution from available ones.
         * Kept from original for possible future use.
         */
        private class ResolutionStructComparer : IComparer<ResolutionStruct>
        {
            private readonly int _preferableDefaultWidth;

            public ResolutionStructComparer(int preferableDefaultWidth)
            {
                _preferableDefaultWidth = preferableDefaultWidth;
            }

            public int Compare(ResolutionStruct a, ResolutionStruct b)
            {
                var aDiff = Mathf.Abs(a.width - _preferableDefaultWidth);
                var bDiff = Mathf.Abs(b.width - _preferableDefaultWidth);
                if (aDiff != bDiff)
                {
                    return aDiff - bDiff;
                }
                if (a.height != b.height)
                {
                    // prefer smaller height
                    return a.height - b.height;
                }
                // prefer smaller frame rate
                return (int)(a.frameRate - b.frameRate);
            }
        }



    }
}
