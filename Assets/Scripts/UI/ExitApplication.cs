using UnityEngine;

public class ExitApplication : MonoBehaviour
{
    public void Exit()
    {
        Debug.Log("EXIT pressed");

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
