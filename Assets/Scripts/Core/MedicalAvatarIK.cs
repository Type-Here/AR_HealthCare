using UnityEngine;
using ARHealthCare.Core;
using ARHealthCare.DataClasses;
using ARHealthCare.Trackers;

namespace ARHealthCare.Visuals
{
    [RequireComponent(typeof(Animator))]
    public class MedicalAvatarIK : MonoBehaviour
    {
        private Animator _animator;

        [Header("Weights")]
        [Range(0, 1)] public float globalWeight = 1.0f;
        [Range(0, 1)] public float bodyWeight = 1.0f;
        [Range(0, 1)] public float headWeight = 0.8f;
        [Range(0, 1)] public float handsWeight = 1.0f;
        [Range(0, 1)] public float feetWeight = 0.8f;

        [Header("Smoothing")]
        [Tooltip("Higher = snappier. 10–20 is a good start.")]
        public float positionLerpSpeed = 12f;
        public float rotationSlerpSpeed = 12f;

        [Header("Tracking Provider")]
        [SerializeField] private MonoBehaviour trackingProviderBehaviour;
        private ITrackingProvider trackingProvider;

        [Header("Debug")]
        public bool logOnce = false;
        private bool _logged;

        private void Awake()
        {
            _animator = GetComponent<Animator>();
            _animator.cullingMode = AnimatorCullingMode.AlwaysAnimate; // XR-safe

            if (trackingProviderBehaviour == null)
            {
                var manager = Object.FindFirstObjectByType<TrackingManager>();
                if (manager != null) trackingProviderBehaviour = manager;
            }

            trackingProvider = trackingProviderBehaviour as ITrackingProvider;
            if (trackingProvider == null)
                Debug.LogError("MedicalAvatarIK: trackingProviderBehaviour is null or does not implement ITrackingProvider.");
        }

        private void OnAnimatorIK(int layerIndex)
        {
            if (_animator == null || trackingProvider == null) return;

            var data = trackingProvider.GetPose();
            if (!data.IsTracked || data.Joints == null || data.Joints.Count == 0)
            {
                ResetIK();
                return;
            }

            if (logOnce && !_logged)
            {
                Debug.Log($"MedicalAvatarIK: IK running. Joints={data.Joints.Count} provider={trackingProvider.GetType().Name}");
                _logged = true;
            }

            // --- ROOT / BODY (hips + torso orientation) ---
            ApplyBodyRoot(data);

            // --- HANDS ---
            ApplyGoalIK(AvatarIKGoal.LeftHand,  "LeftWrist",  data, handsWeight);
            ApplyGoalIK(AvatarIKGoal.RightHand, "RightWrist", data, handsWeight);

            // --- FEET ---
            ApplyGoalIK(AvatarIKGoal.LeftFoot,  "LeftAnkle",  data, feetWeight);
            ApplyGoalIK(AvatarIKGoal.RightFoot, "RightAnkle", data, feetWeight);

            // --- HEAD / LOOK ---
            ApplyHeadLook(data);

            // Optional: elbows/knees hints (helps stability)
            ApplyHintIK(AvatarIKHint.LeftElbow,  "LeftElbow",  data, 0.4f);
            ApplyHintIK(AvatarIKHint.RightElbow, "RightElbow", data, 0.4f);
            ApplyHintIK(AvatarIKHint.LeftKnee,   "LeftKnee",   data, 0.4f);
            ApplyHintIK(AvatarIKHint.RightKnee,  "RightKnee",  data, 0.4f);
        }

        private void ApplyBodyRoot(PatientTrackingData data)
        {
            // Need hips and shoulders to orient the torso reliably
            bool hasLH = TryGet(data, "LeftHip", out var lHip);
            bool hasRH = TryGet(data, "RightHip", out var rHip);
            bool hasLS = TryGet(data, "LeftShoulder", out var lSh);
            bool hasRS = TryGet(data, "RightShoulder", out var rSh);

            if (!(hasLH && hasRH)) return;

            var hipsMid = 0.5f * (lHip.position + rHip.position);

            // Position: drive body position (root) toward hips mid
            var targetPos = hipsMid;

            // Rotation: build a torso frame if shoulders exist; otherwise keep current
            Quaternion targetRot = _animator.bodyRotation;

            if (hasLS && hasRS)
            {
                // Right vector: from left->right shoulder (or hip) defines lateral axis
                var right = (rSh.position - lSh.position).normalized;

                // Up vector: hips->shoulders center
                var shouldersMid = 0.5f * (lSh.position + rSh.position);
                var up = (shouldersMid - hipsMid).normalized;

                // Forward = right x up (ensure non-degenerate)
                var forward = Vector3.Cross(right, up).normalized;
                if (forward.sqrMagnitude > 0.001f && up.sqrMagnitude > 0.001f)
                {
                    targetRot = Quaternion.LookRotation(forward, up);
                }
            }

            // Smooth
            float pT = 1f - Mathf.Exp(-positionLerpSpeed * Time.deltaTime);
            float rT = 1f - Mathf.Exp(-rotationSlerpSpeed * Time.deltaTime);

            _animator.bodyPosition = Vector3.Lerp(_animator.bodyPosition, targetPos, pT * bodyWeight * globalWeight);
            _animator.bodyRotation = Quaternion.Slerp(_animator.bodyRotation, targetRot, rT * bodyWeight * globalWeight);
        }

