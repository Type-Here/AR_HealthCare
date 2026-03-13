//#define AR_HEALTHCARE_DEBUG

using System.Collections.Generic;
using UnityEngine;
using ARHealthCare.Core;
using ARHealthCare.DataClasses;
using ARHealthCare.Trackers;

namespace ARHealthCare.Visuals
{
    /// <summary>
    /// Two-phase hybrid IK approach for driving a humanoid avatar from tracking data.
    ///
    /// Phase 1 — OnAnimatorIK:
    ///   Body root placement + Unity IK goals (wrist positions, ankle positions, head look,
    ///   elbow/knee hints). Unity's built-in 2-bone solver positions the limbs approximately.
    ///   This is the same proven logic as MedicalAvatarIK.
    ///
    /// Phase 2 — LateUpdate:
    ///   Direct bone rotation corrections from tracked joint directions. At this point
    ///   ALL transforms are fully resolved (post-IK), so parent bone rotations are NOT stale.
    ///   This eliminates the IKSkeletonAnimator's fundamental problem: in OnAnimatorIK,
    ///   SetBoneLocalRotation reads stale parent transforms because animator.bodyRotation
    ///   changes don't propagate to bone transforms mid-callback.
    ///
    /// Why neither pure IK nor pure direct-drive works alone:
    ///   - MedicalAvatarIK (pure IK): The 2-bone solver controls limb endpoint distance
    ///     but NOT the joint angle. Elbow/knee bend is a byproduct of wrist/ankle distance
    ///     from shoulder/hip → elbows stay rigid at near-full extension (singularity).
    ///   - IKSkeletonAnimator (direct drive): SetBoneLocalRotation in OnAnimatorIK uses
    ///     boneT.parent.rotation which hasn't been updated for our bodyRotation change
    ///     → cascading rotation errors, especially left/right asymmetry.
    ///   - HybridAvatarIK: IK gets close, LateUpdate corrects angles with fully resolved
    ///     transforms → accurate bend, no stale data, stable positioning.
    ///
    /// Setup: same as MedicalAvatarIK — attach to Humanoid avatar, assign TrackingManager.
    /// Tune correctionWeight (0=pure IK, 1=full tracking angles) and correctionSmoothing.
    /// </summary>
    [RequireComponent(typeof(Animator))]
    public class HybridAvatarIK : MonoBehaviour
    {
        private Animator _animator;

        #region Inspector

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
        [Tooltip("Minimum movement (m) to filter micro-jitter. Recommended: 0.01-0.03.")]
        public float movementDeadZone = 0.02f;
        [Tooltip("Enable velocity-based adaptive smoothing.")]
        public bool useAdaptiveSmoothing = true;
        [Tooltip("Maximum velocity (m/s) for adaptive smoothing normalization.")]
        public float maxVelocity = 2.0f;

        [Header("Spatial Anchor (calibrated at referenceDistance)")]
        [Tooltip("Enable to anchor avatar to patient hips.")]
        public bool useAnchor = true;

        [Tooltip("If true, hips are ignored and root is anchored to nose position instead" + 
               "(for close-range tracking loss fallback).")]
        public bool useOnlyHeadForRoot = false; 
        [Tooltip("Offset from patient hips to avatar root (meters), at reference distance.")]
        public Vector3 anchorOffset = Vector3.zero;
        [Tooltip("Vertical shift (positive = up) at reference distance (1.7m).\n" +
                 "Scales proportionally with avatar size.\n" +
                 "Not applied upward beyond maxVerticalOffsetDistance.")]
        public float verticalBodyOffset = 0.3f;
        [Tooltip("Beyond this distance (m), upward vertical offset is NOT applied.\n" +
                 "At long range the offset causes more harm than good.")]
        public float maxVerticalOffsetDistance = 2.1f;

        [Header("Dynamic Scaling (calibrated at referenceDistance)")]
        [Tooltip("Auto-scale avatar based on estimated body height + segment stretch.")]
        public bool useDynamicScaling = true;
        [Tooltip("Reference distance (m) for which all scale/offset values are calibrated.\n" +
                 "Set this to the typical patient distance during calibration.")]
        public float referenceDistance = 1.7f;
        [Tooltip("Reference body height for uniform scale calc (~1.7m average adult).")]
        public float referenceBodyHeight = 1.7f;
        [Tooltip("Empirical base scale multiplier at reference distance. Adjust if avatar is globally too small/large.")]
        [Range(0.8f, 2.0f)] public float baseScaleMultiplier = 1.2f;
        [Tooltip("Manual scale if useDynamicScaling is off.")]
        [Range(0.5f, 2.5f)] public float manualAvatarScale = 1.0f;

