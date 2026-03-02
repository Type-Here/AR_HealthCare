using UnityEngine;
using ARHealthCare.Visuals;

namespace ARHealthCare.UI
{
    public class PatientInfoFollowBody : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Transform bodyTarget;
        [SerializeField] private Camera targetCamera;
        [SerializeField] private bool autoFindMedicalAvatar = true;

        [Header("Placement")]
        [Tooltip("Offset applied in body local space.")]
        [SerializeField] private Vector3 localOffset = new Vector3(0f, 0.25f, 0f);
        [Tooltip("Distance in front of body (meters).")]
        [SerializeField] private float forwardDistance = 0.45f;

        [Header("Smoothing")]
        [SerializeField] private float positionLerpSpeed = 8f;
        [SerializeField] private float rotationSlerpSpeed = 8f;

        [Header("Facing")]
        [SerializeField] private bool faceCamera = true;
        [SerializeField] private bool yawOnly = true;

        private void Awake()
        {
            if (targetCamera == null)
                targetCamera = Camera.main;

            if (autoFindMedicalAvatar && bodyTarget == null)
            {
                var avatar = Object.FindFirstObjectByType<MedicalAvatarIK>();
                if (avatar != null)
                    bodyTarget = avatar.transform;
            }
        }

        private void LateUpdate()
        {
            if (bodyTarget == null)
                return;

            var flatForward = bodyTarget.forward;
            flatForward.y = 0f;

            if (flatForward.sqrMagnitude < 0.0001f && targetCamera != null)
            {
                flatForward = targetCamera.transform.forward;
                flatForward.y = 0f;
            }

            if (flatForward.sqrMagnitude < 0.0001f)
                flatForward = Vector3.forward;

            flatForward.Normalize();

            var targetPos = bodyTarget.TransformPoint(localOffset) + flatForward * forwardDistance;
            transform.position = Vector3.Lerp(transform.position, targetPos, Time.deltaTime * positionLerpSpeed);

            if (!faceCamera || targetCamera == null)
                return;

            var lookDir = targetCamera.transform.position - transform.position;
            if (yawOnly)
                lookDir.y = 0f;

            if (lookDir.sqrMagnitude < 0.0001f)
                return;

            var targetRot = Quaternion.LookRotation(lookDir.normalized, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * rotationSlerpSpeed);
        }
    }
}
