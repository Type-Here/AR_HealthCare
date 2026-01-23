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

        // Constructor
        public MediaPipeTracker(PoseLandmarkerRunner runner)
        {
            _runner = runner;
            _currentData = new PatientTrackingData();
            _currentData.Joints = new Dictionary<string, Pose>();
        }

        public void Initialize()
        {
            if (_runner != null)
            {
                _runner.Play();
            }
            Debug.Log("MEDIAPIPE TRACKER: Inizializzato (Task API)");
        }

        /// <summary>
        /// Gets the latest tracking data from MediaPipe Pose Landmarker. <br/>
        /// It maps the detected landmarks to the PatientTrackingData structure. <br/>
        /// If no person is detected, IsTracked is set to false. <br/> 
        /// </summary>
        /// <returns> The latest PatientTrackingData with updated joint positions and tracking status. </returns>
        public PatientTrackingData GetTrackingData()
        {
            // 1. Read the result exposed in the Runner
            var result = _runner.LatestResult;

            //if (Time.frameCount % 60 == 0)
            //    Debug.Log($"MP IsTracked={_currentData.IsTracked} joints={_currentData.Joints.Count} t={Time.time:F2}");

            // 2. Security Checks
            // result.poseWorldLandmarks is a list of lists (one list for each detected person)
            if (result.poseWorldLandmarks == null || result.poseWorldLandmarks.Count == 0)
            {
                _currentData.IsTracked = false;
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
                        _currentData.Joints[joint] = ConvertLandmark(landmarks[idx]);
                    }
                    else
                    {
                        // Remove stale joints if the landmark is not present anymore
                        if (_currentData.Joints.ContainsKey(joint))
                            _currentData.Joints.Remove(joint);
                    }
                }
            }
            else
            {
                _currentData.IsTracked = false;
            }

            return _currentData;
        }

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
                var whichOffset = Camera.main != null ? "camera-relative" : "manual only";
                Debug.Log($"MediaPipeTracker: Applying {whichOffset} offset to landmarks.");
                _logManualOffset = false;
            }

            if (Camera.main != null)
            {
                // Camera space -> Unity world space
                var pWorld = Camera.main.transform.TransformPoint(pCam);

                return new Pose { position = pWorld, rotation = Quaternion.identity };
            } 
            //else apply manual offset only
            pCam += manualOffset;
            return new Pose { position = pCam, rotation = Quaternion.identity };
        }

    }
}