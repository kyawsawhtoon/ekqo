// Unity <-> browser bridge for voice (browser text-to-speech) and the start page.
// Used only when no pre-rendered clip exists for a line (see Ekqo.Speech.VoiceService).
mergeInto(LibraryManager.library, {
  EkqoVoice_HasVoice: function (localePtr) {
    if (!window.speechSynthesis) return 0;
    var lang = UTF8ToString(localePtr).toLowerCase().split("-")[0];
    var voices = window.speechSynthesis.getVoices();
    for (var i = 0; i < voices.length; i++) {
      if ((voices[i].lang || "").toLowerCase().replace("_", "-").split("-")[0] === lang) return 1;
    }
    return 0;
  },
  EkqoVoice_Speak: function (textPtr, localePtr, rate, pitch) {
    if (!window.speechSynthesis) return;
    var locale = UTF8ToString(localePtr);
    var lang = locale.toLowerCase().split("-")[0];
    var u = new SpeechSynthesisUtterance(UTF8ToString(textPtr));
    u.lang = locale;
    u.rate = rate;
    u.pitch = pitch;
    var voices = window.speechSynthesis.getVoices();
    for (var i = 0; i < voices.length; i++) {
      if ((voices[i].lang || "").toLowerCase().replace("_", "-").split("-")[0] === lang) { u.voice = voices[i]; break; }
    }
    window.speechSynthesis.cancel();
    window.speechSynthesis.speak(u);
  },
  EkqoVoice_IsSpeaking: function () {
    return (window.speechSynthesis && window.speechSynthesis.speaking) ? 1 : 0;
  },
  EkqoVoice_Stop: function () {
    if (window.speechSynthesis) window.speechSynthesis.cancel();
  },
  // The child's nickname, typed on the start page (WebGLTemplates/EkqoQuest). Never leaves the device.
  EkqoPage_GetPlayerName: function () {
    var name = "";
    try { name = window.localStorage.getItem("ekqo.playerName") || ""; } catch (e) { }
    var size = lengthBytesUTF8(name) + 1;
    var buffer = _malloc(size);
    stringToUTF8(name, buffer, size);
    return buffer;
  },
});
