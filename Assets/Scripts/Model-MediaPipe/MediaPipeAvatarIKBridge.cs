using UnityEngine;
using Mediapipe.Unity.ModifiedSample.PoseLandmarkDetection;
using Mediapipe.Tasks.Vision.PoseLandmarker;

public class MediaPipeAvatarIKBridge : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PoseLandmarkerRunner runner;
    [SerializeField] private MediaPipeAvatarIK avatarIK;

    [Header("World-landmark → Unity conversion")]
    [SerializeField] private bool invertX = true;
    [SerializeField] private bool invertY = false;
    [SerializeField] private bool invertZ = false;
    [SerializeField] private float scale = 1f;

    [SerializeField] private bool offsetFromCamera = true;
    [SerializeField] private Vector3 manualOffset = new Vector3(0, 0, 2f);
    [SerializeField] private Camera mainCamera;

    private readonly Vector3[] _points = new Vector3[33];

    private void Reset()
    {
        runner = FindFirstObjectByType<PoseLandmarkerRunner>();
        avatarIK = FindFirstObjectByType<MediaPipeAvatarIK>();
        mainCamera = Camera.main;
    }

    private void Awake()
    {
        if (mainCamera == null) mainCamera = Camera.main;
    }

    private void OnEnable()
    {
        if (runner != null)
            runner.ResultUpdated += OnResultUpdated;
    }

    private void OnDisable()
    {
        if (runner != null)
            runner.ResultUpdated -= OnResultUpdated;
    }

    private void OnResultUpdated(PoseLandmarkerResult result)
    {
        if (avatarIK == null) return;
        if (result == null) return;
        if (result.poseWorldLandmarks == null || result.poseWorldLandmarks.Count == 0) return;

        var landmarks = result.poseWorldLandmarks[0].landmarks;
        if (landmarks == null || landmarks.Count < 33) return;

        for (int i = 0; i < 33; i++)
        {
            var lm = landmarks[i];

            float x = lm.x * (invertX ? -1f : 1f);
            float y = lm.y * (invertY ? -1f : 1f);
            float z = lm.z * (invertZ ? -1f : 1f);

            Vector3 p = new Vector3(x, y, z) * scale;

            if (offsetFromCamera && mainCamera != null)
            {
                p += mainCamera.transform.position
                   + mainCamera.transform.right   * manualOffset.x
                   + mainCamera.transform.up      * manualOffset.y
                   + mainCamera.transform.forward * manualOffset.z;
            }
            else
            {
                p += manualOffset;
            }

            _points[i] = p;
        }

        // Since conversion is done here, keep IK script neutral:
        // MediaPipeAvatarIK: movementScale = 1, globalOffset = (0,0,0)
        avatarIK.UpdatePose(_points);
    }
}