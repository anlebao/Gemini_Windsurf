# VanAn Community Commerce — Requirements Specification

Đặc tả yêu cầu chính thức cho module Shipper/Salesman (Community Commerce), chia thành 7 sprint (S0-S6), dựa trên đối chiếu với codebase thực tế.

> **Revision history**
> - v1.0 — initial spec (6 sprint).
> - v1.1 — 2026-07-25 — Baseline corrections (social auth đã tồn tại, OrderType là string, "delivering" status), Single-Identity Pattern compliance cho 7 entity mới, redesign Salesman model (composite referral code salesman+product, per-product commission 2-5%, app-install bonus per-product do sysadmin thiết lập — không hardcode), thêm UC-12 (App install attribution bonus), WalletTransaction Reversal pattern, adaptive GPS polling, multi-tenancy isolation cho Community entities.
> - v1.2 — 2026-07-26 — **Self-hosted anti-fraud architecture (zero external dependency):** thêm 2 entity `DeviceRegistration` + `FraudFlag`, thêm `RiskScore` field trên `SalesReferral` + `AppInstallAttribution`, mở rộng `IdentityLevel` (thêm `DeviceVerified=4`), UC-09/UC-12 risk scoring + hold 48h if score≥60, Sprint 0 +2 entity, Sprint 6 +Fraud Review UI. **WebAuthn Passkey OPTIONAL** (post-PoC, zero vendor dependency). **SMS OTP OPTIONAL** (không bắt buộc — replaced by device fingerprint + behavioral rules + KYC bank account cho payout). 5-layer fraud prevention: device fingerprint → device token → behavioral rules → risk scoring → (post-PoC) native attestation.
> - **v1.3 — 2026-07-26 — Phase A Review Fixes (9 BLOCKING items resolved):** (A1) Email/Password login DEFER to Sprint 7+ (post-PoC) — KHÔNG trong PoC scope, tránh contradiction với Section 5.10. PoC auth = Social (Google + Facebook UI Sprint 1) + Device Fingerprint. (A2) Community entities PG ONLY — remove SQLite migration for community tables (cross-tenant data on Gateway PG). (A3) ChatHub/LocationHub auth via X-Customer-Token query string (SignalR support). (A4) "delivering" status flagged as Domain Modification in CC-S1-T0. (A5) IdentityLevel.DeviceVerified=4 flagged as Domain Modification in Sprint 0. (A6) UI spec added: OrderDetail.razor (S1), DeliveryTracking buttons (S2), Wallet dialogs (S5), ShopSettlement (S5), ReverseTransaction admin (S5). (A7) Mobile nav decision: community tabs vào NavMenu.razor (9-tab layout). (A8) Device Fingerprint Consent Dialog spec — GDPR/PDPA compliance. (A9) nginx route for locationHub/chatHub + vendor Leaflet/FingerprintJS (no CDN — consistent zero-dependency).
> - **v1.4 — 2026-07-26 — Hybrid Central + Edge Architecture (CORE COMPETITIVE ADVANTAGE):**新增 Section 7C "Architecture Evolution & Scale-Up Roadmap" — target architecture (Hybrid Central + Edge diagram), 11 bottleneck B1-B11 (CRITICAL/HIGH/MEDIUM), 10 short-term solutions ST1-ST10, 8 long-term solutions LT1-LT8, 9 corrections (edge-only sai ở đâu), 8 refactor impact reduction R1-R8, cuốn chiếu strategy (80% roll + 20% big-bang), architecture evolution roadmap (PoC → 10M users), 12 hard rules mới HR-SCALE-1 đến HR-SCALE-12. Lợi thế cạnh tranh: scale từ 50 users (PoC $50/tháng) đến 10M users ($100K/tháng optimized) với zero paid external services. Break-even edge vs central: ~1M users.

---

## 1. Business Context

### 1.1 Mục tiêu
Biến KhachLink từ customer-only PWA thành nền tảng Community Commerce: một user có thể đồng thời là Buyer, Salesman, Shipper. System Admin kích hoạt vai trò cộng tác viên cho customer đạt điều kiện.

### 1.2 Điều kiện kích hoạt vai trò cộng tác viên
- **Shipper:** IdentityLevel ≥ `Verified` (SMS OTP) **HOẶC** IdentityLevel ≥ `DeviceVerified` (v1.2: device fingerprint đã pass) + LoyaltyPoints ≥ 1000
- **Salesman:** IdentityLevel ≥ `Verified` **HOẶC** IdentityLevel ≥ `DeviceVerified` (v1.2) + LoyaltyPoints ≥ 1000
- **Kích hoạt bởi:** System Admin qua Gateway Admin API

### 1.2.1 Salesman earning model (v1.1 — redesign)
Salesman kiếm tiền từ **2 nguồn**, cả 2 do **SystemAdmin thiết lập per-product** (KHÔNG hardcode):
1. **Commission chốt đơn:** 2-5% giá trị đơn hàng khi customer đặt hàng qua referral code của salesman. Rate lấy từ `ProductReferralConfig.CommissionRate` của product mà salesman chọn giới thiệu.
2. **App-install bonus:** Thưởng cố định khi salesman thuyết phục customer cài KhachLink PWA. Amount lấy từ `ProductReferralConfig.AppInstallBonus` của product mà salesman chọn giới thiệu. Attribution: customer có referralCode trong localStorage khi trigger PWA install event.

**Composite referral code:** Mã salesman được **gộp chung vào mã sản phẩm** mà salesman chọn để giới thiệu/chốt đơn. Format: `{salesmanCode}|{productShortCode}` (vd `ABC123|TR-001`). Customer scan QR → cả 2 lưu localStorage → gửi khi order creation → resolve salesmanId + productId.

### 1.3 Phạm vi PoC (Giai đoạn 1 — 15 ngày)
- 10-20 cửa hàng F&B cùng 1 khu vực
- 50-100 khách hàng
- 10 cộng tác viên (Sales + Shipper)
- 300-500 đơn hàng thực
- 95% đơn giao thành công

### 1.4 Platform: PWA (Blazor WASM)
- Giữ PWA cho PoC. GPS tracking chỉ hoạt động khi tab active.
- Real-time tracking qua SignalR polling (10s interval) thay vì background tracking.
- Đánh giá lại native app sau PoC nếu cần background GPS.

### 1.5 Anti-Fraud Policy (v1.2 — Self-hosted, zero external dependency)

**Nguyên tắc:** KHÔNG phụ thuộc SMS gateway, Zalo OA, WhatsApp, hay bất kỳ nhà cung cấp dịch vụ bên ngoài nào cho việc xác thực + chống gian lận. Toàn bộ anti-fraud logic self-hosted trong hệ thống VanAn.

**5 lớp phòng thủ (defense-in-depth):**

| Lớp | Technique | Cost | Coverage | Dependency |
|---|---|---|---|---|
| 1 | **Device Fingerprint** — FingerprintJS (MIT, self-host) thu thập 15+ signals (canvas/WebGL/audio/fonts/timezone/...) → SHA256 hash | $0 | 80% fraud | Zero |
| 2 | **Device Token (persisted)** — server-signed UUIDv7+HMAC, lưu localStorage + IndexedDB + DB. Max 3 devices/customer | $0 | +10% | Zero |
| 3 | **Behavioral rules** — SQL query đơn giản: salesman-customer fingerprint match, same IP 24h, >3 accounts/device/day, app-install <30s | $0 | +5% | Zero |
| 4 | **Risk Scoring** — deterministic 0-100 score per SalesReferral/AppInstallAttribution. Score≥60 → hold 48h manual review. Score≥80 → auto-reject | $0 | Manual review catch | Zero |
| 5 | **Native App Attestation** (post-PoC, OPTIONAL) — iOS App Attest (verify với Apple public root CA, KHÔNG cần Apple API) + Android Play Integrity (cần Google API — optional) | $0 | +5% chặn VM farm | Zero (iOS) / Google (Android optional) |

**Target fraud rate:** <0.5% (so với 100% lý tưởng không khả thi, so với 2-5% nếu chỉ email/password).

**Phụ thuộc bên ngoài CHO PHÉP:**
- Social login (Google/Facebook OAuth) — đã có `SocialAuthController.cs`. OAuth provider KHÔNG phải nhà cung cấp dịch vụ VN, là identity provider công cộng. **OPTIONAL** — customer có thể đăng nhập bằng email/password nếu không muốn social.
- WebAuthn Passkey — chuẩn W3C, browser native (Chrome/Edge/Firefox/Safari 2022+). **Zero vendor dependency.**

**KHÔNG phụ thuộc:**
- ❌ SMS gateway (Viettel/Twilio) — OPTIONAL, không bắt buộc
- ❌ Zalo OA — OPTIONAL, không bắt buộc
- ❌ WhatsApp Business API — OPTIONAL, không bắt buộc
- ❌ Kafka, Synadia managed, RDS managed — dùng self-host NATS + self-host PG + PgBouncer

**Payout fraud prevention (KYC bank account):**
- Salesman rút commission/bonus YÊU CẦU admin-verified bank account (KYC)
- Minimum payout 500K VND (chi phí transfer > nhỏ bonus → economic disincentive)
- Hold commission/app-install bonus 48h nếu RiskScore ≥ 60
- Salesman account ban nếu FraudFlag confirmed (3 strikes → permanent ban)

---

## 2. Hiện trạng Codebase (Baseline)

> **v1.1 corrections:** Baseline đã verify trực tiếp với codebase 2026-07-25. Các mục ✅ đã confirm tồn tại.

| Component | Status | File |
|---|---|---|
| Customer auth (OTP) | ✅ Tồn tại | `ShopERP/Controllers/CustomerIdentityController.cs` — OTP send/verify, `X-Customer-Token` |
| Customer token service | ✅ Tồn tại | `ICustomerTokenService.CreateToken(customerId)` — custom token, KHÔNG phải JWT |
| **Social auth (Google)** | ✅ **Tồn tại (v1.1 correction)** | `ShopERP/Controllers/SocialAuthController.cs` — `GET /api/auth/google/login` + `GET /api/auth/google/callback`. Tiered Auth P1 PASS, P4 Facebook PASS (project_state.md:182). **KHÔNG cần tạo mới trong Sprint 0.** |
| Customer entity | ✅ Tồn tại | `Domain.cs:626` — có `LoyaltyPoints`, `IdentityLevel`, `PhoneNumber`, `DeviceId` |
| IdentityLevel enum | ✅ Tồn tại | `Domain.cs:615` — Guest=0, Social=1, Verified=2, Full=3 |
| Order entity | ✅ Tồn tại | `Domain.cs:1359` — có `DeliveryAddress` (string), `OrderType` (**string field**, KHÔNG phải enum — query bằng string literal `"DINEIN"`, `"TAKEAWAY"`, `"DELIVERY"`) |
| OrderStatusId | ✅ Tồn tại | `Domain.cs:422` — `record OrderStatusId(string Value)`. Constants: pending→confirmed→preparing→ready→delivering→completed→cancelled. **Lưu ý (v1.1):** `OrderStatuses.Default[]` (Domain.cs:458-508) CHỈ có 6 trạng thái — **KHÔNG có `"delivering"`**. Cần verify `OrderWorkflowService.IsTransitionValidAsync` có chấp nhận `"delivering"` hay cần thêm (Sprint 1 task CC-S1-T0). |
| OrderWorkflowService | ✅ Tồn tại | `OrderWorkflowService.cs:49` — `TransitionStatusAsync(orderId, newStatus, reason)` + `IsTransitionValidAsync`. |
| SignalR hubs | ✅ Tồn tại | `OrderHub`, `KitchenHub` — chỉ join/leave groups |
| TenantSettings lat/lng | ✅ Tồn tại | `TenantSettings.cs:16-17` — `Latitude` (double?), `Longitude` (double?) |
| StoreFinder GPS | ✅ Tồn tại | `StoreFinder.razor` — `navigator.geolocation` 1 lần + radius filter |
| GoogleMaps component | ✅ Tồn tại | `GoogleMaps.razor` — iframe embed tĩnh |
| QRScanner | ✅ Tồn tại | `QRScanner.razor` — scan tenant QR |
| Loyalty system | ✅ Tồn tại | `LoyaltyRewardsService`, `LoyaltyRewards` entity, missions, redemption |
| Voucher QR | ✅ Tồn tại | `Domain.cs:1095` — `Voucher.QRCodeData` |
| Gateway auth | ✅ Tồn tại | JWT + Cookie dual-scheme, policies: `RequireTenantAccess`, `SystemAdmin` |
| UserRole enum | ✅ Tồn tại (2 nơi) | `Domain.cs:437` (marked `[Obsolete]`)+ `Domain/Aggregates/UserAggregate/UserRole.cs:8` (active). Chỉ có Owner/StoreKeeper/Guard/Staff/Masterchef — **KHÔNG thêm Shipper/Salesman vào đây**. Community role tách biệt qua `CommunityRole` entity mới (cross-tenant, không phải tenant RBAC). |
| Shipper/Salesman entities | ❌ Không tồn tại | Cần tạo mới (CommunityRole + CommunityRoleType enum MỚI) |
| DeliveryTask/Assignment | ❌ Không tồn tại | Cần tạo mới |
| Chat hub/message | ❌ Không tồn tại | Cần tạo mới |
| Wallet/Commission | ❌ Không tồn tại | Cần tạo mới |
| SalesReferral/QR | ❌ Không tồn tại | Cần tạo mới (v1.1: redesign — composite code salesman+product) |
| ProductReferralConfig | ❌ Không tồn tại | Cần tạo mới (v1.1 — per-product commission rate + app-install bonus) |
| AppInstallAttribution | ❌ Không tồn tại | Cần tạo mới (v1.1 — track app install attribution cho salesman bonus) |
| Customer location entity | ❌ Không tồn tại | Cần tạo mới (v1.1: bỏ — không có UC rõ ràng, dùng `Order.DeliveryLat/Lng` cho delivery) |
| GPS real-time hub | ❌ Không tồn tại | Cần tạo mới |
| Community role | ❌ Không tồn tại | Tách biệt với `UserRole` enum (tenant RBAC) — dùng `CommunityRole` entity cross-tenant |

---

## 3. Use Cases

### UC-01: Customer đăng nhập (Social + Device Fingerprint, v1.2: SĐT + Email/Password OPTIONAL post-PoC)
**Actor:** Customer
**Precondition:** Chưa đăng nhập
**Flow:**
1. Customer mở KhachLink → bấm "Đăng nhập"
2. Chọn phương thức đăng nhập (PoC scope):
   - **(A) Social login (Google)** → social auth redirect → tiếp bước 3
   - **(B) Social login (Facebook)** → social auth redirect → tiếp bước 3 (v1.2: UI Facebook button cần bổ sung trong Sprint 1)
3. First login hoặc new device: browser generate DeviceToken + compute Fingerprint → show **Device Fingerprint Consent Dialog** (v1.2 NEW — user phải đồng ý trước khi collect) → nếu OK → POST `/api/community/device/register` → server lưu `DeviceRegistration`
4. IdentityLevel = `Social` (nếu social)
5. (OPTIONAL) SĐT verify qua SMS OTP — **KHÔNG bắt buộc**, chỉ nếu customer muốn upgrade lên `Verified`
6. **Post-PoC (Sprint 7+):** Email + Password login + WebAuthn Passkey — KHÔNG trong PoC scope (v1.2 correction: defer để tránh contradict Section 5.10)
**Postcondition:** Customer có token, lưu trong localStorage. DeviceRegistration tồn tại.
**Acceptance Criteria:**
- AC-01.1: Social login (Google) hoạt động (đã có `SocialAuthController.cs`)
- AC-01.2 (v1.2 CORRECTED): **Facebook login UI** bổ sung trong Sprint 1 (controller đã có `SocialAuthController.cs`, UI button cần thêm vào `Login.razor`). Email + Password login **DEFER to Sprint 7+ (post-PoC)** — KHÔNG trong PoC scope.
- AC-01.3 (v1.2 NEW): DeviceRegistration tạo cho mọi login mới (fingerprint + device token) — **chỉ sau khi user đồng ý qua Device Fingerprint Consent Dialog**
- AC-01.4 (v1.2 NEW): Max 3 active devices per Customer — device thứ 4 yêu cầu admin approval
- AC-01.5: (OPTIONAL) SMS OTP gửi thành công nếu customer chọn verify SĐT — **KHÔNG bắt buộc**
- AC-01.6: (OPTIONAL) OTP verify trả về `X-Customer-Token` + `CustomerId`, upgrade IdentityLevel=Verified
- AC-01.7: Token lưu localStorage, tự tự động gửi trong header mọi API call
- AC-01.8 (v1.2 NEW): IdentityLevel values: Guest=0, Social=1, Verified=2, Full=3, **DeviceVerified=4 (v1.2 NEW — Domain Modification, add trong Sprint 0)**
- AC-01.9 (v1.2 NEW): Device Fingerprint Consent Dialog hiển thị trước khi collect fingerprint — user có thể decline (lúc đó RiskScore sẽ cao hơn do không fingerprint)

