using System.Collections.Generic;
using UnityEngine;
using ARHealthCare.Core;
using ARHealthCare.DataClasses;

namespace ARHealthCare.Visuals
{
    /// <summary>
    /// Drives a humanoid avatar using direct bone rotation driving + foot IK with raycast grounding.
    /// Adapted from ZED SDK's ZEDSkeletonAnimator for use with ARHealthCare ITrackingProvider.
    ///
    /// Key differences from MedicalAvatarIK:
    ///   - Drives ALL limb bones directly via SetBoneLocalRotation (not just IK endpoint goals).
    ///     → eliminates the 2-bone singularity that prevents elbow/knee bending at full extension.
    ///   - Foot IK: raycasts to detect ground, smoothed grounding state, optional foot locking.
    ///   - Body root: same anchor/orientation logic as MedicalAvatarIK.
    ///
    /// Setup:
    ///   1. Attach to a Humanoid avatar GameObject (requires Animator component).
    ///   2. Assign raycastDetectionLayers to the floor layer(s).
    ///   3. TrackingManager is auto-found in scene (or assign trackingProviderBehaviour in inspector).
    ///   4. The avatar MUST start in a T-pose so CaptureBi ndPose() records correct bind directions.
    /// </summary>
    [RequireComponent(typeof(Animator))]
    public class ZEDSkeletonAnimator : MonoBehaviour
    {
        protected Animator animator;

        #region Inspector

        [Header("Body Anchor")]
        [Tooltip("Offset from patient hips to avatar root (meters). Same as MedicalAvatarIK.anchorOffset.")]
        public Vector3 anchorOffset = Vector3.zero;
        [Tooltip("Vertical shift of the avatar (positive = up). Same as MedicalAvatarIK.verticalBodyOffset.")]
        public float verticalBodyOffset = 0f;

        [Header("Smoothing")]
        [Tooltip("Smoothing for body root and bone rotations (0 = no smoothing / snappy, 0.99 = very slow).")]
        [Range(0f, 0.99f)]
        public float smoothingValue = 0.2f;

        [Header("Foot IK")]
        [Tooltip("Enable foot grounding IK via raycast.")]
        public bool enableFootIK = true;
        [Tooltip("Enable foot locking to suppress jitter when foot is on ground.")]
        public bool enableFootLocking = true;
        [Tooltip("Smoothing for foot position/rotation lerp (0 = snap, 0.99 = frozen).")]
        [Range(0f, 0.99f)]
        public float footLockingSmoothingValue = 0.3f;
        [Tooltip("Distance (sole → floor) under which foot is considered fully grounded.")]
        public float thresholdEnterGroundedState = 0.03f;
        [Tooltip("Distance (sole → floor) above which no foot IK is applied.")]
        public float thresholdLeaveGroundedState = 0.2f;
        [Tooltip("Horizontal movement below which foot is locked (jitter filter, meters).")]
        public float thresholdFootLock = 0.05f;
        [Tooltip("Layers detected as floor for foot raycast.")]
        public LayerMask raycastDetectionLayers;
        [Range(0f, 1f)] public float ikPositionApplicationRatio = 1f;
        [Range(0f, 1f)] public float ikRotationApplicationRatio = 1f;

        [Header("Rig Settings")]
        [Tooltip("Height from ankle bone to sole of foot (meters).")]
        public float ankleHeightOffset = 0.102f;

        [Header("Avatar Scale")]
        [Tooltip("Uniform scale applied to the avatar. Increase if avatar is too small compared to real body.")]
        [Range(0.5f, 2.5f)]
        public float avatarScale = 1.0f;

        [Header("Tracking Provider")]
        [SerializeField] private MonoBehaviour trackingProviderBehaviour;

        [Header("Debug")]
        [Tooltip("Press this key to reset verticalBodyOffset to 0.")]
        public KeyCode resetVerticalOffset = KeyCode.R;

        #endregion

        #region Private state

        private ITrackingProvider _trackingProvider;
        private Camera _cam;

        // Bind-pose bone directions captured at Start() from T-pose.
        // Key = humanoid bone, Value = world-space direction from that bone toward its child.
        private readonly Dictionary<HumanBodyBones, Vector3>    _bindDirs      = new();
        private readonly Dictionary<HumanBodyBones, Quaternion> _bindWorldRots = new();

