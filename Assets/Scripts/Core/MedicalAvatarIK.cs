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
        private Transform _headBone; // Direct reference to the head bone

        [Header("IK Settings")]
        [Range(0,1)] public float globalWeight = 1.0f;

        [Header("Tracking Provider")]
        [Tooltip("Drag here any component that implements ITrackingProvider (e.g. TrackingManager).")]
        [SerializeField] private MonoBehaviour trackingProviderBehaviour;

         private ITrackingProvider trackingProvider;

        void Awake()
        {
            _animator = GetComponent<Animator>();
            // We get the reference to the head bone through the Animator
            // This works if the rig is set as "Humanoid"
            _headBone = _animator.GetBoneTransform(HumanBodyBones.Head);

            // Try to resolve provider from serialized field or fallback to first TrackingManager in scene
            if (trackingProviderBehaviour == null)
            {
                var manager = Object.FindFirstObjectByType<TrackingManager>();
                if (manager != null)
                {
                    trackingProviderBehaviour = manager;
                    Debug.Log("MedicalAvatarIK: trackingProviderBehaviour not set, using TrackingManager from scene.");
                }
            }

            trackingProvider = trackingProviderBehaviour as ITrackingProvider;
            if (trackingProvider == null)
            {
                Debug.LogError("MedicalAvatarIK: trackingProviderBehaviour is not set or does not implement ITrackingProvider.");
            }
        }

        void OnAnimatorIK(int layerIndex)
        {
            //Debug.Log("IK Funzione in esecuzione.");

            if (_animator == null || trackingProvider == null) return;

            PatientTrackingData data = trackingProvider.GetPose();

            // If we are not tracking, reset the IK weights for the limbs and exit
            if (!data.IsTracked)
            {
                _animator.SetIKPositionWeight(AvatarIKGoal.RightHand, 0);
                _animator.SetIKPositionWeight(AvatarIKGoal.LeftHand, 0);
                return;
            }

            // --- LIMB MANAGEMENT (Standard IK) ---
            UpdateLimbIK(AvatarIKGoal.RightHand, "RightHand", data);
            UpdateLimbIK(AvatarIKGoal.LeftHand, "LeftHand", data);
            // TODO: AvatarIKGoal.RightFoot, etc.

            // --- HEAD MANAGEMENT (Direct Manipulation) ---
            // The head does not use IKGoal, but we move the bone directly
            if (_headBone != null && data.Joints.ContainsKey("Head"))
            {
                // We map the rotation directly
                // Note: For the head position, it is often preferred to let the neck handle it,
                // but if you want 1:1 tracking, we also overwrite the position.
                
                _headBone.position = Vector3.Lerp(_headBone.position, data.Joints["Head"].position, globalWeight);
                _headBone.rotation = Quaternion.Slerp(_headBone.rotation, data.Joints["Head"].rotation, globalWeight);
            }
        }

        // Helper function specific ONLY for limbs (Hands/Feet)
        private void UpdateLimbIK(AvatarIKGoal goal, string dataKey, PatientTrackingData data)
        {
            if (data.Joints.ContainsKey(dataKey))
            {
                _animator.SetIKPositionWeight(goal, globalWeight);
                _animator.SetIKRotationWeight(goal, globalWeight);

                _animator.SetIKPosition(goal, data.Joints[dataKey].position);
                _animator.SetIKRotation(goal, data.Joints[dataKey].rotation);
            }
            else
            {
                _animator.SetIKPositionWeight(goal, 0);
                _animator.SetIKRotationWeight(goal, 0);
            }
        }
    }
}