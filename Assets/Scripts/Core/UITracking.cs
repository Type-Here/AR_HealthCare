using System.Collections.Generic;
using UnityEngine;

namespace ARHealthCare.Core
{
    public class UITracking : MonoBehaviour
    {
        [SerializeField][Tooltip("List of GameObjects to activate for tracking")]
        private List<GameObject> trackingObjects; 

        public void StartTrackingFromCore()
        {
            for (int i = 0; i < trackingObjects.Count; i++)
            {
                trackingObjects[i].SetActive(true);
            }
        }

        public void StopTrackingFromCore()
        {
            for (int i = 0; i < trackingObjects.Count; i++)
            {
                trackingObjects[i].SetActive(false);
            }
        }

        public void HideOnlyAvatar()
        {
            for (int i = 0; i < trackingObjects.Count; i++)
            {
                if (trackingObjects[i].name.ToLower().Contains("dummy") || 
                    trackingObjects[i].tag.ToLower() == "avatar3d")
                {
                    trackingObjects[i].SetActive(false);
                }
            }
        }
    }
}