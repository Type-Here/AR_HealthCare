using UnityEngine;
using ARHealthCare.Trackers;
using Mediapipe.Unity.ModifiedSample.PoseLandmarkDetection; //MediaPipe Runner reference
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
            if (trackingManager == null)
            {
                Debug.LogError("Setup fallito: Tracking Manager non collegato!");
                return;
            }

            // Injection in Tracking Manager
            IBodyTracker trackerToInject;

            if (useMockData)
            {
                // Inject Mock Tracker
                trackerToInject = new MockBodyTracker();
            } else {
                if (poseRunner != null) {
                    // Inject MediaPipe (the only part that knows the PoseLandmarkerRunner type)
                    trackerToInject = new MediaPipeTracker(poseRunner);
                } else {
                    Debug.LogError("Tentativo di usare MediaPipe, ma il Runner non è collegato. Usando Mock come fallback.");
                    trackerToInject = new MockBodyTracker();
                }
            }
            
            // INJECTION: Call the clean method of the Manager
            trackingManager.SetTracker(trackerToInject);

            // Disable the Runner if we use Mock to save resources
            if (useMockData && poseRunner != null)
            {
                 poseRunner.gameObject.SetActive(false);
            }
        }
    }
}