        // Smoothed body root (set each OnAnimatorIK)
        private Vector3    _bodyPosSmoothed;
        private Quaternion _bodyRotSmoothed = Quaternion.identity;
        private bool       _bodyInitialized;

        // Body rotation captured at bind time (T-pose). Used to compute bodyDelta each frame
        // so bind directions stay consistent with the avatar's current world orientation.
        private Quaternion _bodyBindRot = Quaternion.identity;

        // Foot IK state
        private bool      _groundedL, _groundedR;
        private Vector3   _curPosAnkleL, _curPosAnkleR;
        private bool      _lockFootL, _lockFootR;
        private Vector3   _curIKTargetPosL, _curIKTargetPosR;
        private Quaternion _curIKTargetRotL = Quaternion.identity;
        private Quaternion _curIKTargetRotR = Quaternion.identity;
        private Vector3   _footLockTargetL, _footLockTargetR;

        private const float IK_HINT_WEIGHT = 1f;

        #endregion

        // Bone-segment map: each entry drives one bone to point from 'from' joint toward 'to' joint.
        private static readonly (HumanBodyBones bone, string from, string to)[] BoneSegments =
        {
            (HumanBodyBones.LeftUpperArm,  "LeftShoulder",  "LeftElbow"),
            (HumanBodyBones.LeftLowerArm,  "LeftElbow",     "LeftWrist"),
            (HumanBodyBones.RightUpperArm, "RightShoulder", "RightElbow"),
            (HumanBodyBones.RightLowerArm, "RightElbow",    "RightWrist"),
            (HumanBodyBones.LeftUpperLeg,  "LeftHip",       "LeftKnee"),
            (HumanBodyBones.LeftLowerLeg,  "LeftKnee",      "LeftAnkle"),
            (HumanBodyBones.RightUpperLeg, "RightHip",      "RightKnee"),
            (HumanBodyBones.RightLowerLeg, "RightKnee",     "RightAnkle"),
        };

        #region MonoBehaviour

        private void Awake()
        {
            animator = GetComponent<Animator>();
            _cam     = Camera.main;

            if (trackingProviderBehaviour == null)
                trackingProviderBehaviour = Object.FindFirstObjectByType<TrackingManager>();

            _trackingProvider = trackingProviderBehaviour as ITrackingProvider;
            if (_trackingProvider == null)
                Debug.LogError("ZEDSkeletonAnimator: ITrackingProvider not found. Assign a TrackingManager in the inspector.");
        }

        private void Start()
        {
            // Capture bind-pose directions (T-pose must be active at this point).
            CaptureBindPose();
            _bodyPosSmoothed = transform.position;
            _bodyRotSmoothed = transform.rotation;
            transform.localScale = Vector3.one * avatarScale;
        }

        private void Update()
        {
            if (UnityEngine.Input.GetKeyDown(resetVerticalOffset)) verticalBodyOffset = 0f;
            if (_cam == null) _cam = Camera.main;
            // Apply scale dynamically so it can be tuned in inspector at runtime.
            transform.localScale = Vector3.one * avatarScale;
        }

        #endregion

        #region OnAnimatorIK

