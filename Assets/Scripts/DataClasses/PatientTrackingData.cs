using UnityEngine;

namespace ARHealthCare.DataClasses
{
    // Defines the data we need for medical overlay
    public struct PatientTrackingData
    {
        public bool IsTracked;
        // We use a Dictionary to easily access body parts (e.g., "LeftShoulder")
        // Key: Joint name, Value: Position/Rotation
        public System.Collections.Generic.Dictionary<string, Pose> Joints;
    }
}