        [Header("Segment Stretch (proportional body fitting)")]
        [Tooltip("Stretch upper body (shoulders\u2192hips) to match tracked proportions.\n" +
                 "1.0 = match tracked length exactly. >1 = elongate torso.")]
        [Range(0.5f, 2.0f)] public float upperBodyScale = 1.0f;
        [Tooltip("Stretch lower body (hips\u2192feet) to match tracked proportions.\n" +
                 "1.0 = match tracked length exactly. >1 = elongate legs.")]
        [Range(0.5f, 2.0f)] public float lowerBodyScale = 1.0f;
        [Tooltip("Smoothing speed for segment stretch (higher = snappier).")]
        public float stretchLerpSpeed = 5f;

        [Header("IK Hints (Phase 1)")]
        [Tooltip("Elbow hint weight — helps IK solver find correct bend plane.")]
        [Range(0, 1)] public float elbowHintWeight = 1.0f;
        [Tooltip("Knee hint weight — set >0 only if knee tracking is stable.")]
        [Range(0, 1)] public float kneeHintWeight = 0.5f;

        [Header("Bone Correction (Phase 2 — LateUpdate)")]
        [Tooltip("How much to override IK angles with tracked joint directions.\n" +
                 "0 = pure IK (like MedicalAvatarIK)\n" +
                 "1 = full tracking angle correction\n" +
                 "0.5-0.8 recommended.")]
        [Range(0, 1)] public float correctionWeight = 0.7f;
        [Tooltip("Smoothing for tracked directions (0 = snap, 0.9 = very smooth).")]
        [Range(0, 0.95f)] public float correctionSmoothing = 0.3f;

        [Header("Tracking Provider")]
        [SerializeField] private MonoBehaviour trackingProviderBehaviour;

        [Header("Debug")]
        public bool logOnce = true;
        [Tooltip("Draw debug gizmos in Scene view.")]
        public bool drawGizmos = true;

        #endregion

        #region Private State

        private ITrackingProvider _trackingProvider;
        private Camera _cam;
        private bool _logged;

        // Tracking loss handling
        private float _trackingLossTime;
        private const float TRACKING_LOSS_TIMEOUT = 5f;
        private bool _isRecoveringFromLoss;
        private float _recoveryBlendTime;
        private const float RECOVERY_BLEND_DURATION = 1.5f;

        // Dead-zone / adaptive smoothing cache
        private readonly Dictionary<string, Vector3> _lastValidPositions = new();

        // Cached tracking data for LateUpdate
        private PatientTrackingData _latestData;
        private bool _hasValidData;

        // Smoothed tracked directions for Phase 2 bone correction
        private readonly Dictionary<HumanBodyBones, Vector3> _smoothedTrackedDirs = new(8);

        // Cached bone transforms for segment stretch (Phase 3)
        private Transform _spineT, _lUpperLegT, _rUpperLegT;
        private Transform _lShoulderBoneT, _rShoulderBoneT;
        private Transform _hipsBoneT, _lFootBoneT, _rFootBoneT;
        private Transform _headBoneT;

        // Last valid hips midpoint — used as fallback when hips go out of frame
        private Vector3 _lastValidHipsMid;
        private bool _hasLastValidHipsMid;

        //Last Scaling values before losing hips tracking, used to maintain avatar size during tracking loss
        private float _lastUpperBodyScale = 1f;
        private float _lastLowerBodyScale = 1f;


        #endregion

        // Limb segments for Phase 2 correction.
        // MUST be ordered: upper before lower within each limb so that when we
        // correct LowerArm, the UpperArm correction is already applied and
        // childT.position reflects the new elbow position.
        private static readonly (HumanBodyBones bone, HumanBodyBones child,
                                 string fromKey, string toKey)[] LimbSegments =
        {
            // Left arm
            (HumanBodyBones.LeftUpperArm,  HumanBodyBones.LeftLowerArm,  "LeftShoulder",  "LeftElbow"),
            (HumanBodyBones.LeftLowerArm,  HumanBodyBones.LeftHand,      "LeftElbow",     "LeftWrist"),
            // Right arm
            (HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, "RightShoulder", "RightElbow"),
            (HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand,     "RightElbow",    "RightWrist"),
            // Left leg
            (HumanBodyBones.LeftUpperLeg,  HumanBodyBones.LeftLowerLeg,  "LeftHip",       "LeftKnee"),
            (HumanBodyBones.LeftLowerLeg,  HumanBodyBones.LeftFoot,      "LeftKnee",      "LeftAnkle"),
            // Right leg
            (HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg, "RightHip",      "RightKnee"),
            (HumanBodyBones.RightLowerLeg, HumanBodyBones.RightFoot,     "RightKnee",     "RightAnkle"),
        };

        #region MonoBehaviour

