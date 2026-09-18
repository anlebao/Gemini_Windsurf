# TASK CARD — Realtime Platform: Chat + Live Location (Reusable Across Modules)

> **Status:** ✅ **COMPLETE** — **P1-P5 DONE + DEPLOYED + RV** · **P6 DONE** (2026-09-18: E2E 12/12 PASS trên production + RV L1-L3 + reuse guide). Xem Section 18.10.
> **Review P2-P6 (2026-09-17):** 11 findings (F1-F11) — xem Section 20. Quyết định bổ sung: consumer = **Shop chat trên `/store/{slug}`** (không phải Logistics/JobMarket — xem F1); GPS trên trang shop = **map tĩnh + khoảng cách** (không realtime).
> **P1 delivered:** D1 (Google customer sync) · D2/D3 (buyer tracking endpoint + map render) · D4 (checkout coords) · D5 (GPS resume) · D6 (guest chat/tracking via device id). Build 0 errors · 40/40 chat+delivery tests PASS · CI ALL PASSED · CD Multi-VPS SUCCESS.
> **RV:** L1 API ✅ · L2 WASM ✅ · L3 Playwright guest UI ✅ · L4 guest send flow ✅ · L5 manual pending (user).
> **Priority:** P1 (chat + GPS đang chết trên production) → P2 (tái sử dụng)
> **Branch đề xuất:** `feature/realtime-platform`
> **Mode:** P0 ANALYZE → P1 FIX_ONLY → P2..P5 IMPLEMENT
> **Decisions (2026-09-17):** Q1 guest **CÓ** chat + tracking (auth bằng `X-Customer-Device-Id`) · Q2 Domain additive **APPROVED** · Q3 consumer thứ 2 = **CẢ HAI** (Logistics + JobMarket)
> **Supersedes/extends:** `docs/AI/tasks/community_commerce_fixes/task_card_05_chat_gps_qr.md` (fix `26dc9e62` chỉ vá phía client, không đủ)
> **Workflow:** `.devin/workflows/newfeaturebuild.md` (ANALYZE → IMPLEMENT) + `.devin/workflows/Fix_Errors.md` (P1)

---

## 1. GOAL & CONTEXT

- **Mục tiêu cốt lõi:** Nâng cấp 2 tính năng đang có — **chat 2 chiều realtime** (UC-07) và **theo dõi vị trí realtime** (UC-06) — từ *hard-code cho Order/Shipper* thành **một năng lực nền tảng tái sử dụng** (Realtime Platform), để bất kỳ module nào cũng gắn vào được: Logistics (shipment), JobMarket (employer ↔ candidate), ShopERP (staff ↔ shipper), Directory, module tương lai.
- **Nghiệp vụ áp dụng:** mọi luồng có "2 bên cần trao đổi + theo dõi vị trí/trạng thái theo một chủ thể" (đơn hàng, chuyến giao, tin tuyển dụng, ticket hỗ trợ, lịch hẹn onsite…).
- **Kết quả bàn giao:**
  1. Chat + GPS **chạy đúng trên production** (fix 5 defect chặn hiện tại).
  2. **Realtime Platform** = Domain generic + Service generic + Gateway hubs generic + UI Platform components generic + JS generic.
  3. **Reuse recipe 5 bước** để module mới gắn vào (Section 12).
  4. 1 module thứ hai dùng thật (Logistics hoặc JobMarket) làm bằng chứng tái sử dụng.

---

## 2. VÌ SAO CẦN CARD NÀY (hiện trạng đã verify trong code)

### 2.1. Chat/GPS production chết dù đã "fix done"

| # | Defect | Bằng chứng (file:line) | Ảnh hưởng |
|---|---|---|---|
| D1 | **Order không gắn `CustomerId`** với khách Google OAuth → `ChatService` từ chối tạo conversation, `LocationHub` từ chối join | `5_WebApps/ShopERP/Controllers/SocialAuthController.cs:96-104` (tạo Customer ở SQLite, **không** phát `CustomerCreated`) · `2_Gateway/Services/DataSyncSubscriber.cs:378-405` (đường ghi PG duy nhất) · `3_CoreHub/Services/OrderService.cs:831-849` (âm thầm set `CustomerId=null`) · `3_CoreHub/Services/ChatService.cs:48-59` · `2_Gateway/Hubs/LocationHub.cs:56-72` | **Chat + GPS chết cho khách đã đăng nhập** |
| D2 | Trang khách build map bằng endpoint **shipper-only** | `5_WebApps/KhachLink/Pages/OrderTracking.razor:368-400` gọi `GET /api/community/nearby-orders` · `2_Gateway/Controllers/CommunityController.cs:157-171` + `999-1012` → **403 "Bạn không có quyền Shipper"** | Khách không bao giờ thấy map |
| D3 | Handler `LocationUpdate` **không set `_showMap = true`** | `5_WebApps/KhachLink/Pages/OrderTracking.razor:416-423` | Có toạ độ vẫn không render map |
| D4 | Checkout **luôn gửi `DeliveryLat/Lng = null`** | `5_WebApps/KhachLink/Pages/Checkout.razor:606-610` | Shipper không thấy marker khách |
| D5 | GPS chỉ start khi bấm "Đã lấy hàng"; reload là mất | `5_WebApps/KhachLink/Pages/DeliveryTracking.razor:363-377` + `425-443` | Không có ping → không ai thấy ai |
| D6 | Guest không có chat/tracking | `docs/user-guide/community-commerce/07-customer.md` (FAQ cũ) | ❌ **SAI theo quyết định mới** — guest PHẢI có chat + tracking (auth bằng `CustomerDeviceId`). Docs đã sửa 2026-09-17. |

### 2.2. Vì sao KHÔNG tái sử dụng được (coupling)

| # | Coupling hiện tại | Nơi | Hệ quả |
|---|---|---|---|
| C1 | `Conversation(OrderId, ShipperId, CustomerId)` — 2 bên cố định theo Order | `1_Shared/Domain.cs:3963-4004` | Module khác không dùng được (JobMarket cần Employer/Candidate) |
| C2 | `DeliveryTracking(DeliveryTaskId, Lat, Lng)` — khoá theo DeliveryTask | `1_Shared/Domain.cs:3940-3961` | Không track được shipment/ticket/lịch hẹn |
| C3 | Hubs hard-code `order_{orderId}` / `chat_{orderId}` + auth qua `customerToken` → ShopERP `/me` | `2_Gateway/Hubs/LocationHub.cs:47-76` · `ChatHub.cs:43-79` · `Program.cs:801-802` | Chỉ nhận customer token; không nhận staff JWT/device token |
| C4 | Endpoint nằm trong `CommunityController` (`api/community/*`) + check role Shipper | `2_Gateway/Controllers/CommunityController.cs:417-543` | Không có API trung tính cho module khác |
| C5 | `ChatPanel.razor` + `ChatHttpService.cs` + `LocationTrackingService.cs` nằm trong **KhachLink** | `5_WebApps/KhachLink/Components/ChatPanel.razor` · `Services/Http/ChatHttpService.cs` · `Services/LocationTrackingService.cs` | ShopERP/Logistics/JobMarket không dùng lại được |
| C6 | `LeafletMap.razor` nằm trong **KhachLink**, không thuộc UI Platform | `5_WebApps/KhachLink/Components/LeafletMap.razor` | Vi phạm UI Platform rule khi module khác cần map |
| C7 | JS phụ thuộc `vananPWA.*` của KhachLink | `5_WebApps/KhachLink/wwwroot/js/pwa.js:606-633` | App khác không có `vananPWA` |
| C8 | Derive Gateway host thủ công cho chat | `5_WebApps/KhachLink/Components/ChatPanel.razor:105-119` | Sai host với custom domain |

---

## 3. AS-IS ARCHITECTURE MAP

```
CHAT
  Domain    1_Shared/Domain.cs:3963  Conversation(OrderId, ShipperId, CustomerId)
                                     Message(ConversationId, SenderId, Content, SentAt, IsRead)
  Service   3_CoreHub/Services/IChatService.cs | ChatService.cs
  Hub       2_Gateway/Hubs/ChatHub.cs            → /hubs/chat   (group chat_{orderId})
  HTTP      2_Gateway/Controllers/CommunityController.cs:463-543
  UI        5_WebApps/KhachLink/Components/ChatPanel.razor
            5_WebApps/KhachLink/Services/Http/ChatHttpService.cs
  Pages     KhachLink DeliveryTracking.razor:234 | OrderTracking.razor:202

GPS / LIVE LOCATION
  Domain    1_Shared/Domain.cs:3863  DeliveryTask(OrderId, ShipperId, shop/delivery coords)
            1_Shared/Domain.cs:3943  DeliveryTracking(DeliveryTaskId, Lat, Lng, RecordedAt) [append-only]
  Service   3_CoreHub/Services/DeliveryWorkflowService.cs:102-132
  Hub       2_Gateway/Hubs/LocationHub.cs        → /hubs/location (group order_{orderId})
  HTTP      CommunityController.cs:417-457  POST /api/community/location/update
  UI        5_WebApps/KhachLink/Services/LocationTrackingService.cs  (poll 10s)
            5_WebApps/KhachLink/Components/LeafletMap.razor
  JS        KhachLink/wwwroot/js/pwa.js (getCurrentPosition, scrollToBottom)
            KhachLink/wwwroot/js/leaflet.js + /lib/leaflet/
  nginx     /hubs/ → gateway (vanan.multivps.conf.template:283, :601)

DB MAPPING (quan trọng cho migration)
  PG (VanAnDbContext)         DbSet Conversation/Message/DeliveryTracking  → 3_CoreHub/Infrastructure/VanAnDbContext.cs:142-145
  ShopERP (ShopERPDbContext)  modelBuilder.Ignore<Conversation/Message/DeliveryTracking>  → 5_WebApps/ShopERP/Infrastructure/ShopERPDbContext.cs:253-255
  ⇒ Schema thay đổi CHỈ cần migration ở PG (Gateway/CoreHub).
```

---

