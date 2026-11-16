# Vạn An Ecosystem — Danh mục tính năng (Use Case View)

> **Mục đích:** bản đồ toàn bộ tính năng hệ thống theo góc nhìn **actor → use case** (UML use case view).
> **Nguồn:** `ShopErpMenuService.cs` (SSOT menu theo role) · `5_WebApps/KhachLink/Pages/` + `Components/` · `2_Gateway/Controllers/` · `5_WebApps/ShopERP/Components/Pages/`.
> **Ngày:** 2026-11-14 · `main` @ `b8df369c` (+ Entry-Points Retention W1-W3, Tenant AI, voucher framework).

---

## 0. Kiến trúc kênh & actors

```
Khách hàng / CTV / Shipper / Merchant-mới ──► KhachLink PWA (:5002) ──┐
   (multi-instance theo domain:                                       │  HTTP + JWT
    FullCommerce/Directory/Reseller/                                  ▼        │
    Logistics/JobMarket/Hoptacxa)              SQLite per-tenant ◄─── Gateway (:5001) ───► PostgreSQL
Staff / Owner / Kế toán / SystemAdmin ──► ShopERP Blazor (:5003) ──┤   (order creator,      (orders,
   + Masterchef / Guard                                   source of truth,   accounting,
                                                           JWT, tenant, audit) tenants,
                                                           │                  ShopInstances,
                                                           ▼ NATS JetStream (:4222)  KhachLinkInstances)
                                              order.created.{shopInstance}
                                                           │
                                              Metering "Phí nền tảng" (wallet/rate-card)
                                              TenantAi (LLM per-tenant) · Voice (WebRTC calls)
```

| # | Actor | Kênh truy cập |
|---|---|---|
| A1 | **Khách hàng** | KhachLink PWA :5002 (domain theo instance profile) |
| A2 | **CTV / Salesman** | KhachLink (`/community/*`) |
| A3 | **Shipper** | KhachLink (`/community/*`) |
| A4 | **Merchant mới** | KhachLink `/onboard` (self-signup cửa hàng) |
| A5 | **Staff / StoreKeeper** | ShopERP :5003 |
| A6 | **Masterchef** | ShopERP `/kitchen` |
| A7 | **Guard** | ShopERP `/guard/scan` |
| A8 | **Owner** (chủ shop) | ShopERP |
| A9 | **ChiefAccountant / DebtAccountant** | ShopERP |
| A10 | **SystemAdmin** | ShopERP `/admin/*` + Gateway API :5001 |
| A11 | **Hệ thống** (jobs/NATS/sync/AI/voice) | nội bộ |

---

## A1 — Khách hàng (KhachLink PWA)

> Nav items hiển thị theo **instance profile + nav flags + role** (xem `KhachLink_Multi_Profile_Usage_Guide`). Các route `/community/*` chỉ render khi `ShowCommunity=true` + role match.

### UC-1.1 Mua sắm & đơn hàng
| Use case | Mô tả | Trang/Route |
|---|---|---|
| Duyệt catalog | Trang chủ, sản phẩm nổi bật, cửa hàng, HTX portal (profile Hoptacxa) | `Home`, `Store`, `StoreFinder`, `NearbyProducts`, `HoptacxaPortal` |
| Tìm kiếm | Search sản phẩm | `ProductSearchResults` (`/products`) |
| Giỏ hàng | Thêm/sửa/xóa món | `Cart` (`/cart`) |
| Checkout | Đặt hàng online — snapshot ProductName+VatRate gửi Gateway tạo order | `Checkout` (`/checkout`) |
| Theo dõi đơn | Realtime trạng thái + lịch sử + chat với shop | `OrderTracking` (`/order-tracking/{id}`, `/c/{code}`), `OrderHistory` (`/my-orders`) |
| Đặt lịch hẹn | Chọn dịch vụ → giờ → xác nhận → cọc | `BookingOffering/Time/Confirm/Status/Note` (`/booking/{QrToken}/*`) |
| Ghi chú đơn | STT only (không audio) | `VoiceNote` (`/voice-note`) |
| Quét QR | Scan QR shop/CTV + QR thanh toán | `Scan` (`/scan`), `QrPaymentModal` |
| Chat & gọi thoại | Chat realtime per-order + voice call (WebRTC) với shop/shipper | `ChatPanel`, voice hub |

