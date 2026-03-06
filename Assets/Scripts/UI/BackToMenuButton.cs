using UnityEngine;

public class BackToMenuButton : MonoBehaviour
{
    public UIManager uiManager;

    public void OnBackPressed()
    {
        uiManager.ReturnToMenu();
    }
}
