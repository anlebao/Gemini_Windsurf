# Master Plan — Voice Search Input Hardening (chậm + treo)

> **Status:** DRAFT 2026-11-28 — chờ user review
> **Nguồn:** RCA session 2026-11-28 "voice input search xử lý rất chậm và treo luôn"
> **Phạm vi:** voice STT stack trên public search surfaces —
> `UI.Platform/wwwroot/js/voice-input.js` (vananVoiceInput) +
> `5_WebApps/KhachLink/wwwroot/js/voice-note.js` (legacy) + 5 page wirings.
> **KHÔNG chạm:** voice-note feature (ghi chú đơn hàng — Cart/BookingNote/VoiceNote
> giữ voice-note.js), voice-call stack (voice-media/sdk/chat), Domain (không Gate 5).

## 1. Mục tiêu

Voice search input phải: (a) **không bao giờ treo** — mọi session có đường thoát
(watchdog, hard cap, đúng JSInvokable contract); (b) **phản hồi nhanh** — giảm
độ trễ cảm nhận từ "ngừng nói" → "search chạy" (hiện 3–6s xuống ~1–2s);
(c) **một contract duy nhất** cho search surfaces — dứt fork 2 stack JS.

## 2. RCA tóm tắt (verified 2026-11-28)

| # | Root cause | Triệu chứng | Mức |
|---|---|---|---|
| A1 | StoreFinder KL dùng `voice-note.js` nhưng thiếu `OnRecordingEnd`/`SetTranscriptionText` (chỉ có `OnVoiceRecordingEnd`) → `isRecording` không bao giờ reset | "Đang ghi âm..." kẹt vĩnh viễn, tap mic xen kẽ chết | 🔴 |
| A2 | Cả 2 lib: `silenceTimer` chỉ arm trong `onresult` — không watchdog. `onresult` không fire (service stall/network) → session nghe vô hạn | Treo thật sự, không đường thoát | 🔴 |
| A3 | `continuous=true` + môi trường ồn → interim results liên tục reset silence timer → không bao giờ auto-stop | Treo đến khi user bấm lại | 🔴 |
| B1 | `SILENCE_DELAY_MS=2500` cố định + Chrome endpointing vi-VN + STT server-side | Chậm 3–6s sau khi nói xong — by design | 🟡 |
| B2 | Directory SSR: mỗi interim (~3–5/s) = 1 SignalR roundtrip + full render; reconnect modal bị ẩn → rớt mạng = chết lặng | Đơ trong lúc nói, treo khi mạng yếu | 🟡 |
| B3 | Voice transcript nhiễu → ILIKE miss → fuzzy path 500-candidate in-memory | Search response chậm | 🟡 (đã bớt bởi W2 ResponseCache) |
| C1 | voice JS lazy-load sau `window.load` (22 script) nhưng `_speechSupported` check 1 lần ở firstRender → WASM boot nhanh thắng race | Mic không hiện đến khi đổi page | 🟠 |
| C2 | `voice-note.js` globals (`isVoiceRecording`, `recognition`, `voiceDotNetRef`) shared page-wide — navigate giữa chừng → mic chết ở page mới | Mic chết ngẫu nhiên | 🟠 |
| C3 | SW cache-first static JS → stale voice JS 1 lần sau deploy | Nhỏ (SWR đã mitigate) | ⚪ |

**Nhân tố cấu trúc:** KhachLink chạy 2 stack song song —
`voice-note.js` (Home, StoreFinder, Cart, BookingNote, VoiceNote) vs
`vananVoiceInput` (ProductSearchResults) — 2 contract JSInvokable khác nhau.
Chính fork này sinh A1 + C2.

## 3. Wave plan — 3 đợt theo ưu tiên

| Wave | Task card | Nội dung | Trạng thái |
|---|---|---|---|
| **V1** | `task_voice_search_input_hardening.md` | **"Treo" fixes (P0):** V1a migrate StoreFinder KL → `vananVoiceInput` (kill A1+unify) · V1b watchdog: first-result timeout ~8s + hard cap ~30s + silence chỉ re-arm khi transcript đổi (A2+A3) · V1c navigation cleanup (abort on dispose/pagehide) · V1d lazy-race guard (retry isSupported ~3s) (C1+C2) | 🟡 CARD READY — chờ review |
| **V2** | (tạo khi V1 xong) | **"Chậm" fixes (P1):** V2a adaptive silence — submit ~800ms sau isFinal cuối, floor 1.2s + indicator "Đang xử lý" ngay khi stop (B1) · V2b interim qua DOM-fill thuần JS (0 .NET roundtrip khi nói — kill SSR flood + WASM re-render) (B2) · V2c optional `continuous=false` cho search surfaces | ⬜ |
| **V3** | defer | Fuzzy path sâu (đã thuộc card DidYouMean B2/B3 — Gate 5) · SSR reconnect UI · metrics voice-session | ⬜ |

