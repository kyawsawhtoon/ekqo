using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

namespace Ekqo.Speech
{
    /// <summary>
    /// Bridges to the browser: Unity's Microphone API is not available in web builds,
    /// so recognition runs in JavaScript (EkqoSpeech.jslib + the WebGL template) and the
    /// result comes back through SendMessage to this component.
    /// </summary>
    public sealed class WebSpeechService : MonoBehaviour, ISpeechService
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] private static extern int EkqoSpeech_IsAvailable();
        [DllImport("__Internal")] private static extern void EkqoSpeech_Listen(string gameObject, string token, string region, string locale);
        [DllImport("__Internal")] private static extern void EkqoSpeech_Cancel();
#endif

        [Serializable]
        private sealed class JsResult { public bool ok; public string transcript; public string[] nbest; public string error; }

        private Action<Recognition> _pending;

        public bool IsAvailable
        {
            get
            {
#if UNITY_WEBGL && !UNITY_EDITOR
                return EkqoSpeech_IsAvailable() == 1;
#else
                return false; // in the editor, use tap answers or the debug panel
#endif
            }
        }

        public void Listen(string token, string region, string locale, Action<Recognition> onResult)
        {
            _pending = onResult;
#if UNITY_WEBGL && !UNITY_EDITOR
            EkqoSpeech_Listen(gameObject.name, token, region, locale);
#else
            OnRecognition("{\"ok\":false,\"error\":\"no_mic\"}");
#endif
        }

        public void Cancel()
        {
            _pending = null;
#if UNITY_WEBGL && !UNITY_EDITOR
            EkqoSpeech_Cancel();
#endif
        }

        // Called from JavaScript via SendMessage(gameObject, "OnRecognition", json).
        public void OnRecognition(string json)
        {
            var r = JsonUtility.FromJson<JsResult>(json);
            var callback = _pending;
            _pending = null;
            callback?.Invoke(new Recognition
            {
                Ok = r.ok,
                Transcript = r.transcript ?? "",
                NBest = r.nbest != null ? new List<string>(r.nbest) : new List<string>(),
                Error = r.error,
            });
        }
    }
}
