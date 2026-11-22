# TASK W6: AI VOICE & BUSINESS AGENT

> Wave: W6 · Mode: IMPLEMENT (sau W4; có thể spike song song từ W4)
> Master plan §W6 · SRS v2.0 §10 (voice), §8 L6 Execute
> Depends on: W1–W4; `VOICE_PROVIDER_AUDIT.md` (W0)

## Sản phẩm giao được

> Khách nói chuyện với assistant bằng tiếng Việt (voice session realtime); assistant thực hiện nghiệp vụ được cấp quyền — đặt lịch, tạo đơn/booking — luôn qua approved flow + audit. Trợ lý số thật sự của SME.

## Scope IN

### AI Voice Adapter v1
- `IAiVoiceSessionAdapter` interface (provider-agnostic): capabilities, start/send/receive/stop/status.
- `VoiceProviderAdapter` implementation theo provider audit thắng (Gemini Live hoặc tương đương): STT→TenantAI reasoning→TTS, turn-taking, interruption.
- Voice UI trên link/widget (mic explicit gesture, visible state, end/mute control).
- Metering: `ai.voice.session`/`ai.voice.minute` rate cards.
- Consent: mic permission + purpose rõ; không ghi âm mặc định; retention theo policy.
- Fallback: voice fail → text chat vẫn hoạt động.

### L6 Execute (business agent)
- Assistant thực hiện nghiệp vụ được tenant cấp quyền: booking confirm, order draft, CRM update — mỗi action loại một contract riêng.
- Mọi execute action: policy check + explicit confirm/audit trail; reversible hoặc compensating action khi có thể.
- Multi-step workflow cơ bản (vd: hỏi → tư vấn → đặt lịch → xác nhận → notify staff).

## Scope OUT

- PSTN/SIP call-center — không bao giờ trong scope này
- Always-listening/background recording — không bao giờ
- AI tự ý commit giao dịch không confirm — không bao giờ

## Tasks

1. Voice provider integration theo audit (chọn 1 provider, adapter pattern sẵn swap).
2. Voice session lifecycle + UI states trên hosted page/widget.
3. Voice metering + consent + retention.
4. Execute action framework: action registry, policy check, confirm UX, audit.
5. Workflow đầu tiên (booking end-to-end qua approved flow).
6. Tests: voice session E2E (nếu testable), execute authorization matrix, consent, fallback text, regression.

## Acceptance / Gate

- [ ] Voice session: latency/quality đạt bar audit; tiếng Việt tự nhiên
- [ ] Mic chỉ khi explicit; visible state; end/mute hoạt động
- [ ] Execute actions luôn qua approved flow + audit; không silent commit
- [ ] Voice fail → text chat vẫn dùng được
- [ ] Không raw audio lưu mặc định
- [ ] Regression toàn bộ W1–W5 pass

## Risks

- Provider latency/quality không đạt → text-first vẫn là sản phẩm; voice là add-on.
- Cost voice cao → metering minute cap + tenant budget.
- Execute phức tạp → bắt đầu 1 workflow (booking) hoàn chỉnh, mở rộng sau.
