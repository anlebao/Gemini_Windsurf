# Task P0 — AUDIT: Voice Service (`vanan-voice`)

> **Status:** DONE — audit hoàn tất 11/11/2026, quyết định O2/O3/O6 ĐÃ CHỐT, chờ user approve để bắt P1
> **Created:** 2026-11-11 · **Phase:** P0 — theo `docs/AI/plans/voice-service-master-plan.md`
> **Tài liệu gốc:** SRS v1.1 (`docs/requirements/B. SRS – Vạn An Voice Service v1.1.md`)
> **Phương pháp:** xác minh trực tiếp trong code (file + dòng), không suy đoán

---

## 1. KẾT QUẢ AUDIT (đã xác minh)

### 1.1 Auth / token — O2 ✅ CHỐT

| Hạng mục | Hiện trạng (đã verify) | Quyết định |
|---|---|---|
| Staff JWT | HS256, secret `Jwt:Secret` (≥32 ký tự, env `Jwt__Secret`, có `ValidateProductionConfig` chặn sentinel), issuer `VanAnShopERP`, audience `VanAnApi`. Claims: **`sub`** (userId), `email`, `role`, **`tenant_id`** (snake_case, "system" cho SystemAdmin). Phát hành bởi `3_CoreHub/Services/JwtTokenService.cs` (L13, L50, L89) — cùng secret với ShopERP, cross-validate | **REUSE — voice service validate JWT bằng chính `Jwt:Secret`** (HS256, cùng issuer/audience) — không cần sửa Gateway, không duplicate auth |
| Customer token | `X-Customer-Token` → forward ShopERP `/api/customer-identity/me` → `CustomerId` (`2_Gateway/Realtime/CustomerTokenValidator.cs` L32-43). KHÔNG có tenant claim | **REUSE pattern — voice service có validator riêng, forward sang ShopERP `/me`**; tenant của call lấy từ context verified (O6), không từ token |
| Guest device | `customerDeviceId` (localStorage guid) | **KHÔNG cho gọi voice** (FR-001: mọi caller phải authenticated; guest chỉ chat HTTP) |
| SignalR hub auth | Pattern có sẵn: `RealtimeRequestReader` (query `access_token`/`customerToken` + header), `RealtimeIdentityResolver` → chain `IRealtimeTokenValidator` → `RealtimeIdentity(UserId, Kind, TenantId?, DeviceId?)` (`2_Gateway/Realtime/`) | **Clone pattern cho `VoiceHub`** — mỗi connection resolve identity lúc `OnConnectedAsync` |

### 1.2 SignalR conventions

| Hạng mục | Hiện trạng | Ghi chú cho voice |
|---|---|---|
| Hub pattern | `MessagingHub` `/hubs/messaging` (`2_Gateway/Hubs/MessagingHub.cs`) — subject-agnostic, Join gated bằng keyed `IRealtimeParticipantAuthorizer` (default deny), group qua `RealtimeGroups` (msg_{type}_{id}; shop subject: customer join group riêng per-user L60-62) | VoiceHub không cần groups rộng — **group = callId, chỉ 2 participant** |
| Presence | Không có presence store riêng — presence = connection lifecycle (đúng thiết kế SRS §0 row 4) | Voice dùng connection registry riêng của mình |

### 1.3 Chat storefront — O6 ✅ CHỐT

