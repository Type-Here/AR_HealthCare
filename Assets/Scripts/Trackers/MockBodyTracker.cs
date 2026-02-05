using System.Collections.Generic;
using UnityEngine;
using ARHealthCare.DataClasses;

/// <summary> 
/// Mock implementation of a body tracker for testing purposes.
/// Simulates body joint data without using a real tracking system.
/// M2: Enhanced with configurable distance and realistic body proportions for spatial anchoring tests.
/// </summary>

namespace ARHealthCare.Trackers {
    public class MockBodyTracker : IBodyTracker {
        private PatientTrackingData _currentFrame;
        private float _timer;
        
        // M2: Configurable test parameters (can be set from constructor)
        public float patientDistance = 2.0f; // Distance from camera (Z axis)
        public float patientHeight = 1.7f;   // Average human height
        public bool animateArms = true;      // Simulate arm movement

        public MockBodyTracker() : this(2.0f) { }
        
        public MockBodyTracker(float distance)
        {
            patientDistance = distance;
        }

        public void Initialize() {
            _currentFrame = new PatientTrackingData();
            _currentFrame.Joints = new Dictionary<string, Pose>();
            Debug.Log($"MOCK TRACKER: Initialized (distance={patientDistance}m, height={patientHeight}m)");
        }

        public PatientTrackingData GetTrackingData(){
            _currentFrame.IsTracked = true;
            _timer += Time.deltaTime;

            // M2: Realistic body proportions for spatial anchoring tests
            float z = patientDistance;
            float hipHeight = patientHeight * 0.53f;     // Hips at ~53% of height
            float shoulderHeight = patientHeight * 0.82f; // Shoulders at ~82%
            float headHeight = patientHeight * 0.93f;     // Head at ~93%
            float shoulderWidth = 0.4f;
            float hipWidth = 0.3f;

            // --- CORE BODY STRUCTURE ---
            
            // Hips (anchor point for M2)
            _currentFrame.Joints["LeftHip"] = new Pose {
                position = new Vector3(-hipWidth/2, hipHeight, z),
                rotation = Quaternion.identity
            };
            _currentFrame.Joints["RightHip"] = new Pose {
                position = new Vector3(hipWidth/2, hipHeight, z),
                rotation = Quaternion.identity
            };

            // Shoulders
            _currentFrame.Joints["LeftShoulder"] = new Pose {
                position = new Vector3(-shoulderWidth/2, shoulderHeight, z),
                rotation = Quaternion.identity
            };
            _currentFrame.Joints["RightShoulder"] = new Pose {
                position = new Vector3(shoulderWidth/2, shoulderHeight, z),
                rotation = Quaternion.identity
            };

            // Head (Nose for look-at)
            _currentFrame.Joints["Nose"] = new Pose {
                position = new Vector3(0, headHeight, z),
                rotation = Quaternion.identity
            };

            // --- ARMS (with animation) ---
            float armSwing = animateArms ? Mathf.Sin(_timer * 2.0f) * 0.3f : 0f;
            
            // Left arm
            _currentFrame.Joints["LeftElbow"] = new Pose {
                position = new Vector3(-shoulderWidth/2 - 0.1f, shoulderHeight - 0.3f, z),
                rotation = Quaternion.identity
            };
            _currentFrame.Joints["LeftWrist"] = new Pose {
                position = new Vector3(-shoulderWidth/2 - 0.15f, shoulderHeight - 0.6f + armSwing, z),
                rotation = Quaternion.identity
            };

            // Right arm
            _currentFrame.Joints["RightElbow"] = new Pose {
                position = new Vector3(shoulderWidth/2 + 0.1f, shoulderHeight - 0.3f, z),
                rotation = Quaternion.identity
            };
            _currentFrame.Joints["RightWrist"] = new Pose {
                position = new Vector3(shoulderWidth/2 + 0.15f, shoulderHeight - 0.6f - armSwing, z),
                rotation = Quaternion.identity
            };

            // --- LEGS (stationary) ---
            float kneeHeight = hipHeight * 0.5f;
            
            _currentFrame.Joints["LeftKnee"] = new Pose {
                position = new Vector3(-hipWidth/2, kneeHeight, z),
                rotation = Quaternion.identity
            };
            _currentFrame.Joints["RightKnee"] = new Pose {
                position = new Vector3(hipWidth/2, kneeHeight, z),
                rotation = Quaternion.identity
            };
            
            _currentFrame.Joints["LeftAnkle"] = new Pose {
                position = new Vector3(-hipWidth/2, 0.1f, z),
                rotation = Quaternion.identity
            };
            _currentFrame.Joints["RightAnkle"] = new Pose {
                position = new Vector3(hipWidth/2, 0.1f, z),
                rotation = Quaternion.identity
            };

            // M2: Populate distance and body height metadata
            _currentFrame.EstimatedDistance = patientDistance;
            _currentFrame.EstimatedBodyHeight = patientHeight;

            return _currentFrame;
        }

        public void StopTracking() {
            Debug.Log("MOCK TRACKER: Chiuso");
        }
    }
}