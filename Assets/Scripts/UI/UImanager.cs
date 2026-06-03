using UnityEngine;
using TMPro;
using ARHealthCare.Core;
using ARHealthCare.AI;


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

        [Header("Holo Panel")]
        [Tooltip("Root GameObject of the Holo world-space canvas")]
        [SerializeField] private GameObject _holoCanvas;
        [Tooltip("MedBotPanelController on the Holo canvas - notified when panel opens/closes")]
        [SerializeField] private MedBotPanelController _holoPanelController;

        [Header("Core Components")]
        [Tooltip("Reference to the UITracking component")]
        public UITracking uiTracking;

        [Header("Avatar Toggle")]
        [Tooltip("Optional label on the Toggle Avatar button - text is updated when toggled")]
        [SerializeField] private TextMeshProUGUI _toggleAvatarButtonLabel;
        [SerializeField] private GameObject _PatientIssueMarkerPrefab;

        private bool _avatarHidden;
        private bool _holoOpen;

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
            CloseHoloPanel();
        }

        // =========================
        // Display Patient Info
        // =========================
        public void StartPatientInfo()
        {
            CloseHoloPanel(); // Holo and PatientInfo are mutually exclusive (FOV constraint)
            mainMenuFade.FadeOut();
            mainMenuFade.gameObject.SetActive(false);
            patientInfoFade.gameObject.SetActive(true);
            patientInfoFade.FadeIn();
            UIButtonsCanvas.SetActive(true);
        }

        // =========================
        // TOGGLE HOLO PANEL
        // =========================
        public void ToggleHoloPanel()
        {
            if (_holoOpen) CloseHoloPanel();
            else OpenHoloPanel();
        }

        private void OpenHoloPanel()
        {
            // Hide patient info to reduce FOV clutter - only one content panel at a time
            if (patientInfoFade != null && patientInfoFade.isActiveAndEnabled)
                patientInfoFade.FadeOut();

            if (UIButtonsCanvas != null) UIButtonsCanvas.SetActive(false);

            if (_holoCanvas != null) _holoCanvas.SetActive(true);
            if (_holoPanelController != null) _holoPanelController.OnPanelOpened();
            _holoOpen = true;
        }

        private void CloseHoloPanel()
        {
            if (_holoCanvas != null) _holoCanvas.SetActive(false);
            if (_holoPanelController != null) _holoPanelController.OnPanelClosed();
            _holoOpen = false;
            
            if (UIButtonsCanvas != null && !mainMenuFade.isActiveAndEnabled) UIButtonsCanvas.SetActive(true);
        }

        // =========================
        // RETURN TO MENU
        // =========================
        public void ReturnToMenu()
        {
            // Close Holo first if open - back steps through panels one at a time
            if (_holoOpen)
            {
                CloseHoloPanel();
                return;
            }

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
                _PatientIssueMarkerPrefab.SetActive(true);
            }
            else
            {
                uiTracking.HideOnlyAvatar();
                _avatarHidden = true;
                _PatientIssueMarkerPrefab.SetActive(false);
            }

            if (_toggleAvatarButtonLabel != null)
                _toggleAvatarButtonLabel.text = _avatarHidden ? "Avatar: Hidden" : "Avatar: Visible";
        }

        // =========================
        // RESET STATE
        // =========================
        private void ShowMainMenu()
        {
            patientInfoFade.gameObject.SetActive(false);
            UIButtonsCanvas.SetActive(false);
            CloseHoloPanel();

            mainMenuFade.gameObject.SetActive(true);
            mainMenuFade.canvasGroup.alpha = 1f;
            mainMenuFade.canvasGroup.interactable = true;

            _avatarHidden = false;
            if (_toggleAvatarButtonLabel != null)
                _toggleAvatarButtonLabel.text = "Avatar: Visible";
        }

    }

}