**Lưu ý codebase (v1.1+v1.2+v1.3):**
- OTP flow đã có (`CustomerIdentityController.otp/send` + `otp/verify`) — **OPTIONAL** trong v1.2.
- Social auth (Google) ĐÃ CÓ (`SocialAuthController.cs` — Tiered Auth P1 PASS).
- Facebook auth controller ĐÃ CÓ nhưng UI button CHƯA có — Sprint 1 bổ sung UI.
- Email/password login KHÔNG có — **DEFER Sprint 7+** (v1.3 correction: tránh contradiction với Section 5.10).
- Device fingerprint + DeviceRegistration MỚI (v1.2 Sprint 0). Consent dialog MỚI (v1.3 — phải có trước khi collect fingerprint, GDPR/PDPA compliance).

### UC-02: System Admin kích hoạt vai trò Shipper/Salesman
**Actor:** System Admin
**Precondition:** Customer đạt IdentityLevel ≥ Verified + LoyaltyPoints ≥ 1000
**Flow:**
1. Admin mở Gateway Admin API
2. GET /api/admin/community/eligible — list customer đủ điều kiện
3. POST /api/admin/community/{customerId}/activate-role — body: `{ role: "Shipper" | "Salesman" }`
4. Customer nhận notification (push notification)
**Acceptance Criteria:**
- AC-02.1: API trả list customer đủ điều kiện (Verified + ≥1000 points)
- AC-02.2: Activate role tạo bản ghi `CommunityRole` gắn với Customer
- AC-02.3: Customer thấy role mới trong Profile page sau khi login
- AC-02.4: Push notification gửi khi activate
- AC-02.5: Admin có thể deactivate role

### UC-03: Shipper thấy đơn hàng gần (Nearby Orders)
**Actor:** Shipper
**Precondition:** Đã đăng nhập, có role Shipper, đã share vị trí
**Flow:**
1. Shipper mở KhachLink → "Đơn hàng gần" page
2. Browser lấy GPS vị trí hiện tại (1 lần)
3. Gọi API: GET /api/community/nearby-orders?lat={lat}&lng={lng}&radiusKm=5
4. Hiển thị list đơn hàng DELIVERY trong bán kính, kèm khoảng cách
5. Mỗi đơn hiện: shop name, delivery address, total amount, status
**Acceptance Criteria:**
- AC-03.1: GPS lấy vị trí thành công (consent prompt)
- AC-03.2: API trả đơn hàng `OrderType=DELIVERY` + status `confirmed` hoặc `ready` trong bán kính
- AC-03.3: Khoảng cách tính bằng Haversine formula
- AC-03.4: Sort theo khoảng cách tăng dần
- AC-03.5: Không hiện đơn đã được assign cho shipper khác

**Lưu ý codebase:** Cần thêm `DeliveryCoordinates` (lat/lng) vào Order. Hiện `DeliveryAddress` chỉ là string. Cần cross-tenant query (Gateway PG source of truth cho Orders).

### UC-04: Shipper nhận đơn (Accept Order)
**Actor:** Shipper
**Precondition:** Đơn ở status `ready` hoặc `confirmed`, chưa được assign
**Flow:**
1. Shipper bấm "Nhận đơn" trên đơn trong list
2. POST /api/community/orders/{orderId}/accept
3. Tạo `DeliveryTask` record: shipperId, orderId, status=Assigned
4. Order status → `delivering` (nếu đang `ready`) hoặc giữ `confirmed` (nếu chưa ready)
5. Shipper thấy shop location + customer location
**Acceptance Criteria:**
- AC-04.1: Chỉ 1 shipper accept được (concurrency: optimistic locking hoặc DB unique constraint)
- AC-04.2: DeliveryTask tạo với status=Assigned
- AC-04.3: Order.ShipperId set = shipper's CustomerId
- AC-04.4: Shipper thấy shop lat/lng (từ TenantSettings) + customer lat/lng (từ DeliveryCoordinates)
- AC-04.5: Nếu đơn đã được accept → trả 409 Conflict

### UC-05: Shipper cập nhật trạng thái giao hàng
**Actor:** Shipper
**Precondition:** Đã accept đơn, DeliveryTask status=Assigned
**Flow:**
1. Shipper đến shop → bấm "Đã nhận hàng" → DeliveryTask.PickedUpAt
2. Shipper giao hàng → bấm "Đang giao" → DeliveryTask.OutForDelivery
3. Shipper đến nơi → bấm "Đã giao" → DeliveryTask.DeliveredAt
4. Order status → `completed`
5. (Tùy chọn) Shipper bấm "Giao thất bại" → DeliveryTask.Failed + reason
**Acceptance Criteria:**
- AC-05.1: State machine: Assigned → PickedUp → OutForDelivery → Delivered/Failed
- AC-05.2: Mỗi transition ghi timestamp
- AC-05.3: Order status sync: Delivered → Order.Completed
- AC-05.4: Customer nhận SignalR notification mỗi transition
- AC-05.5: Failed transition yêu cầu reason text

### UC-06: Shipper cập nhật vị trí real-time
**Actor:** Shipper
**Precondition:** DeliveryTask status = OutForDelivery
**Flow:**
1. Shipper page tự poll GPS mỗi 10s (khi tab active)
2. POST /api/community/location/update { lat, lng }
3. Server push qua SignalR → customer subscribe order_{orderId}
4. Customer thấy marker shipper di chuyển trên map
**Acceptance Criteria:**
- AC-06.1: GPS polling 10s interval khi tab active
- AC-06.2: Location lưu vào `DeliveryTracking` table (append-only)
- AC-06.3: SignalR push đến customer trong order group
- AC-06.4: Map hiển thị marker shipper (Leaflet, không phải iframe)
- AC-06.5: Dừng polling khi DeliveryTask = Delivered/Failed

**Lưu ý PWA:** GPS chỉ hoạt động khi tab active. Không có background tracking. Thông báo cho user "Giữ app mở để cập nhật vị trí".

### UC-07: Chat giữa Customer và Shipper
**Actor:** Customer, Shipper
**Precondition:** DeliveryTask tồn tại (shipper đã accept đơn)
**Flow:**
1. Customer/Shipper mở chat panel trong order detail
2. Nhập message → POST /api/community/chat/messages
3. SignalR push đến đối phương
4. Message lưu DB (Conversation + Message entity)
**Acceptance Criteria:**
- AC-07.1: Chat chỉ mở khi DeliveryTask tồn tại
- AC-07.2: Message lưu DB với timestamp, senderId, receiverId
- AC-07.3: SignalR real-time push (ChatHub)
- AC-07.4: Chat history load khi mở panel
- AC-07.5: Không có AI chatbot trong PoC (chỉ human-to-human chat)

### UC-08: Salesman thấy sản phẩm tenant gần (Nearby Products) + chọn product để giới thiệu
**Actor:** Salesman
**Precondition:** Đã đăng nhập, có role Salesman, đã share vị trí
**Flow:**
1. Salesman mở "Sản phẩm gần" page
2. GPS lấy vị trí → GET /api/community/nearby-products?lat={lat}&lng={lng}&radiusKm=10
3. Hiển thị list products từ các tenant trong bán kính, kèm **commission rate** + **app-install bonus** từ `ProductReferralConfig` (nếu product chưa có config → hiển thị "Chưa thiết lập")
4. Salesman chọn 1 product → bấm "Tạo mã QR giới thiệu" → generate composite referral code `{salesmanCode}|{productShortCode}`
5. Mở "Mã QR của tôi" page hiển thị QR cho product đã chọn
**Acceptance Criteria:**
- AC-08.1: API trả products từ tenants có TenantSettings.Latitude/Longitude trong bán kính
- AC-08.2: Mỗi product hiện: name, price, shop name, distance, **commissionRate** (từ ProductReferralConfig), **appInstallBonus** (từ ProductReferralConfig)
- AC-08.3: Sort theo khoảng cách
- AC-08.4: Product chưa có ProductReferralConfig → hiển thị "Chưa thiết lập" (salesman vẫn có thể chọn nhưng commission/bonus = 0)
- AC-08.5: Salesman chọn product → composite referral code generate client-side

**Lưu ý codebase (Option C):** Products sống trong per-tenant SQLite. Gateway PG chỉ có `FeaturedProducts`. Cần giải pháp:
- **PoC approach:** Chỉ hiển thị FeaturedProducts (PG) có tenant coordinates trong bán kính. Không query tất cả products.
- **Post-PoC:** Build product search index trên Gateway PG.

### UC-09: Salesman chia sẻ QR chứa composite code (salesman + product)
**Actor:** Salesman → Customer
**Precondition:** Salesman có role + đã chọn 1 product từ UC-08
**Flow:**
1. Salesman mở "Mã QR của tôi" page (sau khi chọn product)
2. Hiển thị QR code chứa URL: `https://khachlink.app/r/{salesmanCode}|{productShortCode}` (vd `/r/ABC123|TR-001`)
3. Customer quét QR → redirect đến KhachLink với composite referral code
4. Composite referral code lưu trong localStorage (cả salesmanCode + productShortCode)
5. Khi customer đặt hàng → composite referral code gửi trong order creation
6. Server resolve → set `Order.SalesmanId` + `Order.ReferralProductId`
**Acceptance Criteria:**
- AC-09.1: Mỗi Salesman có unique `SalesmanCode` (6-8 chars, human-readable)
- AC-09.2: QR generate client-side (qrcode.js library), chứa composite code
- AC-09.3: Composite referral code lưu localStorage khi scan (cả 2 phần)
- AC-09.4: Order creation gửi `referralCode` field (composite format)
- AC-09.5: Order lưu `SalesmanId` (resolve từ salesmanCode) + `ReferralProductId` (resolve từ productShortCode)
- AC-09.6 (v1.3 CORRECTED): ProductShortCode lookup: query **`ProductReferralConfig` table (Gateway PG)** theo `ProductShortCode` → get `ProductId` → set `Order.ReferralProductId`. **KHÔNG query Product table (ShopERP SQLite)** — ProductReferralConfig đã có ProductId + ProductShortCode trên PG, không cần cross-DB lookup. Fallback: nếu ProductReferralConfig không có ProductShortCode (null), dùng ProductId trực tiếp (salesman generate QR với ProductId thay vì short code).
- AC-09.7 (v1.2 NEW): Khi Order.Completed → SalesReferral tạo với `RiskScore` computed (deterministic 0-100)
- AC-09.8 (v1.2 NEW): RiskScore ≥ 60 → `CommissionStatus=Pending` hold 48h + tạo `FraudFlag(Status=Pending)` cho admin review
- AC-09.9 (v1.2 NEW): RiskScore ≥ 80 → auto-reject commission (`CommissionStatus=Rejected`) + FraudFlag(Status=Pending)
- AC-09.10 (v1.2 NEW): RiskScore < 60 → `CommissionStatus=Pending` auto-approve sau 24h (cooling period)
- AC-09.11 (v1.2 NEW): RiskScore factors: salesmanFingerprint==customerFingerprint (+50), same IP 24h (+30), customerAgeDays<7 (+30), deviceFirstSeen<24h (+25), ordersFromDeviceToday>3 (+20), referralBonusAmount>50K (+15), appInstallTime<30s (+40), blacklistedFingerprint (+60)

### UC-10: Salesman xem doanh số, hoa hồng chốt đơn + thưởng app-install
**Actor:** Salesman
**Precondition:** Có role Salesman, có đơn hàng gắn SalesmanId
**Flow:**
1. Salesman mở "Doanh số" page
2. GET /api/community/salesman/{salesmanId}/commissions
3. Hiển thị: list đơn đã chốt, tổng doanh số, **commission chốt đơn** (per-order, rate từ ProductReferralConfig), **thưởng app-install** (per attributed install), trạng thái thanh toán
4. Phân tách 2 nguồn thu: commission (Pending/Paid) + app-install bonus (Pending/Paid)
**Acceptance Criteria:**
- AC-10.1: API trả list Order có SalesmanId = salesman's CustomerId
- AC-10.2: Commission chốt đơn tính theo `ProductReferralConfig.CommissionRate` của product trên order (2-5%, per-product, KHÔNG hardcode)
- AC-10.3: Commission status: Pending → Paid
- AC-10.4: App-install bonus: list `AppInstallAttribution` có SalesmanId, mỗi attribution hiển thị bonus amount (từ ProductReferralConfig.AppInstallBonus của product referral) + status
- AC-10.5: Tổng doanh số + tổng commission + tổng app-install bonus hiển thị tách biệt

### UC-11: Shipper ứng tiền + thu COD
**Actor:** Shipper
**Precondition:** Đã accept đơn, order PaymentMethod = COD
**Flow:**
1. Shipper thấy "Cần ứng tiền" trên đơn (nếu shop yêu cầu)
2. Shipper xác nhận đã ứng tiền cho shop
3. Shipper thu tiền của customer khi giao
4. Shipper xác nhận đã thu COD
5. Wallet transaction ghi: +COD amount cho shipper, -COD amount từ customer
**Acceptance Criteria:**
- AC-11.1: Order.PaymentMethod hỗ trợ "COD"
- AC-11.2: Shipper xác nhận "đã ứng tiền" → timestamp
- AC-11.3: Shipper xác nhận "đã thu COD" → WalletTransaction tạo
- AC-11.4: Wallet balance cập nhật
- AC-11.5: Settlement record tạo cho shop (shop nhận tiền từ shipper)

### UC-12: Salesman được thưởng khi customer cài app (App Install Attribution Bonus) — v1.1 NEW
**Actor:** Customer (install PWA) → Salesman (receive bonus)
**Precondition:** Customer có composite referralCode trong localStorage (đã scan QR của salesman ở UC-09), customer chưa cài PWA trước đó
**Flow:**
1. Customer mở KhachLink lần đầu (có referralCode trong localStorage)
2. Customer trigger PWA install (browser `beforeinstallprompt` event → bấm "Cài app")
3. PWA install success event (`appinstalled`) → POST /api/community/app-install/attributed với body: `{ referralCode: "ABC123|TR-001" }`
4. Server resolve referralCode → salesmanId + productId
5. Server check customer chưa có AppInstallAttribution trước đó (1 customer chỉ được attribute 1 lần)
6. Tạo `AppInstallAttribution` record: customerId, salesmanId, productId, bonusAmount (lấy từ `ProductReferralConfig.AppInstallBonus` của product referral)
7. Tạo `WalletTransaction` type=Commission, amount=bonusAmount cho salesman
8. Salesman thấy bonus trong SalesDashboard (UC-10)
**Acceptance Criteria:**
- AC-12.1: PWA install event trigger API call chỉ khi có referralCode trong localStorage
- AC-12.2: 1 customer chỉ được attribute 1 lần (unique constraint trên AppInstallAttribution.CustomerId)
- AC-12.3: Bonus amount lấy từ `ProductReferralConfig.AppInstallBonus` của product referral — KHÔNG hardcode
- AC-12.4: WalletTransaction type=Commission tạo cho salesman với amount = bonusAmount
- AC-12.5: Nếu ProductReferralConfig không tồn tại hoặc AppInstallBonus = 0 → không tạo WalletTransaction (no-op, vẫn ghi AppInstallAttribution với bonusAmount=0)
- AC-12.6: Salesman thấy bonus trong SalesDashboard với status Pending → Paid (admin settle)
- AC-12.7: Customer đã cài app trước khi scan QR → không qualify (check DeviceRegistration install history)
- AC-12.8 (v1.2 NEW): AppInstallAttribution tạo với `RiskScore` computed (deterministic 0-100)
- AC-12.9 (v1.2 NEW): RiskScore ≥ 60 → bonus hold 48h + FraudFlag(Status=Pending) cho admin review
- AC-12.10 (v1.2 NEW): RiskScore ≥ 80 → auto-reject bonus + FraudFlag
- AC-12.11 (v1.2 NEW): RiskScore < 60 → auto-approve bonus sau 24h cooling period
- AC-12.12 (v1.2 NEW): Anti-fraud signals: salesmanFingerprint==customerFingerprint (+50), same IP 24h (+30), customerAgeDays<7 (+30), deviceFirstSeen<24h (+25), appInstallTime<30s after referralScan (+40), blacklistedFingerprint (+60)

