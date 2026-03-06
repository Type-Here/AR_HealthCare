using ARHealthCare.DataClasses;

namespace ARHealthCare.Trackers 
{
    /// <summary>
    /// Interface for body tracking system implementations in the ARHealthCare project.
    /// Enables modular tracking backends (MediaPipe, Mock, Kinect, etc.) without changing consumer code.
    /// Implementations should convert their native tracking data to PatientTrackingData format.
    /// </summary>
    public interface IBodyTracker
    {
        /// <summary>
        /// Initializes the body tracking system.
        /// Called once by TrackingManager.SetTracker() after injection.
        /// Should start any necessary background processes, allocate resources, and prepare for tracking.
        /// </summary>
        void Initialize();

        /// <summary>
        /// Returns the current tracking data for this frame.
        /// Called by TrackingManager.Update() every frame to poll for new data.
        /// Should return cached/latest data immediately without blocking.
        /// </summary>
        /// <returns>PatientTrackingData containing joint positions, tracking status, and spatial metadata.</returns>
        PatientTrackingData GetTrackingData();

        /// <summary>
        /// Stops the body tracking system and releases resources.
        /// Called by TrackingManager when switching trackers or on cleanup (OnDestroy).
        /// Should halt background processes and dispose of allocated resources.
        /// </summary>
        void StopTracking();
    }
}