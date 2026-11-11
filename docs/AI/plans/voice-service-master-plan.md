# MASTER PLAN: VOICE SERVICE (`vanan-voice`)

> Created: 2026-11-11 — SRS v1.1 (`docs/requirements/B. SRS – Vạn An Voice Service v1.1.md`)
> Status: **P1→P5 READY (11/11/2026) — signaling + SDK + TURN + chat UI + metrics · dotnet 79/79 + node 18/18 · pilot checklist sẵn sàng (chờ VPS + user)**
> Code: folder **`10_Voice/`** trong monorepo — solution riêng `VanAn.Voice.sln`, module độc lập theo pattern `8_Metering/vanan-metering` + `9_TenantAI/vanan-ai`, KHÔNG reference `1_Shared`/`3_CoreHub`
> User directive (11/11/2026): module **tương đối độc lập**, dễ gắn vào module khác; **ưu tiên tích hợp chat storefront trước**

---

## 1. MỤC TIÊU

1. **Voice = module độc lập, attachable by contract** — SDK + auth token, host app không đụng internals (mirror Metering/AI pattern).
2. **P2P-first, TURN chỉ khi cần** — không SFU/SIP/LiveKit v1.1, không audio qua Gateway/CoreHub.
3. **Metering supervision** — `voice.call` + `voice.minute`, wallet gate, admin kill-switch per-tenant, fail-open (G9).
4. **Tích hợp đầu tiên: chat storefront (KhachLink)** — nút gọi trong conversation, `contextType=chat` verified.
5. **An toàn > doanh thu** — không cắt call giữa chừng; same-tenant; verified business context; default-deny.

---

## 2. HIỆN TRẠNG (đã xác minh sơ bộ 2026-11-11 — P0 sẽ xác minh đầy đủ)

| Thành phần | Hiện trạng | Vai trò trong plan |
|---|---|---|
| Module độc lập pattern | `8_Metering/vanan-metering` + `9_TenantAI/vanan-ai` — solution + contract + DB riêng | Mirror toàn bộ pattern |
| SignalR Gateway | `2_Gateway/Hubs/MessagingHub.cs`, `ChatHub.cs`, `NotificationsHub.cs`, `LocationHub.cs` | Pattern hub hiện có để tham khảo (voice dùng hub RIÊNG, không nhét vào Gateway) |
| Chat storefront | `5_WebApps/KhachLink/Components/ChatPanel.razor` + `Pages/Store.razor` (chat trong storefront); `2_Gateway/Controllers/RealtimeController.cs` (POST `/api/realtime/conversations/messages`) | Điểm tích hợp chat-first — cần P0 map model conversation + participant |
| Auth | Token Vạn An do Gateway cấp — **cơ chế validate/keys chưa xác minh** | Voice service reuse token (shared signing keys) — P0 xác minh trước khi chốt |
| Metering | Caller hiện có: `shoperp-a`, `gateway`, `directory`, `sysadmin` | Cần seed caller **`voice`** + rate card `voice.*` |
| Tenant/User/Order/Booking | Gateway PG source of truth | Voice verify context (chat conversation, order, booking) qua records có sẵn |
| WebRTC/Coturn | Không tồn tại trong repo | BUILD mới hoàn toàn |
| PWA | KhachLink WASM storefront, JS interop có sẵn | Nền tảng tích hợp đầu tiên |

---

## 3. QUYẾT ĐỊNH ĐÃ CHỐT (user)

| # | Quyết định |
|---|---|
| D1 | Voice = **module độc lập** `10_Voice/vanan-voice` (user 11/11/2026) — pattern Metering/AI; deviation "no new .csproj" được phê vì user chỉ đạo module tách (tiền lệ metering O4) |
| D2 | **Tích hợp đầu tiên = chat storefront (KhachLink)** — `contextType=chat`, conversation verified |
| D3 | P2P-first, Coturn fallback; KHÔNG SFU/SIP/LiveKit v1.1; không audio qua Gateway/CoreHub |
| D4 | Metering supervision: `voice.call` + `voice.minute`, Enforcement mặc định **Deny**, admin kill-switch `VoiceFeatureEnabled` per-tenant |
| D5 | An toàn > doanh thu: không cắt call giữa chừng vì hết credit; fail-open khi metering chết (G9) |
| D6 | Same-tenant only; TenantId từ token claims; ID/display name không bao giờ lấy từ client |