        private void Awake()
        {
            _animator = GetComponent<Animator>();
            if (_animator == null)
            {
                Debug.LogError("HybridAvatarIK: Animator not found.");
                return;
            }
            _animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            _cam = Camera.main;

            if (trackingProviderBehaviour == null)
                trackingProviderBehaviour = Object.FindFirstObjectByType<TrackingManager>();

            _trackingProvider = trackingProviderBehaviour as ITrackingProvider;
            if (_trackingProvider == null)
                Debug.LogError("HybridAvatarIK: ITrackingProvider not found. Assign a TrackingManager.");

            // Cache bone transforms for Phase 3 segment stretch
            _spineT         = _animator.GetBoneTransform(HumanBodyBones.Spine);
            _lUpperLegT     = _animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg);
            _rUpperLegT     = _animator.GetBoneTransform(HumanBodyBones.RightUpperLeg);
            _lShoulderBoneT = _animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
            _rShoulderBoneT = _animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
            _hipsBoneT      = _animator.GetBoneTransform(HumanBodyBones.Hips);
            _lFootBoneT     = _animator.GetBoneTransform(HumanBodyBones.LeftFoot);
            _rFootBoneT     = _animator.GetBoneTransform(HumanBodyBones.RightFoot);
            _headBoneT      = _animator.GetBoneTransform(HumanBodyBones.Head);

            if (!useDynamicScaling && Mathf.Abs(manualAvatarScale - 1f) > 0.01f)
            {
                transform.localScale = Vector3.one * manualAvatarScale;
                Debug.Log($"HybridAvatarIK: Manual scale = {manualAvatarScale}");
            }
        }

        #endregion

        // ═══════════════════════════════════════════════════════════════════
        //  PHASE 1 — OnAnimatorIK: body root + IK goals (proven from MedicalAvatarIK)
        // ═══════════════════════════════════════════════════════════════════

        #region Phase 1: OnAnimatorIK

        private void OnAnimatorIK(int layerIndex)
        {
            if (_animator == null || _trackingProvider == null) return;

            var data = _trackingProvider.GetPose();

            // ── Tracking loss ──
            if (!data.IsTracked || data.Joints == null || data.Joints.Count == 0)
            {
                _hasValidData = false;
                _trackingLossTime += Time.deltaTime;
                if (_trackingLossTime > TRACKING_LOSS_TIMEOUT) ResetIK();
                return;
            }

            if (_trackingLossTime > 0.5f)
            {
                _isRecoveringFromLoss = true;
                _recoveryBlendTime = 0f;
            }
            _trackingLossTime = 0f;

            if (_isRecoveringFromLoss)
            {
                _recoveryBlendTime += Time.deltaTime;
                if (_recoveryBlendTime >= RECOVERY_BLEND_DURATION)
                    _isRecoveringFromLoss = false;
            }

            if (logOnce && !_logged)
            {
                Debug.Log($"HybridAvatarIK: Running. Joints={data.Joints.Count} " +
                          $"provider={_trackingProvider.GetType().Name}");
                _logged = true;
            }

            // Store for LateUpdate (Phase 2)
            _latestData = data;
            _hasValidData = true;

            // ── Dynamic scaling (BEFORE body root so position uses current-frame scale) ──
            // Uniform scale from EstimatedBodyHeight + baseScaleMultiplier (calibrated at referenceDistance).
            // Segment stretch (Phase 3 in LateUpdate) corrects upper/lower body proportions on top of this.
            if (useDynamicScaling && data.EstimatedBodyHeight > 0.5f)
            {
                float sf = Mathf.Clamp(data.EstimatedBodyHeight / referenceBodyHeight, 0.6f, 1.8f);
                transform.localScale = Vector3.Lerp(transform.localScale,
                    Vector3.one * sf * baseScaleMultiplier, Time.deltaTime * 2f);
            }
            else if (useDynamicScaling)
            {
                transform.localScale = Vector3.Lerp(transform.localScale,
                    Vector3.one * baseScaleMultiplier, Time.deltaTime * 2f);
            }

            // ── Body root (uses current-frame scale for offset compensation) ──
            ApplyBodyRoot(data);

            // ── Hand IK goals ──
            ApplyHandGoal(AvatarIKGoal.LeftHand,  "LeftWrist",  "LeftElbow",  "LeftShoulder",  data, handsWeight);
            ApplyHandGoal(AvatarIKGoal.RightHand, "RightWrist", "RightElbow", "RightShoulder", data, handsWeight);

            // ── Foot IK goals ──
            ApplyGoalIK(AvatarIKGoal.LeftFoot,  "LeftAnkle",  data, feetWeight);
            ApplyGoalIK(AvatarIKGoal.RightFoot, "RightAnkle", data, feetWeight);

            // ── Head look ──
            ApplyHeadLook(data);

            // ── Elbow / knee hints — raw tracked positions so IK solver gets close ──
            ApplyHintIK(AvatarIKHint.LeftElbow,  "LeftElbow",  data, elbowHintWeight);
            ApplyHintIK(AvatarIKHint.RightElbow, "RightElbow", data, elbowHintWeight);
            ApplyHintIK(AvatarIKHint.LeftKnee,   "LeftKnee",   data, kneeHintWeight);
            ApplyHintIK(AvatarIKHint.RightKnee,  "RightKnee",  data, kneeHintWeight);

            // If available, set IK for Index and Pinky fingers 
            // (17, 19 for left hand; 16, 18 for right hand) to improve hand pose. 
            // Requires MediaPipe tracker to include these landmarks 
            // and corresponding keys in PatientTrackingData.


        }

