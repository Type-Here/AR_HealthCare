using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using ARHealthCare.Network;

namespace ARHealthCare.AI
{
    public class MedBotPanelController : MonoBehaviour
    {
        [Header("Dependencies")]
        [SerializeField] private HealthCareApiClient _apiClient;
        [SerializeField] private MedBotController _medBotController;
        [SerializeField] private PatientIssueMarker _issueMarker;
        [SerializeField] private AndroidTTS _tts;

        [Header("Mode")]
        [SerializeField] private TextMeshProUGUI _modeButtonLabel;

        [Header("Panel")]
        [SerializeField] private ScrollRect _scroll;
        [SerializeField] private TextMeshProUGUI _textDisplay;
        [SerializeField] private Transform _checklistContainer;
        [SerializeField] private GameObject _checklistItemPrefab;
        [SerializeField] private GameObject _askButton;

        [Header("TTS Button")]
        [SerializeField] private Image _ttsButtonIcon;
        [SerializeField] private Sprite _stopSprite;
        [SerializeField] private Sprite _replaySprite;

        [Header("Status")]
        [SerializeField] private TextMeshProUGUI _statusLabel;

        [Header("Notification Badge")]
        [Tooltip("Badge GameObject on the Holo toggle button in UIButtonsCanvas - shown when new content arrives while panel is closed")]
        [SerializeField] private GameObject _notificationBadge;
        [SerializeField] private TextMeshProUGUI _notificationCount;

        private readonly Dictionary<int, string> _tabContent = new()
        {
            { 0, "" },
            { 1, "" }
        };

        private PatientRecord _currentPatient;
        private string _currentMode = "student";
        private bool _panelOpen;
        private int _pendingCount;
        private int _activeTab;
        private bool _ttsSpeaking;

        private void Start()
        {
            OnTabClicked(0);
            UpdateModeLabel();
            ClearNotifications();

            if (_tts != null)
                _tts.OnSpeechDone += () => SetTtsIcon(false);
        }


        // Public API

        public void SetPatient(PatientRecord record)
        {
            _currentPatient = record;
            PopulateRecords();
            _issueMarker?.SetPatient(record);
            ShowStatus($"Patient: {record.display_name}");
        }

        public void PopulateChecklist(ChecklistResponse response)
        {
            if (_checklistContainer == null || _checklistItemPrefab == null) return;

            foreach (Transform child in _checklistContainer)
                Destroy(child.gameObject);

            if (response?.items == null) return;

            foreach (string item in response.items)
            {
                var go = Instantiate(_checklistItemPrefab, _checklistContainer);
                var toggle = go.GetComponentInChildren<Toggle>();
                var label  = go.GetComponentInChildren<TextMeshProUGUI>();
                if (toggle != null) toggle.isOn = false;
                if (label  != null) label.text  = item;
            }

            AddNotification();
        }


        // Button callbacks (wire in Inspector)

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

        public void OnTtsButtonClicked()
        {
            if (_ttsSpeaking)
            {
                if (_tts != null) _tts.Stop();
                SetTtsIcon(false);
            }
            else if (_tabContent.TryGetValue(0, out var text) && !string.IsNullOrEmpty(text))
            {
                TtsSpeak(text);
            }
        }

        public void OnTabClicked(int tabIndex)
        {
            _activeTab = tabIndex;
            bool isChecklist = tabIndex == 2;

            if (_textDisplay != null)
            {
                _textDisplay.gameObject.SetActive(!isChecklist);
                if (!isChecklist)
                    _textDisplay.text = _tabContent.TryGetValue(tabIndex, out var text) ? text : "";
            }

            if (_checklistContainer != null)
                _checklistContainer.gameObject.SetActive(isChecklist);

            if (_askButton != null)
                _askButton.SetActive(tabIndex == 0);
        }


        // Private

        private void TtsSpeak(string text)
        {
            if (_tts != null) _tts.Speak(text);
            SetTtsIcon(true);
        }

        private void SetTtsIcon(bool speaking)
        {
            _ttsSpeaking = speaking;
            if (_ttsButtonIcon != null)
                _ttsButtonIcon.sprite = speaking ? _stopSprite : _replaySprite;
        }

        private IEnumerator FetchSuggestion()
        {
            ShowStatus("Thinking…");
            _tabContent[0] = "";
            if (_activeTab == 0 && _textDisplay != null) _textDisplay.text = "";

            yield return _apiClient.GetSuggestion(
                _currentPatient,
                _currentPatient.specialty ?? "general",
                _currentMode,
                "",
                onSuccess: text =>
                {
                    _tabContent[0] = text;
                    if (_activeTab == 0 && _textDisplay != null)
                    {
                        _textDisplay.text = text;
                        ScrollToBottom();
                    }
                    ShowStatus("Ready");
                    AddNotification();
                    TtsSpeak(text);
                },
                onFailure: err =>
                {
                    _tabContent[0] = $"[Error: {err}]";
                    if (_activeTab == 0 && _textDisplay != null)
                        _textDisplay.text = _tabContent[0];
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
            if (_currentPatient == null) return;

            _tabContent[1] =
                $"<b>{_currentPatient.display_name}</b>  |  {_currentPatient.age} anni\n\n"
                + $"<b>Diagnosi:</b> {_currentPatient.diagnosis}\n\n"
                + $"<b>Intervento:</b> {_currentPatient.planned_procedure}\n"
                + $"<b>Data:</b> {_currentPatient.procedure_date}\n\n"
                + $"<b>Trattamento:</b> {_currentPatient.current_treatment}\n\n"
                + $"<b>Note:</b> {_currentPatient.notes}";

            if (_activeTab == 1 && _textDisplay != null)
                _textDisplay.text = _tabContent[1];
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
            if (_scroll != null)
                StartCoroutine(ForceScrollToBottom());
        }

        private IEnumerator ForceScrollToBottom()
        {
            yield return new WaitForEndOfFrame();
            _scroll.verticalNormalizedPosition = 0f;
        }
    }
}