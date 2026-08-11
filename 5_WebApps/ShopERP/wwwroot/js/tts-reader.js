// W3-T5 + #114 r3: TTS reader using Web Speech API (browser native speechSynthesis)
// Used by ShopERP kitchen Detail.razor to read voice note text + dish names aloud.
// #114 r3 fix: Select vi-VN voice explicitly + slower rate for clarity.

window.ttsReader = {
    _voices: [],
    _viVoice: null,

    // Load voices (Chrome loads async, Firefox sync). Call on init + onvoiceschanged.
    loadVoices: function () {
        if (!('speechSynthesis' in window)) return;
        this._voices = window.speechSynthesis.getVoices();
        // Prefer vi-VN voice. Fallback to any voice containing "vi" or "Vietnamese".
        this._viVoice = this._voices.find(v => v.lang === 'vi-VN')
            || this._voices.find(v => v.lang && v.lang.startsWith('vi'))
            || this._voices.find(v => v.name && v.name.toLowerCase().includes('vietnam'))
            || null;
        if (this._viVoice) {
            console.log('TTS: selected vi-VN voice:', this._viVoice.name, this._viVoice.lang);
        } else {
            console.warn('TTS: no vi-VN voice found. Available:', this._voices.map(v => v.lang).join(', '));
        }
    },

    speak: function (text, lang) {
        if (!lang) lang = 'vi-VN';
        if ('speechSynthesis' in window && text) {
            // Cancel any ongoing speech
            window.speechSynthesis.cancel();
            const utterance = new SpeechSynthesisUtterance(text);
            utterance.lang = lang;
            // #114 r3: slower rate for clearer Vietnamese pronunciation
            utterance.rate = 0.9;
            utterance.pitch = 1.0;
            utterance.volume = 1.0;
            // Explicitly select vi-VN voice if available (improves pronunciation significantly)
            if (this._viVoice) {
                utterance.voice = this._viVoice;
            } else {
                // Try loading voices on-demand (Chrome first-call scenario)
                this.loadVoices();
                if (this._viVoice) utterance.voice = this._viVoice;
            }
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

// Chrome loads voices async — register listener + initial load
if ('speechSynthesis' in window) {
    window.speechSynthesis.onvoiceschanged = function () {
        window.ttsReader.loadVoices();
    };
    // Initial load (Firefox returns sync, Chrome may return empty first time)
    window.ttsReader.loadVoices();
}
