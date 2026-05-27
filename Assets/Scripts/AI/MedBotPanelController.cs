using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using ARHealthCare.Network;

namespace ARHealthCare.AI
{
    /// <summary>
    /// Controls the MedBot suggestion panel:
    ///   - Tab strip: Suggestions / Records / Checklist
    ///   - Mode toggle: Student / Physician
    ///   - Calls HealthCareApiClient for LLM suggestions
    ///   - Populates checklist items from server
    ///
    /// Scene setup: attach to a World Space Canvas (380x280px, scale 0.001).
    /// Assign all serialized references in Inspector.
    /// Canvas must have TrackedDeviceGraphicRaycaster for Magic Leap 2 ray interaction.
    /// </summary>
    public class MedBotPanelController : MonoBehaviour
    {
        [Header("Dependencies")]
        [SerializeField] private HealthCareApiClient _apiClient;
        [SerializeField] private MedBotController _medBotController;

        [Header("Mode")]
        [SerializeField] private TextMeshProUGUI _modeButtonLabel;

        [Header("Tab Panels")]
        [SerializeField] private GameObject _panelSuggestions;
        [SerializeField] private GameObject _panelRecords;
        [SerializeField] private GameObject _panelChecklist;

        [Header("Suggestions Panel")]
        [SerializeField] private TextMeshProUGUI _outputText;
        [SerializeField] private ScrollRect _suggestionsScroll;

        [Header("Records Panel")]
        [SerializeField] private TextMeshProUGUI _recordText;

        [Header("Checklist Panel")]
        [SerializeField] private Transform _checklistContent;
        [SerializeField] private GameObject _checklistItemPrefab;

        [Header("Status")]
        [SerializeField] private TextMeshProUGUI _statusLabel;

        [Header("Notification Badge")]
        [Tooltip("Badge GameObject on the Holo toggle button in UIButtonsCanvas - shown when new content arrives while panel is closed")]
        [SerializeField] private GameObject _notificationBadge;
        [SerializeField] private TextMeshProUGUI _notificationCount;

        private PatientRecord _currentPatient;
        private string _currentMode = "student";
        private bool _panelOpen;
        private int _pendingCount;

        private void Start()
        {
            OnTabClicked(0);
            UpdateModeLabel();
            ClearNotifications();
        }


        //  Public API 

        public void SetPatient(PatientRecord record)
        {
            _currentPatient = record;
            PopulateRecords();
            ShowStatus($"Patient: {record.display_name}");
        }

        public void PopulateChecklist(ChecklistResponse response)
        {
            if (_checklistContent == null || _checklistItemPrefab == null) return;

            foreach (Transform child in _checklistContent)
                Destroy(child.gameObject);

            if (response?.items == null) return;

            foreach (string item in response.items)
            {
                var go = Instantiate(_checklistItemPrefab, _checklistContent);
                var toggle = go.GetComponentInChildren<Toggle>();
                var label  = go.GetComponentInChildren<TextMeshProUGUI>();
                if (toggle != null) toggle.isOn = false;
                if (label  != null) label.text  = item;
            }

            AddNotification();
        }


        //  Button callbacks (wire in Inspector) 

        public void OnAskClicked()
        {
            if (_currentPatient == null)
            {
                ShowStatus("No patient loaded — scan QR first");
                return;
            }
            StartCoroutine(FetchSuggestion());
        }

        // Called by UIManager when the Holo canvas is shown
        public void OnPanelOpened()
        {
            _panelOpen = true;
            ClearNotifications();
        }

        // Called by UIManager when the Holo canvas is hidden
        public void OnPanelClosed()
        {
            _panelOpen = false;
        }

        public void OnModeClicked()
        {
            _currentMode = _currentMode == "student" ? "physician" : "student";
            UpdateModeLabel();
            if (_medBotController != null) _medBotController.SetMode(_currentMode == "student");
        }

        public void OnTabClicked(int tabIndex)
        {
            if (_panelSuggestions != null) _panelSuggestions.SetActive(tabIndex == 0);
            if (_panelRecords     != null) _panelRecords.SetActive(tabIndex == 1);
            if (_panelChecklist   != null) _panelChecklist.SetActive(tabIndex == 2);
        }


        // Private

        private IEnumerator FetchSuggestion()
        {
            ShowStatus("Thinking…");
            if (_outputText != null) _outputText.text = "";

            yield return _apiClient.GetSuggestion(
                _currentPatient,
                _currentPatient.specialty ?? "general",
                _currentMode,
                "",
                onSuccess: text =>
                {
                    if (_outputText != null) _outputText.text = text;
                    ScrollToBottom();
                    ShowStatus("Ready");
                    AddNotification();
                },
                onFailure: err =>
                {
                    if (_outputText != null) _outputText.text = $"[Error: {err}]";
                    ShowStatus("Server error");
                });
        }

        private void AddNotification()
        {
            if (_panelOpen) return;
            _pendingCount++;
            if (_notificationBadge != null) _notificationBadge.SetActive(true);
            if (_notificationCount != null) _notificationCount.text = _pendingCount.ToString();
        }

        private void ClearNotifications()
        {
            _pendingCount = 0;
            if (_notificationBadge != null) _notificationBadge.SetActive(false);
        }

        private void PopulateRecords()
        {
            if (_recordText == null || _currentPatient == null) return;

            _recordText.text =
                $"<b>{_currentPatient.display_name}</b>  |  {_currentPatient.age} anni\n\n"
                + $"<b>Diagnosi:</b> {_currentPatient.diagnosis}\n\n"
                + $"<b>Intervento:</b> {_currentPatient.planned_procedure}\n"
                + $"<b>Data:</b> {_currentPatient.procedure_date}\n\n"
                + $"<b>Trattamento:</b> {_currentPatient.current_treatment}\n\n"
                + $"<b>Note:</b> {_currentPatient.notes}";
        }

        private void UpdateModeLabel()
        {
            if (_modeButtonLabel != null)
                _modeButtonLabel.text = _currentMode == "student" ? "Student" : "Physician";
        }

        private void ShowStatus(string msg)
        {
            if (_statusLabel != null) _statusLabel.text = msg;
        }

        private void ScrollToBottom()
        {
            if (_suggestionsScroll != null)
                StartCoroutine(ForceScrollToBottom());
        }

        private IEnumerator ForceScrollToBottom()
        {
            yield return new WaitForEndOfFrame();
            _suggestionsScroll.verticalNormalizedPosition = 0f;
        }
    }
}
