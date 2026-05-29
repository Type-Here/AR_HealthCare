using System.Collections;
using UnityEngine;
using TMPro;
using ARHealthCare.Input;
using ARHealthCare.Network;

namespace ARHealthCare.AI
{
    public class FacePatientScanner : MonoBehaviour
    {
        [Header("Dependencies")]
        [SerializeField] private MLCameraBridge _mlCamera;
        [SerializeField] private HealthCareApiClient _apiClient;
        [SerializeField] private MedBotPanelController _medBotPanel;
        [SerializeField] private PatientProblemsUI _patientInfoUI;

        [Header("UI")]
        [SerializeField] private TextMeshProUGUI _statusLabel;

        [Header("Scan Settings")]
        [SerializeField] private float _scanTimeoutSeconds = 15f;
        [SerializeField] private float _retryIntervalSeconds = 1f;

        private Coroutine _scanCoroutine;

        /// <summary>Call from the "Scan Face" button onClick event. Starts/stops the scan loop.</summary>
        public void ScanForPatient()
        {
            if (_scanCoroutine != null)
            {
                StopCoroutine(_scanCoroutine);
                _scanCoroutine = null;
                ShowStatus("Scan cancelled");
                return;
            }

            if (_mlCamera == null || !_mlCamera.IsReady)
            {
                ShowStatus("Camera not ready");
                return;
            }

            _scanCoroutine = StartCoroutine(ScanLoop());
        }

        private IEnumerator ScanLoop()
        {
            float startTime = Time.time;

            while (Time.time - startTime < _scanTimeoutSeconds)
            {
                int remaining = Mathf.Max(0, Mathf.CeilToInt(_scanTimeoutSeconds - (Time.time - startTime)));
                ShowStatus($"Scanning… {remaining}s");

                Texture2D frame = _mlCamera.CaptureCurrentFrameAsTexture2D();
                if (frame == null)
                {
                    yield return new WaitForSeconds(0.5f);
                    continue;
                }

                string foundId = null;
                yield return _apiClient.RecognizeFace(
                    frame,
                    onSuccess: r => { if (r.patient_id != null) foundId = r.patient_id; },
                    onFailure: err => Debug.LogWarning($"FacePatientScanner: {err}"));

                Destroy(frame);

                if (foundId != null)
                {
                    _scanCoroutine = null;
                    ShowStatus("Face recognized!");
                    StartCoroutine(LoadPatient(foundId));
                    yield break;
                }

                yield return new WaitForSeconds(_retryIntervalSeconds);
            }

            _scanCoroutine = null;
            ShowStatus("Face not recognized — press button to try again");
        }

        private IEnumerator LoadPatient(string patientId)
        {
            ShowStatus("Loading patient record…");
            yield return _apiClient.GetPatient(
                patientId,
                onSuccess: record =>
                {
                    if (_patientInfoUI != null) _patientInfoUI.PopulateFromRecord(record);
                    if (_medBotPanel != null) _medBotPanel.SetPatient(record);
                    ShowStatus($"Loaded: {record.display_name}");

                    if (!string.IsNullOrEmpty(record.specialty))
                        StartCoroutine(_apiClient.GetChecklist(
                            record.specialty,
                            onSuccess: _medBotPanel != null ? _medBotPanel.PopulateChecklist : (System.Action<ChecklistResponse>)null,
                            onFailure: _ => { }));
                },
                onFailure: err =>
                {
                    ShowStatus($"Patient not found: {patientId}");
                    Debug.LogWarning($"FacePatientScanner: {err}");
                });
        }

        private void ClearStatus()
        {
            if (_statusLabel != null) _statusLabel.text = "";
        }

        private void ShowStatus(string msg)
        {
            if (_statusLabel != null) _statusLabel.text = msg;
            Debug.Log($"FacePatientScanner: {msg}");

            CancelInvoke(nameof(ClearStatus));
            Invoke(nameof(ClearStatus), 5f);
        }
    }
}