## 4. TO-BE ARCHITECTURE (REALTIME PLATFORM)

```
┌─ 1_Shared (Domain, PURE) ─────────────────────────────────────────────┐
│ RealtimeSubjectType { Order, Shipment, JobApplication, Ticket, Custom }│
│ Conversation            + SubjectType, SubjectId (additive, giữ cột cũ)│
│ ConversationParticipant (NEW)  ConversationId, ParticipantId, RoleCode │
│ Message                 (không đổi)                                    │
│ DeliveryTracking        + SubjectType, SubjectId, TrackerId (additive) │
└────────────────────────────────────────────────────────────────────────┘
┌─ 3_CoreHub (Services) ────────────────────────────────────────────────┐
│ IRealtimeMessagingService  EnsureConversation / Send / History / Read  │
│ ILiveLocationService       RecordPing / GetLatest / GetHistory         │
│ IRealtimeParticipantAuthorizer (interface)  ← module tự implement      │
│   └ OrderRealtimeAuthorizer (adapter cho community, giữ nguyên logic)  │
│ IChatService / IDeliveryWorkflowService  → giữ làm adapter mỏng        │
└────────────────────────────────────────────────────────────────────────┘
┌─ 2_Gateway (Hubs + API) ──────────────────────────────────────────────┐
│ IRealtimeTokenValidator  → CustomerTokenValidator | StaffJwtValidator  │
│                            | DeviceTokenValidator                       │
│ MessagingHub  /hubs/messaging   group msg_{subjectType}_{subjectId}    │
│ TrackingHub   /hubs/tracking    group loc_{subjectType}_{subjectId}    │
│ /api/realtime/conversations/{subjectType}/{subjectId}                  │
│ /api/realtime/conversations/messages                                   │
│ /api/realtime/location/ping                                            │
│ /api/realtime/location/{subjectType}/{subjectId}/latest                │
│ (giữ /hubs/chat, /hubs/location, /api/community/* làm adapter cũ)      │
└────────────────────────────────────────────────────────────────────────┘
┌─ UI.Platform (shared, mọi app dùng) ──────────────────────────────────┐
│ Core/Interfaces  IRealtimeChatClient | ILiveLocationClient | IMapJsAdapter
│ Adapters         RealtimeHttpAdapter | LeafletMapAdapter               │
│ Components/Realtime  RealtimeChatPanel.razor | VanAnMap.razor          │
│ wwwroot/js       realtime.js (geolocation + scroll) + leaflet/ vendored│
│                  → phục vụ tại _content/VanAn.UI.Platform/js/...       │
└────────────────────────────────────────────────────────────────────────┘
```

---

### 4.1. Guest identity (device-based) — QUYẾT ĐỊNH 2026-09-17

Guest không có `X-Customer-Token`. Danh tính guest = **`CustomerDeviceId`** (localStorage `customer_device_id`, đã gửi kèm mọi checkout → `Order.CustomerDeviceId`).

| Hạng mục | Thiết kế |
|---|---|
| Header | `X-Customer-Device-Id: <guid>` (thay/bổ sung `X-Customer-Token`) |
| Gateway validator | `DeviceTokenValidator` — resolve `Order.CustomerDeviceId == deviceId` (hoặc `Conversation`/`DeliveryTask` suy ra từ order) |
| Participant id | Guest dùng chính `deviceId` (Guid) làm `ParticipantId` — nhất quán với `SenderId`/`TrackerId` |
| Chat | `ConversationParticipant.RoleCode = "Guest"`; guest gửi/nhận tin như customer thường |
| Tracking | Guest join `loc_Order_{orderId}` nếu `Order.CustomerDeviceId == deviceId` |
| Bảo mật | Chỉ đơn của **chính thiết bị đó**; không suy đoán được đơn thiết bị khác; `CustomerDeviceId` là GUID ngẫu nhiên 128-bit (không đoán được) |
| Nâng cấp | Guest login sau → `CustomerMergeService` gắn `CustomerId`; conversation/participant giữ nguyên (backfill `ParticipantId` device → customer nếu cần) |

> Rủi ro chấp nhận: thiết bị bị mất/đổi localStorage → mất quyền xem đơn cũ. PoC chấp nhận (đã có order history fallback theo device id).

---

## 5. DOMAIN CHANGES (✅ APPROVED 2026-09-17 — Domain Phase active)

> Hard stop: Domain phải PURE (no EF/DbContext/DataAnnotations). Chỉ sửa khi là phần của plan đã duyệt. `AccountingEntry` không liên quan.
> Single-Identity: mọi entity mới dùng `BaseEntity.Id` làm PK; business key VO (nếu có) phải `builder.Ignore(...)`.

| # | Thay đổi | Chi tiết | Migration |
|---|---|---|---|
| DOM-1 | `RealtimeSubjectType` enum | `Order, Shipment, JobApplication, Ticket, Custom` | không |
| DOM-2 | `Conversation` + `SubjectType` (string, default `"Order"`) + `SubjectId` (Guid, default = `OrderId`) | **Additive** — giữ `OrderId/ShipperId/CustomerId` để không phá code cũ; thêm ctor generic `Conversation(tenantId, subjectType, subjectId, initiatorId, counterpartId)`; `AssignCounterpart(...)` generic | PG: `AddRealtimeSubjectToConversation` |
| DOM-3 | `ConversationParticipant` (NEW) | `ConversationId`, `ParticipantId`, `RoleCode`, `JoinedAt`, `IsActive`; ctor set `Id` theo Single-Identity | PG: `AddConversationParticipant` |
| DOM-4 | `DeliveryTracking` + `SubjectType` (default `"Delivery"`), `SubjectId` (default = `DeliveryTaskId`), `TrackerId` (Guid?) | Additive, vẫn append-only (không thêm update method) | PG: `AddSubjectToDeliveryTracking` |
| DOM-5 | (Tuỳ chọn) `Message` + `AttachmentUrl` | Chỉ khi module mới cần ảnh/file | PG: `AddMessageAttachment` |

**Acceptance DOM:** build 0 lỗi · `guard-check.ps1` PASS · Domain purity test PASS · migration áp dụng sạch trên PG.

---

## 6. SERVICE CHANGES (3_CoreHub)

| # | File | Thay đổi |
|---|---|---|
| SVC-1 | `3_CoreHub/Services/IRealtimeMessagingService.cs` (NEW) | `EnsureConversationAsync(RealtimeSubjectType, Guid subjectId, Guid initiatorId, Guid counterpartId)` · `SendMessageAsync(Guid conversationId, Guid senderId, string content)` · `GetHistoryAsync(Guid conversationId, int take)` · `MarkAsReadAsync(Guid messageId)` · `IsParticipantAsync(Guid conversationId, Guid userId)` |
| SVC-2 | `3_CoreHub/Services/RealtimeMessagingService.cs` (NEW) | Impl generic trên `Conversations`/`ConversationParticipants`/`Messages`; `IgnoreQueryFilters` cross-tenant như `ChatService` hiện tại |
| SVC-3 | `3_CoreHub/Services/ILiveLocationService.cs` (NEW) | `RecordPingAsync(RealtimeSubjectType, Guid subjectId, Guid trackerId, double lat, double lng)` · `GetLatestAsync(...)` · `GetHistoryAsync(...)` |
| SVC-4 | `3_CoreHub/Services/LiveLocationService.cs` (NEW) | Ghi `DeliveryTracking` với `SubjectType/SubjectId/TrackerId` (backward-compatible: `Order` type → `SubjectId = DeliveryTaskId`) |
| SVC-5 | `3_CoreHub/Services/IRealtimeParticipantAuthorizer.cs` (NEW) | `Task<bool> CanAccessAsync(RealtimeSubjectType type, Guid subjectId, Guid userId)` — module tự cài |
| SVC-6 | `3_CoreHub/Services/Adapters/OrderRealtimeAuthorizer.cs` (NEW) | Di chuyển logic access hiện tại của `ChatHub.JoinConversation` + `LocationHub.JoinOrderTracking` (DeliveryTask.ShipperId ∨ Conversation participant ∨ Order.CustomerId) |
| SVC-7 | `ChatService.cs` / `DeliveryWorkflowService.cs` | Giữ nguyên public API; thân hàm gọi service generic (adapter mỏng) để không phá callers/tests |

**DI:** đăng ký trong `2_Gateway/Program.cs` cạnh dòng 447-451 (CommunityOrderService / DeliveryWorkflowService / ChatService).

---

## 7. GATEWAY CHANGES (Hubs + API)

| # | File | Thay đổi |
|---|---|---|
| GW-1 | `2_Gateway/Hubs/MessagingHub.cs` (NEW) | Group `msg_{subjectType}_{subjectId}`; auth = `IRealtimeTokenValidator`; join verify qua `IRealtimeParticipantAuthorizer`; push `ReceiveMessage` |
| GW-2 | `2_Gateway/Hubs/TrackingHub.cs` (NEW) | Group `loc_{subjectType}_{subjectId}`; push `LocationUpdate`; join verify như trên |
| GW-3 | `2_Gateway/Realtime/IRealtimeTokenValidator.cs` + 3 impl (NEW) | `CustomerTokenValidator` (forward ShopERP `/me` — logic hiện có) · `StaffJwtValidator` (JWT Bearer ShopERP) · **`DeviceTokenValidator` (guest — header `X-Customer-Device-Id`, đối chiếu `Order.CustomerDeviceId`)** |
| GW-4 | `2_Gateway/Controllers/RealtimeController.cs` (NEW) | `POST /api/realtime/conversations/messages` · `GET /api/realtime/conversations/{subjectType}/{subjectId}` · `POST /api/realtime/location/ping` · `GET /api/realtime/location/{subjectType}/{subjectId}/latest` |
| GW-5 | `2_Gateway/Program.cs` | `MapHub<MessagingHub>("/hubs/messaging")`, `MapHub<TrackingHub>("/hubs/tracking")` (giữ 801-802) |
| GW-6 | `CommunityController.cs` + `ChatHub.cs`/`LocationHub.cs` | Giữ làm **adapter deprecated** trỏ vào service generic (không xoá — tránh breaking KhachLink bản cũ) |
| GW-7 | nginx templates | Đã có `location ~ ^/hubs/` cho mọi domain (`vanan.multivps.conf.template:283, :601`) → **không cần đổi**; verify lại khi RV |