        #endregion

        // ═══════════════════════════════════════════════════════════════════
        //  PHASE 2 — LateUpdate: correct bone rotations from tracked directions
        //
        //  At this point Unity's animation pipeline is complete:
        //    Animation evaluation → OnAnimatorIK → IK solve → LateUpdate
        //  All bone transforms are post-IK, fully resolved. No stale parents.
        //
        //  For each limb segment we:
        //    1. Read the CURRENT bone→child direction (post-IK)
        //    2. Compute the TRACKED direction from landmark positions
        //    3. Apply FromToRotation delta to align bone with tracking
        //    4. Process upper limb before lower (sequential dependency)
        //
        //  Since we set boneT.rotation directly (not via SetBoneLocalRotation),
        //  changes propagate IMMEDIATELY to child transforms within this frame.
        // ═══════════════════════════════════════════════════════════════════

        #region Phase 2: LateUpdate

        private void LateUpdate()
        {
            if (!_hasValidData || _animator == null) return;

            var data = _latestData;

            // Phase 2: bone rotation corrections 
            if (correctionWeight >= 0.01f)
            {
                float w = correctionWeight * Mathf.Clamp01(globalWeight);
                if (_isRecoveringFromLoss)
                    w *= Mathf.Clamp01(_recoveryBlendTime / RECOVERY_BLEND_DURATION);

                float smoothT = Mathf.Clamp(1f - correctionSmoothing, 0.05f, 1f);

                foreach (var (bone, child, fromKey, toKey) in LimbSegments)
                {
                    if (!TryGet(data, fromKey, out var fromPose) ||
                        !TryGet(data, toKey,   out var toPose))
                        continue;

                    Transform boneT  = _animator.GetBoneTransform(bone);
                    Transform childT = _animator.GetBoneTransform(child);
                    if (boneT == null || childT == null) continue;

                    Vector3 currentDir = (childT.position - boneT.position).normalized;
                    if (currentDir.sqrMagnitude < 0.001f) continue;

                    Vector3 rawTrackedDir = (toPose.position - fromPose.position).normalized;
                    if (rawTrackedDir.sqrMagnitude < 0.001f) continue;

                    if (!_smoothedTrackedDirs.TryGetValue(bone, out var prevDir))
                        prevDir = rawTrackedDir;

                    Vector3 smoothedDir = Vector3.Slerp(prevDir, rawTrackedDir, smoothT).normalized;
                    _smoothedTrackedDirs[bone] = smoothedDir;

                    Quaternion delta = Quaternion.FromToRotation(currentDir, smoothedDir);
                    Quaternion blendedDelta = Quaternion.Slerp(Quaternion.identity, delta, w);
                    boneT.rotation = blendedDelta * boneT.rotation;
                }
            }

            // Phase 3: segment stretch for proportional body fitting
            if (useDynamicScaling)
                ApplySegmentStretch(data);
        }

        #endregion

        // ═══════════════════════════════════════════════════════════════════
        //  PHASE 3 — Segment stretch: match tracked upper/lower body lengths
        //
        //  Measures tracked shoulder→hip and hip→ankle distances, compares to
        //  the avatar's current bone chain lengths (post-IK, post-Phase2),
        //  and stretches Spine (upper) / UpperLeg (lower) bones via localScale.y.
        //  This fixes "short legs" and adapts proportions across distances.
        // ═══════════════════════════════════════════════════════════════════

        #region Phase 3: Segment Stretch

