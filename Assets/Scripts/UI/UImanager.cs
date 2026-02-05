using UnityEngine;
using ARHealthCare.Core;


namespace ARHealthCare.UI
{

    public class UIManager : MonoBehaviour
    {
        [Header("Canvases")]
        [Tooltip("Canvas containing the main menu")]
        public MainMenuFade mainMenuFade;
        [Tooltip("Canvas containing the patient info")]
        public PatientInfoFade patientInfoFade;
        [Tooltip("Canvas containing the back and patient info button")]
        public GameObject UIButtonsCanvas;

        [Header("Core Components")]
        [Tooltip("Reference to the UITracking component")]
        public UITracking uiTracking;

        void Start()
        {
            ShowMainMenu();
        }

        // =========================
        // START AR TRACKING ENVIRONMENT
        // =========================
        public void ARStartEnvironment()
        {
            Debug.Log("AR Environment Starting...");
            uiTracking.StartTrackingFromCore();
            mainMenuFade.FadeOut();
            UIButtonsCanvas.SetActive(true);
        }

        // =========================
        // Display Patient Info
        // =========================
        public void StartPatientInfo()
        {
            Debug.Log("START SIMULATION");

            mainMenuFade.FadeOut();
            patientInfoFade.FadeIn();
            UIButtonsCanvas.SetActive(true);
        }

        // =========================
        // RETURN TO MENU
        // =========================
        public void ReturnToMenu()
        {
            patientInfoFade.FadeOut();
            // If the patient info canvas is active, use the back button to hide it
            if (patientInfoFade.isActiveAndEnabled)
            {
                patientInfoFade.FadeOut();
            } else if (mainMenuFade.isActiveAndEnabled) {
                mainMenuFade.FadeOut();
            } else {
                mainMenuFade.gameObject.SetActive(true);
                mainMenuFade.canvasGroup.alpha = 1f;
                mainMenuFade.canvasGroup.interactable = true;
                mainMenuFade.canvasGroup.blocksRaycasts = true;
                uiTracking.StopTrackingFromCore();

                UIButtonsCanvas.SetActive(false);
            }
        }

        // =========================
        // RESET STATE
        // =========================
        private void ShowMainMenu()
        {
            patientInfoFade.gameObject.SetActive(false);
            UIButtonsCanvas.SetActive(false);

            mainMenuFade.gameObject.SetActive(true);
            mainMenuFade.canvasGroup.alpha = 1f;
            mainMenuFade.canvasGroup.interactable = true;
            mainMenuFade.canvasGroup.blocksRaycasts = true;
        }

    }

}