| Hạng mục | Hiện trạng (đã verify) | Quyết định |
|---|---|---|
| Điểm tích hợp UI | `UI.Platform/Components/Realtime/RealtimeChatPanel.razor` (subject-agnostic, dùng `IRealtimeChatClient` → Gateway `/api/realtime/conversations/*`, SignalR qua `IRealtimeEndpointProvider`); `KhachLink/Components/ChatPanel.razor` = **legacy shim** → RealtimeChatPanel. **Đã có sẵn param `VoiceEnabled`** (voice note, flag `Voice_Note_Enabled` qua `ShopFeatureSettingsHttpService`) | **P4 gắn nút call vào `RealtimeChatPanel`** (cạnh input row) — 1 component phủ cả KhachLink + ShopERP; không sửa ChatPanel shim |
| Conversation model | `1_Shared/Domain.cs` L5177 `Conversation` (TenantId, SubjectType, SubjectId, CustomerId/ShipperId) + L5273 `ConversationParticipant` (ConversationId, ParticipantId, RoleCode, IsActive) | **Verify `contextType=chat`: Conversation tồn tại (Id) + cả 2 user là `ConversationParticipant` active** — query trực tiếp, hoặc tái dùng logic `RealtimeAuthorizerLookup.CanAccessAsync` |
| Tenant của call | Customer token không có tenant; staff có `tenant_id` | **Tenant của call = `Conversation.TenantId`** khi context=chat (shop tenant) — giải quyết triệt để case customer |
| Chat flow | Message gửi qua HTTP `RealtimeController` → service → push group; `MessagingHub` chỉ subscribe | **KHÔNG đụng** — voice là tính năng ghép thêm |

### 1.4 DB — O3 ✅ CHỐT

| Hạng mục | Hiện trạng | Quyết định |
|---|---|---|
| Pattern module độc lập | `8_Metering/vanan-metering/src/VanAn.Metering.Api/Program.cs`: `EnsureP7Schema(db)` (idempotent DDL, L194) + seed bootstrap callers (L30-38) | **Voice mirror toàn bộ**: SQLite riêng + bootstrap DDL idempotent; KHÔNG `EnsureCreated` production |
| Đọc data context (order/booking/conversation) | Entities trong `1_Shared/Domain.cs`, DB Gateway PG | **Read-only PG** (dedicated connection, chỉ map bảng cần: Conversation/ConversationParticipant/Orders/Bookings) — không sửa Gateway, không shared code. Fallback nếu không chấp nhận: endpoint verify read-only trên Gateway (vẫn không phải voice logic) — KHÔNG cần vì read-only DB là khả thi |

### 1.5 Metering

| Hạng mục | Hiện trạng | Quyết định |
|---|---|---|
| Client pattern | `2_Gateway/Services/MeteringApiClient.cs`: typed HttpClient timeout 3s, circuit breaker 5-fail/60s, fail-open (không throw), self-disable khi thiếu config, BaseAddress trailing `/`, header `X-Caller-Id`/`X-Metering-Key`, `Idempotency-Key` bắt buộc | **Voice có thin client riêng** (mirror, không dùng DLL chung — convention per-host D2; voice độc lập nên không ref `1_Shared`) |
| Caller | Bootstrap: `Metering:BootstrapCaller` + `Metering:BootstrapAdmin` + `Metering:BootstrapCallers` (`id:key` phân cách `;`) | **Seed caller `voice`** qua `Metering:BootstrapCallers` trong env deploy + tạo rate card `voice.call`/`voice.minute` (Enforcement=Deny) |

### 1.6 PWA / frontend

| Hạng mục | Hiện trạng | Quyết định |
|---|---|---|
| KhachLink | WASM, dùng UI.Platform components; feature flags qua `ShopFeatureSettingsHttpService` (`Voice_Note_Enabled` precedent — platform toggle) | `voice_open_calling`/`VoiceFeatureEnabled` theo pattern flag sẵn có |
| SignalR client | `IRealtimeEndpointProvider` (`/hubs/messaging`) — SDK sẽ có endpoint provider riêng trỏ `/hubs/voice` | Voice SDK dùng cùng pattern |

### 1.7 Infra

