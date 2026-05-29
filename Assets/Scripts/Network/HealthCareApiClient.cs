using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

namespace ARHealthCare.Network
{
    public class HealthCareApiClient : MonoBehaviour
    {
        private ServerConfig _config;

        private void Awake()
        {
            _config = Resources.Load<ServerConfig>("ServerConfig");
            if (_config == null)
                Debug.LogError("HealthCareApiClient: ServerConfig asset not found at Resources/ServerConfig. Create it via Assets > Create > ARHealthCare > Server Config.");
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

            var req = new SuggestRequest
            {
                patient   = patient,
                specialty = specialty,
                mode      = mode,
                context   = context
            };
            string body = JsonUtility.ToJson(req);

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


        // HTTP helpers

        private IEnumerator GetJson(string url, Action<string> onSuccess, Action<string> onFailure)
        {
            using var req = UnityWebRequest.Get(url);
            req.timeout = Mathf.RoundToInt(_config.timeoutSeconds);
            yield return req.SendWebRequest();

            if (req.result == UnityWebRequest.Result.Success)
                onSuccess?.Invoke(req.downloadHandler.text);
            else
                onFailure?.Invoke($"GET {url} failed: {req.error}");
        }

        private IEnumerator PostJson(string url, string jsonBody, Action<string> onSuccess, Action<string> onFailure)
        {
            using var req = new UnityWebRequest(url, "POST");
            byte[] bodyBytes = System.Text.Encoding.UTF8.GetBytes(jsonBody);
            req.uploadHandler   = new UploadHandlerRaw(bodyBytes);
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            req.timeout = Mathf.RoundToInt(_config.timeoutSeconds);
            yield return req.SendWebRequest();

            if (req.result == UnityWebRequest.Result.Success)
                onSuccess?.Invoke(req.downloadHandler.text);
            else
                onFailure?.Invoke($"POST {url} failed: {req.error}");
        }

        private IEnumerator PatchJson(string url, string jsonBody, Action<string> onSuccess, Action<string> onFailure)
        {
            using var req = new UnityWebRequest(url, "PATCH");
            byte[] bodyBytes = System.Text.Encoding.UTF8.GetBytes(jsonBody);
            req.uploadHandler   = new UploadHandlerRaw(bodyBytes);
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            req.timeout = Mathf.RoundToInt(_config.timeoutSeconds);
            yield return req.SendWebRequest();

            if (req.result == UnityWebRequest.Result.Success)
                onSuccess?.Invoke(req.downloadHandler.text);
            else
                onFailure?.Invoke($"PATCH {url} failed: {req.error}");
        }
    }
}