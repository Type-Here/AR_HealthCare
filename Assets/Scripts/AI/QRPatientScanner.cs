// Requires ZXing.Net DLL in Assets/Plugins/
// Download from: https://www.nuget.org/packages/ZXing.Net
// Extract zxing.unity.dll from the NuGet package and place in Assets/Plugins/
// Or install via NuGetForUnity: https://github.com/GlitchEnzo/NuGetForUnity

using System.Collections;
using UnityEngine;
using TMPro;
using ARHealthCare.Input;
using ARHealthCare.Network;
using ZXing;

namespace ARHealthCare.AI
{
    public class QRPatientScanner : MonoBehaviour
    {
        [Header("Dependencies")]
        [SerializeField] private MLCameraBridge _mlCamera;
        [SerializeField] private HealthCareApiClient _apiClient;
        [SerializeField] private MedBotPanelController _medBotPanel;
        [SerializeField] private PatientProblemsUI _patientInfoUI;

        [Header("UI")]
        [SerializeField] private TextMeshProUGUI _statusLabel;

        private BarcodeReader _reader;

        private void Awake()
        {
            _reader = new BarcodeReader
            {
                AutoRotate = true,
                Options = { TryHarder = true }
            };
        }

        /// <summary>Call from the "Scan QR" button onClick event.</summary>
        public void ScanForPatient()
        {
            if (_mlCamera == null || !_mlCamera.IsReady)
            {
                ShowStatus("Camera not ready");
                return;
            }

            Texture2D frame = _mlCamera.CaptureCurrentFrameAsTexture2D();
            if (frame == null)
            {
                ShowStatus("No camera frame available");
                return;
            }

            var pixels = frame.GetPixels32();
            var result = _reader.Decode(pixels, frame.width, frame.height);
            Destroy(frame); // free the copy

            if (result == null)
            {
                ShowStatus("No QR code found — hold steady");
                return;
            }

            string patientId = result.Text.Trim();
            ShowStatus($"QR detected: {patientId}");
            StartCoroutine(LoadPatient(patientId));
        }

        private IEnumerator LoadPatient(string patientId)
        {
            ShowStatus("Loading patient record…");
            yield return _apiClient.GetPatient(
                patientId,
                onSuccess: record =>
                {
                    _patientInfoUI?.PopulateFromRecord(record);
                    _medBotPanel?.SetPatient(record);
                    ShowStatus($"Loaded: {record.display_name}");

                    // Also prefetch checklist for this patient's specialty
                    if (!string.IsNullOrEmpty(record.specialty))
                        StartCoroutine(_apiClient.GetChecklist(
                            record.specialty,
                            onSuccess: _medBotPanel != null ? _medBotPanel.PopulateChecklist : (System.Action<ChecklistResponse>)null,
                            onFailure: _ => { }));
                },
                onFailure: err =>
                {
                    ShowStatus($"Patient not found: {patientId}");
                    Debug.LogWarning($"QRPatientScanner: {err}");
                });
        }

        private void ShowStatus(string msg)
        {
            if (_statusLabel != null) _statusLabel.text = msg;
            Debug.Log($"QRPatientScanner: {msg}");
        }
    }
}
