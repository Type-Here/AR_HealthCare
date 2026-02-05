using UnityEngine;
using System.Collections.Generic;
using ARHealthCare.DataClasses;

using Mediapipe.ARHealthCare.Sample.PoseLandmarkDetection; 
using Mediapipe.Tasks.Vision.PoseLandmarker;
using Mediapipe.Tasks.Components.Containers; // Landmark Class here

using System.Linq;
using System;

namespace ARHealthCare.Trackers
{
    public class MediaPipeTracker : IBodyTracker
    {

        [Header("Landmark Conversion Settings")]
        public bool invertX = true;   // MediaPipe → Unity (right-handed → left-handed)
        public bool invertY = false;  // Usually false for world landmarks
        public bool invertZ = false;  // Depends on model facing
        
        [Tooltip("Scale factor for converting landmark coordinates to Unity units.")]
        public float scale = 1.0f;    // 1 = meters; change if you want a bigger/scaled avatar
        [Tooltip("Manual offset to apply to all landmarks in world space.")]
        public Vector3 manualOffset = new Vector3(0, 0, 2f); // e.g. 2m in front of the camera
        [Tooltip("If true, offsets the landmarks from the main camera position.")]
        public bool offsetFromCamera = true;

        private bool _logManualOffset = true;

        // Reference to the Modified runner
        private PoseLandmarkerRunner _runner;
        
        // Current tracking data
        private PatientTrackingData _currentData;

        // M4.6: Cached Camera reference to avoid expensive Camera.main lookups every frame
        private Camera _cachedMainCamera;

        // M4.6: Pre-allocated dictionary to avoid GC allocations per frame
        private Dictionary<string, Pose> _jointsCache;

        /// <summary>
        /// Constructs a new MediaPipeTracker that adapts MediaPipe PoseLandmarker results
        /// to the ARHealthCare tracking system.
        /// </summary>
        /// <param name="runner">The PoseLandmarkerRunner instance to read pose data from.</param>
        public MediaPipeTracker(PoseLandmarkerRunner runner)
        {
            _runner = runner;
            _currentData = new PatientTrackingData();
            // M4.6: Pre-allocate with capacity 33 (MediaPipe Pose has 33 landmarks)
            _jointsCache = new Dictionary<string, Pose>(33);
            _currentData.Joints = _jointsCache;
        }

        /// <summary>
        /// Initializes the MediaPipe tracker by starting the PoseLandmarkerRunner
        /// and caching the main camera reference for performance (M4.6).
        /// </summary>
        public void Initialize()
        {
            if (_runner != null)
            {
                _runner.Play();
            }
            // M4.6: Cache Camera.main reference to avoid per-frame lookup
            _cachedMainCamera = Camera.main;
            Debug.Log("MediaPipeTracker: Initialized (MediaPipe Task API active).");
        }

        /// <summary>
        /// Gets the latest tracking data from MediaPipe Pose Landmarker. <br/>
        /// It maps the detected landmarks to the PatientTrackingData structure. <br/>
        /// If no person is detected, IsTracked is set to false. <br/> 
        /// </summary>
        /// <returns> The latest PatientTrackingData with updated joint positions and tracking status. </returns>
        public PatientTrackingData GetTrackingData()
        {
            // M4.6: Profiling marker for performance analysis
            UnityEngine.Profiling.Profiler.BeginSample("MediaPipeTracker.GetTrackingData");

            // 1. Read the result exposed in the Runner
            var result = _runner.LatestResult;

            //if (Time.frameCount % 60 == 0)
            //    Debug.Log($"MP IsTracked={_currentData.IsTracked} joints={_currentData.Joints.Count} t={Time.time:F2}");

            // 2. Security Checks
            // result.poseWorldLandmarks is a list of lists (one list for each detected person)
            if (result.poseWorldLandmarks == null || result.poseWorldLandmarks.Count == 0)
            {
                _currentData.IsTracked = false;
                UnityEngine.Profiling.Profiler.EndSample();
                return _currentData;
            }

            // From result:
            // - Take the World Landmarks
            // - Take the first person (index 0) Landmarks Container
            // - Take the landmarks list from the container 
            var landmarks = result.poseWorldLandmarks[0].landmarks;

            // 3. Map the Landmarks to our PatientTrackingData structure
            if (landmarks != null && landmarks.Count > 0)
            {
                _currentData.IsTracked = true;
                
                // M4.6: Clear dictionary instead of recreating (reuse allocated memory)
                _jointsCache.Clear();
                
                // --- MAPPING ---
                // MediaPipe Pose Landmark ID Reference:
                // 0: Nose
                // 11: Left Shoulder, 12: Right Shoulder
                // 13: Left Elbow, 14: Right Elbow
                // 15: Left Wrist, 16: Right Wrist
                
                /* TODO: Expand this mapping as needed for more joints
                 * Map landmarks using a lookup to reduce repetitive checks and avoid repeated Count tests
                 * For Now: We only map a subset of joints needed for medical overlay
                 */
                var desiredIds = new[] { 0, 11, 12, 13, 14, 15, 16, 25, 27, 26, 28, 23, 24 }; 
                var landmarkMap = LandMarkPoints.Points
                                    .Where(kv => desiredIds.Contains(kv.Key))
                                    .ToDictionary(kv => kv.Key, kv => kv.Value.Replace(" ", ""));
                
                // For each desired landmark, convert and store in the current data
                foreach (var kvp in landmarkMap)
                {
                    int idx = kvp.Key;
                    string joint = kvp.Value;

                    if (idx >= 0 && idx < landmarks.Count)
                    {
                        _jointsCache[joint] = ConvertLandmark(landmarks[idx]);
                    }
                }

                // M2: Estimate distance and body scale for dynamic anchoring
                EstimateDistanceAndScale(landmarks);
            }
            else
            {
                _currentData.IsTracked = false;
            }

            UnityEngine.Profiling.Profiler.EndSample();
            return _currentData;
        }