        private void ApplySegmentStretch(PatientTrackingData data)
        {
            // Need shoulder, hip, ankle landmarks from tracking
            if (!TryGet(data, "LeftShoulder", out var lSh) || !TryGet(data, "RightShoulder", out var rSh) ||
                !TryGet(data, "LeftHip", out var lHip)      || !TryGet(data, "RightHip", out var rHip) ||
                !TryGet(data, "LeftAnkle", out var lAnk)    || !TryGet(data, "RightAnkle", out var rAnk))
                return;

            if (_spineT == null || _hipsBoneT == null || _lShoulderBoneT == null ||
                _rShoulderBoneT == null || _lFootBoneT == null || _rFootBoneT == null ||
                _lUpperLegT == null || _rUpperLegT == null)
                return;

            // ── Tracked segment lengths (world-space) ──
            Vector3 tShMid  = (lSh.position  + rSh.position)  * 0.5f;
            Vector3 tHipMid = (lHip.position  + rHip.position) * 0.5f;
            Vector3 tAnkMid = (lAnk.position  + rAnk.position) * 0.5f;

            float trackedUpper = Vector3.Distance(tShMid, tHipMid);
            float trackedLower = Vector3.Distance(tHipMid, tAnkMid);

            if (trackedUpper < 0.02f || trackedLower < 0.02f) return; // degenerate

            // ── Avatar segment lengths (post-IK, post-Phase2, world-space) ──
            Vector3 aShMid  = (_lShoulderBoneT.position + _rShoulderBoneT.position) * 0.5f;
            Vector3 aAnkMid = (_lFootBoneT.position     + _rFootBoneT.position)     * 0.5f;

            float avatarUpper = Vector3.Distance(aShMid, _hipsBoneT.position);
            float avatarLower = Vector3.Distance(_hipsBoneT.position, aAnkMid);

            if (avatarUpper < 0.01f || avatarLower < 0.01f) return;

            // ── Stretch ratios: tracked / avatar × inspector multiplier ──
            float upperRatio = Mathf.Clamp((trackedUpper / avatarUpper) * upperBodyScale, 0.5f, 2.0f);
            float lowerRatio = Mathf.Clamp((trackedLower / avatarLower) * lowerBodyScale, 0.5f, 2.0f);

            // If hips are lost, maintain last known stretch ratios to avoid popping back to default scale.
            _lastUpperBodyScale = upperRatio;
            _lastLowerBodyScale = lowerRatio;

            if (!_hasLastValidHipsMid)            
            {
                upperRatio = _lastUpperBodyScale;
                lowerRatio = _lastLowerBodyScale;
            }
            
            float t = Time.deltaTime * stretchLerpSpeed;

            // ── Upper body: stretch Spine bone ──
            Vector3 spS = _spineT.localScale;
            spS.y = Mathf.Lerp(spS.y, upperRatio, t);
            _spineT.localScale = spS;

            // ── Lower body: stretch both UpperLeg bones ──
            Vector3 lLS = _lUpperLegT.localScale;
            lLS.y = Mathf.Lerp(lLS.y, lowerRatio, t);
            _lUpperLegT.localScale = lLS;

            Vector3 rLS = _rUpperLegT.localScale;
            rLS.y = Mathf.Lerp(rLS.y, lowerRatio, t);
            _rUpperLegT.localScale = rLS;
        }

        #endregion

        // ═══════════════════════════════════════════════════════════════════
        //  Body root — hips anchor + face-camera rotation
        // ═══════════════════════════════════════════════════════════════════

        #region Body Root

        private void ApplyBodyRoot(PatientTrackingData data)
        {
            var joints = data.Joints;
            if (joints == null) return;

            joints.TryGetValue("LeftHip",        out Pose lHip);
            joints.TryGetValue("RightHip",       out Pose rHip);
            joints.TryGetValue("LeftShoulder",   out Pose lSh);
            joints.TryGetValue("RightShoulder",  out Pose rSh);

            bool hasHips      = joints.ContainsKey("LeftHip") && joints.ContainsKey("RightHip");
            bool hasShoulders = joints.ContainsKey("LeftShoulder") && joints.ContainsKey("RightShoulder");

            // We always need shoulders for rotation at minimum
            if (!hasShoulders) return;

            Vector3 shoulderMid = (lSh.position + rSh.position) * 0.5f;
            Vector3 hipsMid;

            if (hasHips && !useOnlyHeadForRoot)
            {
                // Normal path: hips visible
                hipsMid = (lHip.position + rHip.position) * 0.5f;
                _lastValidHipsMid = hipsMid;
                _hasLastValidHipsMid = true;
            }
            else
            {
                // ── Close-range fallback: no hips visible ──
                // If Nose landmark is available, anchor avatar so that the avatar's
                // Head bone matches the tracked Nose position. No verticalBodyOffset,
                // no hips estimation. Scaling stays unchanged.
                bool hasNose = joints.TryGetValue("Nose", out Pose nosePose);
                if (hasNose && _headBoneT != null)
                {
                    // Offset from avatar root to head bone (current frame)
                    Vector3 rootToHead = _headBoneT.position - transform.position;
                    Vector3 noseTargetPos = nosePose.position - rootToHead;

                    transform.position = Vector3.Lerp(transform.position, noseTargetPos,
                                                      Time.deltaTime * positionLerpSpeed);

                    // Rotation: use shoulders with Vector3.up (no hips→shoulders vector)
                    if (hasShoulders && _cam != null)
                    {
                        Vector3 noseToCam = (_cam.transform.position - nosePose.position).normalized;
                        Vector3 noseFwd = noseToCam - Vector3.Dot(noseToCam, Vector3.up) * Vector3.up;
                        if (noseFwd.sqrMagnitude > 0.01f)
                        {
                            Quaternion noseRot = Quaternion.LookRotation(noseFwd.normalized, Vector3.up);
                            transform.rotation = Quaternion.Slerp(transform.rotation, noseRot,
                                                                  Time.deltaTime * rotationSlerpSpeed);
                        }
                    }

                    _animator.bodyPosition = transform.position;
                    _animator.bodyRotation = transform.rotation;
                    return; // done — skip normal hips-based positioning below
                }
                else if (_hasLastValidHipsMid)
                {
                    hipsMid = _lastValidHipsMid;
                }
                else
                {
                    return; // no way to position
                }
            }

            // Position: hips center + configurable offsets.
            // verticalBodyOffset is calibrated at referenceDistance (1.7m).
            // Scales proportionally with avatar size. Beyond maxVerticalOffsetDistance,
            // upward offset is suppressed to avoid the avatar floating above the patient.
            float currentScale = transform.localScale.x; // uniform scale

            float camDist = referenceDistance;
            if (_cam != null)
                camDist = Mathf.Max(0.3f, Vector3.Distance(_cam.transform.position, hipsMid));

            // Dynamic Offset: get estimated distance from camera clamped (1.5-2.5)
            float clampedDist = Mathf.Clamp(camDist, referenceDistance - 0.8f, referenceDistance + 0.5f);
            float scaleVOffsetFactor = referenceDistance / clampedDist;

            float vOffset = verticalBodyOffset * currentScale * scaleVOffsetFactor;
            if (camDist > maxVerticalOffsetDistance && vOffset > 0f)
                vOffset = -0.05f; // suppress upward offset at long range

            Vector3 targetPos = hipsMid
                              + anchorOffset * currentScale
                              + Vector3.up * vOffset;
            transform.position = Vector3.Lerp(transform.position, targetPos,
                                              Time.deltaTime * positionLerpSpeed);

            // Rotation: face camera, anatomical up from hips→shoulders
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
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot,
                                                  Time.deltaTime * rotationSlerpSpeed);

