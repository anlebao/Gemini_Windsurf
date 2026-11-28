# TASK CARD: Voice Search Input Hardening V1 — "Treo" fixes (P0)

> Created: 2026-11-28 · Updated: 2026-11-28 · Status: **DONE — deployed `b2ca71ed`+`6bef02f4`, RV 14/14 PASS**
> Mode: IMPLEMENT · TDD · sweep-first RV
> Master plan: `docs/AI/plans/voice-search-input-hardening-master-plan.md`
> RCA: 2026-11-28 — A1 JSInvokable mismatch · A2 no watchdog · A3 noise keep-alive
> · C1 lazy-load race · C2 global-state leak
> Rule: governance "Public Search (anti-drift)" — KHÔNG chạm Domain, không Gate 5

## OBJECTIVE

Voice search input **không bao giờ treo**: mọi session có đường thoát trong thời
gian hữu hạn (watchdog + hard cap), trạng thái `isRecording`/`_listening` luôn
reset đúng, mic luôn hiện/khởi động được sau khi page sẵn sàng. Scope V1 = chữa
"treo" (correctness) — KHÔNG chạm latency tuning (V2).

## CURRENT STATE (verified 2026-11-28)

- `StoreFinder` KL dùng `voice-note.js` contract (`initializeSpeechRecognition`,
  `OnRecordingEnd`, `SetTranscriptionText`) nhưng chỉ định nghĩa
  `OnVoiceRecordingEnd`/`OnVoiceError` → interop fail lặng → `isRecording` kẹt true.
- `ProductSearchResults` + Dir `StoreFinder`/`Home` dùng `vananVoiceInput` đúng
  contract (`OnVoiceInterim/Final/Error/End`).
- `voice-input.js`: `silenceTimer` chỉ arm trong `onresult`; không timeout
  first-result, không max-session; `continuous=true`.
- `voice-note.js` globals shared page-wide; `isVoiceRecording` chỉ reset trong
  `onend` — navigate giữa session → mic chết page mới.
- `index.html` KL: `voice-input.js`/`voice-note.js` trong `__vananLazyJs` inject
  sau `window.load`; `_speechSupported`/`isSpeechRecognitionSupported` check 1 lần
  ở `firstRender` — race với WASM boot nhanh.
- `voice-note.js` vẫn cần nguyên cho voice-NOTE (Cart/BookingNote/VoiceNote).

## IMPACT-NGUỢC GUARDS (không được vỡ)

- `voice-note.js` contract `SetTranscriptionText`/`OnRecordingEnd`/`UpdateVoiceTranscript`
  — GIỮ nguyên (Home KL + voice-note pages phụ thuộc).
- `vananVoiceInput` JS API `isSupported/isActive/start/stop` + JSInvokable names
  `OnVoiceInterim/OnVoiceFinal/OnVoiceError/OnVoiceEnd` — GIỮ (3 page đang dùng).
- `Voice_Note_Enabled` flag gate trên StoreFinder KL — giữ.
- Selectors/E2E hooks: `#search-input`, `.vanan-search-pill`, `.search-learned-note` — giữ.
- StoreFinderTests (7) + SearchCorrectionStoreTests (14) + ProductSearchResultsTests (5)
  — vẫn pass sau migrate.
- `SILENCE_DELAY_MS=2500` — V1 GIỮ (tuning ở V2a).

## V1 Implementation checklist ✅ ALL DONE (`b2ca71ed` + `6bef02f4`)

- [x] **V1a. Migrate StoreFinder KL → `vananVoiceInput`** (kill A1):
  - `OnAfterRenderAsync`: thay `isSpeechRecognitionSupported`/`initializeSpeechRecognition`
    bằng `vananVoiceInput.isSupported` (giữ flag `Voice_Note_Enabled` gate).
  - `ToggleVoiceSearch` → pattern `ToggleVoiceAsync` của ProductSearchResults
    (`vananVoiceInput.start`/`stop`, `_listening` thay `isRecording`).
  - Đổi `OnVoiceRecordingEnd` → `OnVoiceInterim`/`OnVoiceFinal`/`OnVoiceEnd`
    (`OnVoiceInterim`: `_searchKeyword = text` + StateHasChanged, KHÔNG trigger
    `OnSearchValueChanged`; `OnVoiceFinal`: `_searchKeyword` + `_lastInputWasVoice` +
    `LoadStores`/`OnSearchSubmit`).
  - `Dispose`: `_voiceDotNetRef?.Dispose()` + `vananVoiceInput.stop` nếu `_listening`.
  - Markup: `IsListening="isRecording"` → `_listening`; hint text giữ.
- [x] **V1b. Watchdog trong `voice-input.js`** (A2+A3 — 1 chỗ, cover mọi consumer):
  - `FIRST_RESULT_TIMEOUT_MS = 8000`: sau `start()` không có `onresult` nào →
    `OnVoiceError('Không nghe thấy gì — thử nói lại nhé.')` + `abortInternal()`.
  - `MAX_SESSION_MS = 30000`: hard cap → `stop()` (onend → finalize đường thường).
  - Silence re-arm chỉ khi `latestDisplay` thay đổi so với lần trước (noise-guard)
    — vẫn giữ 2.5s silence semantics cho speech thật.