        /// <summary>
        /// Applies IK to a specific AvatarIKGoal (hand/foot).
        /// Uses the jointKey to look up the corresponding Pose in the tracking data.
        /// Differs from ApplyHintIK in that it sets position and rotation for goals.
        /// </summary>
        /// <param name="goal"></param>
        /// <param name="jointKey"></param>
        /// <param name="data"></param>
        /// <param name="weight"></param>
        private void ApplyGoalIK(AvatarIKGoal goal, string jointKey, PatientTrackingData data, float weight)
        {
            if (!TryGet(data, jointKey, out var pose))
            {
                _animator.SetIKPositionWeight(goal, 0);
                _animator.SetIKRotationWeight(goal, 0);
                return;
            }

            float w = Mathf.Clamp01(globalWeight * weight);

            _animator.SetIKPositionWeight(goal, w);
            _animator.SetIKRotationWeight(goal, 0); // start with rotation off (more stable)

            _animator.SetIKPosition(goal, pose.position);

            // If you later compute a reliable joint rotation, enable this:
            // _animator.SetIKRotationWeight(goal, w);
            // _animator.SetIKRotation(goal, pose.rotation);
        }
        
        /// <summary>
        /// Applies IK hint position to a specific AvatarIKHint (elbow/knee).
        /// Uses the jointKey to look up the corresponding Pose in the tracking data.
        /// Differs from ApplyGoalIK in that it only sets hint position.
        /// </summary>
                
        private void ApplyHintIK(AvatarIKHint hint, string jointKey, PatientTrackingData data, float weight)
        {
            if (!TryGet(data, jointKey, out var pose))
            {
                _animator.SetIKHintPositionWeight(hint, 0);
                return;
            }

            float w = Mathf.Clamp01(globalWeight * weight);
            _animator.SetIKHintPositionWeight(hint, w);
            _animator.SetIKHintPosition(hint, pose.position);
        }

        private void ApplyHeadLook(PatientTrackingData data)
        {
            // Use Nose as a look target (more stable than forcing head bone position)
            if (!TryGet(data, "Nose", out var nose)) return;

            float w = Mathf.Clamp01(globalWeight * headWeight);

            _animator.SetLookAtWeight(w, bodyWeight: 0.3f, headWeight: 0.8f, eyesWeight: 0f, clampWeight: 0.7f);
            _animator.SetLookAtPosition(nose.position);
        }

        private void ResetIK()
        {
            _animator.SetIKPositionWeight(AvatarIKGoal.LeftHand, 0);
            _animator.SetIKRotationWeight(AvatarIKGoal.LeftHand, 0);
            _animator.SetIKPositionWeight(AvatarIKGoal.RightHand, 0);
            _animator.SetIKRotationWeight(AvatarIKGoal.RightHand, 0);

            _animator.SetIKPositionWeight(AvatarIKGoal.LeftFoot, 0);
            _animator.SetIKRotationWeight(AvatarIKGoal.LeftFoot, 0);
            _animator.SetIKPositionWeight(AvatarIKGoal.RightFoot, 0);
            _animator.SetIKRotationWeight(AvatarIKGoal.RightFoot, 0);

            _animator.SetLookAtWeight(0);

            _animator.SetIKHintPositionWeight(AvatarIKHint.LeftElbow, 0);
            _animator.SetIKHintPositionWeight(AvatarIKHint.RightElbow, 0);
            _animator.SetIKHintPositionWeight(AvatarIKHint.LeftKnee, 0);
            _animator.SetIKHintPositionWeight(AvatarIKHint.RightKnee, 0);
        }

        private static bool TryGet(PatientTrackingData data, string key, out Pose pose)
        {
            if (data.Joints != null && data.Joints.TryGetValue(key, out pose))
                return true;

            pose = default;
            return false;
        }
    }
}