---

## 4. Domain Entities (Mới)

> **v1.1 — Single-Identity Pattern compliance (governance.md):** Mọi entity mới kế thừa `BaseEntity` PHẢI tuân thủ Single-Identity Pattern:
> - Constructor sync `Id = BusinessKey.Value` (nếu có business key VO) HOẶC dùng `BaseEntity.Id` trực tiếp (không có business key VO — phải explicit trong detailed plan).
> - EF config: `builder.Ignore(e => e.BusinessKey)` nếu có business key VO.
> - LINQ queries filter by `e.Id == someGuid`, KHÔNG filter by `e.BusinessKey == new BusinessKey(someGuid)`.
> - FK references trỏ tới `BaseEntity.Id` (PK).
> - Reference impl: `Order.Create` (Domain.cs:1467-1478) syncs `Id = OrderId.Value`.
>
> **v1.1 — Domain Modification flag:** Việc thêm 7 entity mới + fields vào Order là **Domain Modification approved as part of Community Commerce feature plan** (tương đương KhachLink Phase 5 pattern — project_state.md:57-62). Chỉ thực hiện trong Sprint 0 Domain Phase, có user approval.

### 4.1 CommunityRole
```
CommunityRole : BaseEntity, IMustHaveTenant
- CustomerId (Guid, FK → Customer.Id)
- RoleType (enum CommunityRoleType: Shipper=1, Salesman=2)
- ActivatedAt (DateTime)
- ActivatedBy (Guid, admin userId)
- DeactivatedAt (DateTime?)
- IsActive (bool)
- SalesmanCode (string?, unique, 6-8 chars) — chỉ cho Salesman
```
**Single-Identity:** Dùng `BaseEntity.Id` trực tiếp (không có `CommunityRoleId` VO). Constructor public, EF config `HasKey(e => e.Id)` + unique index trên `SalesmanCode` (filtered, chỉ non-null).

### 4.2 DeliveryTask
```
DeliveryTask : BaseEntity, IMustHaveTenant
- OrderId (Guid, FK → Order.Id)
- ShipperId (Guid, FK → Customer.Id)
- Status (enum DeliveryTaskStatus: Assigned=1, PickedUp=2, OutForDelivery=3, Delivered=4, Failed=5, Cancelled=6)
- AssignedAt (DateTime)
- PickedUpAt (DateTime?)
- OutForDeliveryAt (DateTime?)
- DeliveredAt (DateTime?)
- FailedAt (DateTime?)
- FailureReason (string?)
- ShopLat, ShopLng (double)
- CustomerLat, CustomerLng (double?)
```
**Single-Identity:** Dùng `BaseEntity.Id` trực tiếp. State machine methods: `MarkPickedUp`, `MarkOutForDelivery`, `MarkDelivered`, `MarkFailed(reason)`, `Cancel` — mỗi method validate transition + set timestamp.

### 4.3 DeliveryTracking (append-only)
```
DeliveryTracking : BaseEntity, IMustHaveTenant
- DeliveryTaskId (Guid, FK → DeliveryTask.Id)
- Latitude (double)
- Longitude (double)
- RecordedAt (DateTime)
```
**Single-Identity:** Dùng `BaseEntity.Id` trực tiếp. **Append-only:** Không có update methods (giống WalletTransaction pattern).

### 4.4 Conversation + Message
```
Conversation : BaseEntity, IMustHaveTenant
- OrderId (Guid, FK → Order.Id)
- ShipperId (Guid)
- CustomerId (Guid)

Message : BaseEntity, IMustHaveTenant
- ConversationId (Guid, FK → Conversation.Id)
- SenderId (Guid)
- Content (string)
- SentAt (DateTime)
- IsRead (bool)
```
**Single-Identity:** Cả 2 dùng `BaseEntity.Id` trực tiếp. `Message.MarkAsRead()` là method duy nhất mutate (IsRead = true).

### 4.5 SalesReferral (v1.1 — REDESIGN: composite code salesman+product)
```
SalesReferral : BaseEntity, IMustHaveTenant
- SalesmanId (Guid, FK → Customer.Id)
- SalesmanCode (string, 6-8 chars) — phần salesman của composite code
- ProductId (Guid, FK → Product.Id) — product salesman chọn giới thiệu (v1.1 NEW)
- ProductShortCode (string?) — phần product của composite code (v1.1 NEW)
- ReferredCustomerId (Guid?, FK → Customer.Id)
- OrderId (Guid?, FK → Order.Id)
- CommissionAmount (decimal) — = orderTotal * ProductReferralConfig.CommissionRate (v1.1: per-product, KHÔNG hardcode)
- CommissionRate (decimal) — snapshot rate tại thời điểm chốt đơn (audit)
- CommissionStatus (enum CommissionStatus: Pending=1, Paid=2)
- AppInstallBonusAmount (decimal, default 0) — snapshot từ ProductReferralConfig.AppInstallBonus (v1.1 NEW)
- AppInstallBonusStatus (enum BonusStatus: None=0, Pending=1, Paid=2) — v1.1 NEW
- AppInstallAttributionId (Guid?, FK → AppInstallAttribution.Id) — link tới attribution nếu có (v1.1 NEW)
- CreatedAt (DateTime)
```
**Methods:**
- `AttachToOrder(orderId, customerId, orderTotal, commissionRate)` — set OrderId, ReferredCustomerId, CommissionAmount = orderTotal * commissionRate, CommissionStatus=Pending.
- `MarkCommissionPaid()` — CommissionStatus=Paid.
- `AttachAppInstallBonus(attributionId, bonusAmount)` — set AppInstallAttributionId, AppInstallBonusAmount, AppInstallBonusStatus=Pending.
- `MarkAppInstallBonusPaid()` — AppInstallBonusStatus=Paid.

**Single-Identity:** Dùng `BaseEntity.Id` trực tiếp.

### 4.6 WalletTransaction (immutable, có Reversal pattern — v1.1)
```
WalletTransaction : BaseEntity, IMustHaveTenant
- OwnerId (Guid, FK → Customer.Id)
- Type (enum WalletTransactionType: CODCollection=1, AdvancePayment=2, Commission=3, Withdrawal=4, Settlement=5, Reversal=6) — v1.1: thêm Reversal=6
- Amount (decimal) — Reversal entry có Amount = -original (v1.1)
- Description (string)
- RelatedOrderId (Guid?)
- RelatedTransactionId (Guid?, FK → WalletTransaction.Id) — v1.1 NEW: Reversal entry reference original transaction
- BalanceAfter (decimal)
- CreatedAt (DateTime)
```
**Immutability:** Append-only, no Update/Delete methods (giống `AccountingEntry` pattern). `BalanceAfter` tính khi tạo.
**Reversal pattern (v1.1):** Nếu shipper confirm COD sai số tiền → tạo Reversal entry với `Type=Reversal`, `Amount = -original.Amount`, `RelatedTransactionId = original.Id`, `BalanceAfter = currentBalance + Amount`. Không update/delete transaction gốc.
**Architecture test (v1.1):** Thêm `WalletTransaction_Immutable_NoPublicSetter` + `WalletTransaction_NoUpdateMethod` vào VanAn.Architecture.Tests (tương tự AccountingEntry pattern).

### 4.7 Order fields bổ sung (Domain Modification — approved)
```
Order (existing, add fields):
- ShipperId (Guid?) — FK → Customer.Id
- SalesmanId (Guid?) — FK → Customer.Id
- ReferralCode (string?) — composite code "{salesmanCode}|{productShortCode}" khi đặt hàng (v1.1: composite format)
- ReferralProductId (Guid?) — FK → Product.Id, product salesman chọn giới thiệu (v1.1 NEW)
- DeliveryLat (double?) — customer delivery location latitude
- DeliveryLng (double?) — customer delivery location longitude
- CodAmount (decimal?) — cash on delivery amount
- CodCollectedAt (DateTime?) — khi shipper thu COD
```
**Backward compatible:** Tất cả nullable, không break existing data.

### 4.8 Customer fields bổ sung (v1.1 — REMOVED)
~~`CurrentLat`, `CurrentLng`, `LocationUpdatedAt`~~ — **Bỏ** (v1.1). Lý do: Shipper location đã có `DeliveryTracking` (append-only per DeliveryTask). Customer delivery location đã có `Order.DeliveryLat/Lng`. Customer "current location" không phục vụ UC nào trong spec → thêm field không dùng = tech debt. Nếu sau PoC cần "Salesman thấy khách gần" thì thêm UC + entity riêng.

### 4.9 ProductReferralConfig (v1.1 — NEW: per-product commission + app-install bonus)
```
ProductReferralConfig : BaseEntity, IMustHaveTenant
- ProductId (Guid, FK → Product.Id) — unique (1 config per product)
- ProductShortCode (string?, 20 chars) — short code cho composite referral (vd "TR-001"), unique within tenant
- CommissionRate (decimal, precision 18,4) — 2-5% (0.02m - 0.05m), do sysadmin set per-product
- AppInstallBonus (decimal, precision 18,2) — bonus amount cố định khi customer cài app qua referral, do sysadmin set per-product
- IsActive (bool) — sysadmin có thể disable config
- CreatedAt (DateTime)
- UpdatedAt (DateTime?)
```
**Methods:**
- `Update(commissionRate, appInstallBonus, productShortCode, isActive)` — sysadmin update config.
- `Deactivate()` — IsActive=false.

**Single-Identity:** Dùng `BaseEntity.Id` trực tiếp. Unique index trên `ProductId` (1 config per product). Unique index trên `(TenantId, ProductShortCode)` (filtered, chỉ non-null).
**Auth:** Chỉ SystemAdmin được CRUD. API: `GET/POST/PUT/DELETE /api/admin/products/{productId}/referral-config`.

### 4.10 AppInstallAttribution (v1.1 — NEW: track app install cho salesman bonus)
```
AppInstallAttribution : BaseEntity, IMustHaveTenant
- CustomerId (Guid, FK → Customer.Id) — unique (1 customer chỉ attribute 1 lần)
- SalesmanId (Guid, FK → Customer.Id)
- ProductId (Guid, FK → Product.Id) — product referral
- SalesReferralId (Guid?, FK → SalesReferral.Id) — link tới SalesReferral nếu có order sau đó
- BonusAmount (decimal) — snapshot từ ProductReferralConfig.AppInstallBonus tại thời điểm install
- AttributionStatus (enum AttributionStatus: Pending=1, Paid=2, Rejected=3 v1.2, Held=4 v1.2)
- InstalledAt (DateTime)
- WalletTransactionId (Guid?, FK → WalletTransaction.Id) — WalletTransaction tạo cho salesman
- RiskScore (int, 0-100) — v1.2 NEW — computed tại thời điểm attribution
- RiskFactors (string, JSON) — v1.2 NEW — chi tiết factors đóng góp RiskScore (vd "sameFingerprint:+50,newDevice:+25")
- HoldUntil (DateTime?) — v1.2 NEW — nếu RiskScore>=60, hold đến HoldUntil (now+48h)
- DeviceRegistrationId (Guid?, FK → DeviceRegistration.Id) — v1.2 NEW — link tới device đã cài app
```
**Methods:**
- `MarkPaid(walletTransactionId)` — AttributionStatus=Paid, set WalletTransactionId.
- `MarkRejected(reason)` — v1.2 NEW — AttributionStatus=Rejected (RiskScore>=80 auto-reject hoặc admin manual).
- `MarkHeld(holdUntil)` — v1.2 NEW — AttributionStatus=Held, set HoldUntil (RiskScore 60-79 hold 48h).

**Single-Identity:** Dùng `BaseEntity.Id` trực tiếp. Unique index trên `CustomerId` (1 customer 1 attribution). Index trên `SalesmanId` (query bonus per salesman).

### 4.11 DeviceRegistration (v1.2 NEW — self-hosted device fingerprint + token)
```
DeviceRegistration : BaseEntity, IMustHaveTenant
- CustomerId (Guid, FK → Customer.Id)
- DeviceToken (string, unique, 64 chars) — server-signed UUIDv7 + HMAC-SHA256
- FingerprintHash (string, 64 chars) — SHA256 của 15+ browser signals (canvas/WebGL/audio/fonts/...)
- FingerprintSignals (string, JSON) — raw signals cho debug + recompute (vd {"canvas":"abc...","webgl":"Apple GPU","tz":"Asia/Ho_Chi_Minh",...})
- FirstSeenAt (DateTime) — lần đầu device này xuất hiện
- LastSeenAt (DateTime) — cập nhật mỗi login/API call
- IsActive (bool) — false khi admin deactivate hoặc customer logout all
- IsVerified (bool) — admin review passed (post-fraud review)
- UserAgent (string, 500 chars) — raw UA cho audit
- Platform (string, 50) — navigator.platform (vd "iPhone", "Win32")
- IpAddress (string, 50) — IP lần đầu thấy
- RiskScore (int, 0-100, default 0) — device-level risk (tăng khi flag fraud)
```
**Methods:**
- `Touch(lastSeenAt, ipAddress)` — update LastSeenAt + IP khi API call (refresh activity)
- `Deactivate()` — admin deactivate (set IsActive=false).
- `Verify()` — admin review passed (IsVerified=true).
- `UpdateRiskScore(score)` — tăng/giảm device-level risk.

**Single-Identity:** Dùng `BaseEntity.Id` trực tiếp. Unique index trên `DeviceToken` (1 token = 1 device). Index trên `(CustomerId, IsActive)` (query active devices per customer). Index trên `FingerprintHash` (query: ai khác dùng fingerprint này? — anti-fraud check).

**Constraints:**
- Max 3 active DeviceRegistration per Customer (enforce tại application layer — count active before insert, throw if exceed)
- DeviceRegistration 4+ → require admin approval (create with IsActive=false + FraudFlag)

**Reference library:** FingerprintJS v4 (MIT, self-host) hoặc ClientJS — collect 15+ signals client-side, hash SHA256, send server. **Zero external dependency.**

### 4.12 FraudFlag (v1.2 NEW — admin review queue cho suspicious activity)
```
FraudFlag : BaseEntity, IMustHaveTenant
- EntityType (enum FraudEntityType: Customer=1, Order=2, SalesReferral=3, AppInstallAttribution=4, DeviceRegistration=5)
- EntityId (Guid) — ID của entity bị flag
- CustomerId (Guid?) — customer liên quan (index cho query per customer)
- FlagType (enum FraudFlagType: SelfDeal=1, AccountFarming=2, BotBehavior=3, WashTrading=4, SuspiciousFingerprint=5, DeviceLimitExceeded=6, HighRiskScore=7)
- RiskScore (int, 0-100) — snapshot tại thời điểm flag
- RiskFactors (string, JSON) — chi tiết factors (vd "sameFingerprint:+50,sameIP:+30")
- Description (string, 500) — human-readable mô tả
- Status (enum FraudFlagStatus: Pending=1, Reviewed=2, Confirmed=3, Dismissed=4)
- ReviewedBy (Guid?, FK → User.Id) — admin user đã review
- ReviewedAt (DateTime?)
- ReviewNote (string?, 500) — admin note khi review
- CreatedAt (DateTime)
```
**Methods:**
- `Confirm(reviewedBy, note)` — Status=Confirmed (entity bị penalty: reject commission/bonus, ban account sau 3 strikes)
- `Dismiss(reviewedBy, note)` — Status=Dismissed (false positive, whitelist entity)
- `MarkReviewed(reviewedBy, note)` — Status=Reviewed (neutral, info only — không penalty không whitelist)

