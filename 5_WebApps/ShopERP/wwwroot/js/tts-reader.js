// W3-T5: TTS reader using Web Speech API (browser native speechSynthesis)
// Used by ShopERP kitchen Detail.razor to read voice note text when chef clicks "Đọc ghi chú"
window.ttsReader = {
    speak: function (text, lang) {
        if (!lang) lang = 'vi-VN';
        if ('speechSynthesis' in window && text) {
            // Cancel any ongoing speech
            window.speechSynthesis.cancel();
            const utterance = new SpeechSynthesisUtterance(text);
            utterance.lang = lang;
            utterance.rate = 1.0;
            utterance.pitch = 1.0;
            utterance.volume = 1.0;
            window.speechSynthesis.speak(utterance);
            console.log('TTS speaking:', text);
            return true;
        }
        console.warn('speechSynthesis not supported or empty text');
        return false;
    },
    cancel: function () {
        if ('speechSynthesis' in window) {
            window.speechSynthesis.cancel();
            console.log('TTS cancelled');
        }
    },
    isSupported: function () {
        return 'speechSynthesis' in window;
    }
};
