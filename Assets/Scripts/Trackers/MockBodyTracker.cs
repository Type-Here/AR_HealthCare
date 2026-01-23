using System.Collections.Generic;
using UnityEngine;
using ARHealthCare.DataClasses;

/// <summary> 
/// Mock implementation of a body tracker for testing purposes.
/// Simulates body joint data without using a real tracking system.
/// </summary>

namespace ARHealthCare.Trackers {
    public class MockBodyTracker : IBodyTracker {
        private PatientTrackingData _currentFrame;
        private float _timer;

        public void Initialize() {
            _currentFrame = new PatientTrackingData();
            _currentFrame.Joints = new Dictionary<string, Pose>();
            Debug.Log("MOCK TRACKER: Inizializzato (Simulazione attiva)");
        }

        public PatientTrackingData GetTrackingData(){
            _currentFrame.IsTracked = true;
            _timer += Time.deltaTime;

            // --- DATA SIMULATION ---

            // We simulate the head stationary at the center (height 1.7m)
            _currentFrame.Joints["Head"] = new Pose {
                position = new Vector3(0, 1.7f, 2.0f), // 2 meters in front of us
                rotation = Quaternion.identity
            };

            // We simulate the RIGHT HAND moving up and down (sinusoidal wave)
            // Useful to test if the rigging works
            float handHeight = 1.0f + (Mathf.Sin(_timer * 2.0f) * 0.5f);

            _currentFrame.Joints["RightWrist"] = new Pose {
                position = new Vector3(0.5f, handHeight, 2.0f),
                rotation = Quaternion.identity
            };

            // Add other joints here if needed (LeftWrist, etc.)
            return _currentFrame;
        }

        public void StopTracking() {
            Debug.Log("MOCK TRACKER: Chiuso");
        }
    }
}