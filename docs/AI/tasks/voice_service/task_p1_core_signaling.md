# Task P1 — CORE SIGNALING: Voice Service (`vanan-voice`)

> **Status:** DONE — 11/11/2026 · build Release sạch · **65/65 tests pass** (unit + integration + hub E2E)
> **Created:** 2026-11-11 · **Phase:** P1 — theo `docs/AI/plans/voice-service-master-plan.md`
> **Tài liệu gốc:** SRS v1.1 §3–7, §14
> **Phụ thuộc:** P0 ✅ (O2 auth REUSE, O3 SQLite, O6 context verify)

---

## KẾT QUẢ (11/11/2026)

- **Module**: `10_Voice/vanan-voice` — `VanAn.Voice.sln` (Api/Contracts/Domain/Infra/Tests) + Dockerfile (port 5070), mirror metering pattern, KHÔNG ref `1_Shared`/`3_CoreHub`.
- **Domain pure**: `CallStatus` (14 states), `CallStateMachine` (transition table + terminal + active set), `VoiceCall`/`VoiceCallEvent` (append-only), `CallEndReason`.
- **Infra**: SQLite + `EnsureVoiceSchema` (raw DDL idempotent — KHÔNG `EnsureCreated` production), **partial unique indexes** FR-007 (caller + receiver), `VoiceReconciliation` (stale > 5 phút → FAILED/SERVER_RESTART).
- **Api**: `VoiceHub` `/hubs/voice` (auth per-connection: staff JWT HS256 `Jwt:Secret` + customer token forward ShopERP `/me` — O2), `CallService` (StartCall idempotent by callId, accept first-wins, collision, busy, offline, reconnect ≤2/10s, ring 30s / negotiation 20s / max duration 120 min timers), `PermissionService` (self-call, caller-busy, kill-switch, verified context, open-calling), `MeteringClient` (fail-open G9, cache 30s, circuit breaker 5/60, caller `voice`), `TenantFeatureStore` (kill-switch + open-calling, in-memory P1 + admin endpoints), rate limits SEC-005, payload limits (SDP ≤64KB, ICE ≤8KB/64).
- **Metering (§14)**: deny check `voice.call`/`voice.minute` trước khi ring; ingest `events:batch` lúc terminal với **Idempotency-Key = callId** (voice.minute dùng `callId:min` — 2 events cùng key sẽ bị dedupe); fail-open khi metering chết.
- **Tests 65/65**: state machine (6), permission (8), metering client (6), staff JWT validator (4), CallService integration trên SQLite thật (17 — gồm race concurrent StartCall, reconciliation, reconnect, missed-call), hub E2E qua TestServer + SignalR WebSocket (3).

## Các quyết định phát sinh (P1)

| # | Quyết định | Lý do |
|---|---|---|
| P1-D1 | `NotifyConnected`/`NotifyReconnecting`/`NotifyReconnected` là client→server methods bổ sung (P2 SDK gọi) | SRS §6 không có trigger CONNECTED phía client — thêm tối thiểu, ghi nhận để P2 xác nhận |
| P1-D2 | Self-call / customer-no-context: KHÔNG insert row (event only) | `VoiceCall.Create` cấm caller==receiver; customer không có tenant → không đủ dữ liệu row hợp lệ (TenantId NOT NULL) |
| P1-D3 | Tenant flags (`VoiceFeatureEnabled`/`VoiceOpenCalling`) in-memory + admin endpoints GET/PUT `/v1/tenants/{t}/voice-config` | Không thêm bảng ngoài `voice_calls`/`voice_call_events` (SRS §21); persistence qua admin UI ở P4 |
| P1-D4 | E2E dùng `WebSocketFactory` + `Authorization: Bearer` header (TestServer bỏ query string) | Test-only; production SDK dùng query `access_token` (chuẩn SignalR) |
| P1-D5 | Idempotency-Key cho `voice.minute` = `callId:min` (khác `callId` của `voice.call`) | Dedupe `(CallerId, Key)` trong metering — 2 events cùng key = replay |

## Biết trước / giới hạn P1 (đưa tiếp sang P2/P4)

- `contextType=chat` verifier PG: SQL cấu hình `Voice:ContextVerifySql:chat` — **chưa pin schema thật** (P4 khi tích hợp chat sẽ xác minh Conversation/ConversationParticipant).
- Display name/avatar của caller: P1 trả `CallerUserId` (P4 resolve từ user records).
- User existence check (`FR-002 target exists`): chưa có lookup — P4 khi nối Gateway PG.

## Output

- Module chạy được (signaling hoàn chỉnh, không media) + 65 tests xanh + build Release sạch.
- Sẵn sàng P2 (WebRTC SDK) + P3 (Coturn) chạy song song.