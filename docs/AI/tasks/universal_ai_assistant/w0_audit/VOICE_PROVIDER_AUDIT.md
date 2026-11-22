# W0 AUDIT: VOICE PROVIDER LANDSCAPE (Q6)

> Date: 2026-11-22 · Câu hỏi: AI voice capability có gì khả dụng, provider nào phù hợp?

## Hiện trạng codebase (verified)

- `10_Voice/vanan-voice`: human-to-human WebRTC (audio+video deployed, RV 25/25). SignalR signaling, Coturn, metering `voice.*`/`video.*`.
- **Không có** STT/TTS/realtime speech pipeline nào trong hệ thống.
- TenantAI LLM = Google Gemini (endpoint `generativelanguage.googleapis.com`).

## Provider candidates (web research 2026-11)

| Provider | Model/API | Vietnamese | Latency/Architecture | Cost (indicative) | Fit |
|---|---|---|---|---|---|
| **Google Gemini Live** | `gemini-3.8-live` speech-to-speech, `gemini-3.5-live-translate`, `gemini-3.5-transcribe` (WER 4.0% streaming, 85+ langs) | ✅ `vi` trong 97+ langs, accent consistency | Native audio-to-audio, async function calling, barge-in, WebSocket | ~$0.0368/min audio effective (3.5-live-translate); 3.8-live: audio in $3/1M tok (~$0.005/min), out $12/1M (~$0.018/min) | **PRIMARY candidate** — cùng vendor TenantAI LLM, function calling → tools của TenantAI, một billing relationship |
| Soniox | STT+TTS realtime, endpoint detection, language switching | ✅ VN specialist | Streaming STT→LLM→TTS pipeline (Pipecat/LiveKit compatible) | theo vendor | Strong alt nếu cần best-in-class VN STT; pipeline phức tạp hơn (3 services) |
| Blaze.vn | STT + realtime TTS WebSocket, VN voices | ✅ Vietnam-first | STT→OpenAI→TTS recipe | theo vendor | VN-local option, cần OpenAI riêng |
| MetaConnect realtime | WebSocket voice sessions, VAD→ASR→LLM→TTS server-side | ✅ `asr-vi`/`tts-vi` models (Whisper/Qwen/VieNeu) | Full turn sub-second, served từ VN | theo vendor | VN infra, mới — cần eval độ trưởng thành |
| Evom Labs | STT/TTS VN benchmark (WER 3.8%, TTFB 110ms) | ✅ | streaming | theo vendor | Benchmark tốt, cần verify production readiness |

## Đánh giá kỹ thuật

1. **Gemini Live là ứng viên số 1**: native speech-to-speech (không cần lắp STT+TTS riêng), hỗ trợ `vi`, async function calling → gọi được TenantAI tools/actions sau này (L6 Execute), cùng vendor với chat LLM → một API key/billing, preview status cần lưu ý.
2. **Kiến trúc adapter đúng hướng SRS**: `IAiVoiceSessionAdapter` → `GeminiLiveVoiceAdapter` trước; Soniox/Blaze là fallback swap mà không đổi widget.
3. **Khác biệt căn bản với Voice Service**: human-call = WebRTC P2P media + SignalR signaling; AI voice = WebSocket audio stream → provider → audio stream về. Không reuse media path của vanan-voice; chỉ reuse identity/metering/consent concepts.
4. **Latency target**: full-duplex speech-to-speech của Gemini Live có lợi thế lớn so với pipeline STT→LLM→TTS rời — barge-in/interruption native.

## Rủi ro

- Gemini Live còn preview → API có thể đổi; adapter pattern giảm lock-in.
- Cost voice ~$0.03-0.04/phút → cần metering `ai.voice.minute` Deny-enforced + tenant budget.
- Tiếng Việt accent regions (Bắc/Trung/Nam) cần test thật trước khi claim quality.

## Đề xuất

- **W6 primary: Gemini Live API** qua `VoiceProviderAdapter`.
- **W0/W4 spike sớm (không block)**: 1-2 ngày test Gemini Live với voice VN mẫu → đo latency/quality → ghi kết quả vào decision register trước khi commit W6.
- Alternatives documented để swap: Soniox (nếu Gemini Live fail), Blaze/MetaConnect (VN-local).

## Cần PO quyết

- [ ] Chấp nhận Gemini Live làm primary provider (preview risk)?
- [ ] Budget spike test sớm ở W4 hay chờ W6?
