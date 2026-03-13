using UnityEngine;
using System.Collections.Generic;
using ARHealthCare.DataClasses;
using Mediapipe.ARHealthCare.Sample.PoseLandmarkDetection;

namespace ARHealthCare.Trackers
{
    /// <summary>
    /// MediaPipe tracker V2 — "screen-space projection" approach.
    ///
    /// IDEA: The MediaPipe canvas sample tracks the patient perfectly because it uses
    /// poseLandmarks (normalized 2D, 0-1 image space) → those coordinates follow the
    /// person in pixel-space just like a HUD overlay.
    ///
    /// This tracker does the same for 3D:
    ///   1. XY from poseLandmarks (normalized) → screen pixels.        ← same source as the canvas
    ///   2. Z from poseWorldLandmarks (meters, origin≈hips center).    ← real metric depth
    ///   3. Camera.ScreenToWorldPoint(screenX, screenY, depth) → world pos.
    ///
    /// There is NO manual anchor, NO axis-inversion guessing, NO yaw decomposition.
    /// Anchoring is automatically correct because Unity's projection matrix already
    /// knows where the camera is and how it is oriented.
    ///
    /// Setup:
    ///   Replace MediaPipeTracker with MediaPipeTrackerV2 in SceneTrackerSetup.
    ///   Only parameter that matters: baseDepth (estimated camera-to-patient distance in meters).
    /// </summary>
    public class MediaPipeTrackerV2 : IBodyTracker
    {
        [Header("Depth Settings")]
        [Tooltip("Estimated distance from camera to patient (metres)." +
        " Used as the depth baseline for hips; other joints are offset relative to it using world landmark Z.")]
        public float baseDepth = 2.0f;

        [Tooltip("Vertical offset applied to screen-Y before projecting (positive = shift landmark up on screen)." 
                +" Useful to compensate for camera tilt or avatar height mismatch.")]
        public float screenYOffset = 0f;

        [Header("Debug")]
        public bool verbose = false;

        [Header("Visibility Filter")]
        [Tooltip("Landmarks with Visibility below this threshold are excluded from tracking data.\n" +
                 "MediaPipe always estimates all 33 landmarks, even off-screen ones.\n" +
                 "Filtering by visibility prevents using hallucinated positions.")]
        [Range(0f, 1f)]
        public float visibilityThresholdForHips = 0.4f;
        public float visibilityThresholdForOthers = 0.1f;

        //Internals
        private readonly PoseLandmarkerRunner _runner;
        private PatientTrackingData           _currentData;
        private readonly Dictionary<string, Pose> _jointsCache;
        private Camera _cam;

        // Joint IDs we need (MediaPipe index → IBodyTracker key mapping).
        // Keys must match LandMarkPoints.Points with spaces removed.
        private static readonly (int id, string key)[] _desiredJoints =
        {
            (0,  "Nose"),
            (11, "LeftShoulder"),
            (12, "RightShoulder"),
            (13, "LeftElbow"),
            (14, "RightElbow"),
            (15, "LeftWrist"),
            (16, "RightWrist"),
            (23, "LeftHip"),
            (24, "RightHip"),
            (25, "LeftKnee"),
            (26, "RightKnee"),
            (27, "LeftAnkle"),
            (28, "RightAnkle")
            //(2, "LeftEye"),
            //(5, "RightEye"),
            //(31, "LeftFootIndex"),
            //(32, "RightFootIndex"),
        };

        public MediaPipeTrackerV2(PoseLandmarkerRunner runner)
        {
            _runner       = runner;
            _currentData  = new PatientTrackingData();
            _jointsCache  = new Dictionary<string, Pose>(_desiredJoints.Length);
            _currentData.Joints = _jointsCache;
        }

        public void Initialize()
        {
            // NOTE: We intentionally DO NOT call _runner.Play() here.
            // The PoseLandmarkerRunner manages its own lifecycle through BaseRunner.Start(),
            // which waits for Bootstrap to initialize GPU, AssetLoader, and ImageSource.
            // Calling Play() prematurely causes a native crash on device.
            _cam = Camera.main;
            Debug.Log("[MediaPipeTrackerV2] Initialized. Runner will start via its own lifecycle (Bootstrap → Play).");
        }

