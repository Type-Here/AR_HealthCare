using UnityEngine;
using TMPro;
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

        [Header("Avatar Toggle")]
        [Tooltip("Optional label on the Toggle Avatar button — text is updated when toggled")]
        [SerializeField] private TextMeshProUGUI _toggleAvatarButtonLabel;

        private bool _avatarHidden;

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
            uiTracking.StartTrackingFromCore(); // Activate tracking GO before fading out the menu to minimize delay
            mainMenuFade.FadeOut();
            mainMenuFade.gameObject.SetActive(false);
            UIButtonsCanvas.SetActive(true);
            patientInfoFade.gameObject.SetActive(false); // Ensure patient info is hidden when starting AR
        }

        // =========================
        // Display Patient Info
        // =========================
        public void StartPatientInfo()
        {
            mainMenuFade.FadeOut();
            mainMenuFade.gameObject.SetActive(false);
            patientInfoFade.gameObject.SetActive(true);
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
                patientInfoFade.gameObject.SetActive(false);

            } else if (mainMenuFade.isActiveAndEnabled) {
                mainMenuFade.FadeOut();
                mainMenuFade.gameObject.SetActive(false);
                
            } else {
                mainMenuFade.gameObject.SetActive(true);
                mainMenuFade.canvasGroup.alpha = 1f;
                mainMenuFade.canvasGroup.interactable = true;
                mainMenuFade.canvasGroup.blocksRaycasts = true;
                //uiTracking.StopTrackingFromCore();
                uiTracking.HideOnlyAvatar();

                UIButtonsCanvas.SetActive(false);
            }
        }

        // =========================
        // TOGGLE AVATAR VISIBILITY
        // =========================
        public void ToggleAvatarVisibility()
        {
            if (_avatarHidden)
            {
                uiTracking.StartTrackingFromCore();
                _avatarHidden = false;
            }
            else
            {
                uiTracking.HideOnlyAvatar();
                _avatarHidden = true;
            }

            if (_toggleAvatarButtonLabel != null)
                _toggleAvatarButtonLabel.text = _avatarHidden ? "Show Avatar" : "Hide Avatar";
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

            _avatarHidden = false;
            if (_toggleAvatarButtonLabel != null)
                _toggleAvatarButtonLabel.text = "Hide Avatar";
        }

    }

}