            // Sync animator body with transform for IK solver consistency
            _animator.bodyPosition = transform.position;
            _animator.bodyRotation = transform.rotation;
        }

        #endregion

        // ═══════════════════════════════════════════════════════════════════
        //  IK application methods (from MedicalAvatarIK, with wrist clamping)
        // ═══════════════════════════════════════════════════════════════════

        #region IK Goals

        /// <summary>
        /// Hand IK goal with wrist-reach clamping to prevent 2-bone singularity.
        /// When the arm is nearly fully extended the solver can't bend the elbow;
        /// clamping to 95% of arm reach guarantees a slight bend so the hint is effective
        /// and the Phase 2 correction has a reasonable baseline to improve.
        /// </summary>
        private void ApplyHandGoal(AvatarIKGoal goal, string wristKey, string elbowKey,
                                   string shoulderKey, PatientTrackingData data, float weight)
        {
            if (!TryGet(data, wristKey, out var wristPose))
            {
                _animator.SetIKPositionWeight(goal, 0);
                _animator.SetIKRotationWeight(goal, 0);
                return;
            }

            Vector3 targetPos = wristPose.position;

            // Clamp wrist to 95% of estimated arm reach
            if (TryGet(data, shoulderKey, out var shoulderPose))
            {
                Vector3 toWrist = targetPos - shoulderPose.position;
                float dist = toWrist.magnitude;
                if (dist > 0.001f)
                {
                    float maxReach = 0.62f;
                    if (TryGet(data, elbowKey, out var elbowEst))
                        maxReach = Vector3.Distance(shoulderPose.position, elbowEst.position) * 1.9f;
                    maxReach = Mathf.Clamp(maxReach, 0.35f, 0.75f) * 0.95f;
                    if (dist > maxReach)
                        targetPos = shoulderPose.position + toWrist.normalized * maxReach;
                }
            }

            targetPos = ApplyPositionSmoothing($"{goal}_{wristKey}", targetPos);
            float w = ComputeWeight(weight);

            _animator.SetIKPositionWeight(goal, w);
            _animator.SetIKPosition(goal, targetPos);
            _animator.SetIKRotationWeight(goal, 0); // rotation controlled by Phase 2, not here
        }

        /// <summary>
        /// Foot IK goal — position only, dead-zone + adaptive smoothing.
        /// </summary>
        private void ApplyGoalIK(AvatarIKGoal goal, string jointKey,
                                 PatientTrackingData data, float weight)
        {
            if (!TryGet(data, jointKey, out var pose))
            {
                _animator.SetIKPositionWeight(goal, 0);
                _animator.SetIKRotationWeight(goal, 0);
                return;
            }

            Vector3 targetPos = ApplyPositionSmoothing($"{goal}_{jointKey}", pose.position);
            float w = ComputeWeight(weight);

            _animator.SetIKPositionWeight(goal, w);
            _animator.SetIKPosition(goal, targetPos);
            _animator.SetIKRotationWeight(goal, 0);
        }

        private void ApplyHeadLook(PatientTrackingData data)
        {
            if (!TryGet(data, "Nose", out var nose)) return;
            float w = ComputeWeight(headWeight);
            _animator.SetLookAtWeight(w, bodyWeight: 0.3f, headWeight: 0.8f,
                                      eyesWeight: 0f, clampWeight: 0.7f);
            _animator.SetLookAtPosition(nose.position);
        }

        private void ApplyHintIK(AvatarIKHint hint, string jointKey,
                                 PatientTrackingData data, float weight)
        {
            if (!TryGet(data, jointKey, out var pose))
            {
                _animator.SetIKHintPositionWeight(hint, 0);
                return;
            }
            _animator.SetIKHintPositionWeight(hint, Mathf.Clamp01(globalWeight * weight));
            _animator.SetIKHintPosition(hint, pose.position);
        }

        #endregion

        // ═══════════════════════════════════════════════════════════════════
        //  Smoothing helpers
        // ═══════════════════════════════════════════════════════════════════

        #region Smoothing

        /// <summary>
        /// Dead-zone + adaptive smoothing for IK target positions.
        /// Returns the smoothed position and updates the cache.
        /// </summary>
        private Vector3 ApplyPositionSmoothing(string cacheKey, Vector3 rawPos)
        {
            Vector3 result = rawPos;

            if (_lastValidPositions.TryGetValue(cacheKey, out Vector3 lastPos))
            {
                float dist = Vector3.Distance(rawPos, lastPos);

                if (dist < movementDeadZone)
                {
                    result = lastPos; // filter micro-jitter
                }
                else if (useAdaptiveSmoothing)
                {
                    float vel = dist / Mathf.Max(Time.deltaTime, 0.001f);
                    float normVel = Mathf.Clamp01(vel / maxVelocity);
                    float speed = Mathf.Lerp(positionLerpSpeed * 0.5f,
                                              positionLerpSpeed * 1.5f, normVel);
                    float alpha = 1f - Mathf.Exp(-speed * Time.deltaTime);
                    result = Vector3.Lerp(lastPos, rawPos, alpha);
                }
            }

            _lastValidPositions[cacheKey] = result;
            return result;
        }

        private float ComputeWeight(float baseWeight)
        {
            float w = Mathf.Clamp01(globalWeight * baseWeight);
            if (_isRecoveringFromLoss)
                w *= Mathf.Clamp01(_recoveryBlendTime / RECOVERY_BLEND_DURATION);
            return w;
        }

        #endregion

        // ═══════════════════════════════════════════════════════════════════
        //  Support
        // ═══════════════════════════════════════════════════════════════════

        #region Support

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
            if (data.Joints != null && data.Joints.TryGetValue(key, out pose)) return true;
            pose = default;
            return false;
        }

        #endregion

        // ═══════════════════════════════════════════════════════════════════
        //  Runtime debug overlay — GL immediate-mode drawing.
        //  Works in Editor AND in builds (Android/ML2/etc.).
        //  Uses OnRenderObject() which is called after each camera renders.
        //  Toggle via drawGizmos in Inspector (also at runtime).
        // ═══════════════════════════════════════════════════════════════════

        #region Runtime Debug Overlay

