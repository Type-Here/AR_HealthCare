using System;
using System.Collections;
using System.Linq;
using UnityEngine;
using Mediapipe.ARHealthCare;
//using Mediapipe.Unity; // For RotationAngle / ImageSource base

namespace ARHealthCare.Input
{
    public class MLCameraImageSource : ImageSource
    {
        [Header("ML Camera Bridge")]
        [SerializeField] private MLCameraBridge _mlBridge;

        [Header("Options")]
        [SerializeField] private string _sourceName = "MLCamera";
        [SerializeField] private int _fallbackWidth = 1280;
        [SerializeField] private int _fallbackHeight = 720;

        private bool _isPlaying;
        private ResolutionStruct[] _availableResolutions;

        private Texture CurrentTexture => _mlBridge != null ? _mlBridge.CurrentCameraTexture : null;

        public override int textureWidth  => !isPrepared ? 0 : CurrentTexture.width;
        public override int textureHeight => !isPrepared ? 0 : CurrentTexture.height;

        public override bool isVerticallyFlipped => false;
        public override bool isFrontFacing => false;

        // TODO: TOCHECK: If you see weird rotations, you can fix them here.
        public override Mediapipe.Unity.RotationAngle rotation => Mediapipe.Unity.RotationAngle.Rotation0;

        public override string sourceName => _sourceName;
        public override string[] sourceCandidateNames => new[] { _sourceName };

        public override bool isPrepared => CurrentTexture != null;
        public override bool isPlaying => _isPlaying;

        public override ResolutionStruct[] availableResolutions
        {
            get
            {
                if (_availableResolutions == null || _availableResolutions.Length == 0)
                {
                    var refreshRate = new RefreshRate { numerator = 30, denominator = 1 };
                    var tex = CurrentTexture;

                    _availableResolutions = new[]
                    {
                        new ResolutionStruct
                        {
                            width = tex != null ? tex.width : _fallbackWidth,
                            height = tex != null ? tex.height : _fallbackHeight,
                            frameRate = refreshRate,
                        }
                    };
                }
                return _availableResolutions;
            }
        }

        public override void SelectSource(int sourceId)
        {
            if (sourceId != 0)
                throw new ArgumentException($"Invalid source ID: {sourceId}. MLCameraImageSource supports only source 0.");
        }

         private IEnumerator Initialize()
        {
            if (_mlBridge == null)
                _mlBridge = UnityEngine.Object.FindFirstObjectByType<MLCameraBridge>();

            if (_mlBridge == null)
                Debug.LogWarning("MLCameraImageSource: MLCameraBridge non trovato in scena.");

            yield return null;
        }

        public override IEnumerator Play()
        {
            yield return Initialize();

            _isPlaying = true;

            // aspetta che il bridge produca la texture
            while (_mlBridge == null || _mlBridge.CurrentCameraTexture == null)
                yield return null;
        }

        public override IEnumerator Resume()
        {
            if (!isPrepared)
                throw new InvalidOperationException("ML camera is not prepared yet");

            _isPlaying = true;

            while (_mlBridge == null || _mlBridge.CurrentCameraTexture == null)
                yield return null;
        }

        public override void Pause() => _isPlaying = false;

        public override void Stop() => _isPlaying = false;

        public override Texture GetCurrentTexture() => _mlBridge != null ? _mlBridge.CurrentCameraTexture : null;
    }
}
