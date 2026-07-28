# Review: Shipper/Salesman Role Requirements vs Codebase

Bản đối chiếu yêu cầu tính năng shipper/salesman (từ `docs/requirements/shipper_saleman_role.md`) với codebase thực tế, chỉ ra các điểm sai và không khả thi.

---

## 1. Không có entity nào cho Shipper/Salesman/Community

**Yêu cầu đề xuất:** `CommunityRole`, `CustomerRole`, `DeliveryTask`, `DeliveryOffer`, `DeliveryAssignment`, `DeliveryTracking`, `SalesReferral`, `CommissionRecord`, `WalletTransaction`, `GeoLocation` — 10 entity mới.

**Thực tế codebase:** Không có BẤT KỲ entity nào trong số này. `Domain.cs` (2967 dòng) chỉ có: `Order`, `OrderItem`, `Customer`, `Product`, `Tenant`, `Voucher`, `LoyaltyRewards`, `UserTenant`, `PlatformUser`. Grep toàn workspace cho `Shipper|Salesman|DeliveryTask|SalesReferral|CommissionRecord|WalletTransaction|GeoLocation|CommunityMember` → **0 kết quả**.

**Đánh giá:** Đây là module hoàn toàn mới, không phải "bổ sung tính năng". Khối lượng Domain modeling + migration + EF configuration tương đương một sprint lớn.

---

## 2. Authentication: KhachLink KHÔNG có auth — không thể có "role"

**Yêu cầu:** System Admin kích hoạt vai trò shipper/salesman cho customer đạt điều kiện (xác thực SĐT, ≥1000 điểm).

**Thực tế:**
- `AnonymousAuthenticationStateProvider.cs` — KhachLink trả về **anonymous user không có claims nào**. Không có JWT, không có login flow cho customer.
- Gateway có JWT + Cookie auth, nhưng policies chỉ phục vụ ShopERP staff: `RequireTenantAccess` (cần `tenant_id` claim), `RequireOwnerRole`, `SystemAdmin`.
- `UserRole` enum (cả bản cũ và `UserAggregate.UserRole`): `None, Owner, StoreKeeper, Guard, Staff, Masterchef` — **không có Shipper, Salesman, Customer, CommunityMember**.
- Customer identity hiện tại: `IdentityLevel` (Guest=0, Social=1, Verified=2, Full=3) + `DeviceId` — không có khái niệm "role" của customer.

**Không khả thi:** Không thể "System Admin kích hoạt role" khi customer chưa có auth identity. Cần xây toàn bộ customer auth flow (JWT issuance cho customer, customer login/registration, role assignment) trước khi nói đến shipper/salesman role.

---

## 3. Không có GPS/Location tracking thời gian thực

**Yêu cầu:** Shipper thấy đơn trong bán kính 5-10km, vị trí cập nhật real-time cho khách xem.

**Thực tế:**
- `StoreFinder.razor`: dùng `navigator.geolocation` JS interop — **chỉ lấy vị trí 1 lần** khi user bấm nút, không continuous tracking.
- `GoogleMaps.razor`: iframe embed tĩnh (`maps.google.com/maps?q=...&output=embed`) — **không phải interactive map**, không có marker, không có route, không có real-time update.
- Không có Leaflet/Mapbox/OpenStreetMap.
- `TenantSettings` có `Latitude/Longitude` (cho shop), nhưng `Customer` entity **không có field vị trí**.
- Không có `GeoLocation` entity hay service nào.
- Không có SignalR hub cho location update — `OrderHub` và `KitchenHub` chỉ join/leave group, không push location.

**Không khả thi:** Real-time GPS tracking trong Blazor WASM PWA bị giới hạn bởi browser (geolocation API chỉ hoạt động khi tab active, không background tracking như native app). Cần:
- Native app hoặc PWA with Background Fetch API (hạn chế)
- Map library thật (Leaflet/Mapbox) thay iframe
- `CustomerLocation` entity + SignalR location hub
- Continuous polling service

---

## 4. Không có Chat infrastructure — "AI chatbot" không tồn tại

**Yêu cầu:** Khách và shipper chat qua "khung chat AI chatbot trên KhachLink".

**Thực tế:**
- Grep `ChatHub|ChatService` toàn workspace → **0 kết quả**.
- Chỉ có `OrderHub` (join/leave shop group) và `KitchenHub` (join/leave kitchen group) — không có message send/receive.
- Không có `Conversation`, `Message`, `ChatThread` entity.
- Không có AI chatbot infrastructure (không OpenAI integration, không NLP service).