**Single-Identity:** Dùng `BaseEntity.Id` trực tiếp. Index trên `(Status, CreatedAt)` (admin dashboard query pending flags sort by date). Index trên `(EntityType, EntityId)` (query flags per entity). Index trên `CustomerId`.

**Admin workflow (Sprint 6 — Fraud Review UI):**
1. Admin mở `/admin/fraud-flags` → list FraudFlag(Status=Pending) sort by RiskScore desc
2. Click flag → xem detail: customer, entity, risk factors, related entities (DeviceRegistration fingerprint, IP, order history)
3. Admin action: Confirm (penalty) / Dismiss (whitelist) / MarkReviewed (info only)
4. Confirm → update related entity (SalesReferral.CommissionStatus=Rejected, AppInstallAttribution.AttributionStatus=Rejected, Customer banned nếu 3 strikes)
5. Dismiss → whitelist entity (set IsVerified=true, RiskScore giảm)

### 4.13 SalesReferral RiskScore field (v1.2 NEW — add to existing SalesReferral)
```
SalesReferral (existing v1.1, v1.2 add fields):
- RiskScore (int, 0-100, default 0) — v1.2 NEW — computed khi AttachToOrder
- RiskFactors (string, JSON) — v1.2 NEW — chi tiết factors
- HoldUntil (DateTime?) — v1.2 NEW — nếu RiskScore>=60, hold đến now+48h
- CommissionStatus expansion (v1.2): thêm Rejected=3, Held=4 (Pending=1, Paid=2 giữ nguyên)
```
**Methods update (v1.2):**
- `AttachToOrder(...)` — compute RiskScore + RiskFactors + set HoldUntil nếu cần
- `MarkRejected(reason)` — v1.2 NEW — CommissionStatus=Rejected (RiskScore>=80 hoặc admin)
- `MarkHeld(holdUntil)` — v1.2 NEW — CommissionStatus=Held

### 4.14 IdentityLevel expansion (v1.2 NEW — add DeviceVerified)
```
IdentityLevel enum (existing, v1.2 add):
- Guest=0
- Social=1
- Verified=2 — SMS OTP verified
- Full=3
- DeviceVerified=4 — v1.2 NEW — device fingerprint + behavioral check passed (KHÔNG cần SMS)
```
**Lý do:** Customer không muốn verify SĐT vẫn có thể dùng community features nếu device fingerprint + behavioral pass. `DeviceVerified` tương đương `Verified` cho community role activation (UC-02).

---

## 5. API Surface (Mới)

### 5.1 Community Role Management (Gateway, SystemAdmin policy)
| Method | Endpoint | Mô tả |
|---|---|---|
| GET | `/api/admin/community/eligible` | List customer đủ điều kiện (Verified + ≥1000pts) |
| POST | `/api/admin/community/{customerId}/activate-role` | Kích hoạt role Shipper/Salesman |
| POST | `/api/admin/community/{customerId}/deactivate-role` | Hủy role |

### 5.2 ProductReferralConfig Management (v1.1 — NEW, Gateway, SystemAdmin policy)
| Method | Endpoint | Mô tả |
|---|---|---|
| GET | `/api/admin/products/{productId}/referral-config` | Lấy config của product |
| POST | `/api/admin/products/{productId}/referral-config` | Tạo config (commissionRate 2-5%, appInstallBonus, productShortCode) |
| PUT | `/api/admin/products/{productId}/referral-config` | Update config |
| DELETE | `/api/admin/products/{productId}/referral-config` | Deactivate config (soft delete) |
| GET | `/api/admin/products/referral-configs` | List all configs (admin dashboard) |

### 5.3 Shipper APIs (Gateway, CustomerToken auth)
| Method | Endpoint | Mô tả |
|---|---|---|
| GET | `/api/community/nearby-orders` | Đơn DELIVERY trong bán kính (query: lat, lng, radiusKm) |
| POST | `/api/community/orders/{orderId}/accept` | Nhận đơn |
| POST | `/api/community/orders/{orderId}/pickup` | Đã nhận hàng từ shop |
| POST | `/api/community/orders/{orderId}/delivering` | Đang giao |
| POST | `/api/community/orders/{orderId}/delivered` | Đã giao |
| POST | `/api/community/orders/{orderId}/failed` | Giao thất bại |
| POST | `/api/community/location/update` | Cập nhật vị trí |
| GET | `/api/community/delivery-tasks` | List delivery task của shipper |

### 5.4 Salesman APIs (Gateway, CustomerToken auth) — v1.1 updated
| Method | Endpoint | Mô tả |
|---|---|---|
| GET | `/api/community/nearby-products` | Sản phẩm tenant trong bán kính + commission rate + app-install bonus từ ProductReferralConfig |
| GET | `/api/community/salesman/{salesmanId}/commissions` | Doanh số + commission chốt đơn + app-install bonus (tách biệt 2 nguồn) |
| GET | `/api/community/salesman/qr?productId={productId}` | Lấy composite referral code `{salesmanCode}|{productShortCode}` + QR data (v1.1: yêu cầu productId) |
| POST | `/api/community/app-install/attributed` | Customer báo app installed với referralCode → tạo AppInstallAttribution + WalletTransaction bonus cho salesman (v1.1 NEW) |

### 5.5 Chat APIs (Gateway, CustomerToken auth)
| Method | Endpoint | Mô tả |
|---|---|---|
| GET | `/api/community/chat/conversations/{orderId}` | Chat history |
| POST | `/api/community/chat/messages` | Gửi message |

### 5.6 Wallet APIs (Gateway, CustomerToken auth)
| Method | Endpoint | Mô tả |
|---|---|---|
| GET | `/api/community/wallet` | Wallet balance + transactions |
| POST | `/api/community/wallet/confirm-cod` | Xác nhận thu COD |
| POST | `/api/community/wallet/reverse` | Reverse transaction (tạo Reversal entry, v1.1 NEW) |

### 5.7 SignalR Hubs (Mới)
| Hub | Methods | Mô tả |
|---|---|---|
| `LocationHub` | `JoinOrderTracking(orderId)`, `LeaveOrderTracking(orderId)` | Push shipper location → customer |
| `ChatHub` | `JoinConversation(orderId)`, `LeaveConversation(orderId)` | Push chat message real-time |

### 5.8 Device Registration API (v1.2 NEW — CustomerToken auth)
| Method | Endpoint | Mô tả |
|---|---|---|
| POST | `/api/community/device/register` | Customer first login/new device → register DeviceRegistration (fingerprint + token) |
| GET | `/api/community/devices` | List active devices của customer (profile page) |
| DELETE | `/api/community/devices/{deviceId}` | Customer logout/deactivate device |
| POST | `/api/community/devices/{deviceId}/verify` | (Admin) verify device post-fraud review |

### 5.9 Fraud Review API (v1.2 NEW — SystemAdmin policy)
| Method | Endpoint | Mô tả |
|---|---|---|
| GET | `/api/admin/fraud-flags?status=Pending` | List FraudFlag pending review (sort by RiskScore desc) |
| GET | `/api/admin/fraud-flags/{id}` | Detail FraudFlag + related entities (device, customer, order) |
| POST | `/api/admin/fraud-flags/{id}/confirm` | Confirm fraud (penalty: reject commission/bonus, customer ban if 3 strikes) |
| POST | `/api/admin/fraud-flags/{id}/dismiss` | Dismiss (false positive, whitelist) |
| POST | `/api/admin/fraud-flags/{id}/review` | Mark reviewed (info only, no penalty) |
| GET | `/api/admin/fraud-stats` | Dashboard stats: pending count, confirmed count, dismissed count, total fraud loss prevented |

### 5.10 Auth Alternatives API (v1.2 NEW — ALL DEFERRED to Sprint 7+ post-PoC)
> **v1.3 CORRECTION:** Toàn bộ Section 5.10 DEFER to Sprint 7+ (post-PoC). PoC scope chỉ dùng Social login (Google + Facebook UI) + Device Fingerprint. Email/Password + WebAuthn KHÔNG trong PoC.

| Method | Endpoint | Mô tả | Sprint |
|---|---|---|---|
| POST | `/api/auth/facebook/login` | Facebook OAuth login redirect (controller đã có `SocialAuthController.cs`, UI button Sprint 1) | Sprint 1 (UI only) |
| POST | `/api/auth/email/register` | Email + Password register (self-hosted Identity) | **Sprint 7+ (post-PoC)** |
| POST | `/api/auth/email/login` | Email + Password login | **Sprint 7+ (post-PoC)** |
| POST | `/api/auth/webauthn/register/begin` | (Sprint 7+) Bắt đầu WebAuthn passkey enrollment | **Sprint 7+ (post-PoC)** |
| POST | `/api/auth/webauthn/register/finish` | (Sprint 7+) Hoàn tất passkey enrollment | **Sprint 7+ (post-PoC)** |
| POST | `/api/auth/webauthn/login/begin` | (Sprint 7+) Bắt đầu passkey login challenge | **Sprint 7+ (post-PoC)** |
| POST | `/api/auth/webauthn/login/finish` | (Sprint 7+) Hoàn tất passkey login | **Sprint 7+ (post-PoC)** |

---

## 6. Sprint Plan

### Sprint 0: Foundation (5 ngày — v1.2: tăng từ 4 do +2 entity + risk scoring + device fingerprint)
**Mục tiêu:** Domain entities + Order delivery fields + Migration + **Device fingerprint + Fraud framework (v1.2)**

**Domain (Domain Modification — approved as Community Commerce feature plan):**
- Thêm `CommunityRole`, `DeliveryTask`, `DeliveryTracking`, `Conversation`, `Message`, `SalesReferral` (v1.1: redesign composite, v1.2: + RiskScore fields), `WalletTransaction` (v1.1: + Reversal type), `ProductReferralConfig`, `AppInstallAttribution` (v1.2: + RiskScore/HoldUntil/DeviceRegistrationId), `DeviceRegistration` (v1.2 NEW), `FraudFlag` (v1.2 NEW) entity vào `1_Shared/Domain.cs` — **total 11 entity (v1.2: tăng từ 9)**
- Thêm fields vào `Order`: `ShipperId`, `SalesmanId`, `ReferralCode` (composite), `ReferralProductId`, `DeliveryLat`, `DeliveryLng`, `CodAmount`, `CodCollectedAt`
- Thêm fields vào `SalesReferral` (v1.2 NEW): `RiskScore`, `RiskFactors`, `HoldUntil` + `CommissionStatus` expansion (+Rejected=3, +Held=4)
- Mở rộng `IdentityLevel` enum (v1.2 NEW): thêm `DeviceVerified=4`
- ~~Thêm fields vào `Customer`~~ (v1.1: bỏ — không có UC rõ ràng)
- Thêm enums: `CommunityRoleType`, `DeliveryTaskStatus`, `WalletTransactionType` (+ Reversal=6), `CommissionStatus` (+ Rejected=3, Held=4 v1.2), `BonusStatus`, `AttributionStatus` (+ Rejected=3, Held=4 v1.2), `FraudEntityType` (v1.2 NEW), `FraudFlagType` (v1.2 NEW), `FraudFlagStatus` (v1.2 NEW) — **total 9 enums (v1.2: tăng từ 6)**
- **Single-Identity Pattern:** Tất cả entity mới dùng `BaseEntity.Id` trực tiếp (không có business key VO) — explicit trong detailed plan.

**Infrastructure:**
- EF Configuration cho 11 entity mới (CoreHub + ShopERP) — v1.2: tăng từ 9
- Migration (PG + SQLite)
- Register DbContext sets

**Auth (v1.2 updated):**
- ~~Bổ sung social login (Google/Facebook) endpoint~~ — ĐÃ CÓ (`SocialAuthController.cs`, Tiered Auth P1/P4 PASS).
- `X-Customer-Token` đã tồn tại — giữ nguyên, không chuyển JWT
- **v1.2 NEW:** Device fingerprint + DeviceRegistration endpoint (`POST /api/community/device/register`) — FingerprintJS (MIT, self-host) client-side
- **v1.2 NEW (OPTIONAL, post-PoC):** Email + Password login + WebAuthn Passkey — defer to Sprint 7+
- Thêm auth policy `RequireCommunityRole` (check role trong DB) — Sprint 1 task

**Acceptance Criteria Sprint 0:**
- [ ] `dotnet build VanAn.sln` pass
- [ ] Migration apply thành công (PG + SQLite) — 11 tables mới (v1.2: tăng từ 9)
- [ ] ~~Social login (Google) hoạt động end-to-end~~ (v1.1: bỏ — đã verify trong Tiered Auth P1)
- [ ] OTP login vẫn hoạt động (regression test pass) — **OPTIONAL** trong v1.2 (SMS không bắt buộc)
- [ ] Unit test: CommunityRole, DeliveryTask, WalletTransaction, ProductReferralConfig, AppInstallAttribution, **DeviceRegistration, FraudFlag** — ≥25 test cases (v1.2: tăng từ 22)
- [ ] Architecture test: WalletTransaction immutability (no public setter, no update method) PASS
- [ ] **v1.2 NEW:** Device fingerprint generation (FingerprintJS) hoạt động client-side — test HTML page generate hash
- [ ] **v1.2 NEW:** RiskScore calculation logic unit test — verify deterministic scoring per factor (8 factors)

### Sprint 1: Shipper Nearby Orders + Accept (5 ngày) — v1.1: thêm CC-S1-T0
**Mục tiêu:** Shipper thấy đơn gần + nhận đơn + verify "delivering" status

**API:**
- CC-S1-T0 (v1.1 NEW): Verify/Add `"delivering"` vào `OrderStatuses.Default[]` + transition rules trong `OrderWorkflowService.IsTransitionValidAsync` nếu chưa có. **Domain Modification** — cần approval.
- GET `/api/community/nearby-orders` — query PG Orders (Gateway) với Haversine distance
- POST `/api/community/orders/{orderId}/accept` — tạo DeliveryTask, set Order.ShipperId
- Add `RequireCommunityRole` auth policy

**UI:**
- "Nearby Orders" page (KhachLink) — list đơn với khoảng cách
- "Accept Order" button + confirmation
- Order detail: shop location + customer location

**Acceptance Criteria:**
- [ ] `"delivering"` status có trong `OrderStatuses.Default[]` + transition rules (v1.1 NEW)
- [ ] Shipper thấy đơn DELIVERY trong 5km
- [ ] Accept đơn → DeliveryTask tạo, Order.ShipperId set
- [ ] Double-accept → 409 Conflict
- [ ] GPS consent prompt hoạt động
- [ ] Unit test: Haversine calculation, accept concurrency

### Sprint 2: Delivery Workflow + GPS Tracking (5 ngày)
**Mục tiêu:** Shipper cập nhật trạng thái + vị trí real-time (v1.1: adaptive polling)

**API:**
- POST pickup/delivering/delivered/failed endpoints
- POST `/api/community/location/update`
- `LocationHub` SignalR

**UI:**
- Delivery workflow buttons (PickedUp → OutForDelivery → Delivered/Failed)
- Leaflet map với shipper marker (thay Google Maps iframe)
- Customer order tracking page: thấy shipper marker di chuyển

**Acceptance Criteria:**
- [ ] State machine: Assigned→PickedUp→OutForDelivery→Delivered/Failed
- [ ] GPS polling **adaptive** (v1.1): 10s khi `OutForDelivery`, 30s khi `PickedUp`, stop khi `Delivered/Failed` — tiết kiệm battery + data
- [ ] SignalR push location → customer thấy marker di chuyển
- [ ] Order.Completed khi Delivered
- [ ] Unit test: state transitions, DeliveryTracking append-only

### Sprint 3: Chat (4 ngày)
**Mục tiêu:** Customer ↔ Shipper chat real-time

**API:**
- GET/POST chat endpoints
- `ChatHub` SignalR

**UI:**
- Chat panel trong order detail (cả customer và shipper side)
- Message input + send button
- Chat history load

