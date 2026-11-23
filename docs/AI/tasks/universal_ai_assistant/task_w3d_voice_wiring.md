# TASK W3d: VOICE WIRING V1 — mic trên /a/{slug} + TTS read-aloud

> Wave: W3/W6-bridge · Mode: IMPLEMENT · TDD + sweep-first RV
> Master plan: `docs/AI/plans/w3c-w3d-extract-voice-master-plan.md` (D5–D6)
> Depends on: W1 `/a/{slug}` + AssistantLink (deployed), AiChatWidget VoiceEnabled param (đã có sẵn)

## Khảo sát đã xác nhận (không build lại)

| Đã có | Vị trí |
|---|---|
| STT SpeechRecognition vi-VN (hardened: dedup/silence/mic-handoff) | `UI.Platform/wwwroot/js/voice-input.js` |
| Mic button trong `AiChatWidget` gated `VoiceEnabled` + `_speechSupported` | `UI.Platform/Components/Ai/AiChatWidget.razor` |
| Flag tenant `Voice_Note_Enabled` resolve pattern | `Store.razor` → `TenantProfileService.GetFeatureSettingsAsync` |
| TTS speechSynthesis vi-VN voice-select pattern | `5_WebApps/ShopERP/wwwroot/js/tts-reader.js` (kitchen) |
| Mic đã chạy trên storefront | `Store.razor` truyền `VoiceEnabled` → `/store/{slug}?chat=ai` |

## Gap cần wire

1. `Assistant.razor` (`/a/{slug}` hosted + embed iframe): **không truyền `VoiceEnabled`** → mic ẩn.
2. Không có TTS read-aloud cho câu trả lời AI ở bất kỳ surface nào.

## Scope IN

> **UPDATE 2026-11-23 (user directive):** auto-speak ĐƯỢC thêm vào V1 — mặc định ON
> khi `VoiceEnabled` + TTS supported, opt-out qua toggle megaphone
> (`btn-tts-autospeak`). Scope OUT "auto-speak" phía dưới được override.

### A. Mic cho /a/{slug} + embed
1. `Assistant.razor`: inject `TenantProfileService` (hoặc service tương đương đang resolve feature settings trong KhachLink) → load `Voice_Note_Enabled` sau khi resolve tenant → truyền `VoiceEnabled` vào `AiChatWidget`.
2. Verify embed mode (`?embed=1`) cũng nhận mic (cùng component, không cần thêm gì).
3. Fallback an toàn: resolve fail → flag off → mic ẩn, chat text vẫn hoạt động.

### B. TTS read-aloud (client-side)
4. `UI.Platform/wwwroot/js/voice-tts.js` — port pattern `tts-reader.js`: `isSupported()`, `speak(text)`, `stop()`, vi-VN voice select + `onvoiceschanged`. Plain script, load qua `_content/VanAn.UI.Platform/js/voice-tts.js`.
5. `AiChatWidget.razor`: nút loa nhỏ trên **message AI** (không phải user message) → `speakAsync(text)`; bấm lại → stop; message mới đến / đóng widget / đổi session → auto `stop()`.
6. Gate: cùng `VoiceEnabled` param (tái dùng flag, không flag mới). Browser không hỗ trợ → nút ẩn.
7. Đảm bảo script load trong cả WASM (KhachLink) lẫn Server (nếu widget dùng ở ShopERP) — check `index.html`/`App.razor` host pages, thêm script tag nơi thiếu.

## Scope OUT

- Server-side STT (Gemini audio) cho Firefox/Safari — V2.
- AI voice conversation (speech↔speech) — W6, Gemini Live API.
- TTS cho tin nhắn user / handoff panel.
- ~~Auto-speak mọi câu trả lời~~ — **OVERRIDDEN 2026-11-23 (user): auto-speak ĐÃ làm trong V1**, mặc định ON + toggle opt-out (`btn-tts-autospeak`).

## TDD — tests viết TRƯỚC

- JS unit-ish: không có JS test runner trong repo → cover bằng Playwright spec (mục RV).
- C# testable parts:
  - [x] `Assistant.razor` flag resolution — DONE bằng inspection + E2E: `TenantProfileHttpService.GetFeatureSettingsAsync` (public anonymous, trả defaults khi lỗi) → `VoiceEnabled="_features.Voice_Note_Enabled"`; resolve fail → flag off → degrade graceful.
  - [x] `AiChatWidget`: `VoiceEnabled=false` → không render mic/speaker/auto-speak; `true` + browser support → render. DONE: probe riêng `_speechSupported` (vananVoiceInput) + `_ttsSupported` (vananVoiceTts).
- [x] Repo check: không có bUnit infra cho page này → E2E-covered qua `6_Testing/e2e-tests/assistant-voice.spec.ts` (4 tests).

## RV sweep-first (sau deploy)

1. Container `vanan-khachlink-1` image = commit, healthy.
2. L2 static: `/a/rvAntamAi01x` load `voice-input.js` + `voice-tts.js` (200).
3. L3 Playwright spec mới `assistant-voice.spec.ts`:
   - `/a/{slug}`: mic button hiển thị khi `Voice_Note_Enabled=true` (An Tâm — check flag trước, nếu off thì bật tạm qua shop-features hoặc chọn tenant khác).
   - Gửi chat → AI trả lời → speaker button xuất hiện trên message → click → `speechSynthesis.speaking` state (assert qua JS eval, không cần nghe audio thật).
   - Embed `/a/{slug}?embed=1`: mic + speaker vẫn hoạt động.
   - Regression: flag off → cả hai ẩn.
4. Manual: user test mic nói câu hỏi trên `/a/` + bấm loa nghe trả lời.

## Acceptance / Gate

- [ ] Mic xuất hiện trên `/a/{slug}` + embed khi `Voice_Note_Enabled=true`; ẩn khi false/không support
- [ ] Speaker button trên AI message; click → TTS vi-VN; click lại → stop
- [ ] Đóng widget / message mới → TTS auto-stop (không đọc chồng)
- [ ] Storefront `/store/{slug}` không regression (mic + chat cũ)
- [ ] Flag resolve fail → degrade graceful, không break trang
- [ ] Guard ALL PASSED trước push

## Risks

| Risk | Mitigate |
|---|---|
| `TenantProfileService` không injectable trong Assistant.razor context | Kiểm tra DI registration; fallback gọi endpoint public features nếu có; worst case resolve qua widget-config mở rộng |
| speechSynthesis không có vi-VN voice trên máy user | `tts-reader.js` đã fallback any voice; log warn, vẫn speak |
| TTS đọc markdown/ký tự lạ trong câu trả lời | Strip markdown + ký tự đặc biệt trước speak; giới hạn độ dài (vd 500 chars → cắt + "…") |
| Hai widget cùng trang (embed + store) | `voice-input.js` đã single-session; TTS `cancel()` trước speak mới |
