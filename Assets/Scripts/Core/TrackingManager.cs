using UnityEngine;
using ARHealthCare.DataClasses;
using ARHealthCare.Trackers;

namespace ARHealthCare.Core
{
    /// <summary>
    /// Central manager for body tracking in the ARHealthCare system.
    /// Implements ITrackingProvider and acts as a dependency injection container for IBodyTracker implementations.
    /// Polls the active tracker every frame and caches the latest tracking data for consumers.
    /// </summary>
    public class TrackingManager : MonoBehaviour, ITrackingProvider
    {
        // Current active body tracker
        private IBodyTracker _currentTracker;

        // Last received tracking frame (cached for consumers)
        private PatientTrackingData? _lastFrame;

        /// <summary>
        /// Unity lifecycle: Validates that a tracker has been injected.
        /// Note: Tracker initialization is handled in SetTracker() to avoid double-initialization.
        /// </summary>
        void Start()
        {
            // NOTE: _currentTracker.Initialize() is intentionally NOT called here.
            // It's called in SetTracker() during injection to avoid double-initialization.
            if (_currentTracker == null)
            {
                Debug.LogWarning("TrackingManager: No tracker injected at Start(). Use SceneTrackerSetup or call SetTracker() manually.");
            }
        }

        /// <summary>
        /// Injects a new body tracker into the Tracking Manager.
        /// Stops the previous tracker (if any), initializes the new tracker,
        /// and begins polling for tracking data.
        /// </summary>
        /// <param name="tracker">The new IBodyTracker instance to use for tracking.</param>
        public void SetTracker(IBodyTracker tracker)
        {
            // Stop previous tracker to release resources
            _currentTracker?.StopTracking();

            _currentTracker = tracker;
            _currentTracker.Initialize();

            Debug.Log($"TrackingManager: Tracker injected → {_currentTracker.GetType().Name}");
        }

        /// <summary>
        /// Gets the current patient tracking data from the active tracker.
        /// This is the main API method consumed by visualization components (e.g., MedicalAvatarIK).
        /// Returns cached data from the last Update() poll, or an empty struct if no data available.
        /// </summary>
        /// <returns>The latest PatientTrackingData with joints, distance, and tracking status.</returns>
        public PatientTrackingData GetPose()
        {
            return _lastFrame ?? new PatientTrackingData();
        }

        /// <summary>
        /// Unity lifecycle: Polls the active tracker for new tracking data every frame.
        /// Updates the cached _lastFrame for consumers to read via GetPose().
        /// </summary>
        void Update()
        {
            if (_currentTracker != null)
            {
                _lastFrame = _currentTracker.GetTrackingData();
            }
        }

        /// <summary>
        /// Unity lifecycle: Cleanup when TrackingManager is destroyed.
        /// Stops the active tracker to release resources.
        /// </summary>
        void OnDestroy()
        {
            _currentTracker?.StopTracking();
        }
    }
}