| Hạng mục | Hiện trạng | Quyết định |
|---|---|---|
| Deploy | `cd-multivps.yml`: impact → deploy_set `gateway, khachlink, shoperp, directory, crawler, metering, tenantai`; matrix build image (metering: `8_Metering/vanan-metering/Dockerfile` → `vanan-metering`); step verify secrets `VPS_METERING_*` | Thêm `voice` vào deploy_set + matrix (`10_Voice/vanan-voice/Dockerfile` → `vanan-voice`) + secrets `VPS_VOICE_*` |
| Coturn | Chưa tồn tại | VPS mới (public IP, UDP relay ports) — firewall doc + rollback ở P3 |

## 2. MA TRẬN EXISTING / MODIFY / MISSING / CONFLICT (tóm tắt)

| Nhóm | Kết luận |
|---|---|
| Auth JWT staff + customer token | **EXISTING** — voice clone validator chain (không sửa Gateway) |
| SignalR hub + identity resolver pattern | **EXISTING (pattern)** — clone sang `VoiceHub` |
| Conversation/participant (context verify) | **EXISTING** — read-only query |
| Chat UI (`RealtimeChatPanel`) | **MODIFY** (P4) — thêm nút call + incoming UI (param `VoiceEnabled` có sẵn) |
| Metering client + caller | **EXISTING (pattern)** — thin client mới + seed caller `voice` |
| Voice module, state machine, DB voice | **MISSING** — build P1 |
| WebRTC SDK, Coturn | **MISSING** — build P2/P3 |
| Gateway | **KHÔNG ĐỤNG** — không voice logic trong Gateway |
| CONFLICT | Không phát hiện |

## 3. QUYẾT ĐỊNH ĐÃ CHỐT (P0)

| # | Quyết định | Lý do |
|---|---|---|
| O2 | **REUSE** JWT HS256 (`Jwt:Secret` shared) cho staff + validator forward ShopERP `/me` cho customer; clone `IRealtimeTokenValidator` chain | Đã có sẵn toàn bộ pattern (Realtime platform); không duplicate auth, không sửa Gateway |
| O3 | SQLite riêng + bootstrap DDL idempotent (mirror metering `EnsureP7Schema`) | Convention module độc lập đã chứng minh (metering) |
| O6 | Verify context = read-only query Conversation + ConversationParticipant; tenant của call = Conversation.TenantId | Customer không có tenant claim — context là nguồn tenant duy nhất đáng tin |

## 4. Danh mục hook chat (P4)

1. `UI.Platform/Components/Realtime/RealtimeChatPanel.razor` — nút call + incoming UI (1 component phủ KhachLink + ShopERP).
2. `IRealtimeChatClient` — lấy conversationId từ subject (type,id) làm `contextReferenceId`.
3. `ShopFeatureSettingsHttpService` — flag `voice_open_calling`/`VoiceFeatureEnabled` theo pattern `Voice_Note_Enabled`.
4. Không sửa: `MessagingHub`, `RealtimeController`, chat services.

## 5. Config checklist (tạo ở P1/P3)

- Voice: `Jwt:Secret/Issuer/Audience` (cùng giá trị Gateway), `Voice:EstimatedMinutesPerCall=3`, `Voice:DB` path.
- Metering: `Metering__BaseUrl` (trailing `/`), `Metering__CallerId=voice`, `Metering__CallerKey` (mới), seed vào `Metering:BootstrapCallers`.
- TURN (P3): `TURN_PUBLIC_HOST/UDP_PORT/TLS_PORT/SECRET/REALM/MIN_RELAY_PORT/MAX_RELAY_PORT/CREDENTIAL_TTL=1800`.
- Deploy: secrets `VPS_VOICE_*`; deploy_set `voice`.

## 6. Output chuyển giao P1

- Skeleton `10_Voice/vanan-voice` (minimal API + `VoiceHub` + EF SQLite + health) — mirror metering P1.
- State machine + permission (O2/O6) + 2 bảng + partial unique index + reconciliation + idempotency.
- Metering gate + ingest (caller `voice`, fail-open, Idempotency-Key=callId).
- Unit/integration tests (danh sách trong `task_p1_core_signaling.md`).