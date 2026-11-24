using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Networking;

namespace Ekqo.Speech
{
    /// <summary>
    /// Plays a line out loud (architecture S7). In order:
    /// 1. a pre-rendered clip listed in StreamingAssets/voice/&lt;edition&gt;/voice.json. Clips are
    ///    named by voice and a hash of the text ("ekqo/1a2b3c4d.mp3"), so any line, word or
    ///    answer the content has can be pre-rendered by tools/pipeline (pnpm voice:render),
    /// 2. the browser's own text-to-speech, if it has a voice for the locale,
    /// 3. nothing: the line is still shown, and Ekqo moves his mouth.
    /// English hints use the browser's English voice (SayEnglish).
    /// </summary>
    public sealed class VoiceService : MonoBehaviour
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] private static extern int EkqoVoice_HasVoice(string locale);
        [DllImport("__Internal")] private static extern void EkqoVoice_Speak(string text, string locale, float rate, float pitch);
        [DllImport("__Internal")] private static extern int EkqoVoice_IsSpeaking();
        [DllImport("__Internal")] private static extern void EkqoVoice_Stop();
#endif

        [System.Serializable] private sealed class Manifest { public string[] clips; }

        private readonly HashSet<string> _clips = new HashSet<string>();
        private readonly Dictionary<string, AudioClip> _loaded = new Dictionary<string, AudioClip>();
        private AudioSource _source;
        private string _edition;
        private string _locale;
        private string _cacheBust = "";

        /// <summary>True while a line is playing.</summary>
        public bool Speaking { get; private set; }

        /// <summary>"clips", "browser" or "none", for the corner label.</summary>
        public string Mode => _clips.Count > 0 ? "clips" : HasBrowserVoice() ? "browser" : "none";

        public IEnumerator Init(string edition, string locale, string cacheBust)
        {
            _edition = edition;
            _locale = locale;
            _cacheBust = string.IsNullOrEmpty(cacheBust) ? "" : "?v=" + cacheBust;
            _source = gameObject.AddComponent<AudioSource>();
            _source.playOnAwake = false;
            using var req = UnityWebRequest.Get($"{Application.streamingAssetsPath}/voice/{edition}/voice.json{_cacheBust}");
            yield return req.SendWebRequest();
            if (req.result == UnityWebRequest.Result.Success)
            {
                var m = JsonUtility.FromJson<Manifest>(req.downloadHandler.text);
                if (m?.clips != null) foreach (var c in m.clips) _clips.Add(c);
            }
            Debug.Log($"[Voice] {_clips.Count} clips, browser voice: {HasBrowserVoice()}");
        }

        /// <summary>
        /// Says one line. key names the clip, like "q01/who" or "word/greet.hello".
        /// Waits until it ends, or about as long as reading it would take when there is no voice.
        /// </summary>
        public IEnumerator Say(string key, string text, string speaker)
        {
            Stop();
            Speaking = true;
            // Clips are found by voice and text, so the key from the caller is only a fallback name.
            var hashed = string.IsNullOrEmpty(text) ? null : ClipKey(speaker, text);
            if (hashed != null && _clips.Contains(hashed)) key = hashed;
            try
            {
                if (key != null && _clips.Contains(key))
                {
                    if (!_loaded.TryGetValue(key, out var clip))
                    {
                        using var req = UnityWebRequestMultimedia.GetAudioClip($"{Application.streamingAssetsPath}/voice/{_edition}/{key}.mp3{_cacheBust}", AudioType.MPEG);
                        yield return req.SendWebRequest();
                        if (req.result == UnityWebRequest.Result.Success) _loaded[key] = clip = DownloadHandlerAudioClip.GetContent(req);
                    }
                    if (clip != null)
                    {
                        _source.clip = clip;
                        _source.Play();
                        while (_source.isPlaying) yield return null;
                        yield break;
                    }
                }

                if (string.IsNullOrEmpty(text)) yield break;
#if UNITY_WEBGL && !UNITY_EDITOR
                if (HasBrowserVoice())
                {
                    var ekqo = speaker == "ekqo";
                    EkqoVoice_Speak(text, _locale, ekqo ? 0.85f : 0.9f, ekqo ? 1.4f : 1f);
                    var started = Time.realtimeSinceStartup;
                    // Wait for it to start (up to a second), then for it to end (up to 10 s).
                    while (EkqoVoice_IsSpeaking() == 0 && Time.realtimeSinceStartup - started < 1f) yield return null;
                    while (EkqoVoice_IsSpeaking() == 1 && Time.realtimeSinceStartup - started < 10f) yield return null;
                    yield break;
                }
#endif
                // No voice: give the mouth animation about as long as the line would take.
                yield return new WaitForSeconds(Mathf.Clamp(text.Length * 0.08f, 0.6f, 2.5f));
            }
            finally
            {
                Speaking = false;
            }
        }

        /// <summary>Reads an English hint with the browser's English voice. Does nothing without one.</summary>
        public IEnumerator SayEnglish(string text)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            if (string.IsNullOrEmpty(text) || EkqoVoice_HasVoice("en-US") != 1) yield break;
            Stop();
            Speaking = true;
            EkqoVoice_Speak(text, "en-US", 0.95f, 1f);
            var started = Time.realtimeSinceStartup;
            while (EkqoVoice_IsSpeaking() == 0 && Time.realtimeSinceStartup - started < 1f) yield return null;
            while (EkqoVoice_IsSpeaking() == 1 && Time.realtimeSinceStartup - started < 10f) yield return null;
            Speaking = false;
#else
            yield break;
#endif
        }

        /// <summary>
        /// The clip name for a line: the voice (the speaking character; the child's answers and
        /// teaching use Ekqo's voice) and an FNV-1a hash of the trimmed text. tools/pipeline uses
        /// the same rule (voiceKey in voice.ts).
        /// </summary>
        public static string ClipKey(string speaker, string text)
        {
            var voice = string.IsNullOrEmpty(speaker) || speaker == "child" ? "ekqo" : speaker;
            unchecked
            {
                var h = 2166136261u;
                foreach (var ch in text.Trim()) { h ^= ch; h *= 16777619u; }
                return $"{voice}/{h:x8}";
            }
        }

        public void Stop()
        {
            if (_source != null && _source.isPlaying) _source.Stop();
#if UNITY_WEBGL && !UNITY_EDITOR
            EkqoVoice_Stop();
#endif
            Speaking = false;
        }

        private bool HasBrowserVoice()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return !string.IsNullOrEmpty(_locale) && EkqoVoice_HasVoice(_locale) == 1;
#else
            return false;
#endif
        }
    }
}
