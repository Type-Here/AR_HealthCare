using System.Collections.Generic;
using UnityEngine;
using ARHealthCare.Core;
using ARHealthCare.DataClasses;
using ARHealthCare.Trackers;

namespace ARHealthCare.Visuals
{
    /// <summary>
    /// Drives a humanoid avatar's IK and body positioning based on patient tracking data.
    /// Supports modular tracking providers (MediaPipe, Mock, etc.) via ITrackingProvider.
    /// Ready for production with smoothing, weights, and spatial anchor support.
    /// </summary>
    [RequireComponent(typeof(Animator))]
    public class MedicalAvatarIKv2 : MonoBehaviour
    {
        private Animator _animator;

        [Header("IK Weights")]
        [Range(0, 1)] public float globalWeight = 1.0f;
        [Range(0, 1)] public float bodyWeight = 1.0f;
        [Range(0, 1)] public float headWeight = 0.8f;
        [Range(0, 1)] public float handsWeight = 1.0f;
        [Range(0, 1)] public float feetWeight = 0.8f;

        [Header("Smoothing")]
        [Tooltip("Higher = snappier response. Recommended: 10-20.")]
        public float positionLerpSpeed = 12f;
        [Tooltip("Higher = snappier rotation. Recommended: 10-20.")]
        public float rotationSlerpSpeed = 12f;
        [Header("M4.1 - Advanced Smoothing")]
        [Tooltip("Minimum movement threshold (meters) to filter micro-jitter. Recommended: 0.01-0.03.")]
        public float movementDeadZone = 0.02f;
        [Tooltip("Enable velocity-based adaptive smoothing (faster movement = less smoothing).")]
        public bool useAdaptiveSmoothing = true;
        [Tooltip("Maximum velocity (m/s) for adaptive smoothing normalization.")]
        public float maxVelocity = 2.0f;

        [Header("Spatial Anchor (Milestone 2)")]
        [Tooltip("Enable to anchor avatar to patient body center (hips).")]
        public bool useAnchor = true;
        [Tooltip("Offset from patient hips to avatar root (meters).")]
        public Vector3 anchorOffset = Vector3.zero;
        [Tooltip("Quick vertical shift of the whole avatar (positive = up). Use this to compensate for the avatar appearing too low/high relative to the real body.")]
        public float verticalBodyOffset = 0.3f;
        
        [Header("Dynamic Scaling (M2)")]
        [Tooltip("Enable dynamic avatar scaling based on estimated patient distance and body size.")]
        public bool useDynamicScaling = true;
        [Tooltip("Expected patient-to-camera distance in meters (used in manual mode or for validation).")]
        public float expectedPatientDistance = 1.5f;
        [Tooltip("Manual avatar scale multiplier (used when useDynamicScaling is false).")]
        [Range(0.5f, 2.0f)]
        public float manualAvatarScale = 1.0f;
        [Tooltip("Reference body height for scale calculation (average human ~1.7m).")]
        public float referenceBodyHeight = 1.7f;

        [Header("Arm / Leg IK")]
        [Tooltip("Weight for elbow pole-vector hints (1 = full influence on bend direction).")]
        [Range(0, 1)] public float elbowHintWeight = 1.0f;
        [Tooltip("Weight for knee pole-vector hints. Keep at 0 unless knee tracking is reliable in 3D — " +
                 "noisy 2D-projected knee positions cause leg flipping/instability.")]
        [Range(0, 1)] public float kneeHintWeight = 0.0f;

        [Header("Tracking Provider")]
        [SerializeField] private MonoBehaviour trackingProviderBehaviour;
        private ITrackingProvider trackingProvider;

        [Header("Debug")]
        public bool logOnce = true;
        private bool _logged;
        [Tooltip("Draw debug gizmos in Scene view for anchor and key landmarks.")]
        public bool drawGizmos = true;

        // M4.6: Cached camera reference for performance
        private Camera _cachedMainCamera;

        // M4.7: Tracking loss handling
        private float _trackingLossTime = 0f;
        private const float TRACKING_LOSS_TIMEOUT = 5f;
        private Dictionary<string, Vector3> _lastValidPositions = new Dictionary<string, Vector3>();
        private bool _isRecoveringFromLoss = false;
        private float _recoveryBlendTime = 0f;
        private const float RECOVERY_BLEND_DURATION = 1.5f;