### UC-1.2 Loyalty & ví
| Use case | Trang |
|---|---|
| Thẻ thành viên + điểm | `LoyaltyCard` (`/my-loyalty`) |
| Ví cá nhân / ví liên minh | `Wallet` (`/community/wallet` flow + wallet page), `AllianceWallet` (`/alliance-wallet`) |
| Đổi quà catalog | `RedemptionCatalog` (`/rewards`) |
| Nhiệm vụ kiếm điểm | `Missions` (`/missions`) |
| Xem/tham gia campaign | `Campaigns`, `Campaign`, `Claim` (`/campaigns`, `/claim`) |

### UC-1.3 Tài khoản & hệ thống
| Use case | Trang |
|---|---|
| Đăng ký / đăng nhập OTP | `Register`, `Login` |
| Hồ sơ multi-profile | `Profile` (`/profile`) |
| Đăng ký thành viên HTX | `MembershipRegister` (`/membership/register/{htxTenantId}`) |
| Thông báo | `Notifications` (`/notifications`) |
| Xác minh SMS | `CollaboratorVerification` (`/collaborator-verification`) |
| Dashboard realtime | `Dashboard` (`/dashboard` → `RealTimeDashboard`) |

## A2 — Merchant mới (KhachLink — self onboarding)

| Use case | Trang |
|---|---|
| Đăng ký cửa hàng | `Onboard` (`/onboard`) — form merchant signup |
| Tra trạng thái đăng ký | `OnboardStatus` (`/onboard/status`) |
| Demo storefront | `Demo` (`/demo`) — tạo cửa hàng demo theo ngành |

## A3 — CTV / Salesman (KhachLink `/community/*`)

| Use case | Route |
|---|---|
| Dashboard doanh số + hoa hồng | `/community/sales-dashboard` (`SalesDashboard`) |
| QR giới thiệu khách | `/community/salesman-qr` (`SalesmanQR`) |
| Cửa hàng được gán | `/community/salesman-store` (`SalesmanStore`) |
| Đơn quanh khu vực | `/community/nearby-orders` (`NearbyOrders`) |
| Sản phẩm lân cận | `/community/nearby-products` |
| Ví CTV | `/community/wallet` |
| Yêu cầu rút hoa hồng | (→ qua SystemAdmin `/admin/withdrawals` duyệt) |

## A4 — Shipper (KhachLink `/community/*`)

| Use case | Route |
|---|---|
| Chuyến giao đang chạy | `/community/active-deliveries` (`ActiveDeliveries`) |
| Theo dõi tuyến giao | `/community/delivery-tracking/{orderId}` (`DeliveryTracking`) |

## A5 — Staff / StoreKeeper (ShopERP)

### UC-5.1 Bán hàng tại quầy
| Use case | Route |
|---|---|
| POS bán hàng | `/pos` (+ `/pos/payment/{orderId}`) |
| Danh sách + chi tiết đơn | `/orders`, `/orders/{id}` |
| In hóa đơn | `/orders/{id}/print` (`PrintBill`) |
| Đặt lịch nhanh | `/booking/create` |
| Màn hình bếp | `/kitchen` |

### UC-5.2 Kiểm kê kho *(+ChiefAccountant xem)*
| Use case | Route |
|---|---|
| Báo cáo ca | `/inventory/shifts` |
| Công thức pha chế | `/inventory/recipes` |
| Nguyên liệu | `/inventory/ingredients` |
| Tồn kho dashboard | `/inventory/dashboard` |
| Báo cáo tồn kho | `/inventory/stock-report` |
| Cảnh báo hết hàng | `/inventory/alerts` |
| Dự báo nhập | `/inventory/forecast` |