---

## 4. QUYẾT ĐỊNH KIẾN TRÚC (O1–O6 — P0 ĐÃ CHỐT 11/11/2026)

| # | Câu hỏi | Quyết định (P0 audit — đã xác minh) |
|---|---|---|
| O1 | Vị trí code | `10_Voice/vanan-voice` — solution riêng `VanAn.Voice.sln`, minimal API + SignalR hub riêng + client SDK; không ref `1_Shared`/`3_CoreHub` |
| O2 | Auth | **REUSE JWT HS256** (`Jwt:Secret` shared, issuer `VanAnShopERP`/audience `VanAnApi`, claims `sub`/`tenant_id`) cho staff + validator forward ShopERP `/api/customer-identity/me` cho customer; clone `IRealtimeTokenValidator` chain (đã có sẵn ở `2_Gateway/Realtime/`). Guest device KHÔNG gọi voice. Không sửa Gateway |
| O3 | DB | SQLite riêng của module + bootstrap DDL idempotent (mirror metering `EnsureP7Schema`) → PostgreSQL khi P6 Gate 3. Context verify: **read-only PG** (Conversation/ConversationParticipant/Orders/Bookings) |
| O4 | Signaling | SignalR hub **riêng của voice service** (`VoiceHub`, group = callId, 2 participant) — không dùng hub Gateway; không thêm voice logic vào Gateway |
| O5 | TURN creds | Voice service cấp qua Coturn TURN REST (HMAC), `TURN_SECRET` env-only, TTL 30 phút |
| O6 | Context verify | `chat`: Conversation tồn tại + cả 2 user là `ConversationParticipant` active; **tenant của call = `Conversation.TenantId`** (customer không có tenant claim). `order`/`booking`: record tồn tại + cả 2 là participant |

---

## 5. ROADMAP ↔ PHASES

| Phase | Nội dung | Deliverable | Gate |
|---|---|---|---|
| **P0** | Codebase Audit (KHÔNG code): auth/token scheme, SignalR conventions, chat storefront (conversation model + participant), DB conventions, metering caller/contract, PWA, infra. Ma trận EXISTING/MODIFY/MISSING/CONFLICT + chốt O2/O3/O6 | Task card + ma trận + quyết định | User approve |
| **P1** | Core signaling (module mới): skeleton `10_Voice/vanan-voice` (minimal API + SignalR hub + EF SQLite) · state machine · permission + context verify · `voice_calls`/`voice_call_events` + partial unique index · reconciliation · idempotency · **metering gate + ingest** | Service chạy, unit/integration tests pass, DB constraints verified | tests pass |
| **P2** | WebRTC P2P SDK (client): mic, RTCPeerConnection, SDP/ICE, mute, ICE restart, Opus mono 32–64k | 2 browser kết nối P2P local | tests + RV |
| **P3** | Coturn fallback: Ubuntu VPS, TURN REST creds, UDP/TCP/TLS, firewall doc, forced-TURN test, monitoring | TURN-only test pass, rollback procedure | RV |
| **P4** | PWA chat-first: tích hợp KhachLink (`ChatPanel.razor` + `Store.razor`) — nút call trong conversation · incoming UI · missed-call · admin kill-switch UI + wallet banner · iOS Safari | Voice trong chat conversation chạy production | E2E + RV |
| **P5** | Pilot: 10 merchants · 20–50 users · ≥100 calls · Wi-Fi + mobile | Metrics thật → re-baseline §12/§13 SRS | gate pilot (P2P ≥85%) |
| **P6** | Production rollout: Gate 1 (100) → Gate 2 (1.000) → Gate 3 (10.000); Redis khi scale ngang | Qua từng scaling gate, cost report | scaling gates |

**Dependency:** `P0 → P1 → (P2 ∥ P3) → P4 → P5 → P6` — P2 (client) và P3 (TURN) chạy **song song** sau P1. P4 chat-first ra trước, TimLaThay/Booking theo sau (P4 mở rộng).

---

## 6. THIẾT KẾ CHI TIẾT

### 6.1 Vị trí code