**Access fix kèm theo (D1/D2):**
- GW-8: Endpoint **buyer-accessible** trả toạ độ cho chủ thể của chính họ: `GET /api/realtime/location/{subjectType}/{subjectId}/latest` (authorize qua `IRealtimeParticipantAuthorizer`, KHÔNG check role Shipper). Đây là cái `OrderTracking` phải dùng thay cho `nearby-orders`.
- GW-9: `SocialAuthController` (ShopERP) enqueue `CustomerCreated` sau khi tạo customer Google/Facebook → sync PG (fix D1). **Bắt buộc**, nếu không chat/GPS vẫn chết.

---

## 8. UI PLATFORM CHANGES

| # | File | Thay đổi |
|---|---|---|
| UI-1 | `UI.Platform/Core/Interfaces/IRealtimeChatClient.cs` (NEW) | `GetHistoryAsync(subjectType, subjectId, token)` · `SendMessageAsync(...)` · `ConnectAsync(...)` trả `IAsyncDisposable` + event `OnMessage` |
| UI-2 | `UI.Platform/Core/Interfaces/ILiveLocationClient.cs` (NEW) | `RecordPingAsync(subjectType, subjectId, trackerId, lat, lng, token)` · `GetLatestAsync(...)` · `StartWatching(callback, intervalMs)` · `StopWatching()` |
| UI-3 | `UI.Platform/Core/Interfaces/IMapJsAdapter.cs` (NEW) | Trừu tượng hoá `initMap/addMarker/updateMarker/drawRoute` (giống pattern `ICssAdapter`) |
| UI-4 | `UI.Platform/Adapters/RealtimeHttpAdapter.cs` (NEW) | Impl `IRealtimeChatClient` + `ILiveLocationClient` qua `IHttpClientFactory` (named client truyền vào, không hard-code host) |
| UI-5 | `UI.Platform/Adapters/LeafletMapAdapter.cs` (NEW) | Impl `IMapJsAdapter` gọi `window.vananMap.*` |
| UI-6 | `UI.Platform/Components/Realtime/RealtimeChatPanel.razor` (NEW) | Port từ `KhachLink/Components/ChatPanel.razor`; params: `SubjectType`, `SubjectId`, `CurrentUserId`, `AuthToken`, `ScrollContainerId`; inject `IRealtimeChatClient`; bỏ `DeriveGatewayUrl` |
| UI-7 | `UI.Platform/Components/Realtime/VanAnMap.razor` (NEW) | Port từ `KhachLink/Components/LeafletMap.razor` (params giữ nguyên) + `IMapJsAdapter` |
| UI-8 | `UI.Platform/wwwroot/js/realtime.js` (NEW) | `vananRealtime.getCurrentPosition()` + `vananRealtime.scrollToBottom()` + `window.vananMap.*` (gộp `pwa.js:606-633` + `leaflet.js`) |
| UI-9 | `UI.Platform/wwwroot/lib/leaflet/` (NEW) | Vendor Leaflet 1.9.4 (copy từ KhachLink) |
| UI-10 | `KhachLink/Components/ChatPanel.razor`, `Components/LeafletMap.razor`, `Services/LocationTrackingService.cs` | Chuyển thành **shim** trỏ về component UI Platform (hoặc xoá sau khi migrate hết trang) |
| UI-11 | `KhachLink/Pages/OrderTracking.razor` | Dùng `VanAnMap` + `ILiveLocationClient.GetLatestAsync` thay `nearby-orders`; set `_showMap=true` khi có toạ độ (fix D2/D3) |
| UI-12 | `KhachLink/Pages/DeliveryTracking.razor` | Start GPS khi load nếu task `PickedUp/OutForDelivery`; gọi `RecordPingAsync` với `TrackerId` (fix D5) |
| UI-13 | `KhachLink/Pages/Checkout.razor` | Thu thập GPS giao hàng → gửi `DeliveryLat/Lng` (fix D4) |

---

## 9. REUSE RECIPE — GẮN REALTIME PLATFORM VÀO MODULE MỚI (5 BƯỚC)

Ví dụ: **JobMarket** — chat Employer ↔ Candidate + chia sẻ vị trí phỏng vấn onsite.

```
B1. Khai báo subject type
    RealtimeSubjectType.JobApplication  (đã có trong enum — không cần sửa Domain)

B2. Implement authorizer cho module
    3_CoreHub/Services/Adapters/JobApplicationRealtimeAuthorizer.cs
      CanAccessAsync(type, subjectId, userId)
        => jobApplication.EmployerId == userId || jobApplication.CandidateId == userId
    Đăng ký DI: services.AddScoped<IRealtimeParticipantAuthorizer, JobApplicationRealtimeAuthorizer>()
    (nếu nhiều module: dùng keyed registration theo RealtimeSubjectType)

B3. Tạo conversation khi có sự kiện nghiệp vụ
    await _realtimeMessaging.EnsureConversationAsync(
        RealtimeSubjectType.JobApplication, application.Id, employerId, candidateId);

B4. Ghi vị trí khi cần
    await _liveLocation.RecordPingAsync(
        RealtimeSubjectType.JobApplication, application.Id, employerId, lat, lng);

B5. Gắn UI (UI Platform — KHÔNG tự viết HTML/CSS)
    <RealtimeChatPanel SubjectType="RealtimeSubjectType.JobApplication"
                       SubjectId="application.Id"
                       CurrentUserId="_currentUserId"
                       AuthToken="_token" />
    <VanAnMap MapElementId="job-map" ... ShipperLat="_lat" ShipperLng="_lng" />
```

**Checklist module mới:**
- [ ] Authorizer implemented + DI registered
- [ ] Token validator phù hợp (customer / staff JWT / device)
- [ ] EnsureConversation gọi ở đúng domain event (idempotent)
- [ ] `RealtimeSubjectType` tái dùng (chỉ thêm enum value nếu thật cần — cần Domain approval)
- [ ] UI dùng component UI Platform, không bypass
- [ ] Multi-tenancy: mọi query có `TenantId` / `IgnoreQueryFilters` có chủ đích + comment
- [ ] Test: 2 user khác tenant không thấy conversation của nhau

---

## 10. PHASES (JIT Planning + Pure Execution)

| Phase | Mode | Nội dung | Session |
|---|---|---|---|
| **P0** | ANALYZE | Verify D1..D6 trên production (log + DB) — xem Section 11 | 0.5 |
| **P1** | FIX_ONLY | ✅ **DONE** — D1 (sync customer), D2/D3 (endpoint + map cho buyer), D4 (toạ độ checkout), D5 (GPS resume), D6 (guest device auth). Build 0 errors · 40/40 tests | 1-2 |
| **P2** | IMPLEMENT | ✅ **DONE** (2026-09-17) — DOM-1..DOM-4 + SVC-1..SVC-6 + **F2 (unique index)** + **F6 (tracking index)** + migration PG `20260917101938_AddRealtimePlatformP2` (có backfill `SubjectId`). Build 0 errors · guard ALL PASSED · 11/11 test mới · 248/248 Community regression PASS | 1 |
| **P3** | IMPLEMENT | ✅ **DONE + DEPLOYED** (`d8072ec5`) — GW-1..GW-9 + **F3** (device token vào SignalR handshake qua query string). Build 0 errors · guard ALL PASSED · 37/37 Realtime tests PASS. Xem Section 18.7 | 1-2 |
| **P4** | IMPLEMENT | ✅ **DONE + DEPLOYED** (`d8072ec5`) 2026-09-18 — UI-1..UI-13 + **F4** (`IRealtimeEndpointProvider` + RCL wwwroot + verify static assets qua publish **và production L2**: `_content/VanAn.UI.Platform/js/realtime.js` 200 trên diemthuong2) + **F11** (VanAInput thay `<input class="form-control">`). Build 0 errors · guard ALL PASSED · 56/56 Realtime tests PASS (37 P2/P3 + 4 fallback + 15 UI Platform) · Core.Tests 1635 PASS · CI ✅ · CD Multi-VPS ✅. Xem Section 18.8 | 1-2 |
| **P5** | IMPLEMENT | ✅ **DONE (code + tests, chưa deploy)** 2026-09-18 — consumer = **Shop chat** trên `/store/{slug}` (khách ↔ shop, subject `Shop/{tenantId}`) + **map tĩnh + khoảng cách** (F9 — VanAnMap thay Google iframe) + **inbox chủ shop** `/community/messages` (ShopERP). **F7/F8/F11** cũng xử lý. Build 0 errors · 66/66 Realtime tests (56 + T1-T9 P5 + T12 staff adapter). Xem Section 18.9 | 1-2 |
| **P6** | IMPLEMENT | ✅ **DONE** 2026-09-18 — E2E `realtime-shop-chat.spec.ts` + `realtime-tracking.spec.ts` (**12/12 PASS trên production**) + **RV L1-L3** (L4/L5 manual pending) + reuse guide (`docs/UI_Platform_Implementation_Guide.md` §Realtime). Xem Section 18.10 | 1 |

### Rules
- P1 chỉ sửa đúng defect, không mở rộng scope (Fix_Errors mode).
- P2 trở đi mỗi phase phải build + guard + test trước khi sang phase sau.
- Domain change (DOM-*) chỉ thực hiện sau khi Tech Lead duyệt.
- Playwright chỉ bật sau khi build PASS + implementation xong (Gate 3).

---

## 11. P0 — VERIFY PRODUCTION (bắt buộc trước khi fix)

