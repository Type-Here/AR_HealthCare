using UnityEngine;
using ARHealthCare.DataClasses;
using ARHealthCare.Trackers;

namespace ARHealthCare.Core
{
    public class TrackingManager : MonoBehaviour, ITrackingProvider
    {
        // Current active body tracker
        private IBodyTracker _currentTracker;

        // Last received body frame
        private PatientTrackingData? _lastFrame;

        void Start()
        {
            // _currentTracker.Initialize(); 
            // Moved to SetTracker method so can be called on injection and 
            // if not commented would be called here a second time causing issues.
            if (_currentTracker == null)
            {
                Debug.LogWarning("TrackingManager: nessun tracker iniettato in Start().");
            }
        }

        /// <summary>
        /// Injects a new body tracker into the Tracking Manager.
        /// </summary>
        /// <param name="tracker">
        /// The new body tracker to be injected.
        /// </param>
        public void SetTracker(IBodyTracker tracker)
        {

            _currentTracker?.StopTracking(); // Optional: stop previous tracker if any

            _currentTracker = tracker;
            _currentTracker.Initialize();

            Debug.Log($"TrackingManager: Nuovo tracker iniettato: {_currentTracker.GetType().Name}");
        }

        /// <summary>
        /// API method to get patient pose.
        /// Gets the current patient pose from the active tracker.
        /// 
        /// </summary>
        /// <returns> Tracking data of the patient. </returns>
        public PatientTrackingData GetPose()
        {
            return _lastFrame ?? new PatientTrackingData();
        }

        void Update()
        {
            if (_currentTracker != null)
            {
                _lastFrame = _currentTracker.GetTrackingData();
            }
        }

        void OnDestroy()
        {
            _currentTracker?.StopTracking();
        }
    }
}