```
10_Voice/vanan-voice/
  src/
    VanAn.Voice.Api/            # minimal API + SignalR hub (signaling, state, metering client, TURN creds)
    VanAn.Voice.Domain/         # CallStateMachine, voice_calls/voice_call_events entities (domain riêng, sạch)
    VanAn.Voice.Infra/          # EF Core SQLite (GĐ-1) → PG (P6)
    VanAn.Voice.Contracts/      # DTO API contract (publish cho host app)
  sdk/
    VoiceSdk/                   # client SDK (theo convention frontend KhachLink: Blazor WASM + JS interop)
```

### 6.2 Luồng call (P1)

```
Caller (host app) → VoiceService.StartCall(callId, targetUserId, contextType=chat, contextRef)
  → validate token (O2) → permission rules (FR-002) → metering wallet check (cache 30s, fail-open)
  → deny ⇒ FAILED(METERING_DENIED | FEATURE_DISABLED)
  → callee offline ⇒ OFFLINE (missed-call flow)
  → RINGING → ACCEPTED (first-accept-wins, multi-device) → SDP/ICE → CONNECTED (P2)
  → end ⇒ persist terminal state + ingest usage events (Idempotency-Key = callId)
```

### 6.3 Tích hợp chat storefront (P4 — ưu tiên #1)

```
KhachLink ChatPanel.razor (conversation với merchant)
  → nút call → SDK.StartCall(..., contextType="chat", contextRef=conversationId)
  → voice service verify conversation tồn tại + cả 2 user là participant
  → merchant nhận incoming (app đang mở) hoặc missed-call trên lần mở sau
Không đổi MessagingHub/RealtimeController — voice là tính năng ghép thêm, chat giữ nguyên (regression Gate 4)
```

### 6.4 RV (theo runtime-verification rules — chạy sau mỗi phase có deploy)

- **L1:** image `vanan-voice:<sha>` + container healthy + `/v1/health` 200 (mirror metering L1).
- **L2:** StartCall → IncomingCall → Accept → CallConnected qua API thật (2 client test).
- **L3:** E2E production — call trong chat conversation KhachLink thật.
- **L4:** flow đầy đủ: call → usage event trong metering → wallet deduct → history.
- **L5:** manual (user).

### 6.5 Config env chính

`TURN_PUBLIC_HOST/UDP_PORT/TLS_PORT/SECRET/REALM/MIN_RELAY_PORT/MAX_RELAY_PORT/CREDENTIAL_TTL` · `Metering__BaseUrl/CallerId(=voice)/CallerKey` · `Voice:EstimatedMinutesPerCall` (3) · `VoiceFeatureEnabled` (tenant setting).

---

## 7. RỦI RO

| # | Rủi ro | Giảm thiểu |
|---|---|---|
| R-1 | Token scheme không dùng lại được (keys riêng, issuer khác) | **ĐÃ GIẢI QUYẾT (P0):** JWT HS256 `Jwt:Secret` shared + pattern validator có sẵn — REUSE, không cần fallback |
| R-2 | PWA không ring được khi app bị kill | Không hứa (SRS §9.4); missed-call + callback flow; không build push-to-answer v1.1 |
| R-3 | % TURN cao ở VN mobile (CGNAT) → chi phí vượt ước tính | Metric + alert >20% + cost review hàng tháng + sensitivity §13.2 SRS |
| R-4 | iOS Safari quirks (mic gesture-gated, background bị kill) | Test sớm ở P4; getUserMedia chỉ sau user action |
| R-5 | Metering chết / ví hết giữa chừng | Fail-open (G9); không cắt call giữa chừng (deduct sau call) |
| R-6 | Tích hợp chat làm regression MessagingHub/RealtimeController | Không sửa chat flow; E2E regression ở P4 (Gate 4); voice chỉ ghép UI + SDK |
| R-7 | DB race khi scale (FR-007) | Partial unique index ngay ở P1 (DB-level, không chỉ app-level) |

---

## 8. TASK CARDS

`docs/AI/tasks/voice_service/` — `task_p0_audit.md` · `task_p1_core_signaling.md` · `task_p2_webrtc_sdk.md` · `task_p3_coturn.md` · `task_p4_pwa_chat.md` · `task_p5_pilot.md` · `task_p6_rollout.md` (P5/P6 tạo chi tiết khi qua gate P4).