using UnityEngine;
using ARHealthCare.Core;

public class DebugVisualizer : MonoBehaviour {
    // Write "RightHand" in the inspector
    public string jointName = "RightHand"; 

    void Update() {
        // 1. Request data from the Manager
        var data = TrackingManager.Instance.GetPose();
        
        // 2. If we have data and the body part exists...
        if (data.IsTracked && data.Joints != null && data.Joints.ContainsKey(jointName)){
            // 3. Move the cube
            transform.position = data.Joints[jointName].position;
            transform.rotation = data.Joints[jointName].rotation;
        }
    }
}