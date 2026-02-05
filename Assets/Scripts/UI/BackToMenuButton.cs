using UnityEngine;
using ARHealthCare.UI;

public class BackToMenuButton : MonoBehaviour
{
    public UIManager uiManager;

    public void OnBackPressed()
    {
        uiManager.ReturnToMenu();
    }
}