### UC-5.3 Đặt lịch hẹn *(Owner+StoreKeeper)*
Hàng đợi `/booking/queue` · lịch trống `/booking/availability` · calendar `/booking/calendar` · nhân viên `/booking/staff` · lịch làm việc `/booking/schedules` · đặt cọc `/booking/deposits` · QR channels `/booking/qr-channels` · hoa hồng CTV `/booking/commission` · cấu hình `/booking/config` · dịch vụ `/booking/services`

### UC-5.4 Hóa đơn điện tử *(+SystemAdmin)*
Dashboard `/einvoice` · quản lý hóa đơn `/einvoice/invoices` · cảnh báo `/einvoice/alerts` · health `/einvoice/health` · cấu hình `/einvoice/configuration` · nhà cung cấp `/einvoice/providers`

## A6 — Masterchef
| Use case | Route |
|---|---|
| Bếp realtime (Home mặc định) | `/kitchen` |

## A7 — Guard
| Use case | Route |
|---|---|
| Quét QR ra/vào | `/guard/scan` |
| In vé/phiếu | `/guard/print/{sessionId}` |

## A8 — Owner (toàn quyền tenant)

### UC-8.1 Bán hàng & vận hành
Toàn bộ UC-5.1–5.4 + quản lý sản phẩm `/products` + voucher chung `/vouchers/{code}`

### UC-8.2 Kế toán (dùng chung với ChiefAccountant)

**Nhập liệu chứng từ** (Voucher Framework — registry + strategy):
| Use case | Route |
|---|---|
| Phiếu thu / phiếu chi | `/accounting/revenue`, `/accounting/expenses` (+ `/accounting/voucher` generic entry) |
| Chuyển tiền | `/accounting/transfers` |
| Phiếu thu/chi kế toán tổng hợp | `/accounting/journal-voucher` |
| Nhập mua / phiếu nhập | `/accounting/purchase-receipts` (+ `/new`, `/edit/{id}`) |
| Doanh thu / chi phí nhập tay | `/accounting/revenue`, `/accounting/expenses` |
| Import Excel | `/accounting/import` |
| Số dư đầu kỳ | `/accounting/opening-balance` |
| Lịch sử giao dịch | `/accounting/history` |

**Kiểm soát:**
Rà soát tiền mặt ≥20tr `/accounting/cash-payment-review` · số dư TK `/accounting/balance` · cân đối phát sinh `/accounting/trial-balance` · đóng kỳ `/accounting/period-closing`

**Sổ sách:**
Nhật ký chung `/accounting/journal` · sổ cái `/accounting/ledger` · sổ chi tiết `/accounting/sub-ledger` · công nợ `/accounting/cong-no` (+ `/cong-no/{loai}/{doiTuong}`) · sổ quỹ `/accounting/cash-book` · sổ ngân hàng `/accounting/bank-book` · sổ DNSN `/accounting/dnsn-books`

**Báo cáo tài chính:**
`/accounting/financial-reports` · bảng cân đối `/accounting/balance-sheet` · kết quả kinh doanh `/accounting/income-statement` · lưu chuyển tiền tệ `/accounting/cash-flow-statement` · thuyết minh `/accounting/financial-statement-notes` · **Sổ HKD TT152** (S1a/S2b/S3a) `/accounting/hkd-books` (+ `/{templateCode}`)

**Mapping:**
Quy tắc map `/accounting/mapping-rules` · sơ đồ tài khoản `/accounting/account-map`