        private void Awake()
        {
            _animator = GetComponent<Animator>();
            if (_animator == null)
            {
                Debug.LogError("MedicalAvatarIK: Animator component not found. Ensure this is attached to a humanoid avatar.");
                return;
            }

            _animator.cullingMode = AnimatorCullingMode.AlwaysAnimate; // XR-safe

            // M4.6: Cache Camera.main reference to avoid expensive per-frame lookups
            _cachedMainCamera = Camera.main;

            // Apply manual avatar scale if dynamic scaling is disabled
            if (!useDynamicScaling && Mathf.Abs(manualAvatarScale - 1.0f) > 0.01f)
            {
                transform.localScale = Vector3.one * manualAvatarScale;
                Debug.Log($"MedicalAvatarIK: Manual avatar scale set to {manualAvatarScale}");
            }

            // Auto-find TrackingManager if not assigned
            if (trackingProviderBehaviour == null)
            {
                var manager = Object.FindFirstObjectByType<TrackingManager>();
                if (manager != null)
                {
                    trackingProviderBehaviour = manager;
                    Debug.Log("MedicalAvatarIK: Auto-assigned TrackingManager.");
                }
            }

            trackingProvider = trackingProviderBehaviour as ITrackingProvider;
            if (trackingProvider == null)
                Debug.LogError("MedicalAvatarIK: trackingProviderBehaviour is null or does not implement ITrackingProvider. Assign a TrackingManager in the inspector.");
        }


        /// <summary>
        /// Unity callback for IK updates. Called by the Animator after it evaluates the animation pose.
        /// </summary>
        /// <param name="layerIndex"> The index of the Animator layer being evaluated (not used here).</param>
        private void OnAnimatorIK(int layerIndex)
        {
            // Debug.Log("OnAnimatorIK called");

            if (_animator == null || trackingProvider == null) return;
#if  AR_HEALTHCARE_DEBUG
            // M4.6: Profiling marker for performance analysis
            UnityEngine.Profiling.Profiler.BeginSample("MedicalAvatarIK.OnAnimatorIK");
#endif
            var data = trackingProvider.GetPose();

#if  AR_HEALTHCARE_DEBUG
            Debug.Log($"Tracking data received. Joints count: {data.Joints?.Count ?? 0}," + 
            $" EstimatedDistance: {data.EstimatedDistance:F2}m, EstimatedBodyHeight: {data.EstimatedBodyHeight:F2}m");
#endif
            // M4.7: Tracking loss handling
            if (!data.IsTracked || data.Joints == null || data.Joints.Count == 0)
            {
                _trackingLossTime += Time.deltaTime;
                
                if (_trackingLossTime > TRACKING_LOSS_TIMEOUT)
                {
                    // Timeout: reset to default pose
                    ResetIK();
                    if (logOnce && !_logged)
                    {
                        Debug.LogWarning($"MedicalAvatarIK: Tracking lost for {TRACKING_LOSS_TIMEOUT}s, resetting to T-pose.");
                        _logged = true;
                    }
                }
                // else: maintain last position (freeze)
 #if AR_HEALTHCARE_DEBUG               
                UnityEngine.Profiling.Profiler.EndSample();
#endif
                return;
            }
            
            // Tracking recovered
            if (_trackingLossTime > 0.5f)
            {
                // Was lost for >0.5s, enable recovery blend-in
                _isRecoveringFromLoss = true;
                _recoveryBlendTime = 0f;
                if (logOnce)
                {
                    Debug.Log($"MedicalAvatarIK: Tracking recovered after {_trackingLossTime:F2}s, blending in...");
                }
            }
            _trackingLossTime = 0f;

            if (logOnce && !_logged)
            {
                Debug.Log($"MedicalAvatarIK: IK running. Joints={data.Joints.Count} provider={trackingProvider.GetType().Name}");
                _logged = true;
            }

            // M4.7: Recovery blend-in
            if (_isRecoveringFromLoss)
            {
                _recoveryBlendTime += Time.deltaTime;
                if (_recoveryBlendTime >= RECOVERY_BLEND_DURATION)
                {
                    _isRecoveringFromLoss = false;
                }
            }

            // --- ROOT / BODY (hips + torso orientation) ---
            ApplyBodyRoot(data);

            // --- HANDS ---
            ApplyHandIK(AvatarIKGoal.LeftHand,  "LeftWrist",  "LeftElbow",  "LeftShoulder",  data, handsWeight);
            ApplyHandIK(AvatarIKGoal.RightHand, "RightWrist", "RightElbow", "RightShoulder", data, handsWeight);

            // --- FEET ---
            ApplyGoalIK(AvatarIKGoal.LeftFoot,  "LeftAnkle",  data, feetWeight);
            ApplyGoalIK(AvatarIKGoal.RightFoot, "RightAnkle", data, feetWeight);

            // --- HEAD / LOOK ---
            ApplyHeadLook(data);

            // Elbow hints — raw landmark position, no bias.
            ApplyHintIK(AvatarIKHint.LeftElbow,  "LeftElbow",  data, elbowHintWeight);
            ApplyHintIK(AvatarIKHint.RightElbow, "RightElbow", data, elbowHintWeight);
            // Knee hints — raw landmark position, no bias.
            ApplyHintIK(AvatarIKHint.LeftKnee,  "LeftKnee",  data, kneeHintWeight);
            ApplyHintIK(AvatarIKHint.RightKnee, "RightKnee", data, kneeHintWeight);
            
#if  AR_HEALTHCARE_DEBUG 
            UnityEngine.Profiling.Profiler.EndSample();
#endif
        }

