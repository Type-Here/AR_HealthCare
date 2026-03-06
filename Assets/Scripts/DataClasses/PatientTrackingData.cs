using UnityEngine;

namespace ARHealthCare.DataClasses
{
    /// <summary>
    /// Defines the tracking data for medical overlay with spatial metadata.
    /// M2: Extended with distance estimation and body scale for dynamic anchoring.
    /// </summary>
    public struct PatientTrackingData
    {
        public bool IsTracked;
        
        /// <summary>
        /// Dictionary of body joints. Key: Joint name (from LandMarkPoints), Value: World space Pose.
        /// </summary>
        public System.Collections.Generic.Dictionary<string, Pose> Joints;
        
        /// <summary>
        /// M2: Estimated distance from camera to patient body center (hips) in meters.
        /// Used for dynamic avatar scaling and spatial anchoring.
        /// Set to 0 if distance estimation is unavailable.
        /// </summary>
        public float EstimatedDistance;
        
        /// <summary>
        /// M2: Estimated patient body height in meters (shoulder-to-hip span or full height).
        /// Used for avatar scale matching. Set to 0 if unavailable.
        /// </summary>
        public float EstimatedBodyHeight;
    }
}