        void OnAnimatorIK()
        {
            if (animator == null || _trackingProvider == null) return;

            var data = _trackingProvider.GetPose();
            if (!data.IsTracked || data.Joints == null || data.Joints.Count == 0) return;

            // Step 1 — Set body root position + torso orientation FIRST.
            // DriveBonesFromTrackingData needs _bodyRotSmoothed already updated for this frame
            // so it can correctly re-orient the stored bind-pose directions.
            ApplyBodyRoot(data);

            // Step 2 — Drive all limb bones directly from joint world positions.
            // MUST come after ApplyBodyRoot so _bodyRotSmoothed reflects the current frame.
            DriveBonesFromTrackingData(data);

            // Step 3 — Foot IK with raycast grounding.
            Vector3 lAnkleWorld = GetBoneWorldPos(HumanBodyBones.LeftFoot);
            Vector3 rAnkleWorld = GetBoneWorldPos(HumanBodyBones.RightFoot);

            RaycastFeet(lAnkleWorld, rAnkleWorld,
                out bool hitL, out bool hitR,
                out Vector3 ptL,  out Vector3 ptR,
                out Vector3 nrmL, out Vector3 nrmR);

            if (enableFootIK)
            {
                ApplyFootIK(
                    AvatarIKGoal.LeftFoot,  AvatarIKHint.LeftKnee,
                    HumanBodyBones.LeftLowerLeg, HumanBodyBones.LeftFoot, HumanBodyBones.LeftToes,
                    hitL, ptL, nrmL,
                    ref _groundedL, ref _curIKTargetPosL, ref _curIKTargetRotL, ref _footLockTargetL,
                    _lockFootL);

                ApplyFootIK(
                    AvatarIKGoal.RightFoot, AvatarIKHint.RightKnee,
                    HumanBodyBones.RightLowerLeg, HumanBodyBones.RightFoot, HumanBodyBones.RightToes,
                    hitR, ptR, nrmR,
                    ref _groundedR, ref _curIKTargetPosR, ref _curIKTargetRotR, ref _footLockTargetR,
                    _lockFootR);

                // Update foot lock state for next frame
                if (TryGet(data, "LeftAnkle",  out var la)) UpdateFootLock(ref _lockFootL, ref _curPosAnkleL, la.position);
                if (TryGet(data, "RightAnkle", out var ra)) UpdateFootLock(ref _lockFootR, ref _curPosAnkleR, ra.position);
            }
            else
            {
                ClearFootIK();
            }
        }

        #endregion

        #region Body root

        /// <summary>
        /// Positions and orients the avatar root from hips + shoulders landmarks.
        /// Logic identical to MedicalAvatarIK.ApplyBodyRoot.
        /// </summary>
        private void ApplyBodyRoot(PatientTrackingData data)
        {
            if (!TryGet(data, "LeftHip",       out var lHip)) return;
            if (!TryGet(data, "RightHip",      out var rHip)) return;
            if (!TryGet(data, "LeftShoulder",  out var lSh))  return;
            if (!TryGet(data, "RightShoulder", out var rSh))  return;

            Vector3 hipsMid     = (lHip.position + rHip.position) * 0.5f;
            Vector3 shoulderMid = (lSh.position  + rSh.position)  * 0.5f;

            Vector3 targetPos = hipsMid + anchorOffset + Vector3.up * verticalBodyOffset;

            Vector3 anatomicalUp = (shoulderMid - hipsMid).normalized;
            if (anatomicalUp.sqrMagnitude < 0.01f) return;

            if (_cam == null) { _cam = Camera.main; if (_cam == null) return; }
            Vector3 toCam = (_cam.transform.position - hipsMid).normalized;
            Vector3 fwd   = toCam - Vector3.Dot(toCam, anatomicalUp) * anatomicalUp;
            if (fwd.sqrMagnitude < 0.01f)
            {
                fwd = _cam.transform.forward;
                fwd = fwd - Vector3.Dot(fwd, anatomicalUp) * anatomicalUp;
            }
            Quaternion targetRot = Quaternion.LookRotation(fwd.normalized, anatomicalUp);

            float t = Mathf.Clamp(1f - smoothingValue, 0f, 1f);
            if (!_bodyInitialized)
            {
                _bodyPosSmoothed = targetPos;
                _bodyRotSmoothed = targetRot;
                _bodyInitialized = true;
            }
            else
            {
                _bodyPosSmoothed = Vector3.Lerp(_bodyPosSmoothed, targetPos, t);
                _bodyRotSmoothed = Quaternion.Slerp(_bodyRotSmoothed, targetRot, t);
            }

            animator.bodyPosition = _bodyPosSmoothed;
            animator.bodyRotation = _bodyRotSmoothed;
            // NOTE: do NOT set transform.position here — animator.bodyPosition already handles
            // the pelvis placement. Setting transform.SetPositionAndRotation on top would
            // double-apply the offset, making the body sink lower than expected.
        }

        #endregion

        #region Direct bone driving (replaces SkeletonHandler.MoveAnimator)

