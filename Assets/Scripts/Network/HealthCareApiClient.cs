using System;
using System.Collections;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace ARHealthCare.Network
{
    public class HealthCareApiClient : MonoBehaviour
    {
        [SerializeField] private ServerConfig _config;

        // Single shared instance — HttpClient is thread-safe and meant to be reused.
        private static readonly HttpClient _http = new HttpClient();

        private void Awake()
        {
            if (_config == null)
                _config = Resources.Load<ServerConfig>("ServerConfig");
            if (_config == null)
            {
                Debug.LogError("HealthCareApiClient: ServerConfig not assigned and not found at Resources/ServerConfig.");
                return;
            }
            _http.Timeout = TimeSpan.FromSeconds(_config.timeoutSeconds);
        }

        // Face Recognition

        public IEnumerator RecognizeFace(
            Texture2D frame,
            Action<FaceRecognitionResponse> onSuccess,
            Action<string> onFailure)
        {
            if (_config == null) { onFailure?.Invoke("ServerConfig missing"); yield break; }

            byte[] jpg = ImageConversion.EncodeToJPG(frame, 60);
            string b64 = Convert.ToBase64String(jpg);
            string body = JsonUtility.ToJson(new FaceRecognitionRequest { image_b64 = b64 });

            yield return PostJson(_config.BaseUrl + "/recognize-face", body,
                text => onSuccess?.Invoke(JsonUtility.FromJson<FaceRecognitionResponse>(text)),
                onFailure);
        }

        // Patient Record

        public IEnumerator GetPatient(
            string patientId,
            Action<PatientRecord> onSuccess,
            Action<string> onFailure)
        {
            if (_config == null) { onFailure?.Invoke("ServerConfig missing"); yield break; }

            yield return GetJson(_config.BaseUrl + "/patient/" + patientId,
                text => onSuccess?.Invoke(JsonUtility.FromJson<PatientRecord>(text)),
                onFailure);
        }

        // LLM Suggestion

        public IEnumerator GetSuggestion(
            PatientRecord patient,
            string specialty,
            string mode,
            string context,
            Action<string> onSuccess,
            Action<string> onFailure)
        {
            if (_config == null) { onFailure?.Invoke("ServerConfig missing"); yield break; }

            var reqData = new SuggestRequest
            {
                patient   = patient,
                specialty = specialty,
                mode      = mode,
                context   = context
            };
            string body = JsonUtility.ToJson(reqData);

            yield return PostJson(_config.BaseUrl + "/suggest", body,
                text => onSuccess?.Invoke(JsonUtility.FromJson<SuggestResponse>(text).suggestion),
                onFailure);
        }

        // Checklist

        public IEnumerator GetChecklist(
            string specialty,
            Action<ChecklistResponse> onSuccess,
            Action<string> onFailure)
        {
            if (_config == null) { onFailure?.Invoke("ServerConfig missing"); yield break; }

            yield return GetJson(_config.BaseUrl + "/checklist/" + specialty,
                text => onSuccess?.Invoke(JsonUtility.FromJson<ChecklistResponse>(text)),
                onFailure);
        }

        // Marker Bone

        public IEnumerator SaveMarkerBone(
            string patientId,
            string boneName,
            Action onSuccess,
            Action<string> onFailure)
        {
            if (_config == null) { onFailure?.Invoke("ServerConfig missing"); yield break; }

            string body = $"{{\"marker_bone\":\"{boneName}\"}}";
            yield return PatchJson(_config.BaseUrl + "/patient/" + patientId + "/marker-bone", body,
                _ => onSuccess?.Invoke(),
                onFailure);
        }

        // HTTP helpers — use System.Net.Http.HttpClient to avoid Unity 6's
        // UnityWebRequest "Insecure connection not allowed" restriction on HTTP.

        private IEnumerator GetJson(string url, Action<string> onSuccess, Action<string> onFailure)
        {
            var task = _http.GetAsync(url);
            yield return new WaitUntil(() => task.IsCompleted);

            if (task.IsFaulted)
            {
                onFailure?.Invoke($"GET {url} failed: {task.Exception?.GetBaseException().Message}");
                yield break;
            }

            var response = task.Result;
            if (!response.IsSuccessStatusCode)
            {
                onFailure?.Invoke($"GET {url} failed: HTTP {(int)response.StatusCode}");
                yield break;
            }

            var readTask = response.Content.ReadAsStringAsync();
            yield return new WaitUntil(() => readTask.IsCompleted);
            onSuccess?.Invoke(readTask.Result);
        }

        private IEnumerator PostJson(string url, string jsonBody, Action<string> onSuccess, Action<string> onFailure)
        {
            var content = new StringContent(jsonBody, Encoding.UTF8, "application/json");
            var task = _http.PostAsync(url, content);
            yield return new WaitUntil(() => task.IsCompleted);

            if (task.IsFaulted)
            {
                onFailure?.Invoke($"POST {url} failed: {task.Exception?.GetBaseException().Message}");
                yield break;
            }

            var response = task.Result;
            if (!response.IsSuccessStatusCode)
            {
                onFailure?.Invoke($"POST {url} failed: HTTP {(int)response.StatusCode}");
                yield break;
            }

            var readTask = response.Content.ReadAsStringAsync();
            yield return new WaitUntil(() => readTask.IsCompleted);
            onSuccess?.Invoke(readTask.Result);
        }

        private IEnumerator PatchJson(string url, string jsonBody, Action<string> onSuccess, Action<string> onFailure)
        {
            var request = new HttpRequestMessage(new HttpMethod("PATCH"), url)
            {
                Content = new StringContent(jsonBody, Encoding.UTF8, "application/json")
            };
            var task = _http.SendAsync(request);
            yield return new WaitUntil(() => task.IsCompleted);

            if (task.IsFaulted)
            {
                onFailure?.Invoke($"PATCH {url} failed: {task.Exception?.GetBaseException().Message}");
                yield break;
            }

            var response = task.Result;
            if (!response.IsSuccessStatusCode)
            {
                onFailure?.Invoke($"PATCH {url} failed: HTTP {(int)response.StatusCode}");
                yield break;
            }

            var readTask = response.Content.ReadAsStringAsync();
            yield return new WaitUntil(() => readTask.IsCompleted);
            onSuccess?.Invoke(readTask.Result);
        }
    }
}
