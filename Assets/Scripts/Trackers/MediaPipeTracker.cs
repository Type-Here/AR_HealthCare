using UnityEngine;
using System.Collections.Generic;
using ARHealthCare.DataClasses;

using Mediapipe.ARHealthCare.Sample.PoseLandmarkDetection; 
using Mediapipe.Tasks.Vision.PoseLandmarker;
using Mediapipe.Tasks.Components.Containers; // Landmark Class here

using System;

namespace ARHealthCare.Trackers
{
    public class MediaPipeTracker : IBodyTracker
    {

        [Header("Landmark Conversion Settings")]
        // MediaPipe poseWorldLandmarks: origin=hips, X=person-right, Y=up, Z=toward-camera.
        // Unity: left-handed, X=right, Y=up, Z=forward (away from camera when person faces cam).
        // Conversion: negate Z (person's +Z points toward camera = Unity's -Z).
        // invertX NOT needed for world landmarks (X is already person-right = Unity-right).
        public bool invertX = true;
        public bool invertY = false;
        public bool invertZ = true;
        
        [Tooltip("Scale factor for converting landmark coordinates to Unity units.")]
        public float scale = 1.0f;    // 1 = meters; change if you want a bigger/scaled avatar
        [Tooltip("Manual offset: X=lateral, Y=hips height relative to camera (negative = below eye level), Z=distance in front.")]
        public Vector3 manualOffset = new Vector3(0, -0.5f, 2f); // hips ~0.5m below camera, 2m forward
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

        // Pre-computed landmark ID → joint name mapping (avoids LINQ/allocations per frame)
        private static readonly Dictionary<int, string> _landmarkMap = BuildLandmarkMap();

        private static Dictionary<int, string> BuildLandmarkMap()
        {
            var desiredIds = new HashSet<int> { 0, 11, 12, 13, 14, 15, 16, 23, 24, 25, 26, 27, 28 };
            var map = new Dictionary<int, string>(desiredIds.Count);
            foreach (var kvp in LandMarkPoints.Points)
            {
                if (desiredIds.Contains(kvp.Key))
                    map[kvp.Key] = kvp.Value.Replace(" ", "");
            }
            return map;
        }

        // Anchor lock: keeps the person's world position fixed so the body does NOT
        // orbit when the user rotates the headset.
        [Tooltip("Lock the person anchor in world space on first detection. Prevents the body from following headset rotation. Call ResetAnchor() to re-detect position.")]
        public bool lockAnchorPosition = true;
        [Tooltip("How fast the locked anchor drifts toward the current camera estimate (0 = fully frozen, higher = faster follow). Default: 0.3 m/s equivalent.")]
        [UnityEngine.Range(0f, 2f)]
        public float anchorUpdateSpeed = 0.3f;

        private Vector3 _lockedAnchorWorld;
        private bool _anchorLocked = false;

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
        /// Initializes the MediaPipe tracker by caching the main camera reference.
        /// NOTE: We intentionally DO NOT call _runner.Play() here.
        /// The PoseLandmarkerRunner manages its own lifecycle through BaseRunner.Start(),
        /// which waits for Bootstrap to initialize GPU, AssetLoader, and ImageSource before
        /// starting the Run() coroutine. Calling Play() prematurely causes a native crash
        /// because MediaPipe resources are not yet available.
        /// </summary>
        public void Initialize()
        {
            // M4.6: Cache Camera.main reference to avoid per-frame lookup
            _cachedMainCamera = Camera.main;
            Debug.Log("MediaPipeTracker: Initialized. Runner will start via its own lifecycle (Bootstrap → Play).");
        }

