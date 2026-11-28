# TASK CARD: Voice Search Input Hardening — "Treo" (V1) + "Chậm" (V2) ✅ DONE

> Created: 2026-11-28 · Updated: 2026-11-28 · Status: **DONE — V1 `b2ca71ed`+`6bef02f4` · V2b `9e9c7a43` · V2a/c `abd82648` · commit-path hardening `eac0c36e` — RV prod 75/75 PASS**
> **Manual-test RCA (2026-11-28):** timlathay.com SSR treo sau V1 — mỗi interim
> `onresult` (~3–5/s) = 1 JS→.NET invoke + full page render qua websocket
> (Blazor Server) → render backlog → hang. WASM invoke là local nên KhachLink
> không bị. Fix = V2b interim DOM-fill (kéo trước lịch).
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

## V2 — "Chậm" fixes ✅ DONE (`9e9c7a43` V2b + `abd82648` V2a/c) — RV 20/20

- [x] **V2b interim DOM-fill** — `start(ref, targetId)`: interim ghi `input.value`
  trực tiếp (0 .NET roundtrip, kill SSR SignalR flood = root cause timlathay
  treo); không truyền targetId → fallback `OnVoiceInterim` .NET (AiChatWidget,
  RealtimeChatPanel backward-compat). 4 page truyền InputId.
- [x] **V2a adaptive silence** — `FINAL_COMMIT_DELAY_MS=800`: event đuôi FINAL
  (không interim) → commit 800ms thay floor 2.5s. Fix noise-guard: re-arm cả
  khi interim→final CÙNG text (`prevFinalsCount` — nếu không timer cũ nuốt
  commit nhanh; trường hợp phổ biến nhất). Verify prod: request đến
  **1051ms sau isFinal** (S3b).
- [x] **V2c continuous=false** — Chrome endpointing tự end session; timers giữ
  fallback. Verify: `__fakeSRInstances[0].continuous === false` trên prod (S7).
- [x] **V2c unify Home KL → vananVoiceInput** — bỏ voice-note.js hoàn toàn trên
  Home; `OnVoiceFinal` → `SubmitHomeSearch` respect `_searchMode` (fix bug
  pre-existing: voice hardcode `/stores` bỏ qua product|store toggle).
  Verify: nav `/products?q=an%20tam` (S8).
- Lưu ý: commit `9e9c7a43` cuốn theo untracked files từ session song song
  (`git add -A`) — không rewrite history; lần sau stage chọn lọc (đã áp dụng
  cho `abd82648`).

## Commit-path hardening ✅ DONE (`eac0c36e`) — chốt failure mode Chrome thật

Manual test trên Chrome user: transcript fill được nhưng search không chạy —
Chrome không fire `end`/`speechend` → session không bao giờ commit. 3 commit
path độc lập: `finalCommitTimer` 800ms sau đuôi FINAL (re-arm CHỈ khi có final
mới — interim noise không re-arm được) · `onspeechend` handler · silence 2.5s
fallback + 30s hard cap. Thêm `stop(cause)` param + `__vananVoiceLog`
ring-buffer diagnostic (đọc event sequence thật trên browser user).

## RV kết quả (2026-11-28, prod — sweep mở rộng)

**75/75 PASS, 0 FAIL** — full coverage mọi voice surface:

- **S1–S9 regression:** watchdog ≤12s · noise cap ≤36s · adaptive ~1s sau
  isFinal · reset+re-tap · lazy-race ~1s · SPA nav abort · continuous=false ·
  Home unify nav · **S9 noend dead-man finalize** (Chrome-bug sim: `end` không
  fire → transcript vẫn submit qua fallback)
- **Matrix 3 KL instances × 3 pages** (commienphi/shopai/vanan × `/` `/stores`
  `/products`): mic render + interim DOM-fill + submit đúng endpoint
  (`/api/tenants/search?name=` · `/api/catalog/search?q=` · Home nav `q=`) +
  listening-off — 36/36
- **hoptacxa/** negative: mic ẩn đúng (portal không voice)
- **Dir SSR** `/stores` + `/` (page user report treo): DOM-fill sớm 0 roundtrip,
  search chạy, listening off
- **D-block DidYouMean/fuzzy:** API `name=antaam` → didYouMean "An Tâm" ·
  typed `antaam` → fuzzy/DYM trên 3 KL + Dir · **voice `antaam` → request
  `name=antaam` → "An Tâm" trong DOM** trên 3 KL (fuzzy chain end-to-end)
- **0 pageerror** trên toàn sweep

**Test artifacts fixed trong sweep (không phải bug):** `domProof` short-circuit
sớm khi "An Tâm" nằm sẵn trong default listing (break trước khi voice final
emit → "no req") · typed-flow sleep 4s cứng flake khi WASM cold (→ đợi mic
render = proxy interactive) · SSR listening-off check 1 lần trước render
cycle → poll 3s.

Run trước đó (S1–S9 cơ bản): 20/20 PASS — report `.devin/voice-v1-rv-report.json`.

## OUT OF SCOPE (V3 — ghi để không quên)

- V3: fuzzy-path deep fix (Gate 5 — card DidYouMean B2/B3) · metrics voice
- voice-NOTE feature (Cart/BookingNote/VoiceNote) — giữ voice-note.js
- "Đang xử lý" indicator tách riêng — hiện search-loading state đã cover
  gap finalize→results (adaptive 800ms khiến gap ~1s, chấp nhận được)
