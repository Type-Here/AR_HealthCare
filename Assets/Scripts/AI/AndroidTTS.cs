using System.Collections;
using UnityEngine;

namespace ARHealthCare.AI
{
    public class AndroidTTS : MonoBehaviour
    {
        // Invoked only in Android builds; suppress CS0067 for Editor compilation
#pragma warning disable CS0067
        public event System.Action OnSpeechDone;
#pragma warning restore CS0067

#if UNITY_ANDROID && !UNITY_EDITOR
        private AndroidJavaObject _tts;
        private bool _ready;
        private int _speechGen; // incremented each Speak() call to cancel stale watch coroutines

        private void Start()
        {
            var activity = new AndroidJavaClass("com.unity3d.player.UnityPlayer")
                .GetStatic<AndroidJavaObject>("currentActivity");
            _tts = new AndroidJavaObject("android.speech.tts.TextToSpeech", activity, new InitListener(OnInit));
        }

        private void OnInit(int status)
        {
            if (status != 0) return;
            _ready = true;
            var locale = new AndroidJavaObject("java.util.Locale", "en", "US");
            _tts.Call<int>("setLanguage", locale);
        }

        public void Speak(string text)
        {
            if (!_ready || string.IsNullOrEmpty(text)) return;
            _speechGen++;
            _tts.Call<int>("speak", text, 0, null, "medbot_tts"); // QUEUE_FLUSH
            StartCoroutine(WatchSpeech(_speechGen));
        }

        public void Stop()
        {
            if (!_ready) return;
            _speechGen++; // invalidate any running watch coroutine
            _tts.Call<int>("stop");
        }

        public bool IsSpeaking() => _ready && _tts.Call<bool>("isSpeaking");

        private IEnumerator WatchSpeech(int gen)
        {
            yield return new WaitForSeconds(0.4f); // let Android start speaking
            while (gen == _speechGen && IsSpeaking())
                yield return new WaitForSeconds(0.25f);
            if (gen == _speechGen)
                OnSpeechDone?.Invoke();
        }

        private void OnDestroy()
        {
            _tts?.Call("shutdown");
        }

        private class InitListener : AndroidJavaProxy
        {
            private readonly System.Action<int> _onInit;

            public InitListener(System.Action<int> onInit)
                : base("android.speech.tts.TextToSpeech$OnInitListener")
            {
                _onInit = onInit;
            }

            public void onInit(int status) => _onInit?.Invoke(status);
        }
#else
        public void Speak(string text) { }
        public void Stop() { }
        public bool IsSpeaking() => false;
#endif
    }
}