        /// <summary>
        /// Stops the MediaPipe tracking by halting the PoseLandmarkerRunner.
        /// Called automatically when TrackingManager is destroyed or when switching trackers.
        /// </summary>
        public void StopTracking()
        {
            if (_runner != null) _runner.Stop();
        }

                
        /// <summary>
        /// Converts MediaPipe world-landmark coordinates to Unity world space.
        /// 
        /// MediaPipe world landmarks (PoseLandmarker):
        /// - Units: meters
        /// - Coordinate system: right-handed
        ///   * X = right
        ///   * Y = up
        ///   * Z = forward/backwards (depends on the model)
        ///
        /// Unity world space:
        /// - Units: 1 unit = 1 meter
        /// - Coordinate system: left-handed
        ///   * X = right
        ///   * Y = up
        ///   * Z = forward
        ///
        /// Minimum required conversion:
        /// - Invert X axis when converting from RH → LH space.
        /// - Y axis usually does NOT need inversion (unlike normalized 2D landmarks).
        /// - Z inversion depends on how your avatar faces the camera.
        ///
        /// This method exposes inspector toggles to customize:
        /// - axis inversion,
        /// - scale,
        /// - world offset (fixed or relative to the main camera).
        ///
        /// The output is a Unity Pose, used by IK/rig mapping.
        /// </summary>
        /// <param name="landmark"> The MediaPipe Landmark to convert. </param>
        /// <returns> The converted Pose in Unity world space. </returns>
        private Pose _ConvertLandmark(Landmark landmark)
        {
            // 1. Base position from landmark
            float y = landmark.y;
            float x = landmark.x;
            float z = landmark.z;

            // 2. Axis inversion based on inspector settings (compact form)
            x *= invertX ? -1f : 1f;
            y *= invertY ? -1f : 1f;
            z *= invertZ ? -1f : 1f;

            // 3. Apply scale
            Vector3 pos = new Vector3(x, y, z) * scale;

            // 4. Apply offset
            if (offsetFromCamera && Camera.main != null)
            {
                pos += Camera.main.transform.position
                    + Camera.main.transform.forward * manualOffset.z
                    + Camera.main.transform.right   * manualOffset.x
                    + Camera.main.transform.up      * manualOffset.y;
            }
            else
            {
                pos += manualOffset;
            }

            return new Pose
            {
                position = pos,
                rotation = Quaternion.identity
            };
        }

        private Pose ConvertLandmark(Landmark landmark)
        {
            // MediaPipe world landmarks: meters, camera-centric.
            // Common conversion: invert X (RH->LH) and often invert Z depending on convention.
            var pCam = new Vector3(
                invertX ? -landmark.x : landmark.x,
                invertY ? -landmark.y : landmark.y,
                invertZ ? -landmark.z : landmark.z
            ) * scale;

            if (_logManualOffset)
            {
                var whichOffset = _cachedMainCamera != null ? "camera-relative" : "manual only";
                Debug.Log($"MediaPipeTracker: Applying {whichOffset} offset to landmarks.");
                _logManualOffset = false;
            }

            // M4.6: Use cached camera reference instead of Camera.main lookup
            if (_cachedMainCamera != null)
            {
                // Camera space -> Unity world space
                var pWorld = _cachedMainCamera.transform.TransformPoint(pCam);

                return new Pose { position = pWorld, rotation = Quaternion.identity };
            } 
            //else apply manual offset only
            pCam += manualOffset;
            return new Pose { position = pCam, rotation = Quaternion.identity };
        }

        /// <summary>
        /// M2: Estimates patient distance from camera and body height for dynamic scaling.
        /// M4.6: Uses cached camera reference for optimized distance calculation.
        /// Calculates distance using world-space converted joint positions (after ConvertLandmark).
        /// Updates EstimatedDistance (camera-to-hips center) and EstimatedBodyHeight (extrapolated from torso).
        /// </summary>
        /// <param name="landmarks">The MediaPipe landmarks list (not used directly, reads from _jointsCache).</param>
        private void EstimateDistanceAndScale(System.Collections.Generic.IReadOnlyList<Landmark> landmarks)
        {
            // M4.6: Use cached camera reference
            // Calculate distance using world-space converted joints (after ConvertLandmark)
            if (_jointsCache.ContainsKey("LeftHip") && _jointsCache.ContainsKey("RightHip") && _cachedMainCamera != null)
            {
                var lHipWorld = _jointsCache["LeftHip"].position;
                var rHipWorld = _jointsCache["RightHip"].position;
                var hipsMidWorld = 0.5f * (lHipWorld + rHipWorld);
                
                // Distance from camera to patient hips center (world space)
                _currentData.EstimatedDistance = Vector3.Distance(_cachedMainCamera.transform.position, hipsMidWorld);
            }

            // Estimate body height using shoulder-to-hip vertical span (world space)
            if (_jointsCache.ContainsKey("LeftShoulder") && _jointsCache.ContainsKey("LeftHip"))
            {
                var shoulderPos = _jointsCache["LeftShoulder"].position;
                var hipPos = _jointsCache["LeftHip"].position;
                var torsoHeight = Mathf.Abs(shoulderPos.y - hipPos.y);

                // Torso is roughly 50% of total height; extrapolate full body height
                _currentData.EstimatedBodyHeight = torsoHeight * 2.0f;
            }
        }

    }
}