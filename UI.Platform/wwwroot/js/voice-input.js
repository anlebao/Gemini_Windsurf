// SF-W3 (2026-11-07): shared speech-to-text service for storefront AI inputs
// (StoreSearchBar + AiChatWidget). Browser SpeechRecognition API, vi-VN.
//
// Single active session page-wide — start() aborts any in-progress session so a
// tap on mic B hands off cleanly from mic A (two mics never listen at once).
//
// Blazor contract (JSInvokable on the calling component):
//   OnVoiceInterim(text) — live transcript while speaking (fill input)
//   OnVoiceFinal(text)   — silence (800ms sau isFinal / 2.5s interim) hoặc
//                          browser endpoint (continuous=false) / manual stop
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
    const SILENCE_DELAY_MS = 2500;      // interim còn đuôi → user vẫn đang nói
    // V2a adaptive silence (2026-11-28): event kết thúc bằng isFinal (không còn
    // interim) = câu đã trọn vẹn → commit sau 800ms thay vì floor 2.5s.
    const FINAL_COMMIT_DELAY_MS = 800;
    // V1b watchdog (2026-11-28): mọi session phải có đường thoát — trước đây
    // silenceTimer chỉ arm trong onresult → nếu onresult không bao giờ fire
    // (service stall, lỗi network không trả) thì session nghe VÔ HẠN → "treo".
    const FIRST_RESULT_TIMEOUT_MS = 8000; // không result nào sau start → error + abort
    const MAX_SESSION_MS = 30000;         // hard cap — noise không kéo session sống mãi

    let recognition = null;
    let dotNetRef = null;
    let silenceTimer = null;
    let firstResultTimer = null;
    let maxSessionTimer = null;
    let finalCommitTimer = null; // V2-fix: deadline sau final/speechend — noise KHÔNG re-arm được
    let latestDisplay = ''; // complete transcript — final segments + current interim
    let prevDisplay = '';   // noise-guard: silence chỉ re-arm khi transcript đổi
    let prevFinalsCount = 0; // noise-guard: interim→final cùng text cũng re-arm (V2a)
    let active = false;
    let finalSent = false;
    let targetElId = null;  // V2b: interim fill DOM trực tiếp, 0 .NET roundtrip
    let sessionT0 = 0;      // vlog timestamp base

    // Debug ring-buffer (2026-11-28): Chrome thật có failure mode fake-SR không
    // cover (noise giữ session mở, isFinal không bao giờ set). Đọc qua
    // console: copy(JSON.stringify(window.__vananVoiceLog)).
    window.__vananVoiceLog = window.__vananVoiceLog || [];
    function vlog(ev, data) {
        try {
            window.__vananVoiceLog.push({
                t: sessionT0 ? Date.now() - sessionT0 : 0,
                ev: ev, data: String(data || '').slice(0, 80)
            });
            if (window.__vananVoiceLog.length > 300) window.__vananVoiceLog.splice(0, 100);
        } catch (e) { /* debug only */ }
    }

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

    function clearTimers() {
        clearSilence();
        if (firstResultTimer) {
            clearTimeout(firstResultTimer);
            firstResultTimer = null;
        }
        if (maxSessionTimer) {
            clearTimeout(maxSessionTimer);
            maxSessionTimer = null;
        }
        if (finalCommitTimer) {
            clearTimeout(finalCommitTimer);
            finalCommitTimer = null;
        }
    }

    // V1c: mọi callback vào .NET phải fail-safe — DotNetObjectReference có thể đã
    // disposed (SPA nav/dispose trong khi session còn chạy) → invokeMethodAsync
    // reject với ArgumentException → unhandled pageerror. Nuốt lặng: component
    // chết rồi thì transcript đến cũng vô nghĩa.
    function safeInvoke(name, arg) {
        if (!dotNetRef) { vlog('invoke-skip', name); return; }
        try {
            const p = dotNetRef.invokeMethodAsync(name, arg);
            if (p && typeof p.catch === 'function') {
                p.then(function () { vlog('invoke-ok', name); })
                 .catch(function (e) {
                     // Diag: server-side throw / ref disposed / circuit dead —
                     // lý do "transcript fill nhưng search không chạy" trên máy
                     // thật đọc được từ __vananVoiceLog.
                     vlog('invoke-err', name + '|' +
                         String(e && e.message ? e.message : e).slice(0, 150));
                 });
            }
        } catch (e) {
            vlog('invoke-err', name + '|' +
                String(e && e.message ? e.message : e).slice(0, 150));
        }
    }

    // Deliver the transcript to Blazor exactly once per session.
    function finalize() {
        if (finalSent) return;
        finalSent = true;
        const text = latestDisplay.trim();
        vlog('finalize', text);
        if (text) {
            safeInvoke('OnVoiceFinal', text);
        }
    }

    function abortInternal() {
        clearTimers();
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

    function start(ref, targetId) {
        abortInternal(); // mic hand-off — at most one listener at a time

        const SR = window.SpeechRecognition || window.webkitSpeechRecognition;
        if (!SR) return false;

        dotNetRef = ref || null;
        targetElId = targetId || null;
        latestDisplay = '';
        prevDisplay = '';
        prevFinalsCount = 0;
        finalSent = false;
        sessionT0 = Date.now();
        vlog('start', targetId);

        recognition = new SR();
        recognition.lang = 'vi-VN';
        // V2c (2026-11-28): continuous=false — Chrome tự end ở endpoint câu nói
        // (search query ngắn, 1 utterance = 1 query) thay vì nghe liên tục →
        // onend/finalize đến sớm, không còn phụ thuộc hoàn toàn silence timer.
        // Timers vẫn giữ làm fallback (noise-guard + watchdog).
        recognition.continuous = false;
        recognition.interimResults = true;
        recognition.maxAlternatives = 1;

        recognition.onresult = function (event) {
            // V1b: result đầu tiên đến → huỷ watchdog first-result.
            if (firstResultTimer) {
                clearTimeout(firstResultTimer);
                firstResultTimer = null;
            }
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
            vlog('result', `f${finals.length} ${latestDisplay.slice(0, 50)}`);
            if (latestDisplay) {
                // V2b (2026-11-28): interim fill input qua DOM trực tiếp khi caller
                // truyền targetId — mỗi interim ~3-5/s trước đây là 1 JS→.NET
                // invoke + full render; trên Blazor Server (timlathay SSR) đó là
                // SignalR roundtrip + render toàn trang → backlog → "treo".
                // Không dispatch 'input' event — tránh trigger binding/debounce.
                const el = targetElId && document.getElementById(targetElId);
                if (el) {
                    el.value = latestDisplay;
                } else {
                    safeInvoke('OnVoiceInterim', latestDisplay);
                }
            }

            // Silence → end the session; onend delivers OnVoiceFinal.
            // V1b noise-guard: chỉ re-arm khi transcript ĐỔI — ambient noise sinh
            // interim giống hệt nhau không kéo dài session vô hạn (A3).
            const newFinalArrived = finals.length !== prevFinalsCount;
            if (latestDisplay !== prevDisplay || newFinalArrived) {
                prevDisplay = latestDisplay;
                prevFinalsCount = finals.length;
                clearSilence();
                silenceTimer = setTimeout(function () { stop('silence'); }, SILENCE_DELAY_MS);
            }
            // V2-fix (2026-11-28 manual-test RCA): FINAL_COMMIT timer độc lập —
            // re-arm CHỈ khi có final MỚI; interim noise KHÔNG chạm được. Chrome
            // thật: noise giữ VAD mở → 'end'/'speechend' không fire + interim
            // đổi liên tục → silence cũ bị re-arm mãi → session không commit
            // (transcript hiện nhưng search không chạy = "treo" trên mọi trang
            // in-page search; Home nav không bị vì flow khác).
            if (newFinalArrived && finals.length > 0) {
                if (finalCommitTimer) clearTimeout(finalCommitTimer);
                finalCommitTimer = setTimeout(function () { stop('final-commit'); }, FINAL_COMMIT_DELAY_MS);
            }
        };

        // V2-fix: Chrome VAD tự báo "user ngừng nói" — commit ngay kể cả khi
        // isFinal không bao giờ set (quirk Chrome continuous=false) và kể cả
        // khi 'end' không fire. stop() → Chrome flush → onend → finalize.
        recognition.onspeechend = function () {
            vlog('speechend');
            if (finalCommitTimer) clearTimeout(finalCommitTimer);
            finalCommitTimer = setTimeout(function () { stop('speechend'); }, FINAL_COMMIT_DELAY_MS);
        };
        recognition.onaudiostart = function () { vlog('audiostart'); };
        recognition.onsoundstart = function () { vlog('soundstart'); };
        recognition.onspeechstart = function () { vlog('speechstart'); };
        recognition.onsoundend = function () { vlog('soundend'); };
        recognition.onaudioend = function () {
            vlog('audioend');
            // audioend = mic đã ngắt → user xong — commit như speechend
            if (finalCommitTimer) clearTimeout(finalCommitTimer);
            finalCommitTimer = setTimeout(function () { stop('audioend'); }, FINAL_COMMIT_DELAY_MS);
        };

        recognition.onerror = function (event) {
            vlog('error', event.error);
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
                safeInvoke('OnVoiceError', message);
            }
        };

        recognition.onend = function () {
            vlog('end');
            active = false;
            clearTimers();
            finalize(); // flush whatever transcript we have (manual stop / browser end)
            if (dotNetRef) {
                safeInvoke('OnVoiceEnd');
            }
        };

        try {
            recognition.start();
            active = true;
            // V1b watchdog: không result nào trong 8s → báo lỗi + abort (thay vì
            // nghe vô hạn); hard cap 30s chặn mọi session sống mãi.
            firstResultTimer = setTimeout(function () {
                abortInternal();
                safeInvoke('OnVoiceError',
                    'Không nghe thấy gì — thử nói lại nhé.');
                safeInvoke('OnVoiceEnd');
            }, FIRST_RESULT_TIMEOUT_MS);
            maxSessionTimer = setTimeout(stop, MAX_SESSION_MS);
            return true;
        } catch (e) {
            console.error('vananVoiceInput.start error:', e);
            recognition = null;
            active = false;
            return false;
        }
    }

    function stop(cause) {
        // V2-fix: nếu Chrome không fire 'end' sau stop() (quirk một số build /
        // session đã chết ngầm) → finalize trực tiếp sau 400ms — đảm bảo
        // transcript tới .NET trong mọi trường hợp, không phụ thuộc onend.
        vlog('stop', cause);
        // Manual/silence/max-cap stop → không cần first-result watchdog nữa
        // (edge: nếu onend không fire, tránh error giả bắn ra sau).
        if (firstResultTimer) {
            clearTimeout(firstResultTimer);
            firstResultTimer = null;
        }
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
        // Dead-man finalize: onend không fire (Chrome 'end' stuck) → vẫn submit.
        setTimeout(function () {
            if (!finalSent && dotNetRef) {
                vlog('end-missing-fallback');
                finalize();
                safeInvoke('OnVoiceEnd');
            }
        }, 400);
    }

    // V1c (2026-11-28): session phải chết khi rời page — trước đây navigate giữa
    // chừng để session chạy nền + DotNetRef cũ disposed → mic kẹt ở page mới.
    window.addEventListener('pagehide', abortInternal);

    return { isSupported: isSupported, isActive: isActive, start: start, stop: stop };
})();
