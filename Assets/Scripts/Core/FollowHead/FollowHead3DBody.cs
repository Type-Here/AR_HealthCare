using UnityEngine;

namespace ARHealthCare.Core.FollowHead
{
    /**
    * HeadFollowAnchor
    * Summary:
    * Script to make a GameObject follow the user's head with an offset.
    * Description:
    * Attach this script to a GameObject to have it follow the user's head
    * position and rotation with a specified local offset.
    * 
    * Useful for positioning objects relative to the user's view in AR/VR.
    * 
    */
    public class HeadFollowBodyAnchor : MonoBehaviour
    {
        public Transform head;

        [Header("Offset in head-local space")]
        public Vector3 localPositionOffset = new Vector3(0f, -0.9f, 2.0f);

        [Header("Rotation")]
        public bool followYawOnly = false; // for body often you want full rotation = false/true depending on debug

        void LateUpdate()
        {
            if (head == null)
            {
                if (Camera.main == null) return;
                head = Camera.main.transform;
            }

            transform.position = head.TransformPoint(localPositionOffset);

            if (followYawOnly)
            {
                var e = head.rotation.eulerAngles;
                transform.rotation = Quaternion.Euler(0f, e.y, 0f);
            }
            else
            {
                transform.rotation = head.rotation;
            }
        }
    }

}