        /// <summary>
        /// Records the world-space direction of each bone toward its child while the avatar
        /// is in bind/T-pose. Called once in Start().
        /// </summary>
        private void CaptureBindPose()
        {
            // Record the body (pelvis) world rotation at T-pose so we can compute
            // the delta between bind time and runtime every frame in DriveBonesFromTrackingData.
            _bodyBindRot = (animator.bodyRotation == Quaternion.identity)
                ? transform.rotation   // fallback if animator hasn't ticked yet
                : animator.bodyRotation;

            foreach (var (bone, _, _) in BoneSegments)
            {
                HumanBodyBones child = ChildBoneOf(bone);
                if (child == HumanBodyBones.LastBone) continue;

                Transform boneT  = animator.GetBoneTransform(bone);
                Transform childT = animator.GetBoneTransform(child);
                if (boneT == null || childT == null) continue;

                _bindDirs[bone]      = (childT.position - boneT.position).normalized;
                _bindWorldRots[bone] = boneT.rotation;
            }
        }

        /// <summary>
        /// Sets bone local rotations from tracked joint world positions via SetBoneLocalRotation.
        ///
        /// For each segment (e.g. LeftUpperArm = LeftShoulder→LeftElbow):
        ///   1. Compute the tracked direction in world space.
        ///   2. Build the target world rotation:
        ///        FromToRotation(bindDir, trackedDir) × bindRot
        ///   3. Convert to parent-local space and smooth.
        ///
        /// Must be called inside OnAnimatorIK — SetBoneLocalRotation is only valid there.
        /// </summary>
        // Reuse per-frame to avoid GC allocs (cleared at start of each call).
        private readonly Dictionary<HumanBodyBones, Quaternion> _computedWorldRots = new(8);

        private void DriveBonesFromTrackingData(PatientTrackingData data)
        {
            float t = Mathf.Clamp(1f - smoothingValue, 0f, 1f);
            _computedWorldRots.Clear();

            // bodyDelta = rotation from bind-time body orientation to current body orientation.
            // Applying this to the stored bind dirs/rots brings them into the current world frame,
            // so that FromToRotation compares apples to apples regardless of how ApplyBodyRoot
            // has rotated the avatar. Without this:
            //   - A forward hip flexion (knee toward camera) maps to a side abduction on the avatar.
            //   - Left and right limbs appear swapped or mirrored.
            Quaternion bodyDelta = _bodyRotSmoothed * Quaternion.Inverse(_bodyBindRot);

            foreach (var (bone, fromKey, toKey) in BoneSegments)
            {
                if (!TryGet(data, fromKey, out var fromPose) ||
                    !TryGet(data, toKey,   out var toPose))    continue;

                if (!_bindDirs.TryGetValue(bone,      out Vector3    bindDir) ||
                    !_bindWorldRots.TryGetValue(bone, out Quaternion bindRot)) continue;

                Transform boneT = animator.GetBoneTransform(bone);
                if (boneT == null || boneT.parent == null) continue;

                Vector3 trackedDir = (toPose.position - fromPose.position).normalized;
                if (trackedDir.sqrMagnitude < 0.001f) continue;

                // Re-orient bind data into the current body world frame.
                Vector3    adjustedBindDir = bodyDelta * bindDir;
                Quaternion adjustedBindRot = bodyDelta * bindRot;

                // World rotation: rotate from the (body-delta-adjusted) bind direction
                // to the tracked direction, preserving original bind twist.
                Quaternion targetWorldRot = Quaternion.FromToRotation(adjustedBindDir, trackedDir)
                                           * adjustedBindRot;

                // CRITICAL: use our own computed parent world rotation when the parent was also
                // driven by us in this same frame. SetBoneLocalRotation changes only take effect
                // AFTER OnAnimatorIK returns, so boneT.parent.rotation is still the OLD value.
                HumanBodyBones parentBone = ParentBoneOf(bone);
                Quaternion parentWorldRot = _computedWorldRots.TryGetValue(parentBone, out var pwr)
                    ? pwr : boneT.parent.rotation;

                Quaternion targetLocalRot = Quaternion.Inverse(parentWorldRot) * targetWorldRot;
                Quaternion smoothed       = Quaternion.Slerp(boneT.localRotation, targetLocalRot, t);
                animator.SetBoneLocalRotation(bone, smoothed);

                // Store the ACTUAL world rotation after smoothing so child bones
                // (e.g. LowerArm whose parent is UpperArm) get the correct parent basis.
                _computedWorldRots[bone] = parentWorldRot * smoothed;
            }
        }

