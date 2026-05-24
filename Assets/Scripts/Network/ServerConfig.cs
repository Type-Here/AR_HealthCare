using UnityEngine;

namespace ARHealthCare.Network
{
    [CreateAssetMenu(fileName = "ServerConfig", menuName = "ARHealthCare/Server Config")]
    public class ServerConfig : ScriptableObject
    {
        [Header("Connection")]
        [Tooltip("IP address of the companion laptop running the FastAPI server")]
        public string serverHost = "192.168.1.100"; /*TO BE CONFIGURED*/
        public int serverPort = 8000;
        [Tooltip("Request timeout in seconds")]
        public float timeoutSeconds = 15f;

        public string BaseUrl => $"http://{serverHost}:{serverPort}";
    }
}
