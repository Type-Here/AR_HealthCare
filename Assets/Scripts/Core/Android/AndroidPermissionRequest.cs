using UnityEngine;
using UnityEngine.Android;

namespace ARHealthCare.Core
{
    public class AndroidPermissionRequest : MonoBehaviour
    {
        void Awake()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            // Request Camera permission on Android at runtime
            if (!Permission.HasUserAuthorizedPermission(Permission.Camera))
            {
                Permission.RequestUserPermission(Permission.Camera);
            }
#endif
        }
    }
}