        /// <summary>
        /// Returns the immediate parent bone that may have been driven by us in the same frame.
        /// Used to fix the sequential dependency in DriveBonesFromTrackingData.
        /// </summary>
        private static HumanBodyBones ParentBoneOf(HumanBodyBones bone) => bone switch
        {
            HumanBodyBones.LeftLowerArm  => HumanBodyBones.LeftUpperArm,
            HumanBodyBones.RightLowerArm => HumanBodyBones.RightUpperArm,
            HumanBodyBones.LeftLowerLeg  => HumanBodyBones.LeftUpperLeg,
            HumanBodyBones.RightLowerLeg => HumanBodyBones.RightUpperLeg,
            _                            => HumanBodyBones.LastBone,
        };

        #endregion

        #region Foot IK

        private void ApplyFootIK(
            AvatarIKGoal goal, AvatarIKHint hint,
            HumanBodyBones lowerLegBone, HumanBodyBones footBone, HumanBodyBones toesBone,
            bool hitSuccessful, Vector3 hitPoint, Vector3 hitNormal,
            ref bool grounded,
            ref Vector3 curIKTargetPos, ref Quaternion curIKTargetRot, ref Vector3 lockTarget,
            bool footLocked)
        {
            // Knee/elbow hints are intentionally disabled.
            // We drive upper/lower limb bones directly via SetBoneLocalRotation (see DriveBonesFromTrackingData).
            // Adding a knee/elbow hint on top would make the 2-bone IK solver fight the direct drive,
            // which causes knee hyperextension and elbow instability. The foot IK goal is used only
            // for vertical grounding (snapping ankle to floor height), not for full leg solving.
            animator.SetIKHintPositionWeight(hint, 0f);

            curIKTargetPos = ComputeFootTarget(footLocked, hitPoint, curIKTargetPos, grounded, ref lockTarget);
            curIKTargetRot = ComputeFootRotation(hitNormal,
                animator.GetBoneTransform(toesBone).position,
                animator.GetBoneTransform(footBone).position,
                _bodyRotSmoothed, curIKTargetRot);

            if (hitSuccessful)
            {
                float dist = Mathf.Abs(hitPoint.y - GetBoneWorldPos(footBone).y + ankleHeightOffset);
                grounded = dist <= thresholdEnterGroundedState;
                animator.SetIKPosition(goal, curIKTargetPos);
                animator.SetIKRotation(goal, curIKTargetRot);
                animator.SetIKPositionWeight(goal, GetLinearIKRatio(dist, thresholdLeaveGroundedState, thresholdEnterGroundedState, ikPositionApplicationRatio));
                animator.SetIKRotationWeight(goal, GetLinearIKRatio(dist, thresholdLeaveGroundedState, thresholdEnterGroundedState, ikRotationApplicationRatio));
            }
            else
            {
                grounded = false;
                animator.SetIKPositionWeight(goal, 0);
                animator.SetIKRotationWeight(goal, 0);
            }
        }

        private void ClearFootIK()
        {
            animator.SetIKPositionWeight(AvatarIKGoal.LeftFoot,  0);
            animator.SetIKRotationWeight(AvatarIKGoal.LeftFoot,  0);
            animator.SetIKPositionWeight(AvatarIKGoal.RightFoot, 0);
            animator.SetIKRotationWeight(AvatarIKGoal.RightFoot, 0);
            animator.SetIKHintPositionWeight(AvatarIKHint.LeftKnee,  0);
            animator.SetIKHintPositionWeight(AvatarIKHint.RightKnee, 0);
        }