#if AR_HEALTHCARE_DEBUG
        private Material _debugMat;

        /// <summary>
        /// Lazy-create an unlit material for GL drawing.
        /// Uses "Hidden/Internal-Colored" which is always available in Unity builds.
        /// </summary>
        private Material DebugMaterial
        {
            get
            {
                if (_debugMat == null)
                {
                    var shader = Shader.Find("Hidden/Internal-Colored");
                    if (shader == null) return null;
                    _debugMat = new Material(shader)
                    {
                        hideFlags = HideFlags.HideAndDontSave
                    };
                    // Render on top of everything, no depth test
                    _debugMat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                    _debugMat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                    _debugMat.SetInt("_Cull",     (int)UnityEngine.Rendering.CullMode.Off);
                    _debugMat.SetInt("_ZWrite",   0);
                    _debugMat.SetInt("_ZTest",    (int)UnityEngine.Rendering.CompareFunction.Always);
                }
                return _debugMat;
            }
        }

        /// <summary>
        /// Called after each camera finishes rendering. Draws debug lines/markers
        /// using GL immediate mode — visible in builds (Android, ML2, etc.), not
        /// just in the Editor Scene view.
        /// </summary>
        private void OnRenderObject()
        {
            if (!drawGizmos || !_hasValidData) return;
            if (_trackingProvider == null) return;

            var mat = DebugMaterial;
            if (mat == null) return;

            var data = _latestData;
            if (!data.IsTracked || data.Joints == null) return;

            mat.SetPass(0);

            GL.PushMatrix();
            // Draw in world space
            GL.MultMatrix(Matrix4x4.identity);

            // ── Hips anchor ──────────────────────────────────────────────
            if (TryGet(data, "LeftHip", out var lH) && TryGet(data, "RightHip", out var rH))
            {
                var mid = 0.5f * (lH.position + rH.position);
                GLDrawCross(mid, 0.05f, Color.red);

                if (useAnchor)
                {
                    var anchor = mid + anchorOffset + Vector3.up * verticalBodyOffset;
                    GLDrawCross(anchor, 0.06f, Color.yellow);
                    GLDrawLine(mid, anchor, Color.yellow);
                }
            }

            // ── Key landmarks (cyan crosses) ─────────────────────────────
            foreach (var key in new[] { "Nose", "LeftWrist", "RightWrist",
                                        "LeftAnkle", "RightAnkle",
                                        "LeftShoulder", "RightShoulder",
                                        "LeftElbow", "RightElbow",
                                        "LeftKnee", "RightKnee" })
            {
                if (TryGet(data, key, out var p))
                    GLDrawCross(p.position, 0.03f, Color.cyan);
            }

            // ── Tracked limb segments (green lines) ──────────────────────
            foreach (var (_, _, from, to) in LimbSegments)
            {
                if (TryGet(data, from, out var fp) && TryGet(data, to, out var tp))
                    GLDrawLine(fp.position, tp.position, Color.green);
            }

            // ── Avatar bone segments (magenta lines) — shows actual posed skeleton ──
            if (_animator != null)
            {
                foreach (var (bone, child, _, _) in LimbSegments)
                {
                    var boneT  = _animator.GetBoneTransform(bone);
                    var childT = _animator.GetBoneTransform(child);
                    if (boneT != null && childT != null)
                        GLDrawLine(boneT.position, childT.position, new Color(1f, 0.3f, 1f, 0.8f));
                }
            }

            GL.PopMatrix();
        }

        // ── GL helpers ───────────────────────────────────────────────────

        /// <summary>Draws a 3-axis cross marker at a world position.</summary>
        private static void GLDrawCross(Vector3 pos, float size, Color color)
        {
            GL.Begin(GL.LINES);
            GL.Color(color);
            GL.Vertex(pos + Vector3.right   * size);  GL.Vertex(pos - Vector3.right   * size);
            GL.Vertex(pos + Vector3.up      * size);  GL.Vertex(pos - Vector3.up      * size);
            GL.Vertex(pos + Vector3.forward * size);  GL.Vertex(pos - Vector3.forward * size);
            GL.End();
        }

        /// <summary>Draws a line between two world positions.</summary>
        private static void GLDrawLine(Vector3 a, Vector3 b, Color color)
        {
            GL.Begin(GL.LINES);
            GL.Color(color);
            GL.Vertex(a);
            GL.Vertex(b);
            GL.End();
        }

        /// <summary>Clean up debug material on destroy.</summary>
        private void OnDestroy()
        {
            if (_debugMat != null)
            {
                if (Application.isPlaying)
                    Destroy(_debugMat);
                else
                    DestroyImmediate(_debugMat);
            }
        }
