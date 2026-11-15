// Unity <-> browser bridge for speech. The real work is in window.EkqoSpeech,
// defined by the WebGL template (Assets/WebGLTemplates/Ekqo/ekqo-speech.js).
mergeInto(LibraryManager.library, {
  EkqoSpeech_IsAvailable: function () {
    return (window.EkqoSpeech && window.EkqoSpeech.isAvailable()) ? 1 : 0;
  },
  EkqoSpeech_Listen: function (goPtr, tokenPtr, regionPtr, localePtr) {
    var go = UTF8ToString(goPtr);
    window.EkqoSpeech.listen(UTF8ToString(tokenPtr), UTF8ToString(regionPtr), UTF8ToString(localePtr), function (result) {
      window.unityInstance.SendMessage(go, "OnRecognition", JSON.stringify(result));
    });
  },
  EkqoSpeech_Cancel: function () {
    if (window.EkqoSpeech) window.EkqoSpeech.cancel();
  },
});
