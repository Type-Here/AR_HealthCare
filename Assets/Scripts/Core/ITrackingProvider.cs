using ARHealthCare.DataClasses;

namespace ARHealthCare.Core
{
    /// <summary>
    /// Public API interface for consuming patient tracking data in the ARHealthCare system.
    /// Implemented by TrackingManager to provide a clean abstraction between
    /// tracking backends (IBodyTracker) and visualization consumers (e.g., MedicalAvatarIK).
    /// Consumers should depend on ITrackingProvider, not concrete tracker implementations.
    /// </summary>
    public interface ITrackingProvider
    {
        /// <summary>
        /// Gets the current patient pose/tracking data.
        /// Returns the latest cached tracking data from the active tracker.
        /// Safe to call every frame from OnAnimatorIK or Update.
        /// </summary>
        /// <returns>
        /// PatientTrackingData containing:
        /// - IsTracked: Whether a patient is currently being tracked
        /// - Joints: Dictionary of body joint positions (world space)
        /// - EstimatedDistance: Camera-to-patient distance in meters
        /// - EstimatedBodyHeight: Patient body height in meters
        /// Returns empty/default struct if no tracker is active or tracking is lost.
        /// </returns>
        PatientTrackingData GetPose();
    }
}