### UC-8.3 CRM & Loyalty
Khách hàng `/admin/customers` · chiến dịch KM `/admin/promo-campaigns` · nhiệm vụ `/admin/missions` · catalog đổi điểm `/admin/redemption-catalog` · lịch sử `/admin/redemption-history` · đối soát liên minh `/admin/loyalty-reconcile` · CTV `/community/owner-panel` · hộp thư `/community/messages` · thống kê `/loyalty/dashboard`

### UC-8.4 Quản trị & tài chính
Users `/admin/users` · nhóm quyền `/admin/permission-groups` · thông báo `/notifications` · tài chính `/financial` (+ `/financial/break-even`, `/financial/unit-economics`) · hồ sơ DN `/admin/business-profile` · **ví phí nền tảng** `/wallet/phi-nen-tang` (nạp VietQR) · MFA `/account/mfa`

### UC-8.5 HTX *(chỉ tenant HTX)*
Xét duyệt hồ sơ `/admin/membership` · điều lệ `/admin/membership/htx-profile` · member shops `/admin/membership/members`

### UC-8.6 Cấu hình
Tính năng shop `/settings/shop-features`

## A9 — ChiefAccountant / DebtAccountant

| Role | Quyền |
|---|---|
| **ChiefAccountant** | Toàn bộ UC-8.2 (kế toán + chứng từ) + kiểm kê UC-5.2 + `/financial` + `/notifications` |
| **DebtAccountant** | Subset: lịch sử GD, số dư đầu kỳ, số dư TK, công nợ |

## A10 — SystemAdmin (platform)

### UC-10.1 Tenant & nghiệp vụ
| Use case | Route |
|---|---|
| Quản lý tenants | `/admin/tenants`, `/admin/tenant-registrations` |
| ShopInstances (multi-VPS) | `/admin/shop-instances` (+ Gateway `ShopInstances` API) |
| KhachLink instances + audit | `/admin/khachlink-instances`, `/admin/khachlink-instances/{id}/audit`, `/admin/domains` |
| **Tra soát số liệu kế toán** | `/admin/reconcile` — 13 check R1-R12 read-only + mirror cross-VPS |
| Order-flow profiles | `/admin/order-flow` |
| Commerce mode | `/admin/commerce-mode` |
| Referral configs | `/admin/product-referral-configs` |
| Giá vốn SP | `/admin/product-cost-prices` |
| Featured products | `/admin/featured-products` |
| Reseller accounting | `/admin/reseller-accounting-reconciliation` |
| VALCN v2 features | `/admin/valcn-features` |
| Feature visibility matrix | `/admin/feature-visibility` |
| Voice admin | `/admin/voice` |

### UC-10.2 CRM / Loyalty platform
Catalog global `/admin/global-redemption-catalog` · alliance config `/admin/loyalty-config` · push campaigns `/admin/push-campaigns` · social campaigns `/admin/campaigns` · KM `/admin/promo-campaigns` · missions `/admin/missions` · redemption catalog/history · CRM `/admin/customers` + `/admin/customers-global` · loyalty stats `/loyalty/dashboard`

### UC-10.3 Community Commerce
CTV admin `/admin/community/admin-panel` · fraud flags `/admin/community/fraud-flags` · fraud stats `/admin/community/fraud-stats` · device regs `/admin/device-registrations` · SMS verify `/admin/collaborator-verification` · quỹ cộng đồng `/admin/community-fund` · settlements `/admin/settlements` · **duyệt rút tiền** `/admin/withdrawals`

### UC-10.4 Tenant AI (LLM per-tenant)
Hub `/admin/tenant-ai` · conversations `/admin/tenant-ai/conversations` · knowledge `/admin/tenant-ai/knowledge` (drafts từ owner chat) · policy `/admin/tenant-ai/policy` · profile `/admin/tenant-ai/profile` · usage `/admin/tenant-ai/usage`

