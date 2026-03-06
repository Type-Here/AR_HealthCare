using UnityEngine;

public class UIManager : MonoBehaviour
{
    [Header("Canvases")]
    public MainMenuFade mainMenuFade;
    public PatientInfoFade patientInfoFade;
    public GameObject backButtonCanvas;

    void Start()
    {
        ShowMainMenu();
    }

    // =========================
    // START SIMULATION
    // =========================
    public void StartSimulation()
    {
        Debug.Log("START SIMULATION");

        mainMenuFade.FadeOut();
        patientInfoFade.FadeIn();
        backButtonCanvas.SetActive(true);
    }

    // =========================
    // RETURN TO MENU
    // =========================
    public void ReturnToMenu()
    {
        patientInfoFade.FadeOut();

        mainMenuFade.gameObject.SetActive(true);
        mainMenuFade.canvasGroup.alpha = 1f;
        mainMenuFade.canvasGroup.interactable = true;
        mainMenuFade.canvasGroup.blocksRaycasts = true;

        backButtonCanvas.SetActive(false);
    }

    // =========================
    // RESET STATE
    // =========================
    private void ShowMainMenu()
    {
        patientInfoFade.gameObject.SetActive(false);
        backButtonCanvas.SetActive(false);

        mainMenuFade.gameObject.SetActive(true);
        mainMenuFade.canvasGroup.alpha = 1f;
        mainMenuFade.canvasGroup.interactable = true;
        mainMenuFade.canvasGroup.blocksRaycasts = true;
    }
}
