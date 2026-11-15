using System;
using System.Collections.Generic;

namespace Ekqo.Speech
{
    public sealed class Recognition
    {
        public bool Ok;
        public string Transcript;
        public List<string> NBest = new List<string>();
        public string Error;          // "no_mic", "permission_denied", "no_speech", "network"
    }

    /// <summary>
    /// Speech-to-text for one speaking turn (architecture 4.2). The web build uses the
    /// Azure Speech JavaScript SDK through Plugins/WebGL/EkqoSpeech.jslib; a native build
    /// can add an implementation with Microsoft's Speech SDK for Unity later.
    /// No audio is stored anywhere.
    /// </summary>
    public interface ISpeechService
    {
        bool IsAvailable { get; }
        /// <param name="token">10-minute token from POST /v1/speech/token.</param>
        void Listen(string token, string region, string locale, Action<Recognition> onResult);
        void Cancel();
    }
}
