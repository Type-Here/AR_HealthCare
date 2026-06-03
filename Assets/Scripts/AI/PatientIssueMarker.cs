using System;
using System.Collections.Generic;
using UnityEngine;

using ARHealthCare.Network;

namespace ARHealthCare.AI
{
    public class PatientIssueMarker : MonoBehaviour
    {
        [Header("Avatar")]
        [SerializeField] private Animator _avatarAnimator;

        [Header("Marker Visuals")]
        [SerializeField] private Renderer _markerRenderer;
        [SerializeField] private Material _normalMat;
        [SerializeField] private Material _selectedMat;

        [Header("Anchors")]
        [SerializeField] private Material _anchorMat;
        [SerializeField] [Range(0.02f, 0.15f)] private float _anchorScale = 0.05f;

        [Header("Persistence")]
        [SerializeField] private HealthCareApiClient _apiClient;

        // Keyword → bone mapping (Italian + English, right-side default)
        private static readonly (HumanBodyBones bone, string[] keywords)[] BoneKeywords =
        {
            (HumanBodyBones.RightLowerLeg,  new[] { "crociato", "menisco", "knee", "ginocchio", "tibiale", "perone", "fibula" }),
            (HumanBodyBones.RightUpperLeg,  new[] { "femore", "coscia", "thigh", "quadricipite" }),
            (HumanBodyBones.RightFoot,      new[] { "caviglia", "ankle", "piede", "foot", "tallone", "achille" }),
            (HumanBodyBones.Hips,           new[] { "anca", "hip", "pelvi", "inguine", "iliaco" }),
            (HumanBodyBones.Spine,          new[] { "disco", "lombar", "l4", "l5", "ernia", "vertebr", "colonna", "schiena", "back" }),
            (HumanBodyBones.UpperChest,     new[] { "torace", "sterno", "chest", "costol", "petto", "dorsale", "dorso" }),
            (HumanBodyBones.Neck,           new[] { "collo", "cervicale", "neck", "c3", "c4", "c5", "c6", "c7" }),
            (HumanBodyBones.Head,           new[] { "testa", "head", "cranio", "cerebr", "mandibola" }),
            (HumanBodyBones.RightUpperArm,  new[] { "spalla", "shoulder", "rotatore", "omero" }),
            (HumanBodyBones.RightLowerArm,  new[] { "gomito", "elbow", "avambrac", "radio", "ulna" }),
            (HumanBodyBones.RightHand,      new[] { "polso", "wrist", "mano", "hand", "carpo" }),
        };

        private static readonly HumanBodyBones[] AnchorBones =
        {
            HumanBodyBones.Head,
            HumanBodyBones.Neck,
            HumanBodyBones.UpperChest,
            HumanBodyBones.Spine,
            HumanBodyBones.Hips,
            HumanBodyBones.RightUpperArm,  HumanBodyBones.LeftUpperArm,
            HumanBodyBones.RightLowerArm,  HumanBodyBones.LeftLowerArm,
            HumanBodyBones.RightHand,      HumanBodyBones.LeftHand,
            HumanBodyBones.RightUpperLeg,  HumanBodyBones.LeftUpperLeg,
            HumanBodyBones.RightLowerLeg,  HumanBodyBones.LeftLowerLeg,
            HumanBodyBones.RightFoot,      HumanBodyBones.LeftFoot,
        };

        private HumanBodyBones _currentBone = HumanBodyBones.RightLowerLeg;
        private string _currentPatientId;
        private bool _moveMode;
        private readonly List<GameObject> _anchorGos = new();

        private void Start()
        {
            BuildAnchors();
            SetMoveMode(false);

            var interactable = GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRSimpleInteractable>();
            if (interactable != null)
                interactable.selectEntered.AddListener(_ => ToggleMoveMode());
            
            //Set GO of Mesh Renderer enabled only if we have an avatar animator
            if (_markerRenderer != null) _markerRenderer.enabled = _avatarAnimator != null;
        }