**Acceptance Criteria:**
- [ ] Chat chỉ mở khi DeliveryTask tồn tại
- [ ] Message real-time qua SignalR
- [ ] Chat history persist DB
- [ ] Unit test: message creation, conversation scoping

### Sprint 4: Salesman + Composite QR Referral + Per-Product Commission + App-Install Bonus + Risk Scoring (7 ngày — v1.2: tăng từ 6, +risk scoring)
**Mục tiêu:** Salesman thấy products gần + chọn product + composite QR referral + commission chốt đơn (per-product 2-5%) + app-install bonus (per-product, sysadmin set) + **risk scoring + fraud flagging (v1.2)**

**API:**
- GET `/api/community/nearby-products` — query FeaturedProducts (PG) có tenant coords trong bán kính + join ProductReferralConfig (commission rate + app-install bonus)
- GET `/api/community/salesman/qr?productId={productId}` — trả composite referral code `{salesmanCode}|{productShortCode}` (v1.1: yêu cầu productId)
- GET `/api/community/salesman/{id}/commissions` — tách biệt commission chốt đơn + app-install bonus + **status Pending/Held/Rejected (v1.2)**
- POST `/api/community/app-install/attributed` — customer báo app installed → tạo AppInstallAttribution + WalletTransaction bonus + **RiskScore + FraudFlag if ≥60 (v1.2 NEW)**
- Order creation accept `referralCode` (composite) → resolve `Order.SalesmanId` + `Order.ReferralProductId` (v1.1: + ReferralProductId)
- Admin API: `GET/POST/PUT/DELETE /api/admin/products/{productId}/referral-config` (v1.1 NEW)
- **v1.2 NEW:** `IRiskScoringService` — compute RiskScore (0-100) cho SalesReferral + AppInstallAttribution từ 8 factors (deterministic)
- **v1.2 NEW:** `IFraudFlagService` — create FraudFlag khi RiskScore≥60, query pending flags

**UI:**
- "Nearby Products" page (Salesman) — hiển thị commission rate + app-install bonus per product (v1.1)
- "My QR Code" page — salesman chọn product → generate composite QR (v1.1: chọn product trước)
- "Sales Dashboard" — doanh số, commission chốt đơn, app-install bonus (tách biệt 2 nguồn, v1.1) + **status Pending/Held/Rejected hiển thị (v1.2)**
- Admin UI: ProductReferralConfig CRUD (v1.1 NEW)
- QRScanner update: nhận composite referral URL → lưu cả salesmanCode + productShortCode (v1.1)
- PWA install event handler: `appinstalled` event → POST `/api/community/app-install/attributed` (v1.1 NEW)
- **v1.2 NEW:** Device fingerprint JS (FingerprintJS, self-host) — collect 15+ signals, hash, send khi register device + mỗi app-install attribution

**Acceptance Criteria:**
- [ ] SalesmanCode unique, 6-8 chars
- [ ] Composite referral code format `{salesmanCode}|{productShortCode}` (v1.1)
- [ ] QR generate client-side, chứa composite code
- [ ] Scan QR → lưu composite referral code trong localStorage
- [ ] Order creation với composite referralCode → Order.SalesmanId + Order.ReferralProductId set (v1.1)
- [ ] Commission tính theo `ProductReferralConfig.CommissionRate` (2-5%, per-product, KHÔNG hardcode) (v1.1)
- [ ] App-install bonus: customer cài app qua referral → AppInstallAttribution + WalletTransaction type=Commission tạo (v1.1 NEW)
- [ ] 1 customer chỉ attribute 1 lần (unique constraint) (v1.1)
- [ ] Admin UI set được commission rate + app-install bonus per product (v1.1)
- [ ] **v1.2 NEW:** RiskScore computed deterministic cho mọi SalesReferral + AppInstallAttribution
- [ ] **v1.2 NEW:** RiskScore ≥ 60 → CommissionStatus/BonusStatus=Held, HoldUntil=now+48h, FraudFlag(Status=Pending) tạo
- [ ] **v1.2 NEW:** RiskScore ≥ 80 → CommissionStatus/BonusStatus=Rejected, FraudFlag tạo
- [ ] **v1.2 NEW:** RiskScore < 60 → auto-approve sau 24h cooling period
- [ ] **v1.2 NEW:** Device fingerprint (FingerprintJS) gửi kèm app-install attribution request
- [ ] **v1.2 NEW:** Anti-fraud signals check: salesmanFingerprint==customerFingerprint, same IP 24h, customerAgeDays<7, deviceFirstSeen<24h, appInstallTime<30s, blacklistedFingerprint
- [ ] Unit test: composite referral resolution, commission calculation per-product, app-install attribution, bonus awarding, **risk scoring (8 factors), fraud flag creation (v1.2)**

### Sprint 5: Wallet + COD + Settlement (4 ngày) — v1.1: + Reversal pattern
**Mục tiêu:** Shipper ứng tiền + thu COD + wallet ledger + reversal

**API:**
- GET `/api/community/wallet`
- POST `/api/community/wallet/confirm-cod`
- POST `/api/community/wallet/confirm-advance`
- POST `/api/community/wallet/reverse` (v1.1 NEW — reversal entry)
- Settlement logic: shipper-shop-customer financial flow

**UI:**
- "Wallet" page — balance, transactions
- COD confirm button trong delivery workflow
- Advance payment confirm
- Reverse transaction button (admin, v1.1)

**Acceptance Criteria:**
- [ ] Order.PaymentMethod hỗ trợ "COD"
- [ ] WalletTransaction append-only (immutable, giống AccountingEntry pattern)
- [ ] BalanceAfter tính đúng sau mỗi transaction
- [ ] Reversal entry: `Type=Reversal`, `Amount=-original`, `RelatedTransactionId=original.Id` (v1.1 NEW)
- [ ] Settlement record tạo cho shop
- [ ] Unit test: wallet balance calculation, COD flow, double-entry integrity, reversal flow (v1.1: +reversal)
- [ ] Architecture test: WalletTransaction immutability PASS (v1.1)

### Sprint 6: Admin + Fraud Review + Polish + Legal (4 ngày — v1.2: tăng từ 3, +Fraud Review UI)
**Mục tiêu:** Admin activation UI + **Fraud Review UI (v1.2 NEW)** + push notification + legal checklist

**API:**
- GET `/api/admin/community/eligible`
- POST activate/deactivate role
- Push notification khi activate role
- **v1.2 NEW:** GET `/api/admin/fraud-flags?status=Pending` — list FraudFlag pending review (sort by RiskScore desc)
- **v1.2 NEW:** GET `/api/admin/fraud-flags/{id}` — detail FraudFlag + related entities (device, customer, order)
- **v1.2 NEW:** POST `/api/admin/fraud-flags/{id}/confirm` — confirm fraud (penalty)
- **v1.2 NEW:** POST `/api/admin/fraud-flags/{id}/dismiss` — dismiss (whitelist)
- **v1.2 NEW:** POST `/api/admin/fraud-flags/{id}/review` — mark reviewed (info only)
- **v1.2 NEW:** GET `/api/admin/fraud-stats` — dashboard stats (pending count, confirmed, dismissed, fraud loss prevented)

**UI:**
- Admin panel: list eligible customers, activate/deactivate
- Profile page: hiển thị community roles
- Push notification integration
- **v1.2 NEW:** `/admin/fraud-flags` page — list pending FraudFlag, sort by RiskScore, filter by FlagType/EntityType
- **v1.2 NEW:** Fraud flag detail modal — show customer, entity, risk factors (JSON pretty), related DeviceRegistration (fingerprint, IP, first/last seen), order history, related FraudFlags
- **v1.2 NEW:** Action buttons: Confirm (penalty: reject commission/bonus, ban account if 3 strikes), Dismiss (whitelist + IsVerified=true), Mark Reviewed (info only)
- **v1.2 NEW:** `/admin/fraud-stats` dashboard — pending count, confirmed count (with $ loss prevented), dismissed count, top 5 flagged customers

**Legal:**
- Checklist đăng ký sàn TMĐT (Bộ Công Thương)
- Điều khoản sử dụng cho cộng tác viên
- Chính sách bảo vệ dữ liệu cá nhân — **v1.2 NEW:** bao gồm device fingerprint collection (consent required)
- Quy chế hoạt động sàn
- **v1.2 NEW:** Anti-fraud policy document — quy định 3-strike ban, hold 48h, KYC bank account cho payout

**Acceptance Criteria:**
- [ ] Admin thấy list customer đủ điều kiện
- [ ] Activate/deactivate role hoạt động
- [ ] Push notification gửi khi activate
- [ ] Profile page hiển thị roles
- [ ] Legal documents draft hoàn thành
- [ ] **v1.2 NEW:** `/admin/fraud-flags` hiển thị list pending flags sort by RiskScore
- [ ] **v1.2 NEW:** Fraud flag detail show đầy đủ risk factors + related entities
- [ ] **v1.2 NEW:** Confirm action → update entity (CommissionStatus=Rejected/AttributionStatus=Rejected) + customer ban if 3 strikes
- [ ] **v1.2 NEW:** Dismiss action → whitelist (IsVerified=true, RiskScore giảm)
- [ ] **v1.2 NEW:** `/admin/fraud-stats` hiển thị stats đúng
- [ ] **v1.2 NEW:** Legal docs có clause về device fingerprint consent + anti-fraud policy

---

## 7. Architecture Decisions

### 7.1 Customer Auth: Giữ X-Customer-Token, không chuyển JWT
- Hiện tại: `ICustomerTokenService.CreateToken(customerId)` — custom token
- Lý do: OTP flow đã hoạt động, không phá hiện có
- Community APIs dùng `X-Customer-Token` header (same as existing customer APIs)
- Gateway forward đến ShopERP (hoặc Gateway xử lý trực tiếp nếu PG entity)
- **v1.1:** Social auth (Google/Facebook) ĐÃ CÓ — không cần bổ sung (Tiered Auth P1/P4 PASS).

### 7.2 Cross-tenant: Community entities trên Gateway PG (v1.3: PG ONLY — KHÔNG SQLite)
- `CommunityRole`, `DeliveryTask`, `DeliveryTracking`, `Conversation`, `Message`, `SalesReferral`, `WalletTransaction`, `ProductReferralConfig`, `AppInstallAttribution`, `DeviceRegistration`, `FraudFlag` → **Gateway PG ONLY** (cross-tenant, v1.3: KHÔNG tạo trên ShopERP SQLite)
- Orders: Gateway PG (đã là source of truth theo Option C)
- Products: FeaturedProducts PG (PoC) — không query per-tenant SQLite
- **v1.3 CORRECTION:** Community entities KHÔNG sync xuống ShopERP SQLite. Lý do: (1) cross-tenant nature — shipper/salesman làm việc nhiều tenant, (2) tránh 300K SQLite files phải migrate community tables, (3) Community APIs đều qua Gateway PG trực tiếp. ShopERP SQLite giữ business data per-tenant (Products, Kitchen, POS) — KHÔNG community.

### 7.2.1 Multi-tenancy isolation cho Community entities (v1.1 NEW)
- Community entities implement `IMustHaveTenant` (qua BaseEntity) — `TenantId` = tenant của Order/Product (audit + ownership).
- Cross-tenant query (shipper thấy đơn của nhiều tenant): bypass tenant filter bằng explicit `RequireCommunityRole` policy + cross-tenant read repo (Gateway PG context không có `HasQueryFilter`).
- ShopERP SQLite: tenant-scoped (giữ `HasQueryFilter(IMustHaveTenant.TenantId)`).
- **Enforcement:** Architecture test verify Community entities có TenantId + Gateway DbContext không apply query filter cho Community DbSets.

### 7.3 GPS: PWA polling, adaptive interval (v1.1 updated)
- **Adaptive polling (v1.1):**
  - `OutForDelivery` status: 10s interval (cần tracking chính xác)
  - `PickedUp` status: 30s interval (chưa giao, ít cần update)
  - `Delivered/Failed/Cancelled`: stop polling
- Thông báo user "Giữ app mở"
- Post-PoC: đánh giá native app (MAUI/Flutter) cho background GPS
- **Lý do v1.1:** 10s polling + POST API mỗi lần = 6 requests/phút. Với 10 shippers PoC = 60 req/phút. Adaptive giảm battery drain + mobile data.

### 7.4 Map: Leaflet thay Google Maps iframe
- Leaflet.js (open-source, free, interactive)
- Marker cho shipper + shop + customer
- Route line (optional, dùng OSRM hoặc straight line)

### 7.5 Chat: Human-to-human, không AI chatbot trong PoC
- SignalR ChatHub
- Message persist DB
- AI chatbot là sprint riêng sau PoC

### 7.6 Wallet: Immutable ledger + Reversal pattern (v1.1 updated)
- `WalletTransaction` append-only
- `BalanceAfter` tính khi tạo
- Không update/delete transaction
- **Reversal pattern (v1.1):** Nếu shipper confirm COD sai → tạo Reversal entry (`Type=Reversal`, `Amount=-original`, `RelatedTransactionId=original.Id`) thay vì update transaction gốc. Giống `AccountingEntry` Reversal Entry pattern.
- Tuân thủ domain purity rules
- **Architecture test (v1.1):** `WalletTransaction_Immutable_NoPublicSetter` + `WalletTransaction_NoUpdateMethod` PASS.

### 7.7 Domain Protection
- Tất cả entities mới trong `1_Shared/Domain.cs` (Single Source of Truth)
- EF Configuration trong Infrastructure layer
- No business logic trong Controllers/Hubs
- `AccountingEntry` immutable pattern áp dụng cho `WalletTransaction`
- **Single-Identity Pattern (v1.1):** Tất cả entity mới dùng `BaseEntity.Id` trực tiếp (không có business key VO) — explicit trong detailed plan. Constructor public, EF config `HasKey(e => e.Id)`.

### 7.8 Composite Referral Code (v1.1 NEW)
- Format: `{salesmanCode}|{productShortCode}` (vd `ABC123|TR-001`)
- Salesman chọn product từ UC-08 → generate composite code
- QR chứa URL `https://khachlink.app/r/{salesmanCode}|{productShortCode}`
- Server resolve: split by `|` → lookup `CommunityRole.SalesmanCode` + `ProductReferralConfig.ProductShortCode` (hoặc ProductId fallback)
- Lý do: mã salesman gộp chung vào mã sản phẩm mà salesman chọn giới thiệu → 1 QR chứa cả 2 thông tin, customer scan 1 lần.

### 7.9 Per-Product Commission + App-Install Bonus (v1.1 NEW)
- `ProductReferralConfig` entity: sysadmin set `CommissionRate` (2-5%) + `AppInstallBonus` per product.
- KHÔNG hardcode commission rate hay bonus amount.
- SalesReferral snapshot `CommissionRate` tại thời điểm chốt đơn (audit).
- AppInstallAttribution snapshot `BonusAmount` tại thời điểm install (audit).
- Lý do: thưởng cho salesman khi thuyết phục customer cài app → mở rộng hệ sinh thái. Bonus per-product vì product giá cao có thể thưởng cao hơn.

### 7.10 Self-Hosted Anti-Fraud — Zero External Dependency (v1.2 NEW)
- **5-layer defense-in-depth:** (1) Device Fingerprint (FingerprintJS MIT), (2) Device Token persisted, (3) Behavioral rules (SQL), (4) Risk Scoring deterministic 0-100, (5) Native App Attestation (post-PoC, OPTIONAL).
- **KHÔNG phụ thuộc** SMS gateway, Zalo OA, WhatsApp, Kafka, Synadia managed, RDS managed.
- **CHO PHÉP phụ thuộc:** OAuth providers (Google/Facebook — public identity, không phải VN vendor), WebAuthn Passkey (W3C standard, browser native).
- **SMS OTP OPTIONAL** — không bắt buộc. Thay bằng device fingerprint + behavioral + KYC bank account cho payout.
- **Target fraud rate:** <0.5% (vs 100% lý tưởng không khả thi).
- **Payout fraud prevention:** Hold commission/bonus 48h if RiskScore≥60. Auto-reject if ≥80. KYC bank account required. Min payout 500K VND. 3-strike ban.
- Lý do: tránh phụ thuộc nhà cung cấp dịch vụ VN (SMS gateway có thể tăng giá, Zalo có thể thay đổi API). Self-host toàn bộ anti-fraud → kiểm soát chi phí + dữ liệu nhạy cảm (fingerprint, IP) không rời hệ thống.

