using UnityEngine;

namespace ARHealthCare.Core.FollowHead
{
    /**
    * HeadLockedAnchor
    * Summary:
    * Script to make a GameObject follow the user's head position with an offset,
    * but maintain a fixed rotation (optionally only yaw).
    * Description:
    * Attach this script to a GameObject to have it follow the user's head
    * position with a specified local offset, while keeping a fixed rotation.
    * 
    * Useful for UI elements or objects that should stay in front of the user
    * without rotating with head movements.
    * 
    */
    public class HeadLockedAnchor : MonoBehaviour
    {
        [Tooltip("XR camera / CenterEye. If null, uses Camera.main.")]
        public Transform head;

        [Header("Offset in head-local space")]
        public Vector3 localPositionOffset = new Vector3(0f, 0f, 2.0f);

        [Header("Rotation")]
        public bool useYawOnly = true;          // vertical: no pitch/roll
        public float yawExtraDegrees = 0f;      // small tweaks if needed

        void LateUpdate()
        {
            if (head == null)
            {
                if (Camera.main == null) return;
                head = Camera.main.transform;
            }

            // Position follows head (offset expressed in head local space)
            transform.position = head.TransformPoint(localPositionOffset);

            if (useYawOnly)
            {
                // Take yaw from head but keep upright (no pitch/roll)
                Vector3 e = head.rotation.eulerAngles;
                transform.rotation = Quaternion.Euler(0f, e.y + yawExtraDegrees, 0f);
            }
            else
            {
                // Fully fixed upright (world-forward), no rotation following at all
                transform.rotation = Quaternion.identity;
            }
        }
    }

}
