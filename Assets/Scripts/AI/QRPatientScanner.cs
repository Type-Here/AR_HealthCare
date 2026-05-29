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
using ZXing.Common;

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

        [Header("Scan Settings")]
        [SerializeField] private float _scanTimeoutSeconds = 10f;
        [SerializeField] private float _scanIntervalSeconds = 0.4f;

        private MultiFormatReader _qrReader;
        private Coroutine _scanCoroutine;

        private void Awake()
        {
            _qrReader = new MultiFormatReader();
        }

        /// <summary>Call from the "Scan QR" button onClick event. Starts/stops the scan loop.</summary>
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
            float elapsed = 0f;
            int remaining = Mathf.CeilToInt(_scanTimeoutSeconds);
            ShowStatus($"Scanning… {remaining}s");

            while (elapsed < _scanTimeoutSeconds)
            {
                string patientId = TryScanFrame();
                if (patientId != null)
                {
                    _scanCoroutine = null;
                    ShowStatus($"QR detected: {patientId}");
                    StartCoroutine(LoadPatient(patientId));
                    yield break;
                }

                yield return new WaitForSeconds(_scanIntervalSeconds);
                elapsed += _scanIntervalSeconds;
                remaining = Mathf.Max(0, Mathf.CeilToInt(_scanTimeoutSeconds - elapsed));
                ShowStatus($"Scanning… {remaining}s");
            }

            _scanCoroutine = null;
            ShowStatus("No QR found — press button to try again");
        }

        // Returns the decoded string, or null if nothing was found this frame.
        private string TryScanFrame()
        {
            if (_mlCamera == null || !_mlCamera.IsReady) return null;

            Texture2D frame = _mlCamera.CaptureCurrentFrameAsTexture2D();
            if (frame == null) return null;

            var pixels = frame.GetPixels32();
            int w = frame.width, h = frame.height;
            Destroy(frame);

            // Convert RGBA → grayscale luminance. Using BitmapFormat.Gray8 avoids
            // the 3-arg constructor's BGR auto-detection that causes decode failures.
            byte[] luminance = new byte[pixels.Length];
            for (int i = 0; i < pixels.Length; i++)
            {
                var p = pixels[i];
                luminance[i] = (byte)(p.r * 0.299f + p.g * 0.587f + p.b * 0.114f);
            }

            try
            {
                var source = new RGBLuminanceSource(luminance, w, h, RGBLuminanceSource.BitmapFormat.Gray8);
                var bitmap = new BinaryBitmap(new HybridBinarizer(source));
                Result result = _qrReader.decode(bitmap);
                return result?.Text?.Trim();
            }
            catch
            {
                return null;
            }
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
                    Debug.LogWarning($"QRPatientScanner: {err}");
                });
        }

        private void ClearStatus()
        {
          if (_statusLabel != null) _statusLabel.text = "";
        }


        private void ShowStatus(string msg)
        {
            if (_statusLabel != null) _statusLabel.text = msg;
            Debug.Log($"QRPatientScanner: {msg}");

            CancelInvoke(nameof(ClearStatus));
            Invoke(nameof(ClearStatus), 5f);
        }
    }
}