### UC-10.5 Hạ tầng & vận hành
Rate cards `/admin/metering/rate-cards` · top-up `/admin/metering/topups` · network dashboard `/admin/network-dashboard` · R2 storage `/admin/r2-storage` · OCR settings `/admin/ocr-settings` + onboarding wizard `/admin/onboarding-ocr` · KhachLink home `/admin/khachlink-home-settings` · background services `/admin/background-services` · audit trail `/admin/audit-trail` · impersonate tenant · membership `/admin/membership` · claims queue `/admin/claims` · crawl trigger `/admin/crawl-trigger`

### UC-10.6 Hướng dẫn
8 guide HTML trong `wwwroot/guides/` (`KhachLink Multi-Profile`, `CRM & Loyalty`, `VALCN v2.0`, `Community Commerce`, `Accounting`, `Booking`, `Membership`, `Tenant Onboarding`, `Crawl Onboarding`) + `/sitemap` + **Tra soát guide** (`docs/user-guide/Tra_Soat_So_Lieu_Ke_Toan_Guide.html`)

## A11 — Hệ thống tự động

| Use case | Cơ chế |
|---|---|
| Giao đơn PG→SQLite | NATS `vanan.cloud.order.created.{shopInstance}` — routed per ShopInstance |
| Sync trạng thái ngược | SQLite→PG status updates |
| Proxy nội bộ Gateway↔ShopERP | `X-Internal-Api-Key` (loyalty, metering, mirror-orders R11) |
| Realtime chat/voice | SignalR hubs — per-customer shop chat threads, order conversation, WebRTC voice calls (auto-reconnect, fullscreen call UI) |
| Tenant AI | LLM service per-tenant — FAQ/policy routing, knowledge drafts từ owner chat, contextual "Hỏi AI"/"Gọi" entries (W3) |
| Jobs nền | R2 cleanup 30d, cooling period, held timeout, loyalty budget daily/monthly reset, voucher expiry reminders |
| Metering deny-gate | Chặn tính năng khi ví phí nền tảng hết credit |
| Audit trail | Mọi mutation + admin action ghi log (+ per-instance KhachLink audit) |
| Notification hub | NATS `vanan.cloud.notification.created` → Blazor bell + push (realtime) |
| E-invoice sync | Provider webhook → hóa đơn |
| Dynamic CORS | `DynamicCorsCacheHostedService` 5-min refresh từ `KhachLinkInstance.CustomDomain` |
| KhachLink instance config | `by-domain` API → localStorage cache `khachlink_instance_config_v3` (TTL 1 phút) |
| Cooperatives public API | `GET /api/cooperatives` + `/by-domain/{domain}` + `/{id}/members` (AllowAnonymous, 60rpm, nginx cache 5m) |

---

## Phụ lục — Mapping module → file

| Module | Vị trí |
|---|---|
| Menu SSOT theo role | `5_WebApps/ShopERP/Services/ShopErpMenuService.cs` |
| Admin pages | `5_WebApps/ShopERP/Components/Pages/Admin/` (~57 trang, gồm `TenantAi/` 6 trang) |
| Khách/CTV/Shipper/Merchant | `5_WebApps/KhachLink/Pages/` (~40 trang) + `Components/` (HoptacxaPortal, ChatPanel, QrPaymentModal, SocialHub, VibeShowcase, RealTimeDashboard...) |
| API Gateway | `2_Gateway/Controllers/` (~86 controllers) |
| Kế toán core | `3_CoreHub/Services/` (Accounting, Journal, Ledger, HKDBook, CongNo, Reconcile, Voucher strategies) |
| Metering | `8_Metering/` + `docker-compose.metering.yml` |
| Tenant AI | `Admin/TenantAi/` pages + AI services (`de327354`+ sessions) |
| Infra | `docker-compose.{gateway,shoperp,khachlink,infra,metering}.yml` |

**Quy mô:** ~57 trang SystemAdmin · ~25 module Owner · ~11 trang booking · ~40 trang KhachLink · ~86 API controllers · 13 check tra soát R1-R12 · 8+ guide HTML.