```bash
# 1. Log Gateway: khách Google checkout → CustomerId bị null?
gcloud compute ssh vanan-gateway --zone asia-southeast1-a --project vanan-prod
docker logs vanan-gateway 2>&1 | grep "not found in DB — falling back to guest mode"

# 2. PG: khách Google có row không? Order có CustomerId không?
#    SELECT COUNT(*) FROM "Customers" WHERE "Id" = '<customerId>';
#    SELECT "Id","CustomerId","OrderType","DeliveryLat","DeliveryLng" FROM "Orders" WHERE "Id" = '<orderId>';

# 3. Log ChatHub/LocationHub
docker logs vanan-gateway 2>&1 | grep -E "Invalid customerToken|Access denied|ChatHub:|LocationHub:"

# 4. API buyer map (kỳ vọng 403 → xác nhận D2)
#    curl -H "X-Customer-Token: <token>" \
#      "https://api2.khachvip.online/api/community/nearby-orders?lat=10.8&lng=106.7&radiusKm=5"
```

**Kết quả P0 ghi vào Section 2 (bảng defect) + cập nhật `docs/AI/project_state.md`.**

---

## 12. SUCCESS CRITERIA (ĐO LƯỜNG ĐƯỢC)

- [ ] **SC1 (D1):** Khách Google checkout → `Orders.CustomerId` ≠ null; PG `Customers` có row.
- [ ] **SC2 (chat):** Khách đã đăng nhập gửi/nhận tin realtime trên `/order-tracking/{id}`; shipper thấy tin trên `/community/delivery-tracking/{id}` (2 chiều, < 2s).
- [ ] **SC3 (D2/D3):** Khách (không có role Shipper) gọi API tracking của chính đơn mình → 200; map render; marker shipper di chuyển.
- [ ] **SC4 (D4):** Order DELIVERY có `DeliveryLat/Lng` ≠ null; shipper thấy marker khách.
- [ ] **SC5 (D5):** Shipper reload trang khi task `OutForDelivery` → GPS vẫn ping (10s/lần).
- [ ] **SC6 (generic):** Cùng 1 bộ component/service phục vụ module thứ 2 (JobMarket/Logistics) mà **không copy-paste** UI/JS.
- [ ] **SC7:** `Conversation` cũ (Order) vẫn hoạt động — không regression trên dữ liệu hiện có.
- [ ] **SC8:** Multi-tenancy: user tenant A không join được conversation tenant B (test âm).
- [ ] **SC9:** Build `dotnet build VanAn.sln` 0 lỗi · `guard-check.ps1` ALL PASSED · `dotnet test` ALL PASS.
- [ ] **SC10:** Không có component HTML/CSS tự viết trong module mới — 100% UI Platform (Gate UI Platform).
- [ ] **SC11:** Reuse guide có trong `docs/UI_Platform_Implementation_Guide.md` (Section Realtime).
- [ ] **SC12:** Không tạo `.csproj` mới (dùng project hiện có).

---

## 13. HARD STOPS / BOUNDARY RULES

- ❌ Không sửa Domain để fix UI/Service. DOM-* chỉ khi Domain Phase active + approval.
- ❌ `AccountingEntry` bất biến (không liên quan, không chạm).
- ❌ Domain PURE: không EF Core / DbContext / DataAnnotations trong `1_Shared`.
- ❌ Không tạo `.csproj` mới; dùng `3_CoreHub`, `2_Gateway`, `UI.Platform`.
- ❌ Không bypass UI Platform (cấm tự viết HTML/CSS chat/map mới).
- ❌ Không phá route cũ `/hubs/chat`, `/hubs/location`, `/api/community/chat|location` (giữ adapter).
- ❌ Multi-tenancy: mọi truy vấn cross-tenant phải có comment lý do.
- ❌ Không persist/echo `DevToken__Secret` (`.devin/rules/dev-token-secret.md`).

---

## 14. TDD & E2E TESTING STRATEGY

| Layer | Nội dung | File |
|---|---|---|
| Unit | `RealtimeMessagingService` (ensure idempotent, participant check, cross-tenant) · `LiveLocationService` (subject mapping) | `6_Tests/VanAn.Core.Tests/Realtime/*` |
| Unit | `OrderRealtimeAuthorizer` (shipper/customer/khác) | như trên |
| Integration | Gateway: `MessagingHub.JoinConversation` allow/deny; `RealtimeController` buyer 200 vs người ngoài 403 | `6_Tests/.../Realtime/` |
| E2E | Reuse spec chat (2 browser contexts) + tracking | `6_Testing/e2e-tests/realtime-chat.spec.ts`, `realtime-tracking.spec.ts` |
| E2E helper | Dùng `6_Testing/e2e-tests/helpers/gps-mock.ts` (đã có) | — |
| Regression | Giữ `community-chat.spec.ts` + `community-delivery-flow.spec.ts` PASS | — |

---

## 15. REVERSE IMPACT

| File | Reverse impact | Mitigation |
|---|---|---|
| `1_Shared/Domain.cs` | Entity dùng chung nhiều layer | Additive only, giữ ctor/property cũ |
| `Conversation`/`DeliveryTracking` schema | Dữ liệu production hiện có | Cột mới có default; migration không phá row cũ |
| `2_Gateway/Hubs/*` | KhachLink bản cũ đang gọi `/hubs/chat` | Giữ hub cũ làm adapter |
| `CommunityController` | App/test đang gọi `api/community/*` | Không xoá, chỉ delegate |
| `SocialAuthController` | Thêm outbox event → tăng tải NATS | Idempotent upsert ở `DataSyncSubscriber` (đã có `exists` check) |
| `OrderTracking.razor` | Đổi nguồn toạ độ | Fallback: nếu API mới lỗi → ẩn map như cũ, không crash |
| UI Platform | Mọi app dùng chung | Thêm mới, không đổi component cũ |
| nginx | `/hubs/` đã proxy | Không đổi; verify RV |

---

## 16. RISKS

| # | Risk | Mitigation |
|---|---|---|
| R1 | Domain change bị từ chối (Domain purity) | Tách P2 riêng, xin approval trước; nếu không duyệt → giữ `Conversation` cũ + tạo entity mới song song |
| R2 | Migration PG ảnh hưởng production | Additive + default; chạy trên staging trước; backup |
| R3 | SignalR qua nginx với domain custom | Đã có `location ~ ^/hubs/`; RV Layer 3 bằng Playwright trước khi mở rộng |
| R4 | Khách OAuth vẫn không sync (NATS lỗi) | P1 fix outbox; P0 verify log; fallback: tạo customer PG đồng bộ trong callback |
| R5 | Refactor làm chậm việc fix production | P1 fix trước (không refactor), P2+ mới generalize |
| R6 | Guest không chat được (kỳ vọng người dùng) | Quyết định sản phẩm: thêm `DeviceTokenValidator` + anonymous tracking nếu duyệt |

---

## 17. AI HEALTH CHECK (INITIAL)

- **Verified Facts:** 8 coupling (C1-C8) + 6 defect (D1-D6) đã đối chiếu file:line · schema chat/tracking chỉ map ở PG (`VanAnDbContext.cs:142-145`, `ShopERPDbContext.cs:253-255`) · UI Platform là Razor Class Library có sẵn `Core/Interfaces` + `Adapters` pattern · nginx đã proxy `/hubs/` cho mọi domain.
- **Assumptions:** 1 — P0 sẽ xác nhận D1 trên production (chưa có log thật).
- **Open Questions:** 0 — Q1 guest có chat/tracking ✅ · Q2 Domain additive approved ✅ · Q3 cả Logistics + JobMarket ✅.
- **Gate 6:** ✅ Assumptions (1) < Verified Facts (15+), Open Questions (0) → CLEAR.
- **Recommended Action:** Chạy P0 → P1 → P2..P6.

---

## 18. ESTIMATED EFFORT

- P0: 0.5 session · P1: 1-2 · P2: 1-2 · P3: 1-2 · P4: 1-2 · P5: 1-2 · P6: 1 → **~8-12 sessions**.
- **BLOCKER:** ~~approval Domain + Q1/Q3~~ → đã giải quyết (2026-09-17). Không còn blocker.

---

## 18.5. P1 IMPLEMENTATION RECORD (2026-09-17)

**Build:** `dotnet build VanAn.sln` → 0 errors · **Tests:** 40/40 chat+delivery PASS.

| Defect | File(s) | Change |
|---|---|---|
| D1 | `5_WebApps/ShopERP/Controllers/SocialAuthController.cs` | Inject `IOutboxRepository` + `IVanAnDbContext`; new `EnqueueCustomerSyncAsync(customer)` called for **both new and existing** customers → publishes `CustomerCreated` → Gateway PG upsert. Backfills pre-fix Google customers on next login. |
| D2/D3 | `2_Gateway/Controllers/CommunityController.cs` | New **`GET /api/community/orders/{orderId}/tracking`** (buyer/shipper/guest authorized) returning shop/delivery/latest-shipper coords. |
| D2/D3 | `5_WebApps/KhachLink/Pages/OrderTracking.razor` | `LoadOrderInfoAsync` → **`LoadTrackingAsync`** (new endpoint, no more shipper-only `nearby-orders`); `_showMap` set whenever any coords exist; `LocationUpdate` handler sets `_showMap = true`; coordinates refreshed in the 15s poll. |
| D2/D3 | `5_WebApps/KhachLink/Services/Http/CommunityHttpService.cs` | `GetOrderTrackingAsync(token, deviceId, orderId)` + `OrderTrackingDto`/`OrderTrackingResult` + `AddIdentityHeader` helper. |
| D4 | `5_WebApps/KhachLink/Pages/Checkout.razor` | Capture `vananPWA.getCurrentPosition` for DELIVERY → send real `DeliveryLat`/`DeliveryLng` (was always `null`). |
| D5 | `5_WebApps/KhachLink/Pages/DeliveryTracking.razor` | Resume `StartGpsTracking()` on page load when task is `PickedUp`/`OutForDelivery`. |
| D6 | `2_Gateway/Controllers/CommunityController.cs` | New `ValidateCustomerOrDeviceAsync()` — accepts `X-Customer-Token` **or** `X-Customer-Device-Id`; chat history/send use it (+ participant authorization on history); device id compared to `Order.CustomerDeviceId`. |
| D6 | `3_CoreHub/Services/IChatService.cs` + `ChatService.cs` | Optional `guestDeviceId` param — guest orders (CustomerId=null) get a conversation keyed by the device id, verified against `Order.CustomerDeviceId`. |
| D6 | `5_WebApps/KhachLink/Components/ChatPanel.razor` + `Services/Http/ChatHttpService.cs` | `CustomerDeviceId` param; guest = HTTP-only + **8s history polling** fallback when SignalR is not connected. |
| D6 | `5_WebApps/KhachLink/Pages/OrderTracking.razor` | Chat panel renders for guests (`ChatIdentity = _customerId ?? _customerDeviceId`). |

