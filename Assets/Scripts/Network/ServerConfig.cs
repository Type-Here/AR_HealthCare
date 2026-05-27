using UnityEngine;

namespace ARHealthCare.Network
{
    [CreateAssetMenu(fileName = "ServerConfig", menuName = "ARHealthCare/Server Config")]
    public class ServerConfig : ScriptableObject
    {
        [Header("Connection")]
        [Tooltip("IP address of the companion laptop running the FastAPI server")]
        public string serverHost = "192.168.4.86"; // MacBook M3 WiFi IP — update if network changes
        public int serverPort = 8000;
        [Tooltip("Request timeout in seconds")]
        public float timeoutSeconds = 15f;

        public string BaseUrl => $"http://{serverHost}:{serverPort}";
    }
}