        private void RaycastFeet(
            Vector3 ankL, Vector3 ankR,
            out bool hitL, out bool hitR,
            out Vector3 ptL, out Vector3 ptR,
            out Vector3 nrmL, out Vector3 nrmR)
        {
            ptL = ankL; ptR = ankR; nrmL = Vector3.up; nrmR = Vector3.up;
            hitL = Physics.Raycast(new Ray(ankL + Vector3.up * 5f, Vector3.down), out RaycastHit hl, 10f, raycastDetectionLayers);
            if (hitL) { ptL = hl.point; nrmL = hl.normal; }
            hitR = Physics.Raycast(new Ray(ankR + Vector3.up * 5f, Vector3.down), out RaycastHit hr, 10f, raycastDetectionLayers);
            if (hitR) { ptR = hr.point; nrmR = hr.normal; }
        }

        private Vector3 ComputeFootTarget(
            bool footLocked, Vector3 hitPoint, Vector3 prevPos, bool grounded, ref Vector3 lockTarget)
        {
            float s      = Mathf.Clamp(1f - footLockingSmoothingValue, 0f, 1f);
            Vector3 dest = hitPoint + new Vector3(0, ankleHeightOffset, 0);

            if (!enableFootLocking) return Vector3.Lerp(prevPos, dest, s);
            if (footLocked && grounded)
                return Vector3.Lerp(prevPos, new Vector3(lockTarget.x, hitPoint.y + ankleHeightOffset, lockTarget.z), s);

            lockTarget = dest;
            return Vector3.Lerp(prevPos, dest, s);
        }

        private Quaternion ComputeFootRotation(
            Vector3 hitNormal, Vector3 toePos, Vector3 anklePos, Quaternion rootRot, Quaternion cur)
        {
            Vector3 fwd = Vector3.ProjectOnPlane(rootRot * (toePos - anklePos), Vector3.up);
            return Quaternion.Slerp(cur, Quaternion.LookRotation(fwd, hitNormal),
                Mathf.Clamp(1f - footLockingSmoothingValue, 0f, 1f));
        }

        private float GetLinearIKRatio(float d, float tMin, float tMax, float rMax = 1f)
            => Mathf.Clamp01(rMax * (tMin - d) / (tMin - tMax));

        #endregion

        #region Foot locking

        private void UpdateFootLock(ref bool locked, ref Vector3 curPos, Vector3 newPos)
        {
            locked = HorizontalDist(curPos, newPos) < thresholdFootLock;
            if (!locked) curPos = newPos;
        }

        /// <summary>Public entry point for external callers if needed.</summary>
        public void CheckFootLock(Vector3 newPosAnkleL, Vector3 newPosAnkleR)
        {
            UpdateFootLock(ref _lockFootL, ref _curPosAnkleL, newPosAnkleL);
            UpdateFootLock(ref _lockFootR, ref _curPosAnkleR, newPosAnkleR);
        }

        private static float HorizontalDist(Vector3 a, Vector3 b)
            => Vector2.Distance(new Vector2(a.x, a.z), new Vector2(b.x, b.z));

        #endregion

        #region Utilities

        private Vector3 GetBoneWorldPos(HumanBodyBones bone)
        {
            Transform t = animator.GetBoneTransform(bone);
            return t != null ? t.position : animator.bodyPosition;
        }

        private static bool TryGet(PatientTrackingData data, string key, out Pose pose)
        {
            if (data.Joints != null && data.Joints.TryGetValue(key, out pose)) return true;
            pose = default;
            return false;
        }

        private static HumanBodyBones ChildBoneOf(HumanBodyBones bone) => bone switch
        {
            HumanBodyBones.LeftUpperArm  => HumanBodyBones.LeftLowerArm,
            HumanBodyBones.LeftLowerArm  => HumanBodyBones.LeftHand,
            HumanBodyBones.RightUpperArm => HumanBodyBones.RightLowerArm,
            HumanBodyBones.RightLowerArm => HumanBodyBones.RightHand,
            HumanBodyBones.LeftUpperLeg  => HumanBodyBones.LeftLowerLeg,
            HumanBodyBones.LeftLowerLeg  => HumanBodyBones.LeftFoot,
            HumanBodyBones.RightUpperLeg => HumanBodyBones.RightLowerLeg,
            HumanBodyBones.RightLowerLeg => HumanBodyBones.RightFoot,
            _                            => HumanBodyBones.LastBone,
        };

        #endregion
    }
}