        private void ApplyBodyRoot(PatientTrackingData data)
        {
            var joints = data.Joints;
            if (joints == null) return;
            if (!joints.TryGetValue("LeftHip", out Pose lHip) ||
                !joints.TryGetValue("RightHip", out Pose rHip) ||
                !joints.TryGetValue("LeftShoulder", out Pose lShoulder) ||
                !joints.TryGetValue("RightShoulder", out Pose rShoulder))
                return;

            Vector3 hipsMid = (lHip.position + rHip.position) * 0.5f;
            Vector3 shoulderMid = (lShoulder.position + rShoulder.position) * 0.5f;

            // Anchor: position avatar root on hips
            Vector3 targetRootPos = hipsMid + anchorOffset + Vector3.up * verticalBodyOffset;
            transform.position = Vector3.Lerp(transform.position, targetRootPos,
                                              Time.deltaTime * positionLerpSpeed);

            // Rotate torso
            // anatomical up: from hips to shoulder (world space already converted)
            Vector3 anatomicalUp = (shoulderMid - hipsMid).normalized;

            // Sanity check: if up vector is near zero or degenerate, skip rotation to avoid NaNs
            if (anatomicalUp.sqrMagnitude < 0.01f) return;

            // Null-guard: if no camera, skip rotation this frame
            if (_cachedMainCamera == null)
            {
                _cachedMainCamera = Camera.main;
                if (_cachedMainCamera == null) return;
            }

            // Forward: avatar faces toward the camera.
            // Project (camera → hips) onto the plane perpendicular to anatomicalUp.
            Vector3 toCam = (_cachedMainCamera.transform.position - hipsMid).normalized;
            Vector3 forwardRaw = toCam - Vector3.Dot(toCam, anatomicalUp) * anatomicalUp;

            if (forwardRaw.sqrMagnitude < 0.01f)
            {
                // Degenerate (patient directly above/below camera) → project camera.forward
                forwardRaw = _cachedMainCamera.transform.forward;
                forwardRaw = forwardRaw - Vector3.Dot(forwardRaw, anatomicalUp) * anatomicalUp;
            }

            Vector3 anatomicalForward = forwardRaw.normalized;

            // LookRotation(forward, up) → stable quaternion, no cross product
            Quaternion targetRot = Quaternion.LookRotation(anatomicalForward, anatomicalUp);

            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot,
                                                  Time.deltaTime * rotationSlerpSpeed);

            // Sync body IK with tranform (Animator.bodyPosition relative to root)
            _animator.bodyPosition = transform.position;
            _animator.bodyRotation = transform.rotation;
        }