### 7.11 WebAuthn Passkey — OPTIONAL, Post-PoC (v1.2 NEW)
- **W3C standard** — Chrome/Edge/Firefox/Safari 2022+ support. iOS 16+, Android 9+. Vietnam coverage >95%.
- **Zero vendor dependency** — browser native, không cần Apple/Google API cho việc verify (public root CA).
- **Phishing-resistant** — không nhập password, challenge-response với hardware-backed key.
- **Device-bound** — passkey không sync (trừ iOS iCloud Keychain — opt-in).
- **Implement cost:** ~2-3 ngày (FIDO2.NET library, MIT, self-host).
- **Defer to Sprint 7+** — PoC先用 social login + device fingerprint, post-PoC add WebAuthn cho payout-tier users (salesman rút tiền).
- Lý do: WebAuthn là chuẩn mở, không lock-in vendor, phù hợp yêu cầu "không phụ thuộc bên ngoài". PoC chưa cần vì device fingerprint + behavioral đã đủ <0.5% fraud.

---

## 7B. UI SPEC ADDENDUM (v1.3 NEW — bổ sung UI thiếu từ review)

> **v1.3 NOTE:** Section này bổ sung UI specs cho các pages/components mà detailed plans thiếu. Mỗi spec phải được implement trong sprint tương ứng.

### 7B.1 OrderDetail.razor (Sprint 1 — Shipper xem chi tiết đơn sau accept)
**Route:** `/community/orders/{orderId}`
**Actor:** Shipper (có CommunityRole Shipper, Active)
**Layout:** NavMenu.razor (9-tab mobile nav)
**Components:** VanAnCard, VanAnButton, VanAnMap (Leaflet)
**UI Elements:**
- Header: "Chi tiết đơn hàng #{orderId short}"
- Shop info card: shop name, shop address, shop lat/lng (từ TenantSettings)
- Customer info card: delivery address, delivery lat/lng (từ Order.DeliveryLat/Lng)
- Map: LeafletMap component với shop marker (red) + customer marker (green) + route line (straight)
- Order items list: product name, qty, price
- Total amount (VND format: `{amount:N0}đ`)
- Status badge: current Order.Status (color-coded)
- Back button → NearbyOrders page
**States:**
- Loading: spinner
- Error (order not found / not authorized): "Không tìm thấy đơn hàng hoặc bạn không có quyền xem"
- Empty (no delivery coords): "Khách chưa cung cấp vị trí giao hàng — gọi khách để xác nhận"
**E2E:** `community-nearby-orders.spec.ts` step: accept → navigate to OrderDetail → verify shop + customer info

### 7B.2 DeliveryTracking.razor (Sprint 2 — Shipper delivery workflow + GPS)
**Route:** `/community/delivery/{orderId}`
**Actor:** Shipper (đã accept đơn, DeliveryTask.Status=Assigned)
**UI Elements:**
- Header: "Giao hàng #{orderId short}"
- Map: LeafletMap (shop red marker + shipper blue marker + customer green marker, route line, auto-pan theo shipper)
- **Workflow buttons (state machine — v1.3 NEW explicit):**
  - Status=Assigned → "Đã nhận hàng từ shop" button (VanAnButton Primary) → POST `/api/community/orders/{orderId}/pickup` → Status=PickedUp
  - Status=PickedUp → "Đang giao" button (VanAnButton Primary) → POST `/api/community/orders/{orderId}/delivering` → Status=OutForDelivery
  - Status=OutForDelivery → 2 buttons:
    - "Đã giao" (VanAnButton Success) → POST `/api/community/orders/{orderId}/delivered` → Status=Delivered → navigate to OrderDetail
    - "Giao thất bại" (VanAnButton Danger) → mở FailureReason modal → POST `/api/community/orders/{orderId}/failed` → Status=Failed
- **GPS status indicator (v1.3 NEW):** Badge "Đang theo dõi GPS" (green) khi polling active, "GPS đã dừng" (gray) khi Delivered/Failed
- **"Keep app open" banner (v1.3 NEW):** Yellow banner top: "Giữ app mở để khách theo dõi vị trí của bạn" — hiển thị khi Status=OutForDelivery, ẩn khi Delivered/Failed
- **GPS permission denied UI (v1.3 NEW):** Red banner: "Không lấy được vị trí. Vui lòng cấp quyền GPS trong Settings." + "Thử lại" button
- COD badge (if Order.PaymentMethod=="COD"): "COD: {amount:N0}đ" + "Đã thu COD" button (visible sau Delivered)
**Adaptive polling (v1.3 explicit):**
- OutForDelivery: 10s interval
- PickedUp: 30s interval
- Delivered/Failed/Cancelled: STOP polling
- Tab hidden (Page Visibility API `document.hidden`): pause polling, resume when visible

### 7B.3 OrderTracking.razor (Sprint 2 — Customer tracking, EXTEND existing file)
**File:** `5_WebApps/KhachLink/Pages/OrderTracking.razor` (ĐÃ TỒN TẠI — extend, KHÔNG tạo mới)
**Current:** Hiển thị order status + `isTabVisible` + `isPolling` spinner (line 99, 281)
**v1.3 Extension:**
- IF Order.ShipperId != null (có shipper assigned):
  - Add LeafletMap component với shipper marker (blue, moves) + shop marker (red) + customer marker (green)
  - Subscribe SignalR LocationHub `JoinOrderTracking(orderId)` → receive `LocationUpdate` → update shipper marker
  - "Shipper đang đến" ETA text (straight-line distance / average speed 20km/h)
- IF Order.ShipperId == null (chưa assign):
  - Hiển thị "Đang tìm shipper..." (existing status display giữ nguyên)
**Lưu ý:** KHÔNG break existing order status display. Map chỉ add khi ShipperId != null.

### 7B.4 Wallet.razor (Sprint 5 — Shipper/Salesman wallet, FULL spec)
**Route:** `/community/wallet`
**UI Elements:**
- Balance card: large number `{balance:N0}đ` (green if positive, red if negative)
- Filter bar (v1.3 NEW): dropdown Type (All/CODCollection/AdvancePayment/Commission/Withdrawal/Settlement/Reversal) + date range picker (default: last 30 days)
- Transaction list: each row — Type icon + description + amount (`{amount:N0}đ`, green +/red -) + timestamp + related order link
- **Export CSV button (v1.3 NEW):** "Xuất CSV" → download transactions (filtered) as CSV
- Empty state: "Chưa có giao dịch"
**COD confirm dialog (v1.3 NEW — modal):**
- Trigger: "Đã thu COD" button trong DeliveryTracking (sau Delivered)
- Modal: "Xác nhận thu COD" + amount input (VND, pre-filled from Order.CodAmount) + "Xác nhận" / "Hủy"
- Validation: amount > 0, confirm dialog "Bạn chắc chắn thu {amount:N0}đ từ khách?"
- Success: toast "Đã ghi nhận COD" + wallet balance update
- Error (already confirmed): toast "COD đã được xác nhận trước đó"
**Advance payment confirm dialog (v1.3 NEW):** Tương tự COD confirm, amount = advance amount.
**VND formatting (v1.3 NEW):** Tất cả amount display dùng `@amount.ToString("N0")đ` — ví dụ `1,234,567đ`. KHÔNG dùng decimal places.

### 7B.5 ShopSettlement.razor (Sprint 5 — Shop owner wallet, NEW page)
**Route:** `/community/settlement` (trên ShopERP, KHÔNG KhachLink)
**Actor:** Shop owner (tenant Owner role)
**UI Elements:**
- Settlement balance card: total COD collected on behalf of shop (pending settlement)
- Settlement records list: each row — order ID + COD amount + shipper name + settlement date + status (Pending/Settled)
- "Yêu cầu thanh toán" button → tạo settlement request (admin review)
- Empty state: "Chưa có giao dịch COD"
**Note:** Shop owner xem COD mà shipper thu thay shop. Settlement = chuyển tiền từ shipper wallet → shop.

### 7B.6 ReverseTransactionDialog.razor (Sprint 5 — Admin reverse, NEW component)
**Trigger:** Admin button trong `/admin/fraud-flags` detail modal (Sprint 6) HOẶC `/admin/wallet` page
**Modal UI:**
- "Đảo ngược giao dịch" header
- Transaction info: type, amount, date, owner
- Reason textarea (required): "Lý do đảo ngược"
- "Xác nhận đảo ngược" button (VanAnButton Danger) + "Hủy"
- Confirm dialog: "Giao dịch sẽ bị tạo bản ghi Reversal (số âm). Tiền sẽ bị trừ. Tiếp tục?"
- Success: toast "Đã đảo ngược giao dịch" + WalletTransaction Reversal entry created
**Auth:** SystemAdmin JWT only

### 7B.7 DeviceFingerprintConsentDialog.razor (Sprint 0 — v1.3 NEW, GDPR/PDPA compliance)
**Trigger:** First login OR new device detected (KHÔNG có DeviceRegistration cho customer này)
**Modal UI:**
- "Đồng ý thu thập thông tin thiết bị" header
- Body text: "Để bảo vệ chống gian lận, chúng tôi thu thập dấu vết thiết bị (browser fingerprint) bao gồm: loại trình duyệt, hệ điều hành, độ phân giải màn hình, font chữ, GPU. KHÔNG thu thập tên, SĐT, vị trí, hay dữ liệu cá nhân nhạy cảm. Dữ liệu chỉ dùng cho phát hiện gian lận, KHÔNG chia sẻ với bên thứ ba."
- 2 buttons: "Đồng ý" (VanAnButton Primary) + "Từ chối" (VanAnButton Secondary)
- "Đồng ý" → POST `/api/community/device/register` với fingerprint → DeviceRegistration created → IdentityLevel=DeviceVerified (if pass behavioral)
- "Từ chối" → KHÔNG collect fingerprint → IdentityLevel stays Social/Guest → RiskScore sẽ cao hơn (+25 deviceFirstSeen<24h factor không có fingerprint để match)
- Checkbox "Không hỏi lại" (optional — ghi nhớ preference trong localStorage)
**Lưu ý:** KHÔNG auto-collect. User phải đồng ý rõ ràng. PDPA Vietnam (Nghị định 13/2023) yêu cầu consent.

### 7B.8 NavMenu.razor community tabs (Sprint 1 — v1.3 NEW, mobile nav decision)
**File:** `5_WebApps/KhachLink/Components/Layout/NavMenu.razor` (ĐÃ TỒN TẠI — extend, KHÔNG tạo mới)
**Current mobile bottom nav (line 70-108):** 9 tabs — Trang chủ, Giỏ hàng, Đơn hàng, Điểm thưởng, Cửa hàng, Khuyến mãi, Quét QR, Ghi âm, Tài khoản
**v1.3 Decision:** Add community tabs vào NavMenu.razor (KHÔNG VanAnLayout.razor — layout kia chỉ 4 tabs, simpler, không phù hợp).
**Community tabs (conditional on CommunityRole):**
- IF CommunityRole(Shipper, Active): add tab "Đơn gần" (icon bi-geo-alt) → `/community/nearby-orders`
- IF CommunityRole(Shipper, Active): add tab "Giao hàng" (icon bi-truck) → `/community/delivery/{activeOrderId}` (if any)
- IF CommunityRole(Salesman, Active): add tab "Sản phẩm gần" (icon bi-shop) → `/community/nearby-products`
- IF CommunityRole(Salesman, Active): add tab "Mã QR" (icon bi-qr-code) → `/community/salesman-qr`
- IF any CommunityRole active: add tab "Ví" (icon bi-wallet2) → `/community/wallet`
**Implementation:** Query CommunityRole trên Profile load → set flags `_isShipper`, `_isSalesman` → conditional render tabs.
**Mobile:** Bottom nav có thể quá nhiều tabs (9 + 5 = 14). Solution: group community tabs vào "Cộng tác viên" expandable section (vd tab "Cộng tác" icon bi-people → expand submenu).

### 7B.9 Scan.razor + pwa.js modifications (v1.3 NEW — clarify modify existing files)

**Issue resolved:** Sprint 4 spec nói "QRScanner update" và "PWA install event handler" nhưng KHÔNG rõ modify file nào. Codebase có `Scan.razor` (QR scan flow hiện tại cho tenant QR) + `pwa.js` (appinstalled handler hiện tại chỉ set isInstalled=true).

**7B.9.1 Scan.razor (Sprint 4 — MODIFY existing, KHÔNG tạo mới)**
**File:** `5_WebApps/KhachLink/Pages/Scan.razor` (ĐÃ TỒN TẠI — line 248 hiện scan QR → parse QRCodePayload → add to cart)
**v1.3 Modification:**
- Add NEW branch trong `ProcessQrPayload` method (sau existing tenant QR flow):
  ```csharp
  // v1.3 NEW: Check if URL matches /r/{salesmanCode}|{productShortCode} pattern
  if (qrCodeText.StartsWith("/r/") || qrCodeText.Contains("/r/"))
  {
      var referralPart = qrCodeText.Split("/r/")[1];
      // Save composite referral code to localStorage
      await JSRuntime.InvokeVoidAsync("localStorage.setItem", "vanan_referral_code", referralPart);
      // Show toast "Đã lưu mã giới thiệu"
      // Navigate to home (customer can browse + order, referral auto-applied at checkout)
      Navigation.NavigateTo("/");
      return;
  }
  // Existing tenant QR flow continues below...
  ```
- **KHÔNG break existing tenant QR scan** — composite referral check FIRST, tenant QR fallback.
- **Lưu ý:** Composite referral URL format `https://khachlink.app/r/{salesmanCode}|{productShortCode}` — khi scan bởi camera hoặc Zalo scanner, URL sẽ vào `qrCodeText`. Parse theo pattern `/r/`.

**7B.9.2 pwa.js (Sprint 4 — MODIFY existing appinstalled handler, KHÔNG tạo app-install-tracker.js riêng)**
**File:** `5_WebApps/KhachLink/wwwroot/js/pwa.js` (ĐÃ TỒN TẠI — line 499-505 hiện `appinstalled` handler chỉ set isInstalled=true)
**v1.3 Modification:** Extend existing `appinstalled` handler (KHÔNG tạo file mới):
```javascript
// v1.3: Extend existing appinstalled handler (line 499-505)
window.addEventListener('appinstalled', () => {
    window.vananPWA.isInstalled = true;
    console.log('[PWA] appinstalled fired (immediate listener)');
    if (window.vananPWA.dotNetRef) {
        window.vananPWA.dotNetRef.invokeMethodAsync('HandleInstallStateChanged', true);
    }

    // v1.3 NEW: Community Commerce app-install attribution
    const referralCode = localStorage.getItem('vanan_referral_code');
    if (!referralCode) {
        console.log('[PWA] No referral code — skip attribution');
        return;  // organic install, no salesman attribution
    }

    // v1.3 NEW: Collect device fingerprint for risk scoring
    if (window.vananFingerprint && window.vananFingerprint.collect) {
        window.vananFingerprint.collect().then(fp => {
            return fetch('/api/community/app-install/attributed', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    'X-Customer-Token': localStorage.getItem('vanan_customer_token') || ''
                },
                body: JSON.stringify({
                    referralCode: referralCode,
                    fingerprintHash: fp.hash,
                    fingerprintSignals: fp.signals,
                    deviceToken: localStorage.getItem('vanan_device_token') || ''
                })
            });
        }).then(resp => {
            if (resp.ok) {
                console.log('[PWA] App install attributed to salesman');
                localStorage.removeItem('vanan_referral_code');  // clear after attribution
            } else if (resp.status === 409) {
                console.log('[PWA] Already attributed — clearing referral code');
                localStorage.removeItem('vanan_referral_code');
            }
        }).catch(err => console.error('[PWA] Attribution failed:', err));
    } else {
        console.warn('[PWA] FingerprintJS not loaded — skip attribution');
    }
});
```
**Decision (v1.3):** MODIFY `pwa.js` (extend existing handler) — KHÔNG tạo `app-install-tracker.js` riêng. Lý do: (1) 1 file dễ maintain hơn 2 files làm cùng việc, (2) existing handler đã có `dotNetRef` reference, (3) `appinstalled` event chỉ fire 1 lần — không cần separate handler.