**Gate rule:** implement tuần tự — wave xong = tests xanh + guard-check + RV sweep
(stubbed SpeechRecognition). Deploy checkpoint per wave.

## 4. Quyết định thiết kế V1 (đề xuất, cần duyệt)

1. **Unify contract — không thêm method vào mismatch:** StoreFinder KL chuyển từ
   `voice-note.js` sang `vananVoiceInput` (contract `OnVoiceInterim/Final/Error/End`
   giống ProductSearchResults). `voice-note.js` giữ nguyên cho voice-NOTE feature
   (Cart/BookingNote/VoiceNote ghi chú đơn — UX khác, out of scope). Home KL đang
   wired ĐÚNG với voice-note.js home-mode — V1 không đổi (đánh giá unify ở V2c).
2. **Watchdog trong `vananVoiceInput` (1 chỗ duy nhất):**
   - `FIRST_RESULT_TIMEOUT_MS ≈ 8000` — không result nào sau start → `OnVoiceError`
     "Không nghe thấy — thử lại nhé" + abort.
   - `MAX_SESSION_MS ≈ 30000` — hard cap, `stop()` + finalize.
   - Silence timer chỉ re-arm khi `latestDisplay` **thay đổi** → noise không kéo
     session sống mãi (A3) mà vẫn cho phép pause suy nghĩ giữa câu.
3. **Navigation safety:** page gọi `vananVoiceInput.stop()` trong `DisposeAsync`
   (ProductSearchResults + Dir pages đã có); thêm `pagehide` listener trong JS
   abort luôn — cover StoreFinder mới + SPA nav leak (C2 cho vananVoiceInput;
   voice-note.js chỉ patch `isVoiceRecording=false` trong `initializeSpeechRecognition`).
4. **Lazy-race (C1):** thêm `window.vananVoiceReady` Promise resolve sau khi
   `voice-input.js` + `voice-note.js` inject xong; page check support bằng cách
   `await vananVoiceReady` (timeout ~3s) thay vì gọi trực tiếp 1 lần. Không re-render
   loop.

## 5. Ranh giới cứng

- Không chạm Domain/entity/migration — không Gate 5.
- `Voice_Note_Enabled` flag gate giữ nguyên (StoreFinder KL) — không đổi semantics.
- E2E hooks + markup selectors (`#search-input`, `.search-learned-note`...) giữ nguyên.
- `voice-note.js` contract cho voice-NOTE (SetTranscriptionText/OnRecordingEnd) —
  KHÔNG đổi (Cart/BookingNote/VoiceNote phụ thuộc).
- Search-related interop chỉ qua `vananVoiceInput` sau V1 — 1 contract duy nhất.

## 6. Test & RV strategy

- **TDD C# (bUnit):** port voice tests từ `ProductSearchResultsTests` sang
  `StoreFinderTests` — OnVoiceFinal→search+source=voice, OnVoiceError→reset,
  OnVoiceEnd→reset, ToggleVoice→start.
- **JS logic (watchdog/timers) không bUnit được** → RV sweep Playwright với
  `page.addInitScript` inject **FakeSpeechRecognition** emit scripted events:
  (a) không result → watchdog error ≤10s; (b) noise stream → hard cap end ≤32s;
  (c) speech→silence→result → transcript đúng; (d) stuck-check: `isRecording` reset.
  Extend `personalized-didyoumean-rv-sweep.mjs` hoặc spec mới `voice-input-rv-sweep.mjs`.
- Playwright tắt trong IMPLEMENT — RV chạy sau deploy (sweep-first như W1/W2).

## 7. Rủi ro

| Rủi | Mitigation |
|---|---|
| FakeSpeechRecognition không bắt được bug thật của Chrome | RV script + L5 manual trên thiết bị thật (Chrome Android + desktop) |
| DOM-fill interim bị Blazor diff ghi đè | V2b quyết sau — fallback throttle .NET call 500ms |
| Giảm silence floor → cắt sớm khi user ngập ngừng | V2a dùng isFinal boundary, không giảm mù — 800ms sau isFinal cuối |
| Migrate StoreFinder phá voice-note consumers | StoreFinder không share state với note pages (init per-page) — regression suite StoreFinderTests phải xanh |

## 8. Out of scope

- Voice-note feature (ghi chú) — Cart/BookingNote/VoiceNote giữ voice-note.js.
- Voice call (voice-media/sdk/chat), AiChatWidget voice.
- `pg_trgm`/`name_norm` (B3 deep fix — Gate 5, card DidYouMean).
- Reconnect modal policy (C3/B2 partial — V3).
