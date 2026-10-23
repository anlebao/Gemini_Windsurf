# Task P0 — ANALYZE: Phi nền tảng Metering (`vanan-metering`)

> **Status:** ✅ DONE — ma trận + phân loại hoàn tất, chờ approve P1
> **Created:** 2026-10-23 · **Phase:** P0 (GĐ-1) — theo `docs/AI/plans/phi-nen-tang-metering-master-plan.md`
> **Tài liệu gốc:** Policy v1.3 · SRS v1.0 (`docs/requirements/van_an_phi_nen_tang_metering_srs_v1.md`)
> **Quyết định đã chốt:** D1–D5, O1–O5 (master plan §3–4)

---

## 1. Ma trận REUSE/MODIFY/EXTEND/BUILD — 4 hook nguồn event

Đã xác minh trực tiếp trong code (không suy đoán).

### Nguồn (a) — `store.visit` — storefront tenant (KhachLink WASM)

| Hạng mục | Quyết định | Chi tiết |
|---|---|---|
| `5_WebApps/KhachLink/Pages/Store.razor` | **MODIFY** | Routes `/store/{Slug}` + `/store/by-id/{TenantId}`; `_store.Id` = tenantId. Fire event sau khi `_store` load xong (JS interop → fetch Gateway). Trang public, không auth → không giữ API key phía client (SRS [G15]) |
| Gateway `POST /api/public/metering/event` | **BUILD** | Endpoint public mới: rate-limited, validate tenantId tồn tại, forward vào `POST /v1/usage/events` với caller credential của Gateway |
| Anti-bot | **BUILD (MVP)** | Lọc User-Agent + fingerprint thô phía Gateway [G16]; fraud rules nâng cấp GĐ-3 |

### Nguồn (b) — `chat.message` + `chat.bytes` — chat

| Hạng mục | Quyết định | Chi tiết |
|---|---|---|
| `3_CoreHub/Services/RealtimeMessagingService.cs` | **MODIFY** | Hook **trong** `SendMessageAsync` — 1 điểm duy nhất phủ mọi caller (`RealtimeController.SendMessage` POST `/api/realtime/conversations/messages`, shop chat, subject-agnostic). TenantId = `Conversation.TenantId` (explicit param — interface đã thiết kế đúng). Payload = UTF8 byte count của `Content` |
| `3_CoreHub/Services/ChatService.cs` (`IChatService`) | **MODIFY** | Đường gửi thứ 2: `CommunityController.SendMessage` → `_chatService.SendMessageAsync` (order chat legacy). TenantId từ Order |
| `2_Gateway/Hubs/MessagingHub.cs` | **REUSE (không hook)** | Hub chỉ chứa subscribe (Join/LeaveConversation) — KHÔNG phải điểm gửi tin. Message đi qua HTTP → service. Không sửa hub |
| `IMeteringClient` inject vào 2 service | **EXTEND** | CoreHub là class lib — inject `IMeteringClient?` (nullable, fire-and-forget); DI đăng ký ở Gateway (host RealtimeMessagingService) |

### Nguồn (c) — `directory.search` + `directory.redirect` — timlathay.com (Directory SSR)

| Hạng mục | Quyết định | Chi tiết |
|---|---|---|
| `Directory/Components/Pages/Home.razor` `SearchStores()` | **MODIFY** | Search chạy server-side (SSR) → fire `directory.search`. **Lưu ý attribution:** search là cross-tenant → ghi event mức platform (tenantId = system/platform tenant cấu hình), không gán tenant đích |
| `Home.razor` `NavigateToStore(slug, khachLinkDomain)` | **MODIFY** | `TenantStoreDto.Id` (Guid) đã có sẵn trong `store` — truyền thêm tenantId vào signature → fire `directory.redirect` **với tenantId đích** trước khi `NavigateTo`. Đây là event referral có giá trị [G16] |
| `StoreFinder.razor` | **REUSE (không meter)** | Trang bounce thuần (redirect ngay sang khachvip.online) — không có nội dung để đo |
| `MeteringApiClient` cho Directory host | **BUILD (client mỏng)** | Directory là host Blazor SSR riêng → cần bản client riêng (cùng contract `VanAn.Metering.Contracts`) |

### Nguồn (d) — `feature.booking` / `feature.financial` / `feature.inventory` — ShopERP

| Hạng mục | Quyết định | Chi tiết |
|---|---|---|
| `3_CoreHub/Services/Booking/BookingService.cs` `CreateBookingAsync` (L63) | **MODIFY** | Per-action: mỗi booking tạo thành công → `feature.booking`. TenantId là param sẵn. Hook ở service phủ cả đường public booking (`BookingPublicHttpService` trên Store.razor — booking entry card L130-154) lẫn staff tạo |
| Financial module | **MODIFY** | O-3 baseline = **module-active-month**: event `feature.financial` 1 lần/tenant/tháng khi tenant dùng module đầu tiên trong tháng (dedupe phía metering theo `serviceCode+period` — không đếm mỗi click). Hook: `Financial/Dashboard.razor` + report services (`ProfitSummaryService`, `BreakEvenAnalysisService`, `CongNoService`) — gọi `FeatureUsageRecorder` chung |
| Inventory module | **MODIFY** | Tương tự: `feature.inventory` monthly-active. Hook: `Inventory/InventoryDashboard.razor`, `Forecast.razor`, `Ingredients.razor` → cùng `FeatureUsageRecorder` |
| `FeatureUsageRecorder` (ShopERP) | **BUILD** | Service mỏng ShopERP: check `check?serviceCode=` (deny) + ghi monthly-active event (dedupe key `feature.{module}:{tenantId}:{yyyy-MM}`) |