        /// <summary>
        /// Applies hand IK position + rotation derived from the forearm direction (elbow→wrist).
        /// The rotation gives the 2-bone IK solver a full arm configuration constraint,
        /// producing correct elbow bending even at large angles.
        /// Falls back to position-only IK if the elbow landmark is unavailable.
        /// </summary>
        private void ApplyHandIK(AvatarIKGoal goal, string wristKey, string elbowKey, string shoulderKey,
                                 PatientTrackingData data, float weight)
        {
            if (!TryGet(data, wristKey, out var wristPose))
            {
                _animator.SetIKPositionWeight(goal, 0);
                _animator.SetIKRotationWeight(goal, 0);
                return;
            }

            // ── position (same smoothing as ApplyGoalIK) ──────────────────────
            Vector3 targetPos = wristPose.position;

            // Clamp wrist target to ~95% of estimated arm reach.
            // When the arm is nearly fully extended the 2-bone solver reaches a singularity
            // and cannot produce a visible bend regardless of the elbow hint.
            // Keeping a small guaranteed bend makes the elbow hint effective at all angles.
            if (TryGet(data, shoulderKey, out var shoulderPose))
            {
                Vector3 toWrist = targetPos - shoulderPose.position;
                float currentDist = toWrist.magnitude;
                if (currentDist > 0.001f)
                {
                    // Estimate full arm length as 2 × shoulder→elbow segment (or fallback 0.65m)
                    float maxReach = 0.62f;
                    if (TryGet(data, elbowKey, out var elbowPoseEst))
                        maxReach = Vector3.Distance(shoulderPose.position, elbowPoseEst.position) * 1.9f;
                    maxReach = Mathf.Clamp(maxReach, 0.35f, 0.75f) * 0.95f; // 95% of reach
                    if (currentDist > maxReach)
                        targetPos = shoulderPose.position + toWrist.normalized * maxReach;
                }
            }
            string cacheKey = $"{goal}_{wristKey}";

            if (_lastValidPositions.TryGetValue(cacheKey, out Vector3 lastPos))
            {
                float movementDist = Vector3.Distance(targetPos, lastPos);
                if (movementDist < movementDeadZone)
                    targetPos = lastPos;
                else if (useAdaptiveSmoothing)
                {
                    float velocity = movementDist / Time.deltaTime;
                    float normalizedVelocity = Mathf.Clamp01(velocity / maxVelocity);
                    float adaptiveSpeed = Mathf.Lerp(positionLerpSpeed * 0.5f, positionLerpSpeed * 1.5f, normalizedVelocity);
                    float alpha = 1f - Mathf.Exp(-adaptiveSpeed * Time.deltaTime);
                    targetPos = Vector3.Lerp(lastPos, targetPos, alpha);
                }
            }
            _lastValidPositions[cacheKey] = targetPos;

            float w = Mathf.Clamp01(globalWeight * weight);
            if (_isRecoveringFromLoss)
                w *= Mathf.Clamp01(_recoveryBlendTime / RECOVERY_BLEND_DURATION);

            _animator.SetIKPositionWeight(goal, w);
            _animator.SetIKPosition(goal, targetPos);

            // SetIKRotation controls WRIST TWIST, not elbow bend in Unity Humanoid IK.
            // Using it with forearm direction causes elbow hyperextension artefacts when
            // the arm is straight. Elbow bend direction is controlled exclusively via
            // SetIKHintPosition (ApplyHintIK). Keep rotation weight at zero.
            _animator.SetIKRotationWeight(goal, 0);
        }

