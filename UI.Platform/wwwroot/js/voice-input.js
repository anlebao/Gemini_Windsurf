// SF-W3 (2026-11-07): shared speech-to-text service for storefront AI inputs
// (StoreSearchBar + AiChatWidget). Browser SpeechRecognition API, vi-VN.
//
// Single active session page-wide — start() aborts any in-progress session so a
// tap on mic B hands off cleanly from mic A (two mics never listen at once).
//
// Blazor contract (JSInvokable on the calling component):
//   OnVoiceInterim(text) — live transcript while speaking (fill input)
//   OnVoiceFinal(text)   — silence (~2.5s) or manual stop → auto-submit
//   OnVoiceError(msg)    — localized Vietnamese error message
//   OnVoiceEnd()         — recognition ended (reset listening state)
//
// Transcript is REBUILT from event.results[0..n] on every result — never
// incrementally folded. Folding (accumulated += interim-tail before stop())
// double-counts the tail when the browser flushes it as a final result on
// recognition.stop() → "câu hỏi câu hỏi" (SF-W3 fix 2026-11-07).
//
// Loaded via <script src="/_content/VanAn.UI.Platform/js/voice-input.js"> —
// plain script (not module) so any host (WASM/Server/SSR) can include it.

window.vananVoiceInput = (function () {
    const SILENCE_DELAY_MS = 2500; // 2.5s without new results → auto-submit

    let recognition = null;
    let dotNetRef = null;
    let silenceTimer = null;
    let latestDisplay = ''; // complete transcript — final segments + current interim
    let active = false;
    let finalSent = false;

    function isSupported() {
        return !!(window.SpeechRecognition || window.webkitSpeechRecognition);
    }

    function isActive() {
        return active;
    }

    function clearSilence() {
        if (silenceTimer) {
            clearTimeout(silenceTimer);
            silenceTimer = null;
        }
    }

    // Deliver the transcript to Blazor exactly once per session.
    function finalize() {
        if (finalSent) return;
        finalSent = true;
        const text = latestDisplay.trim();
        if (text && dotNetRef) {
            dotNetRef.invokeMethodAsync('OnVoiceFinal', text);
        }
    }

    function abortInternal() {
        clearSilence();
        if (recognition) {
            const r = recognition;
            recognition = null;
            active = false;
            try {
                // Strip handlers so the abort of the old session can't deliver
                // callbacks into the NEW session's dotNetRef.
                r.onresult = null;
                r.onerror = null;
                r.onend = null;
                r.abort();
            } catch (e) { /* already stopped */ }
        }
    }

    function start(ref) {
        abortInternal(); // mic hand-off — at most one listener at a time

        const SR = window.SpeechRecognition || window.webkitSpeechRecognition;
        if (!SR) return false;

        dotNetRef = ref || null;
        latestDisplay = '';
        finalSent = false;

        recognition = new SR();
        recognition.lang = 'vi-VN';
        recognition.continuous = true;
        recognition.interimResults = true;
        recognition.maxAlternatives = 1;

        recognition.onresult = function (event) {
            // Rebuild the whole transcript every event — results[] carries all
            // segments since session start; a result flips interim→final in
            // place. Immune to resultIndex quirks and stop()-flush duplicates.
            //
            // 2026-11-08 dedup round 2 ("nội dung lặp gấp đôi/gấp 3"): Chrome
            // continuous mode can emit the SAME segment as a duplicate final
            // entry on endpointing, and the pending interim may re-appear as
            // an extra final on stop()-flush → adjacent identical segments.
            // Collapse consecutive repeats (normalized); a different segment
            // between two identical ones is still kept.
            const finals = [];
            let interim = '';
            let prev = '';
            for (let i = 0; i < event.results.length; i++) {
                const result = event.results[i];
                const t = result[0].transcript || '';
                const norm = t.replace(/\s+/g, ' ').trim().toLowerCase();
                if (!norm) continue;
                if (result.isFinal) {
                    if (norm !== prev) { finals.push(t); prev = norm; }
                } else if (norm !== prev) {
                    interim += t + ' ';
                    prev = norm;
                }
            }
            latestDisplay = (finals.join(' ') + ' ' + interim).replace(/\s+/g, ' ').trim();
            if (dotNetRef && latestDisplay) {
                dotNetRef.invokeMethodAsync('OnVoiceInterim', latestDisplay);
            }

            // Silence → end the session; onend delivers OnVoiceFinal.
            clearSilence();
            silenceTimer = setTimeout(stop, SILENCE_DELAY_MS);
        };

        recognition.onerror = function (event) {
            // Don't auto-submit partial text on failure — surface the error instead.
            latestDisplay = '';
            let message = 'Lỗi nhận dạng giọng nói.';
            switch (event.error) {
                case 'no-speech': message = 'Không nghe thấy gì — thử nói lại nhé.'; break;
                case 'audio-capture': message = 'Không truy cập được micro.'; break;
                case 'not-allowed':
                case 'service-not-allowed':
                    message = 'Quyền micro bị từ chối — cho phép micro rồi thử lại.'; break;
                case 'network': message = 'Lỗi mạng — kiểm tra kết nối rồi thử lại.'; break;
            }
            if (dotNetRef) {
                dotNetRef.invokeMethodAsync('OnVoiceError', message);
            }
        };

        recognition.onend = function () {
            active = false;
            clearSilence();
            finalize(); // flush whatever transcript we have (manual stop / browser end)
            if (dotNetRef) {
                dotNetRef.invokeMethodAsync('OnVoiceEnd');
            }
        };

        try {
            recognition.start();
            active = true;
            return true;
        } catch (e) {
            console.error('vananVoiceInput.start error:', e);
            recognition = null;
            active = false;
            return false;
        }
    }

    function stop() {
        if (recognition && active) {
            try {
                // Browser flushes pending interim segments as finals before
                // onend — latestDisplay rebuilds correctly, no manual folding.
                recognition.stop();
            } catch (e) {
                console.error('vananVoiceInput.stop error:', e);
            }
        }
        active = false;
    }

    return { isSupported: isSupported, isActive: isActive, start: start, stop: stop };
})();
