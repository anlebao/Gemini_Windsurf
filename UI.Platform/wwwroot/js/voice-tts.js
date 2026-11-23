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

    // ---- W3e: Vietnamese number → words (for currency amounts) ----
    // "1000đ" must read "một nghìn đồng", not "một không không không đ".
    var DIGIT_NAMES = ['', 'một', 'hai', 'ba', 'bốn', 'năm', 'sáu', 'bảy', 'tám', 'chín'];

    function readTwo(n) { // 0..99
        var t = Math.floor(n / 10), u = n % 10;
        if (t === 0) return DIGIT_NAMES[u];
        if (t === 1) return u === 0 ? 'mười' : 'mười ' + (u === 1 ? 'mốt' : DIGIT_NAMES[u]);
        var unit = u === 0 ? '' : ' ' + (u === 1 ? 'mốt' : u === 5 ? 'lăm' : DIGIT_NAMES[u]);
        return DIGIT_NAMES[t] + ' mươi' + unit;
    }

    function readThree(n) { // 0..999
        if (n === 0) return '';
        var h = Math.floor(n / 100), r = n % 100;
        if (h === 0) return readTwo(r);
        return DIGIT_NAMES[h] + ' trăm' + (r === 0 ? '' : ' ' + (r < 10 ? 'lẻ ' + DIGIT_NAMES[r] : readTwo(r)));
    }

    function numberToWords(n) { // non-negative integer → Vietnamese words
        if (n === 0) return 'không';
        var scales = [[1000000000, 'tỷ'], [1000000, 'triệu'], [1000, 'nghìn']];
        var parts = [], rem = n, hasHigher = false;
        for (var i = 0; i < scales.length; i++) {
            var g = Math.floor(rem / scales[i][0]);
            rem %= scales[i][0];
            if (g > 0) {
                // Leading group reads naturally ("một nghìn"); non-leading
                // groups <100 need the "không trăm (lẻ)" padding ("một triệu
                // không trăm lẻ năm nghìn").
                var gw = g >= 100 ? readThree(g)
                    : parts.length > 0 ? 'không trăm ' + (g < 10 ? 'lẻ ' + DIGIT_NAMES[g] : readTwo(g))
                    : readTwo(g);
                parts.push(gw + ' ' + scales[i][1]);
                hasHigher = true;
            }
        }
        if (rem > 0) {
            var rw;
            if (rem < 10 && hasHigher) rw = 'lẻ ' + DIGIT_NAMES[rem];
            else if (rem < 100 && hasHigher) rw = 'không trăm ' + readTwo(rem);
            else rw = readThree(rem);
            parts.push(rw);
        }
        return parts.join(' ');
    }

    // "850.000đ" | "1,200,000 đồng" | "1000đ" | "850k" → spoken Vietnamese amount.
    function speakableCurrency(text) {
        return text.replace(/(?<![\p{L}\p{N}])(\d{1,3}(?:[.,\s]\d{3})+|\d+)\s*(đồng|vnđ|vnd|đ|₫|k)(?![\p{L}\p{N}])/giu,
            function (m, num, unit) {
                var digits = num.replace(/[.,\s]/g, '');
                var value = parseInt(digits, 10);
                if (isNaN(value) || value > 999999999999) return m;
                if (/^k$/i.test(unit)) value *= 1000;
                return numberToWords(value) + ' đồng';
            });
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
        t = speakableCurrency(t);                    // "1000đ" → "một nghìn đồng"
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
