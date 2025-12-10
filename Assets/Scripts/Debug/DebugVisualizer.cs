using UnityEngine;
using ARHealthCare.Core;
using ARHealthCare.DataClasses;

public class DebugVisualizer : MonoBehaviour
{
    [Header("Joint to visualize")]
    public string jointName = "RightHand";

    [Header("Tracking Provider")]
    [Tooltip("Drag here any component that implements ITrackingProvider (e.g. TrackingManager).")]
    [SerializeField] private MonoBehaviour trackingProviderBehaviour;

    private ITrackingProvider trackingProvider;

    void Awake()
    {
        // Try to resolve provider from Inspector
        if (trackingProviderBehaviour == null)
        {
            // Fallback: first TrackingManager in the scene
            var manager = FindObjectOfType<TrackingManager>();
            if (manager != null)
            {
                trackingProviderBehaviour = manager;
            }
        }

        trackingProvider = trackingProviderBehaviour as ITrackingProvider;
        if (trackingProvider == null)
        {
            Debug.LogError("DebugVisualizer: trackingProviderBehaviour is not set or does not implement ITrackingProvider.");
        }
    }

    void Update()
    {
        if (trackingProvider == null) return;

        // 1. Request data from the provider
        PatientTrackingData data = trackingProvider.GetPose();

        // 2. If we have data and the body part exists...
        if (data.IsTracked && data.Joints != null && data.Joints.ContainsKey(jointName))
        {
            // 3. Move the cube
            transform.position = data.Joints[jointName].position;
            transform.rotation = data.Joints[jointName].rotation;
        }
    }
}
