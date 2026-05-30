using System.Collections;
using System.Collections.Generic;
using UnityEngine;


namespace ARHealthCare.Core.FollowHead
{       /// <summary>
        /// FollowHeadCanvas: A versatile script to make a UI canvas follow the player's head
        /// in both AR and VR. 
        /// 
        /// It maintains a configurable distance and height, and smoothly rotates to face the player. 
        /// 
        /// Supports two modes: 
        /// - "Classic" (follows head movement in all directions)
        /// - "Base Height" (maintains constant height from the ground, ignoring vertical head movement).
        ///</summary>
    public class UICanvasFollowHead : MonoBehaviour
    {
        [Header("Riferimenti")]
        public Transform playerHead;       // Camera XR
        public Transform playerBase;       // Character base / feet

        [Header("Distanza UI")]
        public float distance = 1.5f;

        [Header("Altezza UI")]
        public float heightOffset = -0.2f;     // only for Classic mode
        public float heightFromBase = 1.4f;    // constant height from feet

        [Header("Smooth")]
        public float followSpeed = 1f;

        [Header("Flags")]
        public bool useBaseHeight = false;

        private void OnEnable()
        {
            // Snap immediately so the panel appears at the correct position on first open,
            // instead of lerping from whatever world position it had in the scene.
            SnapToTarget();
        }

        void LateUpdate()
        {
            if (!playerHead) return;

            Vector3 flatForward = playerHead.forward;
            flatForward.y = 0f;
            if (flatForward.sqrMagnitude < 0.001f) return;
            flatForward.Normalize();

            Vector3 targetPos = playerHead.position + flatForward * distance;

            transform.position = Vector3.Lerp(
                transform.position,
                targetPos,
                Time.deltaTime * followSpeed
            );

            Quaternion targetRot = Quaternion.LookRotation(flatForward);
            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                targetRot,
                Time.deltaTime * followSpeed
            );
        }

        private void SnapToTarget()
        {
            Transform head = playerHead;
            if (head == null) head = Camera.main != null ? Camera.main.transform : null;
            if (head == null) return;

            Vector3 flatForward = head.forward;
            flatForward.y = 0f;
            if (flatForward.sqrMagnitude < 0.001f) return;
            flatForward.Normalize();

            transform.position = head.position + flatForward * distance;
            transform.rotation = Quaternion.LookRotation(flatForward);
        }
    }

}