        public PatientTrackingData GetTrackingData()
        {
            // Lazy camera cache
            if (_cam == null) _cam = Camera.main;

            if (_runner == null || !_runner.isRunning)
            {
                // Runner not ready yet (Bootstrap still initializing).
                // Do NOT call Play() here — that causes native crash.
                _currentData.IsTracked = false;
                return _currentData;
            }

            var result = _runner.LatestResult;

            // Need both normalized (2D XY) and world (depth Z) landmarks.
            bool hasNorm  = result.poseLandmarks  != null && result.poseLandmarks.Count  > 0;
            bool hasWorld = result.poseWorldLandmarks != null && result.poseWorldLandmarks.Count > 0;

            if (!hasNorm || !hasWorld)
            {
                _currentData.IsTracked = false;
                return _currentData;
            }

            var normLandmarks  = result.poseLandmarks[0].landmarks;   // NormalizedLandmark list
            var worldLandmarks = result.poseWorldLandmarks[0].landmarks; // Landmark list (metres)

            int count = Mathf.Min(normLandmarks.Count, worldLandmarks.Count);
            if (count == 0)
            {
                _currentData.IsTracked = false;
                return _currentData;
            }

            _currentData.IsTracked = true;
            _jointsCache.Clear();

            // ── hips Z: compute depth of hips center from world landmarks ────
            // poseWorldLandmarks Z convention: negative = closer to camera.
            // Hips center is near origin (0,0,0), so world.z ≈ 0 for hips.
            // Each joint's actual depth ≈ baseDepth - worldLandmark.z
            // (subtracting because negative Z = closer = smaller screen depth).
            float hipsWorldZ = 0f;
            if (count > 24)
                hipsWorldZ = (worldLandmarks[23].z + worldLandmarks[24].z) * 0.5f;

            foreach (var (id, key) in _desiredJoints)
            {
                if (id >= count) continue;

                var norm  = normLandmarks[id];
                var world = worldLandmarks[id];

                // Skip landmarks that MediaPipe hallucinated (off-screen / low confidence).
                // Without this, hips are always "present" even when out of frame,
                // preventing the close-range Nose-based fallback in HybridAvatarIK.
                if (key.ToLower().Contains("hip") && norm.visibility.HasValue && norm.visibility.Value < visibilityThresholdForHips)
                    continue;

                // XY: normalized image coordinates → screen pixels.
                // Image Y: 0 = top → flip for Unity screen (0 = bottom).
                float sx = norm.x * Screen.width;
                float sy = (1f - norm.y) * Screen.height + screenYOffset;

                // Z (depth from camera):
                // depth = baseDepth + (worldLandmark.z - hipsZ)
                // • hips: depth ≈ baseDepth (hipsZ offset cancels)
                // • joint closer to camera than hips: worldZ < hipsZ → depth < baseDepth
                // • joint farther: worldZ > hipsZ → depth > baseDepth
                float depth = Mathf.Max(0.05f, baseDepth + (world.z - hipsWorldZ));

                if (_cam == null)
                {
                    // Fallback: no camera, store zero
                    _jointsCache[key] = new Pose { position = Vector3.zero, rotation = Quaternion.identity };
                    continue;
                }

                Vector3 worldPos = _cam.ScreenToWorldPoint(new Vector3(sx, sy, depth));
                _jointsCache[key] = new Pose { position = worldPos, rotation = Quaternion.identity };

                if (verbose && key == "Nose")
                    Debug.Log($"[V2] Nose screen=({sx:F0},{sy:F0}) depth={depth:F2} → world={worldPos}");
            }

            // M2: estimated distance (hips center depth)
            _currentData.EstimatedDistance = baseDepth;

            // M2: body height estimate from shoulder/hip world Y span
            if (_jointsCache.TryGetValue("LeftShoulder", out var ls) &&
                _jointsCache.TryGetValue("LeftHip",      out var lh))
            {
                float torso = Mathf.Abs(ls.position.y - lh.position.y);
                _currentData.EstimatedBodyHeight = torso * 2f;
            }

            return _currentData;
        }

        public void StopTracking() => _runner?.Stop();
    }
}