- [x] **V1c. Navigation cleanup** (C2):
  - `voice-input.js`: `window.addEventListener('pagehide', abortInternal)` (SPA-safe,
    không cần hook từng page).
  - `voice-note.js`: `initializeSpeechRecognition` đặt `window.isVoiceRecording=false`
    + abort `recognition` cũ nếu còn (đỡ leak từ page trước) — KHÔNG đổi contract.
- [x] **V1d. Lazy-race guard** (C1):
  - `index.html`: sau khi inject `voice-input.js`/`voice-note.js` resolve
    `window.__vananVoiceReady` (Promise tạo sẵn trước `vananLoadLazyJs`).
  - Pages check support: `await vananVoiceReady` (timeout ~3s, fail → mic hidden)
    rồi mới gọi `isSupported` — thay check-1-lần. Áp StoreFinder KL +
    ProductSearchResults (+ Home KL dùng voice-note.js).
- [x] **V1e. Ship:** build 0 err · Core.Tests ALL PASS · guard-check ALL PASSED ·
  commit → push → CD → RV sweep.

## V1 TDD test list (viết trước)

- `StoreFinderTests` (bUnit — port pattern `ProductSearchResultsTests`, JSInterop Loose):
  1. ToggleVoice → `vananVoiceInput.start` invoked (verify call), `_listening=true`
  2. `OnVoiceFinal("abc")` → `LoadStores` chạy (HTTP call với `name=abc`) + `_listening=false` + `_lastInputWasVoice` (record source=voice khi accept chip — cover bởi flow test hiện có nếu còn hợp lệ)
  3. `OnVoiceError("x")` → `_listening=false` + `_error` set + re-render
  4. `OnVoiceEnd` → `_listening=false`
  5. `OnVoiceInterim` → `_searchKeyword` cập nhật, KHÔNG gọi HTTP (không debounce)
- Regression: 7 StoreFinderTests hiện có (debounce/min-length/CT/cache/learned/undo) vẫn xanh.
- Watchdog/noise/lazy-race (V1b/V1d) là JS thuần → **không bUnit** — verify bằng RV sweep.

## V1 RV sweep-first (`6_Testing/rv-scripts/voice-input-rv-sweep.mjs` — mới)

Inject `FakeSpeechRecognition` qua `page.addInitScript` trước khi page load:

- **S1 no-result watchdog:** fake SR không emit gì → ≤10s sau bấm mic phải có
  error "Không nghe thấy" + `_listening` off (mic không đỏ) + `isSupported` vẫn OK.
- **S2 noise hard-cap:** fake SR emit interim mới mỗi 500ms → session phải tự
  `stop()` ≤32s → `OnVoiceEnd` + final transcript submit.
- **S3 silence→submit:** emit "an tam" rồi ngừng → ~2.5s+ sau → request
  `/api/tenants/search?name=...` (KL) + transcript vào input đúng (không gấp đôi).
- **S4 StoreFinder KL isRecording reset:** S3 xong → không còn "Đang ghi âm" +
  tap mic lần 2 start được ngay (không cần 2 tap).
- **S5 lazy-race:** thêm init-script `document.readyState` warm — assert
  `vananVoiceInput.isSupported()` callable ≤4s sau firstRender (retry path).
- **S6 navigation:** đang nghe → điều hướng page khác → session abort (mic off,
  không lỗi console) → quay lại tap mic start được.
- Run trên `commienphi.timlathay.com/stores` + `/products` + `timlathay.com/stores`.

## V1 Definition of 100% done

1. StoreFinder KL trên `vananVoiceInput` — không còn reference voice-note.js trong page
2. Watchdog first-result (≤8s) + hard cap (≤30s) + noise-guard trong `voice-input.js`
3. `isRecording`/`_listening` reset đúng mọi path (final/error/end/dispose/pagehide)
4. Lazy-race guard — mic check đợi script sẵn sàng (≤3s timeout)
5. TDD tests V1 (5 mới) + toàn bộ regression Core.Tests xanh
6. guard-check ALL PASSED → push → CD success
7. RV sweep `voice-input-rv-sweep.mjs` S1–S6 PASS trên prod — **DONE 2026-11-28:
   14/14 PASS** (S1 watchdog no-result → error+abort ≤12s ✅ · S2 noise hard-cap
   end ≤36s ✅ · S3 silence→submit `name=an tam` ✅ · S4 listening reset + re-tap
   ngay ✅ · S5 lazy-race `vananWhenLazyReady` + isSupported ~1s ✅ · S6 SPA nav
   → isActive=false ✅ · Dir SSR mic + submit ✅ · 0 pageerror).
   **Bug phụ bắt được qua RV:** DotNetRef disposed vẫn bị JS invoke →
   `safeInvoke`/`voiceSafeInvoke` wrapper (commit `6bef02f4`).

## OUT OF SCOPE (V2/V3 — ghi để không quên)

- V2a adaptive silence (floor 1.2s + submit 800ms sau isFinal + "Đang xử lý" UI)
- V2b interim DOM-fill (0 .NET roundtrip khi nói — kill SSR flood)
- V2c `continuous=false` option + unify Home KL sang vananVoiceInput
- V3: fuzzy-path deep fix (Gate 5 — card DidYouMean B2/B3) · SSR reconnect UI · metrics
- voice-NOTE feature (Cart/BookingNote/VoiceNote) — giữ voice-note.js