**7B.9.3 Checkout.razor (Sprint 4 — MODIFY existing, auto-apply referral code)**
**File:** `5_WebApps/KhachLink/Pages/Checkout.razor` (ĐÃ TỒN TẠI)
**v1.3 Modification:**
- On checkout, read `localStorage.getItem('vanan_referral_code')` → if exists, include `referralCode` field trong CreateOrderCommand
- Display "Mã giới thiệu: {referralCode}" badge trong checkout summary (read-only)
- After order submitted successfully → `localStorage.removeItem('vanan_referral_code')` (clear, single-use)

---

### 7B.10 GoogleMaps.razor migration scope (v1.3 NEW — C4 resolved)

**Issue resolved:** Spec Sprint 2 nói "LeafletMap thay GoogleMaps" nhưng KHÔNG nói migration scope. Codebase có `GoogleMaps.razor` dùng ở `Store.razor` (line 197).

**v1.3 Decision:** **KHÔNG migrate GoogleMaps.razor.** LeafletMap.razor là component MỚI cho Community Commerce ONLY.
- `GoogleMaps.razor` — KEEP as-is, dùng cho `Store.razor` (shop location display, static iframe embed, no interaction needed)
- `LeafletMap.razor` — NEW component cho Community Commerce (Sprint 2 delivery tracking, Sprint 1 OrderDetail) — interactive map với markers + route line
- **Lý do:** GoogleMaps iframe đủ cho static shop location (Store page). Leaflet cần cho interactive shipper tracking (markers move, route). 2 components phục vụ 2 use cases khác nhau — KHÔNG cần migrate.
- **Tech debt:** Post-PoC có thể consolidate sang Leaflet nếu muốn eliminate Google Maps dependency hoàn toàn. KHÔNG trong PoC scope.

---

## 7C. Architecture Evolution & Scale-Up Roadmap (v1.4 NEW — Hybrid Central + Edge)

> **v1.4 — 2026-07-26:** Section này документ hóa **lợi thế cạnh tranh cốt lõi** của VanAn Community Commerce — kiến trúc **Hybrid Central + Edge** cho phép scale từ PoC (50 users) đến 10M users với chi phí tối ưu. Toàn bộ nội dung từ review sessions 2026-07-26 đã được đưa vào docs để tránh mất knowledge.

### 7C.1 Target Architecture: Hybrid Central + Edge

**Nguyên tắc:** Central PG cho global consistency + Edge Gateway cho local scale + Self-host everything (zero paid external services).

```
                     ┌─────────────────────────────────────────┐
                     │  Central PG (1 instance, self-host)      │
                     │  + TimescaleDB extension (GPS, post-PoC) │
                     │  + PgBouncer (post-PoC >10K shippers)    │
                     │  + PostGIS extension (post-PoC >1K)      │
                     │  Holds GLOBAL entities:                  │
                     │  - Customer account (cross-region)       │
                     │  - WalletTransaction (global ledger)     │
                     │  - AppInstallAttribution (unique global) │
                     │  - SalesmanCode (global registry)        │
                     │  - Tenant registry (service discovery)   │
                     │  - SystemAdmin + auth                    │
                     │  - SalesReferral (cross-region commission)│
                     │  - ProductReferralConfig                 │
                     │  - DeviceRegistration (anti-fraud)       │
                     │  - FraudFlag (admin review queue)        │
                     └─────────────────────────────────────────┘
                                    ▲
                                    │ async (NATS self-host, free)
                                    │
       ┌──────────────┬─────────────┼─────────────┬──────────────┐
       │              │             │             │              │
   Gateway-HN    Gateway-HCMC   Gateway-ĐN   Gateway-CT   ... (~100 edge)
   (in-memory     (in-memory     (in-memory   (in-memory
    cache,         cache,         cache,       cache,
    SignalR,       SignalR,       SignalR,     SignalR,
    GPS buffer)    GPS buffer)    GPS buffer)  GPS buffer)
       │              │             │             │
   KhachLink    KhachLink      KhachLink    KhachLink
   (users near  (users near    (users near  (users near
    HN, 15-20km)  HCMC, 15-20km) ĐN, 15-20km) CT, 15-20km)

       │              │             │             │
       └──────┬───────┴──────┬──────┘             │
              │              │                    │
          ShopERP-HN-1   ShopERP-HCMC-1     ShopERP-CT-1
          (5-10 tenants   (5-10 tenants      (5-10 tenants
           per instance,   per instance,      per instance,
           PG + RLS)        PG + RLS)          PG + RLS)
```

**Lớp lưu trữ:**
| Lớp | Lưu trữ | Lý do |
|---|---|---|
| **Central PG** | Global entities (Customer, Wallet, AppInstallAttribution, SalesmanCode, Tenant registry, SalesReferral, ProductReferralConfig, SystemAdmin auth, DeviceRegistration, FraudFlag) | Cần global consistency — không shard theo region được |
| **Edge Gateway** | Local cache (in-memory .NET IMemoryCache), SignalR connections, GPS buffer (in-memory + batch persist) | Per-region scale, giảm load central |
| **ShopERP** | 5-10 tenants per instance, **PG với Row-Level Security (RLS)** (KHÔNG SQLite shared) | Tenant isolation qua RLS, tránh 300K SQLite files |
| **NATS (self-host)** | Async glue giữa gateway ↔ ShopERP cross-region, durable JetStream | Decoupled, retry auto, free |

**Loại được (zero paid external dependency):**
- ✅ Redis — in-memory Gateway cache (.NET IMemoryCache) thay thế
- ✅ Kafka — NATS self-host (free) + PG native cho event volume này
- ✅ Synadia managed — self-host NATS
- ✅ RDS managed — self-host PG + PgBouncer + backup scripts
- ✅ Cloudflare Pro — free tier + GeoDNS cheap

**KHÔNG loại được:**
- ❌ Central PG (global consistency cho Wallet, AppInstallAttribution, SalesmanCode, Customer account)
- ❌ Self-host NATS (async glue giữa gateway và ShopERP)
- ❌ TimescaleDB extension (GPS write volume khi >10K shippers)
- ❌ GeoDNS (routing user → nearest gateway — Cloudflare/AWS Route 53)
- ❌ Phone verification (anti-fraud — multi-channel Viettel + Zalo + WhatsApp)

### 7C.2 Bottleneck Analysis @ Scale (11 critical points — B1-B11)

> Verified against codebase 2026-07-26. Severity đánh giá theo threshold scale khi sẽ gãy.

#### 🔴 CRITICAL — Sẽ gãy ở 10K-50K active users

**B1. GPS write throughput → PG write wall**
- Spec Sprint 2: `POST /api/community/location/update` mỗi 10s khi OutForDelivery, append-only vào DeliveryTracking (PG)
- 10K shippers × 6 req/min = 1K writes/sec sustained → ~28M rows/ngày
- PG single primary: WAL + 2 indexes → ~3-5K writes/sec là ceiling
- **Vượt ceiling ở ~30-50K shippers active**

**B2. WalletTransaction BalanceAfter race condition**
- Spec: `BalanceAfter = balanceBefore + amount` tính lúc tạo transaction
- Shipper confirm COD + receive commission cùng lúc trên same OwnerId → 2 row có BalanceAfter inconsistent
- 100K shippers daily COD: xảy ra hàng nghìn lần/ngày
- **Spec KHÔNG đề cập SELECT FOR UPDATE hoặc atomic sequence**

**B3. Haversine query full table scan**
- Sprint 1 `GET /api/community/nearby-orders` + Sprint 4 `nearby-products`: Haversine trong WHERE clause = full scan
- 100-300K tenants × 10 đơn/ngày × 30 ngày retain = 30-90M rows
- 10K shippers query mỗi 30s → 333 req/sec × 90M rows = PG CPU 100% ngay lập tức
- **Sẽ chết ở 1K shippers**

**B4. SignalR single-instance connection limit**
- Single Gateway: ~65K TCP connections limit (ephemeral port range)
- 5-10M customers + 100K shippers active tracking = vượt 65K từ 1% penetration
- **Spec KHÔNG đề cập SignalR backplane (Redis)**

#### 🟠 HIGH — Pain ở 100K-1M users

**B5. Single PostgreSQL = single point of failure + contention**
- Tất cả 11 Community entities + Orders + Accounting + Tenants + Users + FeaturedProducts trên 1 PG
- 5-10M customers: Customer 5-10M rows, Order 1-3M/ngày, WalletTransaction append-only → billion rows sau 1 năm
- Index size + VACUUM block writes
- Single max_connections (500-1000) → PgBouncer bắt buộc

**B6. NATS subject explosion + 300K SQLite files**
- 100-300K tenants = 100-300K SQLite files cần backup, vacuum, migrate
- 300K subjects trên NATS JetStream → memory footprint spike
- SQLite migration Sprint 0 phải apply cho 300K file = không khả thi

**B7. SalesmanCode uniqueness 6-8 chars global**
- 6 chars alphanumeric = 56B combinations → collision rate tăng theo birthday paradox
- Unique index global = lock contention khi 100+ salesman đăng ký đồng thời (admin bulk activate)

**B8. Commission calc synchronous trong OrderWorkflowService**
- Nếu implement sync trong HandleOrderCompletedAsync → +50-100ms vào critical path
- 100 orders/sec → +100ms mỗi cái = threading pool starvation

#### 🟡 MEDIUM — Pain ở 1-5M users

**B9. AppInstallAttribution unique constraint global**
- 5-10M customers × 1 attribution = 5-10M rows (OK)
- 100K installs/ngày burst (marketing campaign) → contention trên unique index

**B10. Chat storage growth**
- 100K orders/ngày × 5 messages = 500K messages/ngày = 180M messages/năm
- 90GB/năm (chưa tính index)
- PG still OK nhưng cần partition

**B11. Cross-tenant query KHÔNG có region scoping**
- 100-300K tenants spread across Vietnam (~63 provinces)
- Shipper Hà Nội KHÔNG cần thấy đơn Cà Mau
- Query hiện tại: WHERE Haversine(...) < 5km = scan toàn Vietnam
- **Phải có region column + filter WHERE Region = 'HN' trước khi Haversine**

### 7C.3 Short-Term Solutions (ST1-ST10 — apply trong architecture hiện tại, 1-3 tháng)

| # | Bottleneck | Solution | Cost | Apply khi |
|---|---|---|---|---|
| **ST1** | B3 Haversine scan | **PostGIS extension**: store `geography(Point, 4326)` + `ST_DWithin` + GIST index. Reduces scan từ 90M rows → ~100 rows | Thấp — PG extension free | >1K shippers HOẶC >10K orders/ngày |
| **ST2** | B1 GPS write wall | **Redis buffer + batched persist**: GPS update vào Redis (TTL 5min, key `dt:{taskId}`), batch persist vào PG mỗi 60s hoặc khi status change. Giảm write load 6x | Thấp — Redis self-host | >1K shippers active (BUT v1.4: in-memory .NET IMemoryCache thay Redis — zero dependency) |
| **ST3** | B4 SignalR limit | **Redis backplane**: `AddStackExchangeRedis` → multiple Gateway instances scale out | Thấp — Redis self-host | >1K shippers active OR >65K total connections |
| **ST4** | B5 PG contention | **PgBouncer transaction-mode** + read replica cho nearby-orders/products/commissions queries | Trung bình | >10K users |
| **ST5** | B2 Wallet race | **`SELECT ... FOR UPDATE`** trên last WalletTransaction của OwnerId HOẶC **atomic sequence table** `WalletBalance(CustomerId, Balance)` updated via `UPDATE ... SET Balance += amount RETURNING Balance` | Thấp | Sprint 5 (implement ngay từ đầu) |
| **ST6** | B8 sync commission | **Outbox + NATS** cho commission calc (như Loyalty L-A pattern đã có). Order.Completed → enqueue `CommissionCalculationRequested` → handler tạo SalesReferral async | Thấp | Sprint 4 (implement ngay từ đầu) |
| **ST7** | B11 region scoping | **Thêm `Region` (string, 2 chars province code) vào Order + TenantSettings + Community entities**. Query nearby: `WHERE Region IN (...) AND ST_DWithin(...)`. Giảm 63x scan scope | Trung bình — schema change | Sprint 0 (add field) OR Sprint 7 (migration) |
| **ST8** | B6 SQLite 300K files | **Lazy migration**: Sprint 0 SQLite migration chỉ apply khi tenant first access. Track migration version per-tenant trong `ShopInstances` table | Thấp | Sprint 7+ (khi migrate SQLite → PG RLS) |
| **ST9** | B7 SalesmanCode collision | **Tenant prefix + 4 random chars** = `{tenantShortCode}{4chars}` (vd `HNA01A3B`) → unique per tenant thay vì global. Index unique `(TenantId, SalesmanCode)` | Thấp | Sprint 0 (implement ngay) |
| **ST10** | B5 partition | **PG declarative partitioning by month** cho `WalletTransaction`, `DeliveryTracking`, `Message` (RANGE on CreatedAt). Giảm index size, query chỉ scan partition hiện tại | Trung bình | >100K users |

### 7C.4 Long-Term Solutions (LT1-LT8 — architectural refactor, 6-12 tháng)

| # | Bottleneck | Solution | Migration strategy |
|---|---|---|---|
| **LT1** | B1 GPS write | **Dedicated time-series store** (TimescaleDB extension trên cùng PG OR InfluxDB separate). Hypertable auto-partition by time + compression cho data cũ | Dual-write PG → TimescaleDB trong 1 tháng, sau đó read chỉ từ TimescaleDB, drop PG table |
| **LT2** | B5 single PG | **Service decomposition** với database-per-service: (1) Delivery Service + own DB, (2) Chat Service + own DB (Cassandra/Mongo), (3) Wallet Service + own DB (event-sourced), (4) Referral Service + own DB | Strangler fig — extract read model trước, sau đó move writes. NATS làm integration glue |
| **LT3** | B2 Wallet race | **Event-sourced Wallet**: WalletTransaction = events (append-only đã có sẵn). Read model `WalletBalance` rebuilt from events. Idempotent consumers. Replay-able | WalletTransaction đã append-only → chỉ cần rebuild read model. Không cần rewrite event store |
| **LT4** | B6 SQLite 300K files | **Shared PG với Row-Level Security (RLS)** thay per-tenant SQLite. Tenant isolation = RLS policy `USING (TenantId = current_setting('app.tenant_id'))` | Big migration — cần tool sync SQLite → PG per tenant. Phần lớn ứng dụng đã qua Gateway → SQLite chỉ là replica, có thể drop |
| **LT5** | B3 nearby queries | **Geo-sharding**: PG cluster per region (Hà Nội, HCMC, Đà Nẵng, Cần Thơ). Tenant gán region lúc tạo. Cross-region query qua federation (rare) | Khó nhất — cần routing layer ở Gateway quyết định PG cluster theo tenant region |
| **LT6** | B10 chat storage | **Cassandra/MongoDB** cho Messages (write-optimized, horizontal scale, TTL auto-expire 30 ngày) | Dual-write PG → Cassandra 1 tháng, switch read, drop PG Message table |
| **LT7** | B9 attribution burst | **Bloom filter** cho dedup (Redis hoặc in-memory). Bloom filter check install history trước khi query DB | Bloom filter ở API layer, miss → query DB. Giảm DB load 99% |
| **LT8** | B7 SalesmanCode | **Snowflake ID** hoặc **ULID** (sortable, globally unique, no coordination) | Drop-in thay SalesmanCode generation, không thay schema |

### 7C.5 9 Corrections — Edge-Only Proposal SAI ở đâu

> User đề xuất edge-only (loại central, 10km geo-fence cứng, 5-10 tenants/ShopERP, PG chịu tải lớn thay Redis/Kafka, bỏ OTP SMS). Review honest:

| # | User claim | Verdict | Correction |
|---|---|---|---|
| 1 | Bỏ OTP SMS hoàn toàn | **SAI** | Thay bằng Viettel SMS + Zalo OA + WhatsApp multi-channel. KHÔNG bỏ phone verification (fraud prevention) |
| 2 | KhachLink chỉ kết nối gateway đăng ký + 10km | **SAI** | Phá 4 UC (UC-09 referral cross-region, UC-12 app-install, UC-10 dashboard, shipper du lịch). Geo-fence MỀM — default nearest gateway, fallback central cho cross-region |
| 3 | 5-10 tenants/ShopERP không vấn đề | **SAI** | Blast radius (1 ShopERP chết = 10 tenants down) + admin reporting (10K instances = không query được) + tenant migration phức tasa |
| 4 | PG "chịu tải lớn" thay Redis/Kafka | **SAI** | PG không tối ưu cho cache (ms vs μs), pub-sub (LISTEN/NOTIFY 10K events/sec limit), time-series (VACUUM block writes). Dùng in-memory .NET + TimescaleDB extension + NATS thay thế |
| 5 | Chỉ PG chịu tải lớn, không cần central | **SAI** | Wallet/AppInstallAttribution/SalesmanCode/Customer account cần global consistency. Edge-only = data inconsistency |
| 6 | Không cần NATS | **SAI** | NATS vẫn cần làm async glue giữa gateway và ShopERP cross-region. HTTP sync = coupling + partial failure |
| 7 | Edge gateway rẻ hơn central | **SAI ở 10K-100K** | 100 gateways × $40/tháng = $4K/tháng vs central $2.4K/tháng. **Đúng ở 1M+** (break-even ~1M users) |
| 8 | Phạm vi đăng ký cứng 10km | **SAI** | UX friction + migration hell (10% dân di cư/năm = 2,700 migrations/ngày) |
| 9 | Referral cross-region edge-only OK | **SAI** | UC-09/UC-12 composite QR share online (Facebook, Zalo) bắt buộc central authority cho commission calculation |

### 7C.6 Refactor Impact Reduction (R1-R8 — apply từ Sprint 0)

> Giảm tác động refactor khi scale up. Nguyên tắc: **Strangler Fig + Anti-Corruption Layer + Event-Driven Integration**.

| # | Technique | Apply vào Community Commerce | Khi nào |
|---|---|---|---|
| **R1** | **Strangler Fig** | Wrap Community Commerce trong bounded context riêng. Khi extract ra microservice (LT2), chỉ cần replace interface impl — consumer không biết | Sprint 0 (design) |
| **R2** | **Anti-Corruption Layer** | Community entities KHÔNG reference trực tiếp Order/Customer/Product aggregate. Dùng `OrderId`, `CustomerId`, `ProductId` (Guid) as FK references. Khi tách service, FK thành external ID — không phá domain | Sprint 0 (design — đã đúng trong spec) |
| **R3** | **Event-Driven Integration** | Community events (OrderCompleted, DeliveryCompleted, CommissionCalculated, AppInstallAttributed) qua NATS/Outbox — đã có pattern. Consumer loose-coupled. Refactor 1 service không break others | Sprint 4 (commission Outbox) |
| **R4** | **Versioned APIs** | Spec dùng `/api/community/*` — **NÊN đổi thành `/api/v1/community/*`** ngay từ Sprint 1. Khi refactor v2, cả 2 version chạy song song 6 tháng | Sprint 1 (hard rule mới) |
| **R5** | **Read Model Separation** | Nearby orders/products/commissions queries → CQRS read model (materialized view hoặc separate table). Khi migrate storage (LT1, LT6), chỉ thay read model source — write side không động | Sprint 7+ |
| **R6** | **Domain Module Split** | Hiện `Domain.cs` single file khổng lồ. Khi extract services, tách thành `Domain.Community.cs`, `Domain.Wallet.cs`, `Domain.Delivery.cs` — cùng assembly nhưng module boundaries rõ. Refactor không phá build | Sprint 7+ |
| **R7** | **Migration Rehearsal** | Mỗi Sprint 0 migration phải test trên copy of production DB (pg_dump → restore → migrate). Đo thời gian. Nếu >5min → expand-contract bắt buộc | Sprint 0 (process) |
| **R8** | **Backward-Compat Window** | Mỗi schema change giữ 2 phiên bản field (vd `ReferralCode` old + `ReferralCodeComposite` new) trong 30 ngày. App đọc cả 2. Sau 30 ngày → drop old | Sprint 7+ (schema changes) |

### 7C.7 Cuốn Chiếu (Rolling Deployment) Strategy

**Verdict:** ÁP DỤNG ĐƯỢC 80%, KHÔNG áp dụng được 20% (big-bang bắt buộc).

**✅ Roll được (gradual rollout OK):**
| Layer | Strategy |
|---|---|
| API endpoints | Feature flag per-tenant: `GET /api/feature/community/{tenantId}` trả enabled/disabled. Roll out 10 tenants → 100 → 1K → all |
| GPS write (B1) | In-memory buffer behind flag. Toggle on cho 10 shippers trước, monitor write rate, expand |
| Wallet reconciliation | Reversal pattern là nền tảng cho cuốn chiếu: nếu batch reconcile sai → reversal entry thay vì update → an toàn rollback dữ liệu |
| PG migrations (expand-contract) | (1) Add nullable column (online, no lock) → (2) Backfill batch 1K rows/min → (3) Switch app to write new column → (4) Drop old column sau 1 tuần. `CREATE INDEX CONCURRENTLY` cho index. `pg_repack` cho table rewrite |
| NATS subscribers | Blue/green: subscribe v2 handler cùng v1, drain v1 sau khi v2 confirmed healthy |
| CD pipeline (existing) | Zero-downtime deploy đã verified — multi-container health check |

**❌ KHÔNG roll được (big-bang bắt buộc):**
| Layer | Lý do |
|---|---|
| Sprint 0 Domain Modification (11 entity + 8 Order fields + 3 enum expansion) | `Domain.cs` thay đổi = compile-time, không có runtime flag. Toàn bộ codebase phải build cùng version |
| EF migration schema | Apply 1 lần cho cả DB. KHÔNG có "apply cho 10 tenant trước" — PG schema = 1 instance |
| `OrderStatuses.Default[]` + "delivering" status (CC-S1-T0) | Hằng số Domain = compile-time |
| SQLite per-tenant migration (B6) | 300K file phải migrate. Cuốn chiếu = lazy migration (ST8) — apply khi tenant access. KHÔNG đồng loạt |

**🟡 Roll được nhưng RỦI RO cao:**
| Layer | Risk |
|---|---|
| Composite referral code (Sprint 4) | Mới + cũ cùng tồn tại: old `referralCode` (salesmanCode only) + new (`{salesmanCode}|{productShortCode}`). Parse logic phải handle cả 2 → backward compat |
| AppInstallAttribution unique constraint | Add constraint online = `ALTER TABLE ... ADD CONSTRAINT ... NOT VALID` + `VALIDATE CONSTRAINT` sau (PG 12+). Nhưng nếu data đã có duplicate → fail. Phải dedup trước |

### 7C.8 Architecture Evolution Roadmap (PoC → 10M users)

```
PoC (hiện tại, 50 users)  →  10K users         →  100K users        →  1M+ users (Edge switch)
─────────────────────────────────────────────────────────────────────────────────────────────
Sprint 0-6 (spec v1.4)       +PostGIS (ST1)       +PgBouncer (ST4)     +Service decomp (LT2)
                             +Redis GPS (ST2)     +Partition (ST10)    +Geo-sharding (LT5)
                             +SignalR Redis (ST3) +Region (ST7)        +TimescaleDB (LT1)
                             +Wallet atomic (ST5) +Lazy SQLite (ST8)  +Event-sourced Wallet (LT3)
                             +Outbox commission   +SalesmanCode tenant +Cassandra Chat (LT6)
                             (ST6)                prefix (ST9)
                             +/api/v1/ (R4)
                             +Anti-Corruption (R2)
                             +Strangler Fig (R1)
```

**Switch point:** Central-only đến 1M users. Từ 1M+ → bắt đầu edge deployment (Sprint 7+).

### 7C.9 Hard Rules mới (v1.4 — apply từ Sprint 0 hoặc khi threshold reached)

> Các hard rules này được thêm vào Section "Hard Rules" của master plan.

| Rule | Apply khi | Sprint |
|---|---|---|
| **HR-SCALE-1:** API endpoints MUST use `/api/v1/community/*` versioning | Từ Sprint 1 | Sprint 1 |
| **HR-SCALE-2:** Community entities MUST use Guid FK references (KHÔNG direct aggregate references) — Anti-Corruption Layer | Từ Sprint 0 | Sprint 0 (đã đúng trong spec) |
| **HR-SCALE-3:** WalletTransaction BalanceAfter MUST be computed via atomic sequence (`SELECT FOR UPDATE` hoặc `UPDATE ... RETURNING`) — KHÔNG read-then-write | Từ Sprint 5 | Sprint 5 |
| **HR-SCALE-4:** Commission calculation MUST go through Outbox + NATS (KHÔNG inline sync trong OrderWorkflowService) | Từ Sprint 4 | Sprint 4 |
| **HR-SCALE-5:** SalesmanCode MUST use tenant prefix + random chars (unique per tenant, KHÔNG global unique) | Từ Sprint 0 | Sprint 0 |
| **HR-SCALE-6:** PostGIS extension + ST_DWithin + GIST index MUST be installed when >1K shippers active OR >10K orders/ngày | Khi threshold reached | Sprint 7+ |
| **HR-SCALE-7:** SignalR Redis backplane MUST be configured when >1K shippers active OR >65K total connections | Khi threshold reached | Sprint 7+ |
| **HR-SCALE-8:** PgBouncer transaction-mode MUST be deployed when >10K users | Khi threshold reached | Sprint 7+ |
| **HR-SCALE-9:** PG declarative partitioning by month MUST be configured cho WalletTransaction, DeliveryTracking, Message when >100K users | Khi threshold reached | Sprint 7+ |
| **HR-SCALE-10:** Region column (province code, 2 chars) MUST be added vào Order, TenantSettings, CommunityRole, DeliveryTask before geo-sharding (LT5) | Sprint 7+ (migration) | Sprint 7+ |
| **HR-SCALE-11:** Mọi PG migration trên production MUST test trên copy of production DB trước (pg_dump → restore → migrate). Nếu >5min → expand-contract | Từ Sprint 0 (process) | Sprint 0+ |
| **HR-SCALE-12:** Geo-fence MỀM — default nearest gateway, fallback central cho cross-region flows (referral, app-install, wallet, dashboard). KHÔNG geo-fence cứng | Từ Sprint 7+ (edge) | Sprint 7+ |

## 8. Risks & Mitigations

| Risk | Severity | Mitigation |
|---|---|---|
| PWA GPS không background track | High | Thông báo user, adaptive polling (v1.1: 10s/30s/stop), post-PoC native app |
| Cross-tenant auth complexity | High | Community entities trên PG, custom auth policy check role trong DB, multi-tenancy isolation architecture test (v1.1) |
| Product catalog chỉ FeaturedProducts | Medium | PoC: đủ. Post-PoC: build search index |
| COD financial flow phức tạp | High | Sprint 5 riêng, immutable wallet, Reversal pattern (v1.1), unit test kỹ |
| Double-accept race condition | Medium | DB unique constraint trên DeliveryTask.OrderId (1 active task per order) |
| SMS gateway cost cho OTP | Low | Dev: X-Dev-OTP. Prod: tích hợp SMS gateway (Twilio/Viettel) |
| Legal: sàn TMĐT đăng ký | High | Sprint 6 draft documents. Tham vấn luật sư trước launch |
| NATS order sync conflict | Medium | Delivery status sync SQLite→PG đã có. Shipper updates qua Gateway PG trực tiếp |
| App install attribution fraud (v1.1 NEW) | High | Unique constraint AppInstallAttribution.CustomerId (1 customer 1 attribution). Check install history. Bonus chỉ award khi customer chưa cài app trước đó (AC-12.7). Audit log mọi attribution. |
| ProductReferralConfig missing cho product (v1.1 NEW) | Medium | Product không có config → commission/bonus = 0 (graceful degrade). Salesman thấy "Chưa thiết lập" trong UI. Admin CRUD để set. |
| Composite referral code parse error (v1.1 NEW) | Low | Validate format `{salesmanCode}|{productShortCode}` server-side. Fallback: nếu không có `|`, treat toàn bộ là salesmanCode (backward compat với PoC early). |
| "delivering" status chưa có trong OrderStatuses.Default[] (v1.1 NEW) | Medium | Sprint 1 task CC-S1-T0 verify/add. Domain Modification — cần approval. |
| Device fingerprint collision (v1.2 NEW) | Low | 15+ signals → ~20-30 bits entropy → 1/1M trùng. 5-10M users → ~5-10 trùng. Mitigate: behavioral layer (IP, account age) + admin manual review. Whitelist false positives. |
| Device fingerprint spoofing (v1.2 NEW) | Medium | VM/emulator có thể giả fingerprint. Mitigate: canvas/WebGL/audio khó giả hoàn hảo. Risk scoring + behavioral catch cluster. Post-PoC: native app attestation (95%+ chặn VM). |
| Customer clear localStorage = lost device token (v1.2 NEW) | Low | Phải re-register device. UX friction. Mitigate: fallback email/social login. DeviceRegistration mới = device mới (count against 3-device limit). |
| False positive fraud flag (v1.2 NEW) | Medium | Family cùng nhà (cùng IP, fingerprint gần giống) → RiskScore cao. Mitigate: admin manual review + Dismiss + whitelist. Hold 48h không reject — customer vẫn dùng app, chỉ commission/bonus hold. |
| WebAuthn support gap (v1.2 NEW, post-PoC) | Low | iOS <16, Android <9 không support WebAuthn. Mitigate: fallback social/email login. WebAuthn OPTIONAL, không bắt buộc. |
| Anti-fraud bypass — click farm thuê người thật (v1.2 NEW) | Medium | Attacker thuê 100 người cài app → fingerprint khác → khó catch. Mitigate: economic disincentive — click farm VN ~$1-2/install, bonus $0.5/install → attacker LỖ 50-150% → tự chết. Hold 48h + KYC bank account cho payout thêm rào cản. |
| 3-strike ban false positive (v1.2 NEW) | Low | Customer bị ban oan do 3 false positive flags. Mitigate: admin review mọi flag (không auto-ban). Dismiss flag = không tính strike. Customer có thể appeal qua email. |

---

## 9. Legal Checklist (Sprint 6)

### 9.1 Giai đoạn PoC (15 ngày)
- [ ] Thông báo app/website TMĐT (Bộ Công Thương) — form thông báo
- [ ] Điều khoản sử dụng cho khách hàng
- [ ] Chính sách bảo mật dữ liệu cá nhân (Nghị định 13/2023)

### 9.2 Giai đoạn 2 (30 ngày sau PoC)
- [ ] Hồ sơ đăng ký sàn TMĐT (nếu thuộc diện)
- [ ] Quy chế hoạt động sàn
- [ ] Điều khoản cho cộng tác viên (Salesman/Shipper)
- [ ] Chính sách hoàn tiền
- [ ] Quy trình giải quyết tranh chấp
- [ ] Quy trình KYC cộng tác viên
- [ ] Cơ chế chống gian lận

### 9.3 Giai đoạn 3 (PMF)
- [ ] Báo cáo tài chính minh bạch
- [ ] Tuân thủ thuế cho cộng tác viên (PIT)
- [ ] E-invoice cho giao dịch sàn

---

## 10. Success Metrics (PoC)

| Metric | Target | Measurement |
|---|---|---|
| Cửa hàng tham gia | 10-20 | Tenant count |
| Khách hàng active | 50-100 | Customer count |
| Cộng tác viên | 10 | CommunityRole count |
| Đơn hàng thực | 300-500 | Order count |
| Tỷ lệ giao thành công | ≥95% | Delivered/Total DeliveryTask |
| Khách quay lại | ≥30% | Repeat customer rate |
| Đơn có referral code | ≥20% | Order with SalesmanId / Total |
| Chat usage | ≥50% | Orders with chat messages / Delivered orders |
| GPS tracking uptime | ≥80% | DeliveryTask with ≥3 tracking points |
