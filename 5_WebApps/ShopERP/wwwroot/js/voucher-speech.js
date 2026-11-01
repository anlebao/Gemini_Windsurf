// Task PG — Smart Entry AI: mic input qua Web Speech API (vi-VN).
// Browser không hỗ trợ → supported() = false → nút mic ẩn, text input vẫn dùng được.
window.vananSpeech = {
  supported: function () {
    return typeof window !== 'undefined' &&
      ('SpeechRecognition' in window || 'webkitSpeechRecognition' in window);
  },
  listen: function (dotnetRef) {
    var SR = window.SpeechRecognition || window.webkitSpeechRecognition;
    if (!SR) { dotnetRef.invokeMethodAsync('OnSpeechError', 'unsupported'); return; }
    try {
      var rec = new SR();
      rec.lang = 'vi-VN';
      rec.interimResults = false;
      rec.maxAlternatives = 1;
      rec.onresult = function (e) {
        dotnetRef.invokeMethodAsync('OnSpeechResult', e.results[0][0].transcript || '');
      };
      rec.onerror = function (e) {
        dotnetRef.invokeMethodAsync('OnSpeechError', e.error || 'error');
      };
      rec.onend = function () {
        dotnetRef.invokeMethodAsync('OnSpeechEnd');
      };
      rec.start();
    } catch (err) {
      dotnetRef.invokeMethodAsync('OnSpeechError', String(err));
    }
  }
};