**Không khả thi:** Cần xây toàn bộ chat module: message entity, SignalR chat hub, message persistence, UI chat component. "AI chatbot" là một feature riêng cần tích hợp LLM service.

---

## 5. Order status flow không hỗ trợ delivery lifecycle

**Yêu cầu:** Shipper nhận đơn → xem giai đoạn xử lý → nhận hàng → giao → cập nhật trạng thái.

**Thực tế:**
- `OrderStatusId` hiện có: `pending → confirmed → preparing → ready → delivering → completed → cancelled`.
- Không có trạng thái: `assigned_to_shipper`, `picked_up`, `out_for_delivery`, `delivery_failed`, `returned`.
- `Order` entity không có field: `ShipperId`, `DeliveryTaskId`, `AssignedAt`, `PickedUpAt`, `DeliveredAt`.
- `Order.DeliveryAddress` là `string?` — không có tọa độ (lat/lng) để shipper điều hướng.
- `OrderWorkflowService.TransitionStatusAsync` — không có logic phân công/nhận đơn cho shipper.

**Đánh giá:** Cần mở rộng Order state machine + thêm delivery-specific fields + delivery assignment logic. Việc này ảnh hưởng `OrderWorkflowService`, `OrdersController`, `OrderHub`, và toàn bộ order sync PG→SQLite qua NATS.

---

## 6. Không có Wallet/Commission/Referral system

**Yêu cầu:** Shipper ứng tiền trả shop, thu lại của người mua. Salesman có mã QR gắn vào đơn để tính doanh số/lương.

**Thực tế:**
- Không có `WalletTransaction`, `CommissionRecord`, `SalesReferral` entity.
- `Voucher.QRCodeData` — chỉ cho loyalty redemption voucher, không phải salesman referral QR.
- `Order` không có `SalesmanId` / `ReferralCode` field.
- Không có cơ chế COD (Cash on Delivery) hay financial settlement giữa shipper-shop-customer.
- Payment hiện tại: `CASH`, `VIETQR`, `CREDIT_CARD` — không có "shipper-collect" payment method.

**Đánh giá:** Cần xây toàn bộ financial ledger cho community members. Đây là logic tài chính — phải tuân thủ `AccountingEntry` immutable pattern, domain purity rules, và có thể ảnh hưởng kế toán HKD.

---

## 7. Multi-tenant conflict: Shipper cross-tenant không khớp architecture

**Yêu cầu:** Shipper thấy đơn hàng trong bán kính 5-10km từ vị trí của họ — đơn hàng từ **nhiều shop khác nhau**.

**Thực tế:**
- Theo Option C (approved 2026-07-18): Gateway PG là source of truth cho Orders, nhưng Orders thuộc về **specific Tenant**.
- Auth policy `RequireTenantAccess` yêu cầu `tenant_id` claim — staff truy cập orders trong 1 tenant.
- Shipper là cross-tenant entity — không thuộc 1 tenant cụ thể. Không có auth policy nào cho "cross-tenant order access".
- Products sống trong **per-tenant SQLite** (ShopERP) — yêu cầu "salesman thấy sản phẩm của tenants trong 10km" cần query **nhiều ShopERP instance**, không có cơ chế aggregate catalog query hiện tại.

**Không khả thi:** Cần thiết kế lại auth model cho community members (cross-tenant identity), và giải quyết product catalog aggregation (Gateway không có Products table — chỉ FeaturedProducts PG).

---

## 8. "Salesman thấy sản phẩm tenant trong 10km" — mâu thuẫn Option C

**Yêu cầu:** Salesman thấy sản phẩm của các tenant trong bán kính 10km.

**Thực tế (Option C):**
- Products sống trong **per-tenant SQLite** ở ShopERP instances.
- Gateway PG **không có Products table** — chỉ có `FeaturedProducts` (curated subset).
- `KhachLink.Home.razor` dùng `GET /api/catalog/recommended` (PG query) — không forward to ShopERP.
- Product catalog browse forwards via YARP to ShopERP — nhưng chỉ cho 1 tenant tại thời điểm.
- Không có cơ chế query products từ **nhiều ShopERP instances cùng lúc**.

**Không khả thi:** Cần hoặc (a) đồng bộ product catalog lên Gateway PG (thay đổi Option C), hoặc (b) fan-out query nhiều ShopERP instances (phức tạp, latency cao), hoặc (c) build dedicated product search index.