        /// <summary>
        /// Gets the latest tracking data from MediaPipe Pose Landmarker. <br/>
        /// It maps the detected landmarks to the PatientTrackingData structure. <br/>
        /// If no person is detected, IsTracked is set to false. <br/> 
        /// </summary>
        /// <returns> The latest PatientTrackingData with updated joint positions and tracking status. </returns>
        public PatientTrackingData GetTrackingData()
        {
            // Safety: if the runner hasn't started yet (Bootstrap still initializing),
            // just return not-tracked data. Do NOT call _runner.Play() here — that would
            // start Run() before MediaPipe resources are initialized → native crash.
            if (_runner == null || !_runner.isRunning)
            {
                _currentData.IsTracked = false;
                return _currentData;
            }

            // M4.6: Profiling marker for performance analysis
            UnityEngine.Profiling.Profiler.BeginSample("MediaPipeTracker.GetTrackingData");

            // 1. Read the result exposed in the Runner
            var result = _runner.LatestResult;

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
                // Uses static pre-computed _landmarkMap (zero per-frame allocations)

                // Step A: Compute hips center in MediaPipe world space.
                // MediaPipe poseWorldLandmarks have origin approximately at the person's hips center.
                // Subtracting this gives each joint's offset RELATIVE to the body root,
                // which is coordinate-system independent and safe to scale/rotate.
                Vector3 hipsCenterMP = Vector3.zero;
                if (landmarks.Count >= 25)
                {
                    hipsCenterMP = new Vector3(
                        (landmarks[23].x + landmarks[24].x) * 0.5f,
                        (landmarks[23].y + landmarks[24].y) * 0.5f,
                        (landmarks[23].z + landmarks[24].z) * 0.5f
                    );
                }

                // Step B: Compute yaw-only camera rotation (once per frame, shared by anchor + all landmarks).
                // Using ONLY yaw (horizontal rotation) means:
                //   - Tilting the head up/down (pitch) does NOT move the body.
                //   - Rolling the head does NOT move the body.
                //   - Turning left/right (yaw) correctly orients the patient in front of the camera.
                // MediaPipe world landmark X-axis = camera's right projected onto the horizontal plane,
                // so we must rotate landmark offsets by camera yaw to place them in correct world space.
                Quaternion cameraYaw = Quaternion.identity;
                if (_cachedMainCamera != null)
                    cameraYaw = Quaternion.Euler(0f, _cachedMainCamera.transform.eulerAngles.y, 0f);

                // Step C: Compute person anchor in world space (yaw-aware, lockable).
                Vector3 personAnchorWorld = ComputePersonAnchorWorld(cameraYaw);

                // Step D: Convert each landmark — relative offset from hips → Unity world space.
                foreach (var kvp in _landmarkMap)
                {
                    int idx = kvp.Key;
                    string joint = kvp.Value;
                    if (idx >= 0 && idx < landmarks.Count)
                    {
                        _jointsCache[joint] = ConvertLandmark(landmarks[idx], hipsCenterMP, personAnchorWorld, cameraYaw);
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
        /// Resets the world-space anchor so it is re-locked on the next tracking frame.
        /// Call this if the patient has moved to a significantly different position.
        /// </summary>
        public void ResetAnchor()
        {
            _anchorLocked = false;
            Debug.Log("MediaPipeTracker: Anchor reset — will re-lock on next tracked frame.");
        }

                
        /// <summary>
        /// Returns the person anchor position in Unity world space.
        /// The anchor is the world position of the patient's hips center.
        ///
        /// Uses ONLY the camera yaw (horizontal rotation) so that:
        ///   - Tilting the head (pitch) does NOT shift the anchor vertically.
        ///   - Rolling the head does NOT rotate the anchor.
        ///   - Turning left/right (yaw) places the patient in the correct world direction.
        ///
        /// If lockAnchorPosition=true, the anchor is frozen on first detection and only
        /// drifts toward the fresh estimate at anchorUpdateSpeed (m/s equivalent).
        /// Call ResetAnchor() to re-lock at a new position.
        /// </summary>
        private Vector3 ComputePersonAnchorWorld(Quaternion cameraYaw)
        {
            if (_cachedMainCamera == null) return manualOffset;

            var camPos = _cachedMainCamera.transform.position;

            // Compute "in front of camera" using yaw-only rotation, no tilt.
            var fwd   = cameraYaw * Vector3.forward;
            var right = cameraYaw * Vector3.right;

            var freshEstimate = new Vector3(
                camPos.x + fwd.x * manualOffset.z + right.x * manualOffset.x,
                camPos.y + manualOffset.y,   // relative to eye height
                camPos.z + fwd.z * manualOffset.z + right.z * manualOffset.x
            );

            if (!lockAnchorPosition)
                return freshEstimate;

            // First detection: lock anchor at current estimate.
            if (!_anchorLocked)
            {
                _lockedAnchorWorld = freshEstimate;
                _anchorLocked = true;
                Debug.Log($"MediaPipeTracker: Anchor locked at world pos {_lockedAnchorWorld}");
                return _lockedAnchorWorld;
            }

            // Slow drift toward fresh estimate so the anchor follows large patient movements
            // without snapping with every head turn.
            if (anchorUpdateSpeed > 0.001f)
                _lockedAnchorWorld = Vector3.MoveTowards(_lockedAnchorWorld, freshEstimate,
                                                         anchorUpdateSpeed * Time.deltaTime);
            return _lockedAnchorWorld;
        }

        /// <summary>
        /// Converts a single MediaPipe world landmark to a Unity world-space Pose.
        ///
        /// MediaPipe poseWorldLandmarks coordinate system (gravity-aligned, NOT camera-local):
        ///   - Origin : approximately at the person's hips center
        ///   - X      : person's anatomical right
        ///   - Y      : up (same as Unity — no inversion needed)
        ///   - Z      : toward camera when person faces camera (+Z = toward camera = Unity -Z)
        ///
        /// Conversion (invertX=false, invertZ=true by default):
        ///   1. Subtract hips center → offset relative to body root.
        ///   2. Negate Z (person's +Z toward camera → Unity -Z away from camera).
        ///   3. Apply scale (1 = meters).
        ///   4. Add person anchor (NO camera.rotation multiplication — landmarks are
        ///      already gravity-aligned; rotating by camera would cause joints to orbit
        ///      with head movement).
        /// </summary>
        private Pose ConvertLandmark(Landmark lm, Vector3 hipsCenterMP, Vector3 personAnchorWorld, Quaternion cameraYaw)
        {
            if (_logManualOffset)
            {
                Debug.Log($"MediaPipeTracker: ConvertLandmark — invertX={invertX} invertY={invertY} invertZ={invertZ} scale={scale} anchor={personAnchorWorld}");
                _logManualOffset = false;
            }

            // 1. Relative offset from body root (person-space)
            var relMP = new Vector3(
                lm.x - hipsCenterMP.x,
                lm.y - hipsCenterMP.y,
                lm.z - hipsCenterMP.z
            );

            // 2+3. Axis conversion + scale
            var relUnity = new Vector3(
                (invertX ? -1f : 1f) * relMP.x,
                (invertY ? -1f : 1f) * relMP.y,
                (invertZ ? -1f : 1f) * relMP.z
            ) * scale;

            // 4. Rotate offsets by camera YAW ONLY.
            // MediaPipe world landmark X = camera-right projected onto horizontal plane.
            // Rotating by yaw-only maps that X correctly into Unity world space
            // WITHOUT letting pitch or roll tilt/orbit the whole body.
            var worldOffset = cameraYaw * relUnity;

            return new Pose { position = personAnchorWorld + worldOffset, rotation = Quaternion.identity };
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