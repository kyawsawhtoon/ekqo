// Browser speech for Ekqo: one short recognition per speaking turn with Azure AI Speech.
// Audio streams from the microphone straight to Azure and is not stored.
// Requires the Azure Speech SDK for JavaScript (loaded in index.html as window.SpeechSDK).
(function () {
  var recognizer = null;

  function finish(cb, result) {
    if (recognizer) { recognizer.close(); recognizer = null; }
    cb(result);
  }

  window.EkqoSpeech = {
    isAvailable: function () {
      return !!(window.SpeechSDK && navigator.mediaDevices && navigator.mediaDevices.getUserMedia);
    },
    listen: function (token, region, locale, cb) {
      var sdk = window.SpeechSDK;
      try {
        var config = sdk.SpeechConfig.fromAuthorizationToken(token, region);
        config.speechRecognitionLanguage = locale;                       // "my-MM"
        config.outputFormat = sdk.OutputFormat.Detailed;                  // n-best alternatives
        config.setProperty(sdk.PropertyId.SpeechServiceConnection_EndSilenceTimeoutMs, "800");
        var audio = sdk.AudioConfig.fromDefaultMicrophoneInput();
        recognizer = new sdk.SpeechRecognizer(config, audio);
        recognizer.recognizeOnceAsync(function (r) {
          if (r.reason === sdk.ResultReason.RecognizedSpeech) {
            var nbest = [];
            try {
              var detail = JSON.parse(r.json);
              nbest = (detail.NBest || []).slice(1, 4).map(function (n) { return n.Display || n.Lexical; });
            } catch (e) { /* keep empty */ }
            finish(cb, { ok: true, transcript: r.text, nbest: nbest });
          } else if (r.reason === sdk.ResultReason.NoMatch) {
            finish(cb, { ok: false, error: "no_speech" });
          } else {
            finish(cb, { ok: false, error: "network" });
          }
        }, function () {
          finish(cb, { ok: false, error: "permission_denied" });
        });
      } catch (e) {
        finish(cb, { ok: false, error: "no_mic" });
      }
    },
    cancel: function () {
      if (recognizer) { recognizer.close(); recognizer = null; }
    },
  };
})();