**Docs updated:** `07-customer.md` (§2.1/§2.4/§5.1/§6.1/FAQ) · `04-shipper.md` (§8.3) · `README.md` (§3.3) — guest now has chat + tracking.

### P1 RV (production, 2026-09-17 — commit `b12a99d2`)

| Layer | Check | Result |
|---|---|---|
| L1 | `GET /api/community/orders/{id}/tracking` no identity → **401 JSON** (was SPA HTML 200 before deploy) | ✅ |
| L1 | fake device + fake order → **404 JSON** | ✅ |
| L1 | guest device MATCH (order `01a0ad6a…`, device `81f43d82…`) → **200** + `shopLat 10.9659 / shopLng 106.5943` + live `shipperLat 10.966012 / shipperLng 106.5945537` (`OutForDelivery`) | ✅ |
| L1 | guest device **WRONG** → **403** "Bạn không có quyền xem đơn hàng này." | ✅ |
| L1 | guest chat history → **200** (conversation `aebefcac…`, shipperId assigned) · guest send → **200**, message persisted with `senderId` = device id | ✅ |
| L2 | deployed `VanAn.KhachLink.wasm` contains `X-Customer-Device-Id` (utf16LE=1), `_customerDeviceId`, `GetOrderTrackingAsync`, `LoadTrackingAsync`, `OrderTrackingDto` | ✅ |
| L2 | ShopERP `vanan-shoperp-1` recreated 06:02:24Z (matches CD run) — D1 fix deployed | ✅ |
| L3 | Playwright `diemthuong2.khachvip.online/order-tracking/01a0ad6a…` with guest device id → `.chat-panel`=1 · `Chưa đăng nhập`=0 · `.alert-danger`=0 · `#customer-tracking-map`=1 · guest message visible · chat input present · status "Đang giao hàng" · **0 console errors** | ✅ |
| L4 | Guest typed + clicked "Gửi" in the UI → message appeared (`RV-P1 UI send 06:10:24`), 0 console errors | ✅ |
| L5 | Manual browser check by user | ⏳ |

> RV note: D1 end-to-end needs a real Google login (not triggerable headlessly). The ShopERP fix reuses the exact `CustomerCreated` outbox path already proven by the OTP flow, and the container is confirmed redeployed. Verify manually: log in with Google on KhachLink → checkout a DELIVERY order → `Orders.CustomerId` must be non-null (and chat/map work).

### P1b RV follow-up defects (manual test 2026-09-17)

After the P1 deploy the user's manual test found: chat OK, but (a) no GPS/map on either side, (b) Free/Charity products could not create an order.