        private void LateUpdate()
        {
            if (_avatarAnimator == null) return;
            var boneTransform = _avatarAnimator.GetBoneTransform(_currentBone);
            if (boneTransform != null)
                transform.position = boneTransform.position;
        }

        public void SetPatient(PatientRecord record)
        {
            _currentPatientId = record.id;

            if (!string.IsNullOrEmpty(record.marker_bone) &&
                Enum.TryParse(record.marker_bone, out HumanBodyBones saved))
                _currentBone = saved;
            else
            {
                string text = $"{record.diagnosis} {record.planned_procedure} {record.notes}".ToLowerInvariant();
                _currentBone = MatchBone(text);
            }
        }

        public void ToggleMoveMode() => SetMoveMode(!_moveMode);

        private void SetMoveMode(bool on)
        {
            _moveMode = on;
            if (_markerRenderer != null)
                _markerRenderer.material = on ? _selectedMat : _normalMat;
            foreach (var go in _anchorGos)
                go.SetActive(on);
        }

        private void SnapToBone(HumanBodyBones bone)
        {
            _currentBone = bone;
            SetMoveMode(false);

            if (_apiClient != null && !string.IsNullOrEmpty(_currentPatientId))
                StartCoroutine(_apiClient.SaveMarkerBone(
                    _currentPatientId, bone.ToString(),
                    onSuccess: null,
                    onFailure: err => Debug.LogWarning($"[PatientIssueMarker] SaveMarkerBone failed: {err}")));
        }

        private void BuildAnchors()
        {
            if (_avatarAnimator == null) return;

            foreach (var bone in AnchorBones)
            {
                var boneTransform = _avatarAnimator.GetBoneTransform(bone);
                if (boneTransform == null) continue;

                var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                go.name = $"Anchor_{bone}";
                go.transform.SetParent(boneTransform, false);
                go.transform.localScale = Vector3.one * _anchorScale;

                if (_anchorMat != null)
                    go.GetComponent<Renderer>().sharedMaterial = _anchorMat;

                // Destroy the default physics collider — XRI will add its own interaction volume
                Destroy(go.GetComponent<Collider>());
                var col = go.AddComponent<SphereCollider>();
                col.isTrigger = false;

                var anchor = go.AddComponent<BoneAnchor>();
                anchor.Bone = bone;

                var interactable = go.AddComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRSimpleInteractable>();
                var capturedBone = bone;
                interactable.selectEntered.AddListener(_ =>
                {
                    if (_moveMode) SnapToBone(capturedBone);
                });

                _anchorGos.Add(go);
            }
        }

        private HumanBodyBones MatchBone(string text)
        {
            bool isLeft = text.Contains("sinistra") || text.Contains("(sx)") || text.Contains(" sx") || text.Contains("left");

            int bestScore = 0;
            HumanBodyBones best = HumanBodyBones.RightLowerLeg;

            foreach (var (bone, keywords) in BoneKeywords)
            {
                int score = 0;
                foreach (var kw in keywords)
                    if (text.Contains(kw)) score++;

                if (score > bestScore)
                {
                    bestScore = score;
                    best = isLeft ? MirrorToLeft(bone) : bone;
                }
            }
            return best;
        }

        private static HumanBodyBones MirrorToLeft(HumanBodyBones bone) => bone switch
        {
            HumanBodyBones.RightUpperArm => HumanBodyBones.LeftUpperArm,
            HumanBodyBones.RightLowerArm => HumanBodyBones.LeftLowerArm,
            HumanBodyBones.RightHand     => HumanBodyBones.LeftHand,
            HumanBodyBones.RightUpperLeg => HumanBodyBones.LeftUpperLeg,
            HumanBodyBones.RightLowerLeg => HumanBodyBones.LeftLowerLeg,
            HumanBodyBones.RightFoot     => HumanBodyBones.LeftFoot,
            _                            => bone
        };
    }
}