        /// <summary>
        /// Applies IK to a specific AvatarIKGoal (hand/foot) — position only.
        /// </summary>
        private void ApplyGoalIK(AvatarIKGoal goal, string jointKey, PatientTrackingData data, float weight)
        {
            if (!TryGet(data, jointKey, out var pose))
            {
                _animator.SetIKPositionWeight(goal, 0);
                _animator.SetIKRotationWeight(goal, 0);
                return;
            }

            // M4.1: Dead-zone filtering to reduce micro-jitter
            Vector3 targetPos = pose.position;
            string cacheKey = $"{goal}_{jointKey}";
            
            if (_lastValidPositions.ContainsKey(cacheKey))
            {
                Vector3 lastPos = _lastValidPositions[cacheKey];
                float movementDist = Vector3.Distance(targetPos, lastPos);
                
                // M4.1: Apply dead-zone threshold
                if (movementDist < movementDeadZone)
                {
                    targetPos = lastPos; // Skip micro-movement
                }
                else if (useAdaptiveSmoothing)
                {
                    // M4.1: Velocity-based adaptive smoothing
                    float velocity = movementDist / Time.deltaTime;
                    float normalizedVelocity = Mathf.Clamp01(velocity / maxVelocity);
                    // Higher velocity → less smoothing (more responsive)
                    float adaptiveSpeed = Mathf.Lerp(positionLerpSpeed * 0.5f, positionLerpSpeed * 1.5f, normalizedVelocity);
                    float alpha = 1f - Mathf.Exp(-adaptiveSpeed * Time.deltaTime);
                    targetPos = Vector3.Lerp(lastPos, targetPos, alpha);
                }
            }
            
            _lastValidPositions[cacheKey] = targetPos;

            float w = Mathf.Clamp01(globalWeight * weight);
            
            // M4.7: Apply recovery blend-in weight
            if (_isRecoveringFromLoss)
            {
                float recoveryWeight = Mathf.Clamp01(_recoveryBlendTime / RECOVERY_BLEND_DURATION);
                w *= recoveryWeight;
            }

            _animator.SetIKPositionWeight(goal, w);
            _animator.SetIKRotationWeight(goal, 0); // start with rotation off (more stable)

            _animator.SetIKPosition(goal, targetPos);

            // If you later compute a reliable joint rotation, enable this:
            // _animator.SetIKRotationWeight(goal, w);
            // _animator.SetIKRotation(goal, pose.rotation);
        }
        
        /// <summary>
        /// Applies IK hint position to a specific AvatarIKHint (elbow/knee).
        /// Passes the raw landmark position directly — no bias modification.
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

        /// <summary>
        /// Applies head look-at tracking using the Nose landmark as target.
        /// Uses Unity's built-in LookAt IK for natural head orientation.
        /// More stable than directly manipulating head bone transforms.
        /// </summary>
        private void ApplyHeadLook(PatientTrackingData data)
        {
            // Use Nose as a look target (more stable than forcing head bone position)
            if (!TryGet(data, "Nose", out var nose)) return;

            float w = Mathf.Clamp01(globalWeight * headWeight);

            // SetLookAtWeight params: weight, bodyWeight, headWeight, eyesWeight, clampWeight
            // bodyWeight: how much body bends (0.3 = minimal torso rotation)
            // headWeight: how much head turns (0.8 = strong head tracking)
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

#if UNITY_EDITOR
        /// <summary>
        /// Draw debug gizmos in Scene view to visualize anchor point and key landmarks.
        /// Helps verify spatial anchoring and patient-avatar alignment in M2.
        /// </summary>
        private void OnDrawGizmos()
        {
            if (!drawGizmos || trackingProvider == null) return;

            var data = trackingProvider.GetPose();
            if (!data.IsTracked || data.Joints == null) return;

            // Draw hips anchor point (red sphere)
            if (TryGet(data, "LeftHip", out var lHip) && TryGet(data, "RightHip", out var rHip))
            {
                var hipsMid = 0.5f * (lHip.position + rHip.position);
                Gizmos.color = Color.red;
                Gizmos.DrawWireSphere(hipsMid, 0.05f);

                // Draw anchor offset target (yellow sphere)
                if (useAnchor)
                {
                    var anchorPos = hipsMid + anchorOffset;
                    Gizmos.color = Color.yellow;
                    Gizmos.DrawWireSphere(anchorPos, 0.08f);
                    Gizmos.DrawLine(hipsMid, anchorPos);
                }
            }

            // Draw key landmarks (cyan)
            Gizmos.color = Color.cyan;
            var keyJoints = new[] { "Nose", "LeftWrist", "RightWrist", "LeftAnkle", "RightAnkle", "LeftShoulder", "RightShoulder" };
            foreach (var joint in keyJoints)
            {
                if (TryGet(data, joint, out var pose))
                {
                    Gizmos.DrawWireSphere(pose.position, 0.03f);
                }
            }
        }
#endif
    }
}