| # | Defect | Root cause (verified) | Fix |
|---|---|---|---|
| D7 | Shipper never sees a map (so never sees the customer's position) | `DeliveryTracking.razor` loaded shop/customer coords from `GET /api/community/nearby-orders`, which **excludes orders that already have an active DeliveryTask** — i.e. the very order the shipper is viewing. Coords were always empty → `_showMap=false`. | Shipper page now uses `GET /api/community/orders/{id}/tracking` (authorized for the assigned shipper) → shop + delivery + latest-ping coords + shop name. |
| D8 | Customer map blank / no shipper pin | Tenant/DeliveryTask coordinate defaults are `0`, which passed `HasValue` → map centred on (0,0) (blank ocean). Markers were also only added on the component's **first** render, so a live shipper ping never moved/added the pin. | Endpoint normalises `0` coords → `null`; client requires real (non-zero) coords and centres shop→delivery→shipper; `LeafletMap` now upserts markers on **every** render (`leafletMap.upsertMarker`). |
| D9 | Free/Charity product cannot create an order | The ShopERP product API (`GET shoperp/api/products`, used by the Store page) has **no `productType`**, so KhachLink's `ProductDto.ProductType` defaulted to `Paid` → cart `IsFree=false` → Gateway Tier 0 rejected the 0-price item ("giá không hợp lệ (UnitPrice=0)"). | (1) Gateway resolves Free/Charity **server-side** from `FeaturedProducts` before the price guard (authoritative, path-independent; spoof guard kept for client-claims-free-but-server-paid). (2) `Store.razor` enriches `ProductType` from the featured catalog so the UI also shows Miễn phí/Từ thiện. |

### P1b RV (production, 2026-09-17 — commit `96e733a6`)

| Check | Result |
|---|---|
| D9 API: charity item with client `IsFree=false` (the Store-page bug) → order created | ✅ 200 |
| D9 API: spoof (Paid product claimed `IsFree=true`) → still rejected | ✅ 400 "Loại sản phẩm không hợp lệ" |
| D9 UI: Store page → add "cơm chay thập cẩm" → cart | ✅ `IsFree=true IsCharity=true` |
| D8 UI: customer `/order-tracking/01a0ad6a…` → map | ✅ `.leaflet-container`=1 · markers=2 · tiles=8 · 0 console errors |
| D7 data source: `GET /api/community/orders/{id}/tracking` for an order WITH an active DeliveryTask (previously excluded by nearby-orders) | ✅ 200 + `shopName` + `shopLat/Lng` + live `shipperLat/Lng` |
| L2 static: served `/js/leaflet.js` contains `upsertMarker`; WASM contains `GetOrderTrackingAsync`/`upsertMarker`/`shopName` | ✅ |
| L5 manual (shipper login → map + customer pin) | ⏳ user |

> Note: shipper-side UI cannot be logged in headlessly (needs the shipper's customer token), so D7 is verified at the API/data level plus the same map component already proven on the customer page.

**Not yet done (P2-P6):** Domain additive (`Conversation.SubjectType/SubjectId`, `ConversationParticipant`, `DeliveryTracking.SubjectId/TrackerId`) + generic `IRealtimeMessagingService`/`ILiveLocationService`/`IRealtimeParticipantAuthorizer` + generic hubs/endpoints + UI Platform extraction + Logistics/JobMarket consumers.

---

## 18.6. P2 IMPLEMENTATION RECORD (2026-09-17)

**Build:** `dotnet build VanAn.sln` 0 errors · **Guard:** ALL CHECKS PASSED · **Tests:** 11/11 mới + 248/248 Community regression PASS.

| # | File | Change |
|---|---|---|
| DOM-1 | `1_Shared/Domain.cs` | `RealtimeSubjectType` enum (Order, Delivery, Shop, Shipment*, JobApplication*, Ticket, Custom — *reserved R3) + `RealtimeParticipantRole` const |
| DOM-2 | `1_Shared/Domain.cs` | `Conversation` + `SubjectType`/`SubjectId` (additive) + generic ctor + `AssignCounterpart()` (delegates to `AssignShipper()` for Order subjects) |
| DOM-3 | `1_Shared/Domain.cs` | `ConversationParticipant` (NEW) — ConversationId/ParticipantId/RoleCode/JoinedAt/IsActive |
| DOM-4 | `1_Shared/Domain.cs` | `DeliveryTracking` + `SubjectType`/`SubjectId`/`TrackerId` (additive); legacy ctor giữ `DeliveryTaskId` |
| F2 | `ConversationConfiguration.cs` | Unique index `OrderId` → **`(TenantId, SubjectType, SubjectId)`** + index thường trên `OrderId` (legacy path) |
| F6 | `DeliveryTrackingConfiguration.cs` | + index `(TenantId, SubjectType, SubjectId, RecordedAt)` |
| — | `ConversationParticipantConfiguration.cs` (NEW) | Unique `(ConversationId, ParticipantId)` + index `ParticipantId` |
| — | `VanAnDbContext` / `IVanAnDbContext` / `ShopERPDbContext` | `DbSet<ConversationParticipant>` + `Ignore<ConversationParticipant>()` (PG-only) |
| SVC-1/2 | `IRealtimeMessagingService.cs` + `RealtimeMessagingService.cs` (NEW) | `EnsureConversationAsync` (idempotent + race-safe), `GetConversationAsync`, `SendMessageAsync`, `GetHistoryAsync(take)` (newest-N rồi đảo), `MarkAsReadAsync`, `IsParticipantAsync` |
| SVC-3/4 | `ILiveLocationService.cs` + `LiveLocationService.cs` (NEW) | `RecordPingAsync`/`GetLatestAsync`/`GetHistoryAsync`; Delivery subject giữ `DeliveryTaskId` |
| SVC-5/6 | `IRealtimeParticipantAuthorizer.cs` + `Adapters/OrderRealtimeAuthorizer.cs` (NEW) | Access rules của `ChatHub.JoinConversation` + `LocationHub.JoinOrderTracking` gom về 1 chỗ (shipper ∨ conversation party ∨ participant ∨ owner/guest-device) |
| DI | `2_Gateway/Program.cs` | `AddScoped<IRealtimeMessagingService/ILiveLocationService>` + `AddKeyedScoped<IRealtimeParticipantAuthorizer, OrderRealtimeAuthorizer>(RealtimeSubjectType.Order)` |
| Migration | `20260917101938_AddRealtimePlatformP2` | + **backfill `SubjectId = OrderId` / `= DeliveryTaskId`** trước khi tạo unique index (nếu không, tenant có ≥2 conversation cũ sẽ vi phạm index) |
| Tests | `6_Tests/VanAn.Core.Tests/Realtime/RealtimePlatformP2Tests.cs` (NEW) | T1-T11: subject keys · idempotent · **F2 collision regression** · participants · non-participant denied · bounded history · generic ping · legacy delivery ping · legacy ctor backfill (SC7) · authorizer allow/deny · tenant scope |

**Deviations so với card (đã ghi nhận):**
- `RealtimeSubjectType` **thiếu trong card là `Shop`** → đã thêm (yêu cầu mới 2026-09-17).
- Card SVC-1/SVC-3 không có tham số `TenantId` → **đã thêm** (bắt buộc: `Conversation.TenantId` là required, Gateway không có ambient tenant).
- `ConversationParticipant` dùng `.NET 8 keyed DI` thay vì registry tự viết.
- `IChatService`/`ChatService` **giữ nguyên 100%** (không nhồi generic vào thân hàm) — xem F5.

---

## 18.7. P3 IMPLEMENTATION RECORD (2026-09-17)

**Goal:** expose the P2 generic services through Gateway hubs + HTTP endpoints with one identity
layer, and close **F3** (guests had no realtime — SignalR accepted only `customerToken`).

**Build:** `dotnet build VanAn.sln` 0 errors · **Guard:** ALL PASSED · **Tests:** 37/37 Realtime PASS (11 P2 + 26 P3).

### New files

| # | File | Purpose |
|---|---|---|
| GW-3 | `2_Gateway/Realtime/RealtimeIdentity.cs` | `RealtimeIdentityKind {Customer, Device, Staff}` + `RealtimeIdentity(UserId, Kind)`; `RoleCode` maps to `RealtimeParticipantRole` |
| GW-3 | `2_Gateway/Realtime/IRealtimeTokenValidator.cs` | One authentication strategy; returns null when it does not apply |
| GW-3 | `2_Gateway/Realtime/CustomerTokenValidator.cs` | `customerToken` query **or** `X-Customer-Token` header → ShopERP `/api/customer-identity/me` |
| GW-3 | `2_Gateway/Realtime/DeviceTokenValidator.cs` | **F3** — `customerDeviceId` query **or** `X-Customer-Device-Id` header → guest identity (device guid) |
| GW-3 | `2_Gateway/Realtime/StaffJwtValidator.cs` | `access_token` query **or** `Authorization: Bearer` → validates HS256 with `Jwt:Secret/Issuer/Audience`, identity = `sub` |
| GW-3 | `2_Gateway/Realtime/RealtimeIdentityResolver.cs` | Runs validators in registration order (Customer → Device → Staff); first accept wins |
| GW-3 | `2_Gateway/Realtime/RealtimeRequestReader.cs` | Query-then-header credential reader shared by all three validators |
| GW-3 | `2_Gateway/Realtime/RealtimeAuthorizerLookup.cs` | Keyed authorizer resolution, **default deny** for unregistered subject types |
| GW-3 | `2_Gateway/Realtime/RealtimeSubjectParser.cs` | Parses `(subjectType, subjectId)` from SignalR string args |
| GW-1/2 | `2_Gateway/Hubs/MessagingHub.cs` | `/hubs/messaging`, group `msg_{subjectType}_{subjectId}`, push `ReceiveMessage` |
| GW-2 | `2_Gateway/Hubs/TrackingHub.cs` | `/hubs/tracking`, group `loc_{subjectType}_{subjectId}`, push `LocationUpdate` |
| GW-4 | `2_Gateway/Controllers/RealtimeController.cs` | `/api/realtime/conversations/messages` · `GET /conversations/{type}/{id}` · `POST /location/ping` · `GET /location/{type}/{id}/latest` |
| — | `3_CoreHub/Services/IRealtimeSubjectResolver.cs` + `RealtimeSubjectResolver.cs` | subject → `TenantId` (Order/Delivery from the entity, Shop = tenant id, unknown → null) |

### Modified files

| File | Change |
|---|---|
| `2_Gateway/Program.cs` | DI: `IRealtimeSubjectResolver`, 3 × `IRealtimeTokenValidator` (registration order = trust order), `RealtimeIdentityResolver`; `MapHub<MessagingHub>("/hubs/messaging")` + `MapHub<TrackingHub>("/hubs/tracking")` |
| `2_Gateway/Hubs/ChatHub.cs` | **GW-6** — legacy `/hubs/chat` kept (same method + group names); token validation + join check now delegate to the shared resolver + keyed `OrderRealtimeAuthorizer`; private `/me` forward deleted |
| `2_Gateway/Hubs/LocationHub.cs` | Same as ChatHub for `/hubs/location` |
| `6_Tests/VanAn.Architecture.Tests/AuthorizationEnforcementTests.cs` | `RealtimeController` added to the W12-G7 exemption list (customer/guest-facing, header/query auth — `[Authorize]` would reject guests before the endpoint runs) |

### F3 — device token in the SignalR handshake

A browser WebSocket handshake cannot carry custom headers, which is why guests could only poll HTTP
(P1 D6). Every validator now reads the **query string first**, then the header — so a guest connects
with `/hubs/messaging?customerDeviceId={guid}` and authenticates like any other caller. The legacy
`/hubs/chat` + `/hubs/location` accept the same, so the F3 gap closes on both paths.

### Deviations from the card (recorded)

- **New service `IRealtimeSubjectResolver`** (not in GW-1..GW-9). `ILiveLocationService.RecordPingAsync`
  requires an explicit `TenantId` and Gateway has no ambient tenant; without a resolver the ping would
  have to trust a caller-supplied tenant (multi-tenancy hole). Resolver returns null for unknown
  subjects → `400`, never a guessed tenant.
- **`RealtimeController` is in the W12-G7 exemption list** rather than carrying class-level `[Authorize]`
  — same category and precedent as `CommunityController`/`DeviceRegistrationController`.
- **Legacy hubs refactored, not left alone.** P2 deliberately left `ChatService` untouched (F5), but the
  hubs' access checks were a verbatim duplicate of `OrderRealtimeAuthorizer`. Delegating removes the
  divergence risk; the authorizer is a strict superset of the old rules (adds participant rows + guest
  device + excludes cancelled tasks), so nobody who previously had access loses it. Group names and
  client-facing method names are unchanged (SC7).
- **`POST /location/ping` rejects (0, 0).** Unset coordinates default to 0 and silently centre maps in
  the ocean — the D8 defect. Rejected at the API instead of persisted as noise.

### P3 test coverage (`6_Tests/VanAn.Core.Tests/Realtime/`)

| File | Tests |
|---|---|
| `RealtimePlatformP3Tests.cs` | T1-T4 — subject → tenant for Order / Delivery / Shop (exists vs missing) / unknown + empty |
| `RealtimeGatewayP3Tests.cs` | T5-T15 — device guid via query + header, invalid inputs, resolver first-wins, no-validator→null, group naming, **default deny** for unregistered subject, registered authorizer delegation, staff JWT (absent/garbage/valid), customer token (no call when absent, identity when valid) |
| `RealtimeControllerP3Tests.cs` | T16-T23 — 401 no identity, 400 invalid subjectType, 403 default deny, 403 skips service, 200 history, 400 (0,0) ping, ping stamped with the subject's tenant + pushed to `loc_` group, latest-with-no-ping = 200 + nulls |

**Not yet done:** RV L4 (shop inbox staff reply flow — cần login thật) + L5 (manual user) — P6 phần còn lại.

---

## 18.10. P6 IMPLEMENTATION RECORD (2026-09-18)

**Goal:** tests + E2E + RV Layer 1-5 + reuse guide.

### E2E specs (mới — chạy thẳng lên production qua `realtime-rv.config.ts`)

| Spec | Coverage | Result |
|---|---|---|
| `e2e-tests/realtime-shop-chat.spec.ts` (7 tests) | P5-1/2 401 không identity · P5-3 **guest mới TẠO conversation** (chicken-egg fix) · P5-4 send + read-back · P5-5 device khác KHÔNG thấy tin (history filter) · P5-6 hubs tồn tại · P5-7 **UI store page**: map + chat panel + guest gửi tin xuất hiện + 0 console error | ✅ 7/7 |
| `e2e-tests/realtime-tracking.spec.ts` (5 tests) | P4-1 401 · P4-2 stranger 403 (default deny) · P4-3 ping (0,0) 400 · P4-4 invalid subject 400 · P4-5 order-tracking page render 0 lỗi | ✅ 5/5 |
| `helpers/gps-mock.ts` | + mock `vananRealtime.getCurrentPosition` (thêm namespace, additive) | ✅ |

### RV trên production (deploy `3fb71866`)

| Layer | Check | Result |
|---|---|---|
| L1 API | P5-1..P5-6 + P4-1..P4-4 (auth contracts, guest create, send/history, privacy filter, validation) | ✅ |
| L2 Static | `_content/VanAn.UI.Platform/js/realtime.js` + `lib/leaflet/*` **200** trên **3 host**: diemthuong2.khachvip.online · api2.khachvip.online/shoperp · timlathay.com (F4) | ✅ |
| L3 Playwright | P5-7 UI (store chat send end-to-end) + P4-5 (page render) | ✅ |
| L4 UI flow | Shop inbox staff reply (cần login thật) | ⏳ manual |
| L5 Manual | user | ⏳ |

### 2 bug thật bị E2E bắt (production, đã fix + deploy)

1. **Chicken-and-egg Shop chat** (`7406d83a` → `f05960c5`): authorizer chạy trước ensure → khách mới
   403 mãi, conversation không bao giờ tạo. Fix: Shop = **public widget** — ensure create-only trước
   authorize (staff không create), privacy chuyển xuống data plane: **history filter per-caller**
   (tin của mình + reply của shop; staff thấy toàn bộ) + **per-user SignalR group**
   (`msg_Shop_{tenantId}_u_{userId}` — staff join shared group, khách join group riêng; SendMessage
   push cả 2). Known limitation (documented): shop reply hiển thị cho mọi khách của thread đó —
   per-customer reply threading cần domain change (defer).
2. **Razor string-param binding** (`3fb71866`): `Param="_field"` (không `@`) → Razor coi là **literal
   string** khi param nhận string (Guid/double thì thành expression). `CustomerToken="_customerToken"`
   gửi literal "_customerToken" → 401 → chat input disabled. **Ảnh hưởng P4**: ChatPanel shim + LeafletMap
   shim cũng dính (order chat logged-in 401 từ lúc P4 deploy — E2E mới phát hiện). Fix: bind `@` cho mọi
   string param không-literal. Verify: literal `_customerToken` = 0 occurrences trong WASM.

### Reuse guide
`docs/UI_Platform_Implementation_Guide.md` + §Realtime: quick start 5 bước, checklist module mới,
identity table, endpoint list, static assets, E2E pointers.

### Verification
Build 0 errors · guard ALL PASSED · 68/68 Realtime tests · Core.Tests 1647 PASS · E2E 12/12 PASS trên production.

---

## 18.9. P5 IMPLEMENTATION RECORD (2026-09-18)

**Goal (REVISED 2026-09-17):** consumer thật đầu tiên = **Shop chat** — khách nhắn với cửa hàng trên
`/store/{slug}` (subject `Shop/{tenantId}`), chủ shop trả lời từ inbox `/community/messages` (ShopERP).
Logistics/JobMarket → P7 (chưa có entity — F1). GPS trang shop = **map tĩnh + khoảng cách** (F9).

**Build:** `dotnet build VanAn.sln` 0 errors · **Tests:** 66/66 Realtime (56 P2-P4 + T1-T6 authorizer +
T7-T9 controller + T12 staff adapter) · Core.Tests chờ kết quả full.

### Server (CoreHub + Gateway)

| # | File | Change |
|---|---|---|
| — | `IRealtimeParticipantAuthorizer` | + overload `CanAccessAsync(type, subjectId, userId, tenantId, ct)` (default → userId-only, giữ P2 semantics) — shop side là tenant không phải user |
| — | `Adapters/ShopRealtimeAuthorizer.cs` (NEW) | Keyed `Shop`: staff có `tenant_id == subjectId` ∨ conversation initiator (CustomerId) ∨ participant row (guest device). Tenant-scoped query |
| — | `RealtimeMessagingService` | `EnsureParticipantAsync` private → **public** (staff thêm vào participant trước khi send) · + `GetConversationsAsync(type, subjectId, take)` (inbox list) |
| — | `RealtimeIdentity` | + `TenantId (Guid?)` — staff JWT `tenant_id` claim |
| — | `StaffJwtValidator` | Parse `tenant_id` claim → identity.TenantId |
| — | `RealtimeAuthorizerLookup` + `MessagingHub` + `TrackingHub` | + tenant-aware overload; hubs truyền `identity.TenantId` |
| — | `RealtimeController` | `GetOrEnsureConversationAsync` + branch **Shop**: `EnsureConversationAsync(tenantId, Shop, subjectId, userId, subjectId, role, Shop)` (initiator = caller, counterpart = shop) · SendMessage: staff của Shop được `EnsureParticipantAsync` trước sender check · **NEW `GET /api/realtime/shop/conversations`** — inbox: staff-only (tenant từ JWT), list conversation Shop/{tenant} + last-message preview + customer name (PG Customers, 1 pass) |
| — | `2_Gateway/Program.cs` | `AddKeyedScoped<…, ShopRealtimeAuthorizer>(RealtimeSubjectType.Shop)` |

### KhachLink — `/store/{slug}` (FullCommerce)

| # | File | Change |
|---|---|---|
| F8 | `Pages/Store.razor` | Tạo `customer_device_id` ngay trên trang store (không đợi Checkout) · load customer token + `GetCustomerIdAsync` (token hết hạn → fallback guest) · **section chat**: `<RealtimeChatPanel SubjectType="Shop" SubjectId="_store.Id" …>` |
| F9/F11 | `Components/GoogleMaps.razor` | **Viết lại**: `VanAnMap` tĩnh (shop marker) thay iframe Google + **khoảng cách** (haversine từ `vananRealtime.getCurrentPosition`, badge "Cách bạn ~X km") · bỏ `<style>` inline · giữ param `ShopConfig` |

### UI.Platform — staff auth

| # | File | Change |
|---|---|---|
| — | `IRealtimeChatClient`/`ILiveLocationClient` | + optional `staffToken` (last param, non-breaking) |
| — | `RealtimeHttpAdapter` | Precedence: staff Bearer → customer token → device id |
| — | `RealtimeChatPanel` | + `[Parameter] StaffToken` — HTTP Bearer + SignalR `?access_token=` (WebSocket không set header được); guests giờ cũng connect SignalR (F3) |

### ShopERP — inbox `/community/messages`

| # | File | Change |
|---|---|---|
| F7 | `Services/ShopInboxApiClient.cs` (NEW) | Pattern TenantCommunityAdminApiClient — mint Owner JWT (tenant_id claim) → `GET api/realtime/shop/conversations` · expose `MintOwnerTokenAsync` (StaffToken cho panel) + `GetCurrentUserIdAsync` |
| F7 | `Components/Pages/Community/ShopInbox.razor` (NEW) | `@page "/community/messages"` · AdminLayout · `[Authorize(Roles="Owner")]` · list conversations (tên khách + preview) trái, `RealtimeChatPanel` (StaffToken) phải |
| — | `Components/App.razor` | + leaflet css/js + realtime.js từ `_content/VanAn.UI.Platform/...` |
| — | `Components/Layout/NavMenu.razor` | + "Hộp thư tin nhắn" (CRM & Loyalty, Owner) |
| — | `Program.cs` | + `AddScoped<ShopInboxApiClient>()` |

### Tests (`6_Tests/VanAn.Core.Tests/Realtime/`)

| File | Tests |
|---|---|
| `ShopRealtimeP5Tests.cs` (NEW) | T1-T2 staff tenant match/mismatch · T3 initiator · T4 guest participant · T5-T6 stranger/empty denied · T7 inbox staff 200 + customer name + preview · T8 inbox customer 403 · T9 Shop conversation ensure (initiator = caller, counterpart = shop) |
| `RealtimeUiPlatformP4Tests.cs` (+1) | T12 staff token → Authorization Bearer |

### Deviations (đã ghi nhận)

- **Authorizer interface + overload tenant** (không nằm trong card P5 — cần thiết: shop side là tenant
  không phải user; default impl giữ P2 semantics nên không breaking).
- **Inbox endpoint `GET /api/realtime/shop/conversations`** (không có trong card) — cần cho trang inbox;
  staff-only + tenant-scoped từ JWT claim.
- **`EnsureParticipantAsync` public** — staff không phải conversation party mặc định; gọi sau authorizer
  nên không mở rộng quyền.
- **Không có feature-flag cho shop chat** — section chat hiển thị trên mọi `/store/{slug}` (page vốn là
  commerce profile). Thêm flag nếu cần ở phase sau.
- **Unread count chưa làm** trong inbox (chỉ last-message preview) — ngoài phạm vi P5.

---

## 18.8. P4 IMPLEMENTATION RECORD (2026-09-18)

**Goal:** extract the chat + map UI into UI.Platform (reusable across modules, SC6/SC10) and switch
KhachLink from the legacy `/hubs/chat` + `/api/community/*` surface to the generic realtime surface.

**Build:** `dotnet build VanAn.sln` 0 errors · **Guard:** ALL CHECKS PASSED · **Tests:** 56/56 Realtime
(37 P2/P3 + 4 new fallback T24-T27 + 15 new UI Platform T1-T11) · **Core.Tests:** 1635 PASS (0 fail).

### New files (UI.Platform — F4: RCL wwwroot + endpoint provider, host-agnostic)

| # | File | Purpose |
|---|---|---|
| F4 | `Core/Interfaces/IRealtimeEndpointProvider.cs` | Gateway base + hub URL provider (the RCL never hard-codes a host) |
| F4 | `Adapters/RealtimeEndpointUrls.cs` | Pure derivation logic — port of ChatPanel.DeriveGatewayUrl (C8): `*.khachvip.online` suffix maps 1:1, custom domains = same origin (nginx proxies `/api/` + `/hubs/`) |
| F4 | `Adapters/NavigationRealtimeEndpointProvider.cs` | Default impl from `NavigationManager.BaseUri` — works on WASM + Server + SSR with zero config |
| UI-1 | `Core/Interfaces/IRealtimeChatClient.cs` | `GetHistoryAsync` / `SendMessageAsync` (subject-type + id, customer token OR device id) |
| UI-2 | `Core/Interfaces/ILiveLocationClient.cs` | `GetLatestAsync` / `RecordPingAsync` — **trackerId NOT a client param** (server stamps identity — no spoofing) |
| UI-3 | `Core/Interfaces/IMapJsAdapter.cs` | Map interop abstraction (`ICssAdapter` pattern) |
| UI-4 | `Adapters/RealtimeHttpAdapter.cs` | Impl both clients → `{gateway}/api/realtime/*`; named `"realtime"` HttpClient with **no base address** (absolute URLs per call — sidesteps WASM-vs-Server scoping, F4) |
| UI-5 | `Adapters/LeafletMapAdapter.cs` | `IMapJsAdapter` → `window.vananMap.*` |
| UI-6 | `Components/Realtime/RealtimeChatPanel.razor` | Port of ChatPanel — `SubjectType/SubjectId` params, `IRealtimeChatClient`, `/hubs/messaging` (group `msg_{type}_{id}`), **F11**: VanAInput thay `<input class="form-control">`; 404 = "chưa có cuộc trò chuyện" → empty state (không error) |
| UI-7 | `Components/Realtime/VanAnMap.razor` | Port of LeafletMap — `IMapJsAdapter` |
| UI-8 | `wwwroot/js/realtime.js` | `window.vananRealtime` (GPS + scrollToBottom, port từ pwa.js:606-633) + `window.vananMap` (port leaflet.js, rename namespace) |
| UI-9 | `wwwroot/lib/leaflet/` | Vendored Leaflet 1.9.4 (copy từ KhachLink) |
| — | `Realtime/RealtimeModels.cs` | Shared DTOs (history/send/location/ping results, map point) |
| — | `Extensions/RealtimeServiceCollectionExtensions.cs` | `AddRealtimePlatform()` — 1-call registration (endpoint provider + adapters + named HttpClient) |

### KhachLink changes (migration)

| # | File | Change |
|---|---|---|
| UI-10 | `Components/ChatPanel.razor` | **Shim** → `RealtimeChatPanel` (SubjectType="Order", giữ params cũ) |
| UI-10 | `Components/LeafletMap.razor` | **Shim** → `VanAnMap` (giữ params cũ) |
| UI-11 | `Pages/OrderTracking.razor` | `VanAnMap` thay LeafletMap · shipper coords từ `ILiveLocationClient.GetLatestAsync("Order", orderId)` (generic surface) · join `/hubs/tracking` (group `loc_Order_{id}`) cho ping push live · legacy hub giữ cho status pushes (GW-6) · 15s poll fallback |
| UI-12 | `Pages/DeliveryTracking.razor` | `VanAnMap` · ping qua `ILiveLocationClient.RecordPingAsync("Order", OrderId, ...)` thay `POST /api/community/location/update` (D5 resume giữ nguyên) |
| — | `Program.cs` | `AddRealtimePlatform()` · bỏ đăng ký `ChatHttpService` |
| — | `Services/Http/ChatHttpService.cs` | **Xoá** (dead sau migration — shim dùng IRealtimeChatClient) |
| — | `wwwroot/index.html` | leaflet css/js + realtime.js từ `/_content/VanAn.UI.Platform/...` · bỏ `/js/leaflet.js` |
| — | `Services/LocationTrackingService.cs` | GPS helper → `vananRealtime.getCurrentPosition` (realtime.js) |
| — | `Pages/Checkout.razor` | UI-13 đã xong ở P1 (D4 — toạ độ checkout) |

### Gateway change (bắt buộc cho migration — conversation lazy-create)

`RealtimeController` (P3) trả 404 "Chưa có cuộc trò chuyện" khi conversation chưa tồn tại — đơn mới
sẽ 404 cả history lẫn send nếu RealtimeChatPanel dùng thẳng generic surface. **Fix:** khi
`subjectType == Order` và generic lookup miss → fallback `IChatService.GetOrCreateConversationAsync`
(legacy adapter, GW-6) với guest device id từ identity (Device kind). Caller đã qua
`IRealtimeParticipantAuthorizer` nên không mở rộng quyền. Non-Order subject → vẫn 404 (không fallback).

### Hosts (F4 — "3 host")

`AddRealtimePlatform()` đăng ký ở **KhachLink** (WASM) + **ShopERP** (Server, cho inbox P5) +
**Directory** (SSR). **Verify static assets:** `dotnet publish KhachLink -c Release` → 
`wwwroot/_content/VanAn.UI.Platform/js/realtime.js` + `lib/leaflet/leaflet.{js,css}` present ✓
(cơ chế framework giống nhau cho Server/SSR — verify serving thật ở P6 RV L2).

### Tests (`6_Tests/VanAn.Core.Tests/Realtime/`)

| File | Tests |
|---|---|
| `RealtimeUiPlatformP4Tests.cs` (NEW) | T1-T3 DeriveGatewayBaseUrl (khachvip suffix / custom domain / localhost) · T4-T5 endpoint provider hub URLs · T6-T11 RealtimeHttpAdapter (URL + X-Customer-Token / X-Customer-Device-Id headers, subject POST body, null-coords 200, UTC round-trip parse, 403 error mapping) |
| `RealtimeControllerP3Tests.cs` (+4) | T24 Order + no conversation → ensure via legacy adapter (200) · T25 Shop + no conversation → 404 (no fallback) · T26 existing conversation → legacy NOT consulted · T27 first-message send → fallback creates conversation |

### Deviations so far (đã ghi nhận)

- **RealtimeHttpAdapter không có trackerId param** (card UI-2 có) — RealtimeController stamp TrackerId
  = identity.UserId server-side; client không gửi được trackerId. Ghi nhận: interface theo API thật.
- **RealtimeController fallback Order→IChatService** (không nằm trong card P4) — bắt buộc để migration
  không 404 trên đơn mới; additive, không đổi behavior surface generic cũ.
- **ChatHttpService xoá hẳn** thay vì giữ shim (card UI-10 cho phép "hoặc xoá sau khi migrate hết trang").
- **KhachLink vẫn giữ legacy `/hubs/location` trên OrderTracking** cho `DeliveryStatusUpdate` +
  `OrderStatusUpdated` (generic TrackingHub chỉ mang LocationUpdate); generic hub thêm cho ping push.
  Hậu quả: 3 SignalR connections khi đăng nhập (location + tracking + chat panel) — chấp nhận cho P4,
  consolidate nếu cần ở phase sau.
- **Ping generic Order/orderId** thay Delivery/DeliveryTaskId: DeliveryTaskId = Guid.Empty trên ping mới
  → legacy tracking endpoint (đọc theo DeliveryTaskId) không thấy ping mới — khách/shipper đều đã migrate
  sang generic surface (GetLatestAsync / RecordPingAsync) nên không ảnh hưởng UI hiện tại; legacy endpoint
  giữ cho old builds (GW-6).

---

## 20. REVIEW P2-P6 — FINDINGS (2026-09-17, REVIEW_ONLY)

| # | Severity | Finding | Evidence |
|---|---|---|---|
| F1 | 🔴 BLOCKER (P5) | Logistics/JobMarket **không phải module** — chỉ là preset nav-flags. Không có entity `Shipment`/`JobApplication` nào trong repo. P5 như viết không chứng minh được SC6. | `KhachLinkNavFlags.cs:52-53` (`// TODO R3`) · `sprint8_logistics_task_card.md:3` ⏳ · `sprint9_jobmarket_task_card.md:3` ⏳ |
| F2 | 🔴 BLOCKER (P2) | Unique index chỉ trên `OrderId` (không tenant/subject) → conversation generic thứ 2 toàn DB vi phạm. | `ConversationConfiguration.cs:19` (trước fix) |
| F3 | 🟠 HIGH (P3) | Guest **không có realtime** — SignalR chỉ nhận `customerToken`; D6 mới vá đường HTTP → guest chỉ polling 8s. | `ChatHub.cs:26-36`, `LocationHub.cs:30-40`, `ChatPanel.razor:137-139` |
| F4 | 🟠 HIGH (P4) | UI.Platform **không có `wwwroot/`** và chưa có HTTP adapter nào; chưa định nghĩa cách RCL lấy `HttpClient` (WASM vs Server). | `UI.Platform/` (0 file static) · `ICssAdapter`+`BootstrapAdapter` là adapter duy nhất |
| F5 | 🟡 MED | SVC-7 "adapter mỏng" không khả thi: `ChatService` gắn chặt Order (`c.OrderId ==`, chặn non-DELIVERY, suy ShipperId từ DeliveryTask). | `ChatService.cs:31,70,78-83` |
| F6 | 🟡 MED (P2) | Ping generic để `DeliveryTaskId = Guid.Empty` → index `(DeliveryTaskId, RecordedAt)` vô dụng. | `DeliveryTrackingConfiguration.cs:16,20` (trước fix) |
| F7 | 🟡 MED (P5) | Không có UI chat phía shop trong ShopERP. | grep `Chat|Conversation` trong `5_WebApps/ShopERP/**/*.razor` → chỉ `Settings/ShopFeatures.razor` |
| F8 | 🟡 MED (P5) | `/store/{slug}` anonymous; `customer_device_id` chỉ được tạo ở Checkout → khách chưa có đơn không có identity. | `TenantProfileHttpService.cs:11` · `Checkout.razor:537-542` |
| F9 | 🔵 LOW | Map trang shop là **Google iframe**, không phải Leaflet → thay bằng `VanAnMap` là thay UI hiện hữu (Gate 4: cần E2E). | `GoogleMaps.razor:11-18` · `ShopDto.cs:29-30` |
| F10 | 🔵 LOW | State file ghi `c94a490f` nhưng HEAD thực `d5e055a0`; working tree có 2 file diff **thuần CRLF↔LF** (600/600 dòng). | `git status` · `project_state.md` §3/§10 |
| F11 | 🔵 LOW | Vi phạm UI Platform trong component sẽ được port: `<input class="form-control">` thô, icon `fas fa-*` lẫn `bi bi-*`, ~490 dòng `<style>` inline. | `ChatPanel.razor:87` · `GoogleMaps.razor:25-31` · `Store.razor:268-760` |

**Quyết định sau review (user, 2026-09-17):**
1. GPS trên trang shop = **map tĩnh + khoảng cách** (thay iframe Google bằng `VanAnMap`, không realtime).
2. P5 = **Shop chat**; Logistics/JobMarket giữ cho **P7** (khi Sprint 8/9 thành module thật).
3. Phía shop = **trang inbox riêng** trong ShopERP (`/community/messages`) dùng lại `RealtimeChatPanel`.
4. Thứ tự: **P2 → P3 → P4 → P5(shop)**.

---

## 19. COMPLETION SUMMARY (điền khi xong)

**REALTIME PLATFORM — COMPLETE** — commit `<HASH>` on `main`.

### Files created
| File | Purpose |
|------|---------|
| _TBD_ | _TBD_ |

### Files modified
| File | Change |
|------|--------|
| _TBD_ | _TBD_ |

### Verification

#### Static
- **Build:** _TBD_ · **Unit tests:** _TBD_ · **guard-check.ps1:** _TBD_

#### Live Runtime Verification
| # | Test | Status | Evidence |
|---|------|--------|----------|
| RV1 | Khách Google login → order có CustomerId | _TBD_ | _TBD_ |
| RV2 | Chat 2 chiều realtime (khách ↔ shipper) | _TBD_ | _TBD_ |
| RV3 | Map khách render + marker shipper di chuyển | _TBD_ | _TBD_ |
| RV4 | Shipper reload → GPS vẫn ping | _TBD_ | _TBD_ |
| RV5 | Module thứ 2 dùng lại component (không copy UI) | _TBD_ | _TBD_ |
| RV6 | Regression `/hubs/chat` + `/api/community/*` cũ | _TBD_ | _TBD_ |
