//Per rendere il movimento più fluido, si usa Lerp per interpolare posizione e rotazione, 
//invece di spostarli bruscamente a ogni frame.
using System.Collections;
using System.Collections.Generic;
using UnityEngine;


namespace ARHealthCare.Core.FollowHead
{
    /**
        * FollowHeadCanvas
        * Summary:
        * Script per far seguire la UI alla testa del giocatore
        * Description:
        * Attacca questo script al canvas che contiene la UI che deve seguire la testa.
        * 
        * Il canvas seguirà la testa del giocatore mantenendo una distanza e altezza configurabile.
        * La rotazione del canvas si adatterà per guardare sempre verso il giocatore.
        * 
        * Include due modalità:
        * - Modalità Classica: segue anche su/giù con la testa (utile in AR)
        * - Modalità Base Height: mantiene un'altezza costante dai piedi, non segue su/giù (più comoda in VR)
        */
    public class UICanvasFollowHead : MonoBehaviour
    {
        [Header("Riferimenti")]
        public Transform playerHead;       // Camera XR
        public Transform playerBase;       // Base del Character / piedi

        [Header("Distanza UI")]
        public float distance = 1.5f;

        [Header("Altezza UI")]
        public float heightOffset = -0.2f;     // usato solo in modalità classica
        public float heightFromBase = 1.4f;    // altezza costante dai piedi

        [Header("Smooth")]
        public float followSpeed = 1f;

        [Header("Flags")]
        public bool useBaseHeight = false;

        void LateUpdate()
        {
            if (!playerHead) return;

            // -----------------------------------------------------
            // BASE HEIGHT MODALITY (VR FRIENDLY)
            // -----------------------------------------------------

            // Orizzontal Forward (ignore head pitch)
            Vector3 flatForward = playerHead.forward;
            flatForward.y = 0f;
            flatForward.Normalize();

            // XZ follow head → follows physical movement
            Vector3 targetPos =
                playerHead.position +
                flatForward * distance;

            // Y Blocked on Base height → doesn't follow head up/down
            //if (playerBase != null)
            //    targetPos.y = playerBase.position.y + heightFromBase;

            // Position Smooth
            transform.position = Vector3.Lerp(
                transform.position,
                targetPos,
                Time.deltaTime * followSpeed
            );

            // Only Orizzontal Rotation → UI always faces player but doesn't tilt with head
            Quaternion targetRot = Quaternion.LookRotation(flatForward);
            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                targetRot,
                Time.deltaTime * followSpeed
            );
        }
    }

}