### Thành phần hạ tầng dùng chung

| Hạng mục | Quyết định | Chi tiết |
|---|---|---|
| `8_Metering/vanan-metering` | **BUILD** | Toàn bộ service mới (O4/O5) — solution riêng, không ref `1_Shared`/`3_CoreHub` |
| `MeteringApiClient` (ShopERP) | **BUILD** | Pattern clone `5_WebApps/ShopERP/Services/GatewayAdminApiClientBase.cs` (đã xác minh tồn tại) — timeout 3s + circuit breaker |
| `BalanceAlertService` (CoreHub) | **REUSE (pattern)** | Đã xác minh: NON-BLOCKING alert — trả `BalanceAlertResult`, caller quyết hiển thị (feature flag `Balance_Alert_Enabled`). Clone pattern này cho wallet alert UI, KHÔNG tái sử dụng code (service này đo TK 111/152/156 kế toán, không liên quan credit) |
| `IAlertNotifier` + Telegram/Zalo stub | **REUSE** | Kênh notify cho cảnh báo ví 50%/90% |
| `CollaboratorVerificationService` | **REUSE (precedent)** | Flow check-balance→task→charge duy nhất đang chạy; tham khảo, không sửa |
| QR hiển thị | **REUSE (pattern)** | `Store.razor` đã render `QrCodePngBase64` (booking entry L146-151) — top-up QR render tương tự; payload sinh ở metering service [G19] |
| `Idempotency-Key` / unique index | **REUSE (pattern)** | Booking POST + alliance đã có — áp dụng cho events/topup confirm |
| `WalletTransaction`/`AllianceWallet` ledger | **REUSE (pattern)** | Clone cho `CreditLedgerEntry` append-only |
| Domain `1_Shared/Domain.cs` | **KHÔNG ĐỤNG** | Metering service độc lập — entity metering sống trong `VanAn.Metering.Domain`, không vào Domain.cs (tránh vi phạm Single-Source-of-Truth ở phía sai: entity của metering thuộc metering) |

## 2. Phân loại chức năng (cốt lõi / trả phí / tạm ngừng)

| Nhóm | serviceCode | Enforcement baseline [G20] | Lý do |
|---|---|---|---|
| Trả phí — Deny | `feature.booking` `feature.financial` `feature.inventory` `chat.message` `chat.bytes` | **Deny** khi hết ví | Giá trị trực tiếp tenant nhận; chặn được mà không ảnh hưởng buyer |
| Đo — không chặn | `store.visit` `directory.search` `directory.redirect` | **MeterOnly** | Chặn storefront = chặn doanh thu của chính tenant → vẫn đo để charge hậu kỳ (**O-4 đã chốt**) |
| Cốt lõi — không meter | Order/checkout, payment, auth, sổ kế toán bắt buộc pháp luật | Không hook | Không bao giờ đo hay chặn luồng bán hàng/nghèo nghiệp vụ pháp lý |
| Buyer (KhachLink) | — | Không meter | Policy §6: buyer không trả phí nền tảng |

## 3. Quyết định ghi nhận trong P0

1. **Hook chat ở service layer, không ở controller/hub** — `RealtimeMessagingService.SendMessageAsync` + `ChatService.SendMessageAsync` là 2 điểm duy nhất phủ hết đường gửi; MessagingHub chỉ là subscribe.
2. **`directory.search` không gán tenant đích** — search cross-tenant → event platform-level; attribution tenant chỉ có ở `directory.redirect`.
3. **Entity metering không vào `1_Shared/Domain.cs`** — service độc lập giữ domain riêng; governance Single-Source áp cho entities của hệ Vạn An, metering là hệ khác (O1/D3).
4. **`FeatureUsageRecorder` chung 1 service** thay vì rải logic monthly-dedupe ở từng page.
5. **Public ingest qua Gateway** thay vì cho WASM gọi thẳng metering — WASM không giữ credential [G15].

## 4. Open question — ✅ TẤT CẢ ĐÃ CHỐT (23/10/2026)

| # | Câu hỏi | Quyết định |
|---|---|---|
| O-4 | Hết ví có chặn `store.visit`/`directory.*`? | ✅ **MeterOnly** (không chặn traffic bán hàng); Deny chỉ `feature.*` + `chat.*` |
| O-3 phụ | `feature.financial`/`inventory` per-action hay monthly? | ✅ **Module-active-month** (dedupe `serviceCode+tenantId+yyyy-MM`) |
| — | Ngưỡng cảnh báo mặc định | 50% và 90% đã dùng [G18] — cấu hình được, đổi không cần deploy |

## 5. Output chuyển giao P1–P7

- **P1:** skeleton `8_Metering/vanan-metering` (minimal API + SQLite + UsageEvent + CallerKey + Dockerfile)
- **P2:** RateCardVersion CRUD + audit (thêm cột `Enforcement`)
- **P3:** ingest events/batch + idempotency + summary + deduct-at-ingest
- **P4:** `MeteringApiClient` ShopERP + `FeatureUsageRecorder` + 4 hook (a: Gateway public endpoint + Store.razor JS; b: 2 chat service; c: Home.razor NavigateToStore + SearchStores; d: BookingService + FeatureUsageRecorder)
- **P7:** CreditWallet/Ledger + check API + WalletAlert + TopupConfig/TopupRequest + VietQR + UI banner/topup/confirm