#endif
        #endregion

#if UNITY_EDITOR
        // Keep Editor gizmos too (visible in Scene view during Play without camera rendering)
        #region Editor Gizmos

        private void OnDrawGizmos()
        {
            if (!drawGizmos || _trackingProvider == null) return;
            var data = _trackingProvider.GetPose();
            if (!data.IsTracked || data.Joints == null) return;

            if (TryGet(data, "LeftHip", out var lH) && TryGet(data, "RightHip", out var rH))
            {
                var mid = 0.5f * (lH.position + rH.position);
                Gizmos.color = Color.red;
                Gizmos.DrawWireSphere(mid, 0.05f);
                if (useAnchor)
                {
                    var anchor = mid + anchorOffset;
                    Gizmos.color = Color.yellow;
                    Gizmos.DrawWireSphere(anchor, 0.08f);
                    Gizmos.DrawLine(mid, anchor);
                }
            }

            Gizmos.color = Color.cyan;
            foreach (var key in new[] { "Nose", "LeftWrist", "RightWrist",
                                        "LeftAnkle", "RightAnkle",
                                        "LeftShoulder", "RightShoulder",
                                        "LeftElbow", "RightElbow",
                                        "LeftKnee", "RightKnee" })
            {
                if (TryGet(data, key, out var p))
                    Gizmos.DrawWireSphere(p.position, 0.03f);
            }

            Gizmos.color = Color.green;
            foreach (var (_, _, from, to) in LimbSegments)
            {
                if (TryGet(data, from, out var fp) && TryGet(data, to, out var tp))
                    Gizmos.DrawLine(fp.position, tp.position);
            }
        }

        #endregion
#endif
    }
}