---

## 9. QR mã salesman — QRScanner hiện tại không hỗ trợ

**Yêu cầu:** Salesman đưa QR cho khách quét, QR chứa mã salesman, gắn vào đơn hàng khi shipper nhận đơn.

**Thực tế:**
- `QRScanner.razor` tồn tại — nhưng scan QR code của **tenant** (chuyển hướng đến shop).
- Không có `SalesmanCode` / `ReferralCode` entity.
- `Order` không có field `ReferralCode` / `SalesmanId`.
- Không có cơ chế generate QR chứa salesman ID.

**Đánh giá:** Cần thêm referral entity + QR generation service + modify order creation flow để accept referral code.

---

## 10. Tài liệu là chat log, không phải requirements spec

**Vấn đề:** File `shipper_saleman_role.md` là **bản ghi cuộc trò chuyện** với một consultant bên ngoài, không phải tài liệu requirements chính thức. Nhiều đề xuất mang tính aspirational:
- "Local Commerce Network" (Grab + Shopee Affiliate + Loyalty + POS + CRM + Mini ERP)
- "10 Entity mới, 25 API, 15 màn hình" — ước lượng thiếu cơ sở từ codebase thực
- "Giai đoạn 4: bùng nổ Đông Nam Á" — không liên quan technical
- Consultant chỉ xem 3 file (VanAn.sln, Program.cs, csproj) rồi đưa ra đánh giá — **không đủ thông tin** để đánh giá chính xác

**Đánh giá:** Cần viết lại thành requirements spec chính thức, dựa trên codebase thực, với use case cụ thể và acceptance criteria đo được.

---

## Tóm tắt: Các điểm KHÔNG KHẢ THI

| # | Yêu cầu | Lý do không khả thi |
|---|---|---|
| 1 | System Admin kích hoạt role cho customer | KhachLink không có auth, customer không có identity JWT |
| 2 | Shipper thấy đơn trong 5-10km | Không có GPS entity, không có cross-tenant order query, Order không có coordinates |
| 3 | Vị trí shipper real-time | WASM PWA không background track, không có SignalR location hub, không có interactive map |
| 4 | Chat giữa buyer và shipper | Không có ChatHub, không có message entity, không có AI chatbot |
| 5 | Salesman thấy products tenant trong 10km | Products trong per-tenant SQLite, không có aggregate catalog query |
| 6 | Shipper ứng tiền + thu COD | Không có wallet/settlement entity, không có COD payment method |
| 7 | QR salesman gắn vào order | Order không có referral field, QRScanner chỉ scan tenant QR |
| 8 | Community module (10 entity, 25 API, 15 màn hình) | Khối lượng tương đương rebuild platform — không phải "bổ sung tính năng" |

## Tóm tắt: Các điểm SAI so với codebase

| # | Tuyên bố trong tài liệu | Thực tế |
|---|---|---|
| 1 | "Shipper/Salesman hoàn toàn có thể là role mở rộng của Customer" | Customer không có auth identity, không có role system |
| 2 | "Không cần tạo ứng dụng mới" | KhachLink WASM không hỗ trợ background GPS, cần native app hoặc PWA major upgrade |
| 3 | "KhachLink chỉ gọi Gateway bằng HTTP" — đúng, nhưng consultant kết luận "rất phù hợp" | Gateway không có API cho community/delivery/GPS/chat — cần xây toàn bộ |
| 4 | "tận dụng tốt hạ tầng KhachLink, ShopERP và Gateway hiện có" | Hạ tầng hiện tại không có bất kỳ component nào cho delivery/community |
| 5 | "10 Entity mới" | Thiếu thực tế — cần thêm: CustomerLocation, ChatMessage, Conversation, DeliveryRoute, ShipperRating, SalesmanRating, AntiFraudLog, KYCRecord... |

## Khuyến nghị

1. **Viết lại requirements spec** thành tài liệu chính thức với use case + acceptance criteria
2. **Chia thành sprint nhỏ** — không thể build toàn bộ community module trong 1 sprint
3. **Ưu tiên Phase 0:** Customer auth (JWT cho customer) + Customer location field + Order delivery fields
4. **Đánh giá lại PWA vs Native app** cho GPS tracking — PWA có giới hạn nghiêm trọng
5. **Xem xét pháp lý** — consultant đúng về việc có thể cần đăng ký sàn TMĐT với Bộ Công Thương
