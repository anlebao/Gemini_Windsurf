// W3d (2026-11-23): shared text-to-speech for AI chat surfaces (AiChatWidget).
// Browser SpeechSynthesis API, vi-VN — ported from ShopERP tts-reader.js pattern.
// Plain script (not module) so any host (WASM/Server/SSR) can include it via
// <script src="/_content/VanAn.UI.Platform/js/voice-tts.js">.
//
// Blazor contract: none — fire-and-forget speak/stop; component tracks state.
// speak() cancels any ongoing utterance first → never two voices overlap.
// Text is sanitized before speak: markdown stripped, whitespace collapsed,
// capped at MAX_CHARS with "…" (risk: TTS đọc ký tự lạ / câu quá dài).

window.vananVoiceTts = (function () {
    const MAX_CHARS = 600;
    const LANG = 'vi-VN';

    let voices = [];
    let viVoice = null;

    function loadVoices() {
        if (!isSupported()) return;
        voices = window.speechSynthesis.getVoices() || [];
        viVoice = voices.find(v => v.lang === 'vi-VN')
            || voices.find(v => v.lang && v.lang.indexOf('vi') === 0)
            || voices.find(v => v.name && v.name.toLowerCase().indexOf('vietnam') >= 0)
            || null;
    }

    function isSupported() {
        return !!('speechSynthesis' in window);
    }

    // Strip markdown + control chars so TTS reads natural prose only.
    function sanitize(text) {
        if (!text) return '';
        var t = String(text)
            .replace(/```[\s\S]*?```/g, ' ')          // code fences
            .replace(/`([^`]*)`/g, '$1')             // inline code
            .replace(/!\[[^\]]*\]\([^)]*\)/g, ' ')   // images
            .replace(/\[([^\]]*)\]\([^)]*\)/g, '$1') // links → text
            .replace(/[#>*_~|]/g, ' ')               // md punctuation
            .replace(/[ \t]+/g, ' ')
            .replace(/\s*\n\s*/g, '. ')             // newlines → sentence pause
            .trim();
        if (t.length > MAX_CHARS) t = t.slice(0, MAX_CHARS).trimEnd() + '…';
        return t;
    }

    function speak(text) {
        var clean = sanitize(text);
        if (!clean || !isSupported()) return false;
        try {
            window.speechSynthesis.cancel(); // never overlap utterances
            var u = new SpeechSynthesisUtterance(clean);
            u.lang = LANG;
            u.rate = 0.95;
            u.pitch = 1.0;
            u.volume = 1.0;
            if (!viVoice) loadVoices(); // Chrome first-call: voices may load late
            if (viVoice) u.voice = viVoice;
            window.speechSynthesis.speak(u);
            return true;
        } catch (e) {
            return false;
        }
    }

    function stop() {
        if (isSupported()) {
            try { window.speechSynthesis.cancel(); } catch (e) { /* ignore */ }
        }
    }

    return { isSupported: isSupported, speak: speak, stop: stop, sanitize: sanitize };
})();

// Chrome loads voices async — refresh the vi-VN pick when they arrive.
if ('speechSynthesis' in window) {
    window.speechSynthesis.onvoiceschanged = function () {
        // re-pick happens lazily inside speak() via loadVoices()
    };
}
