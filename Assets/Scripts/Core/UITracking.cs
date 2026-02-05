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
    }
}