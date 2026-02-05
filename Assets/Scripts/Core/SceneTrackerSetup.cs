using UnityEngine;
using ARHealthCare.Trackers;
using Mediapipe.ARHealthCare.Sample.PoseLandmarkDetection; //MediaPipe Runner reference
using ARHealthCare.Core; // Manager reference

namespace ARHealthCare.Core 
{
    /**
        * SceneTrackerSetup
        * Summary:
        * Script for Tracker Injection in Tracking Manager
        * Description:
        * Attach this script to an empty GameObject in the scene to perform
        * the injection of the desired tracker into the Tracking Manager.
        * 
        * Allows choosing between Mock (fake data) and MediaPipe (real data)
        * and injects the chosen tracker into the connected TrackingManager.
        * 
        */
    public class SceneTrackerSetup : MonoBehaviour
    {
        [Header("Injection Target")]
        [Tooltip("Il GameObject con lo script TrackingManager")]
        public TrackingManager trackingManager;

        [Header("Tracker Configuration")]
        [Tooltip("ATTIVA per usare i dati sinusoidali finti")]
        public bool useMockData = false;

        [Space]
        [Tooltip("Trascina il PoseLandmarkerRunner dalla scena qui")]
        public PoseLandmarkerRunner poseRunner;

        void Awake()
        {
            // Auto-wiring: Try to find components in scene if not assigned in inspector
            if (trackingManager == null)
            {
                trackingManager = FindFirstObjectByType<TrackingManager>();
                if (trackingManager == null)
                {
                    Debug.LogError("SceneTrackerSetup: TrackingManager not found in scene! Please assign it in inspector or add it to the scene.");
                    return;
                }
                Debug.LogWarning("SceneTrackerSetup: TrackingManager auto-wired from scene. Consider assigning it in inspector for better performance.");
            }

            if (poseRunner == null && !useMockData)
            {
                poseRunner = FindFirstObjectByType<PoseLandmarkerRunner>();
                if (poseRunner != null)
                {
                    Debug.LogWarning("SceneTrackerSetup: PoseLandmarkerRunner auto-wired from scene. Consider assigning it in inspector.");
                }
            }

            // Injection in Tracking Manager
            IBodyTracker trackerToInject;

            if (useMockData)
            {
                // Inject Mock Tracker for testing/development
                trackerToInject = new MockBodyTracker();
                Debug.Log("SceneTrackerSetup: Injecting MockBodyTracker (test mode).");
            } else {
                if (poseRunner != null) {
                    // Inject MediaPipe tracker (production mode)
                    trackerToInject = new MediaPipeTracker(poseRunner);
                    Debug.Log("SceneTrackerSetup: Injecting MediaPipeTracker.");
                } else {
                    Debug.LogError("SceneTrackerSetup: Attempted to use MediaPipe but PoseLandmarkerRunner is not assigned or found. Falling back to MockBodyTracker.");
                    trackerToInject = new MockBodyTracker();
                }
            }
            
            // Inject tracker into TrackingManager
            trackingManager.SetTracker(trackerToInject);

            // Disable MediaPipe Runner if using Mock to save resources
            if (useMockData && poseRunner != null)
            {
                 poseRunner.gameObject.SetActive(false);
            }
        }
    }
}