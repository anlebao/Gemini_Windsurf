# Task P4 — PWA CHAT-FIRST: Tích hợp KhachLink Storefront

> **Status:** DONE (code + tests) — 11/11/2026 · dotnet 77/77 · node 18/18 · UI.Platform build sạch · E2E spec sẵn sàng (validation env-dependent)
> **Created:** 2026-11-11 · **Phase:** P4 — theo `docs/AI/plans/voice-service-master-plan.md`
> **Tài liệu gốc:** SRS v1.1 §9.2–9.6, §14.6 (UX), FR-003/014
> **Ưu tiên #1 (user):** voice trong chat storefront

---

## KẾT QUẢ (11/11/2026)

### 1. Voice service — chat context + counterpart (server)

- **`contextType=chat` verify**: SQL pin theo schema PG thật (audit migration `20261110131115`): `Conversations` (Id, TenantId, SubjectType, SubjectId, unique TenantId+SubjectType+SubjectId) + `ConversationParticipants` (ConversationId, ParticipantId, RoleCode, IsActive). `Voice:ContextVerifySql:chat` = verify subject + cả 2 user là participant active + trả `Conversation.TenantId` (O6: tenant của call = tenant conversation).
- **Counterpart resolution (P4-D1)**: `StartCall` không cần targetUserId — `PermissionService.ResolveCounterpartAsync` lấy participant còn lại của conversation (SQL `chat:counterpart`), trả `TargetUserId` trong `PermissionResult`. Không resolve được → `INVALID_CONTEXT`.
- **Missed-calls endpoint hardening**: `GET /v1/missed-calls/{userId}` giờ cần auth + chỉ đọc events của chính user (403 nếu khác — sửa pitfall `Results.Forbid()` → 500 không auth scheme, dùng JSON 403 như metering rules).
- Config: `Voice:ContextVerifySql:chat` + `chat:counterpart` trong appsettings (split `SubjectType:SubjectId` từ contextRef).

### 2. UI.Platform — voice call trong chat (1 component phủ cả 2 app)

- **`wwwroot/js/voice-chat.js`**: bridge Blazor ↔ `window.vananVoice` — initialize (hub + TURN URL + token), callbacks → `[JSInvokable]` .NET (`OnVoiceStateChange/OnVoiceIncoming/OnVoiceLocalStream/OnVoiceRemoteStream/OnVoiceError`), `startCall(target, subjectType, subjectId)` (context `chat:{SubjectType}:{SubjectId}`), accept/reject/end/mute/getState/fetchMissedCalls/dispose.
- **`Components/Realtime/VoiceCallPanel.razor`** (+ .cs + .css): nút "Gọi thoại" (idle), overlay incoming (ringing: Nghe/Từ chối; calling: Hủy), active bar (connected: Tắt mic/Kết thúc), missed-call hint, error line. UI Platform components (VanAnButton), data-testid đầy đủ, `Enabled` gating (mặc định false → zero impact).
- **`RealtimeChatPanel.razor`**: +params `VoiceCallEnabled`/`VoiceHubUrl`/`VoiceServiceUrl`; render `<VoiceCallPanel>` sau input row. Token truyền: `IsStaff ? StaffToken : CustomerToken`.

### 3. Flag + wiring

- `ShopFeatureSettingsDto.Voice_Call_Enabled` (1_Shared/Services) + ShopERP `ShopFeatures.razor` checkbox "Bật gọi thoại trong chat (P4)" + KhachLink `ChatPanel.razor` đọc flag → `VoiceCallEnabled` + param `VoiceServiceUrl` (host cấu hình URL voice service).

### 4. Tests

- **dotnet 77/77** (+`MissedCallsEndpointTests` 3: 401/403/200 · +`CallService` 2: counterpart resolve → RINGING + target đúng; không resolve → INVALID_CONTEXT).
- **node 18/18** (+`voice-chat.test.mjs` 4: initialize wires hub+TURN+callbacks→.NET; startCall chat context; accept/reject/end/mute bridge; fetchMissedCalls fail-open).
- **E2E `voice-chat.spec.ts`** (Gate 4): UI assertions (button gating theo flag, incoming overlay accept/reject) — chạy validation phase với `VOICE_CHAT_TEST_URL`.

## Quyết định P4

| # | Quyết định | Lý do |
|---|---|---|
| P4-D1 | Chat context dùng `SubjectType:SubjectId` làm contextRef + server resolve counterpart | Panel không biết conversation id/staff userId; 1 nút call cho UX "gọi người kia trong chat" |
| P4-D2 | Voice UI gắn trong `RealtimeChatPanel` (UI.Platform) | 1 component phủ KhachLink storefront + ShopERP inbox (SRS §9.2 "không duplicate") |
| P4-D3 | Flag `Voice_Call_Enabled` riêng (không dùng chung `Voice_Note_Enabled`) | Voice note (STT) ≠ voice call (P2P) — semantics khác, admin bật riêng |

## Giới hạn / còn lại

- `VoiceServiceUrl`/`VoiceHubUrl` host phải cấu hình URL thật (P5 deploy wire).
- Bản thân UI chưa attach `<audio>` element cho remote stream (P4 chỉ state UI; audio attach ở demo/host — P5 hoàn thiện).
- Admin kill-switch UI đầy đủ (`/admin/voice` + wallet banner): voice service đã có endpoints (`/v1/tenants/{t}/voice-config`); UI trang admin tách P5 cùng deploy.
- E2E media flow (P2P audio) vẫn chờ môi trường WebRTC ICE hoạt động (ghi ledger P2).