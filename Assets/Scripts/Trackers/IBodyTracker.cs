using ARHealthCare.DataClasses;

/// <summary>
/// Interface for body tracking systems
/// This allows for different implementations
/// </summary>
/// 
namespace ARHealthCare.Trackers {
    public interface IBodyTracker{
        // Initializes the body tracking system.
        void Initialize();

        // Returns current tracking data (called every frame)
        PatientTrackingData GetTrackingData();

        // Stops the body tracking system.
        void StopTracking();
    }

}