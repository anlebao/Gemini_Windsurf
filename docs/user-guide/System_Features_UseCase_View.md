# Vạn An Ecosystem — Danh mục tính năng (Use Case View)

> **Mục đích:** bản đồ toàn bộ tính năng hệ thống theo góc nhìn **actor → use case** (UML use case view).
> **Nguồn:** `ShopErpMenuService.cs` (SSOT menu theo role) · `5_WebApps/KhachLink/Pages/` · `2_Gateway/Controllers/` · `5_WebApps/ShopERP/Components/Pages/`.
> **Ngày:** 2026-10-30 · `main` @ `96fb249c` (+ feature Tra soát `76ac6f3c`).

---

## 0. Kiến trúc kênh & actors

```
Khách hàng / CTV / Shipper ──► KhachLink PWA (:5002) ──┐
                                                     │  HTTP + JWT
Staff / Owner / Kế toán ──► ShopERP Blazor (:5003) ──┤        │
   + SystemAdmin                                     ▼        ▼
                              SQLite per-tenant ◄─── Gateway (:5001) ───► PostgreSQL
                              (mirror orders/          (order creator,
                               products, POS)           source of truth,
                                                     JWT, tenant, audit)
                                                     │
                                                     ▼ NATS JetStream (:4222)
                                              order.created.{shopInstance}
                                                     │
                                              Metering "Phí nền tảng" (wallet/rate-card)
```

| # | Actor | Kênh truy cập |
|---|---|---|
| A1 | **Khách hàng** | KhachLink PWA :5002 |
| A2 | **CTV / Salesman** | KhachLink |
| A3 | **Shipper** | KhachLink |
| A4 | **Staff / StoreKeeper** | ShopERP :5003 |
| A5 | **Masterchef** | ShopERP `/kitchen` |
| A6 | **Guard** | ShopERP `/guard/scan` |
| A7 | **Owner** (chủ shop) | ShopERP |
| A8 | **ChiefAccountant / DebtAccountant** | ShopERP |
| A9 | **SystemAdmin** | ShopERP `/admin/*` + Gateway API :5001 |
| A10 | **Hệ thống** (jobs/NATS/sync) | nội bộ |

---

## A1 — Khách hàng (KhachLink PWA)

### UC-1.1 Mua sắm & đơn hàng
| Use case | Mô tả | Trang/Route |
|---|---|---|
| Duyệt catalog | Trang chủ, sản phẩm nổi bật, cửa hàng | `Home`, `Store`, `StoreFinder`, `NearbyProducts` |
| Tìm kiếm | Search sản phẩm | `ProductSearchResults` |
| Giỏ hàng | Thêm/sửa/xóa món | `Cart` |
| Checkout | Đặt hàng online — snapshot ProductName+VatRate gửi Gateway tạo order | `Checkout` |
| Theo dõi đơn | Realtime trạng thái + lịch sử | `OrderTracking`, `OrderHistory` |
| Đặt lịch hẹn | Chọn dịch vụ → giờ → xác nhận → cọc | `BookingOffering/Time/Confirm/Status/Note` |
| Ghi chú đơn | STT only (không audio) | `VoiceNote` |
| Quét QR | Scan QR shop/CTV | `Scan` |

### UC-1.2 Loyalty & ví
| Use case | Trang |
|---|---|
| Thẻ thành viên + điểm | `LoyaltyCard` |
| Ví cá nhân / ví liên minh | `Wallet`, `AllianceWallet` |
| Đổi quà catalog | `RedemptionCatalog` |
| Nhiệm vụ kiếm điểm | `Missions` |
| Xem/tham gia campaign | `Campaigns`, `Campaign`, `Claim` |

### UC-1.3 Tài khoản & hệ thống
| Use case | Trang |
|---|---|
| Đăng ký / đăng nhập OTP | `Register`, `Login` |
| Hồ sơ multi-profile | `Profile` |
| Đăng ký thành viên HTX | `MembershipRegister` |
| Thông báo | `Notifications` |
| Xác minh SMS | `CollaboratorVerification` |

## A2 — CTV / Salesman (KhachLink)

| Use case | Trang |
|---|---|
| Dashboard doanh số + hoa hồng | `SalesDashboard` |
| QR giới thiệu khách | `SalesmanQR` |
| Cửa hàng được gán | `SalesmanStore` |
| Đơn quanh khu vực | `NearbyOrders` |
| Yêu cầu rút hoa hồng | (→ qua SystemAdmin `/admin/withdrawals` duyệt) |

## A3 — Shipper (KhachLink)

| Use case | Trang |
|---|---|
| Chuyến giao đang chạy | `ActiveDeliveries` |
| Theo dõi tuyến giao | `DeliveryTracking` |

## A4 — Staff / StoreKeeper (ShopERP)

### UC-4.1 Bán hàng tại quầy
| Use case | Route |
|---|---|
| POS bán hàng | `/pos` |
| Danh sách + chi tiết đơn | `/orders`, `/orders/{id}` |
| In hóa đơn | `PrintBill` |
| Đặt lịch nhanh | `/booking/create` |
| Màn hình bếp | `/kitchen` |

### UC-4.2 Kiểm kê kho *(+ChiefAccountant xem)*
| Use case | Route |
|---|---|
| Báo cáo ca | `/inventory/shifts` |
| Công thức pha chế | `/inventory/recipes` |
| Nguyên liệu | `/inventory/ingredients` |
| Tồn kho dashboard | `/inventory/dashboard` |
| Cảnh báo hết hàng | `/inventory/alerts` |
| Dự báo nhập | `/inventory/forecast` |

### UC-4.3 Đặt lịch hẹn *(Owner+StoreKeeper)*
Hàng đợi `/booking/queue` · lịch trống `/booking/availability` · calendar `/booking/calendar` · nhân viên `/booking/staff` · lịch làm việc `/booking/schedules` · đặt cọc `/booking/deposits` · QR channels `/booking/qr-channels` · hoa hồng CTV `/booking/commission` · cấu hình `/booking/config` · dịch vụ `/booking/services`

### UC-4.4 Hóa đơn điện tử *(+SystemAdmin)*
Dashboard `/einvoice` · quản lý hóa đơn `/einvoice/invoices` · cảnh báo `/einvoice/alerts` · health `/einvoice/health`

## A5 — Masterchef
| Use case | Route |
|---|---|
| Bếp realtime (Home mặc định) | `/kitchen` |

## A6 — Guard
| Use case | Route |
|---|---|
| Quét QR ra/vào | `/guard/scan` |

## A7 — Owner (toàn quyền tenant)

### UC-7.1 Bán hàng & vận hành
Toàn bộ UC-4.1–4.4 + quản lý sản phẩm `/products`

### UC-7.2 Kế toán (dùng chung với ChiefAccountant)
| Nhóm | Use case |
|---|---|
| Nhập liệu | Phiếu thu/chi, lịch sử giao dịch `/accounting/history`, số dư đầu kỳ `/accounting/opening-balance`, import Excel `/accounting/import` |
| Kiểm soát | Rà soát tiền mặt ≥20tr `/accounting/cash-payment-review`, số dư TK `/accounting/balance`, đóng kỳ `/accounting/period-closing` |
| Sổ sách | Nhật ký chung `/accounting/journal`, sổ cái `/accounting/ledger`, sổ chi tiết `/accounting/sub-ledger`, công nợ `/accounting/cong-no` |
| Báo cáo | Báo cáo tài chính `/accounting/financial-reports`, **Sổ HKD TT152** (S1a/S2b/S3a) `/accounting/hkd-books` |

### UC-7.3 CRM & Loyalty
Khách hàng `/admin/customers` · chiến dịch KM `/admin/promo-campaigns` · nhiệm vụ `/admin/missions` · catalog đổi điểm `/admin/redemption-catalog` · lịch sử `/admin/redemption-history` · đối soát liên minh `/admin/loyalty-reconcile` · CTV `/community/owner-panel` · hộp thư `/community/messages` · thống kê `/loyalty/dashboard`

### UC-7.4 Quản trị & tài chính
Users `/admin/users` · nhóm quyền `/admin/permission-groups` · thông báo `/notifications` · tài chính `/financial` (+ BreakEven, UnitEconomics) · hồ sơ DN `/admin/business-profile` · **ví phí nền tảng** `/wallet/phi-nen-tang` (nạp VietQR)

### UC-7.5 HTX *(chỉ tenant HTX)*
Xét duyệt hồ sơ `/admin/membership` · điều lệ `/admin/membership/htx-profile`

### UC-7.6 Cấu hình
Tính năng shop `/settings/shop-features`

## A8 — ChiefAccountant / DebtAccountant

| Role | Quyền |
|---|---|
| **ChiefAccountant** | Toàn bộ UC-7.2 (kế toán) + kiểm kê UC-4.2 + `/financial` + `/notifications` |
| **DebtAccountant** | Subset: lịch sử GD, số dư đầu kỳ, số dư TK, công nợ |

## A9 — SystemAdmin (platform)

### UC-9.1 Tenant & nghiệp vụ
| Use case | Route |
|---|---|
| Quản lý tenants | `/admin/tenants`, `/admin/tenant-registrations` |
| ShopInstances (multi-VPS) | (Gateway `ShopInstances` API) |
| KhachLink instances + domain reseller | `/admin/khachlink-instances`, `/admin/domains` |
| **Tra soát số liệu kế toán** | `/admin/reconcile` — 13 check R1-R12 read-only + mirror cross-VPS |
| Order-flow profiles | `/admin/order-flow` |
| Commerce mode | `/admin/commerce-mode` |
| Referral configs | `/admin/product-referral-configs` |
| Giá vốn SP | `/admin/product-cost-prices` |
| Featured products | `/admin/featured-products` |
| Reseller accounting | `/admin/reseller-accounting-reconciliation` |
| VALCN v2 features | `/admin/valcn-features` |

### UC-9.2 CRM / Loyalty platform
Catalog global `/admin/global-redemption-catalog` · alliance config `/admin/loyalty-config` · push campaigns `/admin/push-campaigns` · social campaigns `/admin/campaigns` · KM `/admin/promo-campaigns` · missions `/admin/missions` · redemption catalog/history · CRM `/admin/customers` · loyalty stats `/loyalty/dashboard`

### UC-9.3 Community Commerce
CTV admin `/admin/community/admin-panel` · fraud flags `/admin/community/fraud-flags` · fraud stats `/admin/community/fraud-stats` · device regs `/admin/device-registrations` · SMS verify `/admin/collaborator-verification` · quỹ cộng đồng `/admin/community-fund` · settlements `/admin/settlements` · **duyệt rút tiền** `/admin/withdrawals`

### UC-9.4 Hạ tầng & vận hành
Rate cards `/admin/metering/rate-cards` · top-up `/admin/metering/topups` · network dashboard `/admin/network-dashboard` · R2 storage `/admin/r2-storage` · OCR settings `/admin/ocr-settings` · KhachLink home `/admin/khachlink-home-settings` · background services · audit trail `/admin/audit-trail` · impersonate tenant · membership `/admin/membership` · claims queue · crawl trigger · metering topups · tenant community · customer global list · booking deposits · reseller settlement

### UC-9.5 Hướng dẫn
6 guide HTML (`KhachLink Multi-Profile`, `CRM & Loyalty`, `VALCN v2.0`, `Community Commerce`, `Accounting`, `Booking`) + `/sitemap` + **Tra soát guide** (`docs/user-guide/Tra_Soat_So_Lieu_Ke_Toan_Guide.html`)

## A10 — Hệ thống tự động

| Use case | Cơ chế |
|---|---|
| Giao đơn PG→SQLite | NATS `vanan.cloud.order.created.{shopInstance}` — routed per ShopInstance |
| Sync trạng thái ngược | SQLite→PG status updates |
| Proxy nội bộ Gateway↔ShopERP | `X-Internal-Api-Key` (loyalty, metering, **mirror-orders R11**) |
| Jobs nền | R2 cleanup 30d, cooling period, held timeout, loyalty budget daily/monthly reset |
| Metering deny-gate | Chặn tính năng khi ví phí nền tảng hết credit |
| Audit trail | Mọi mutation + admin action ghi log |
| Notification hub | NATS `vanan.cloud.notification.created` → Blazor bell |
| E-invoice sync | Provider webhook → hóa đơn |

---

## Phụ lục — Mapping module → file

| Module | Vị trí |
|---|---|
| Menu SSOT theo role | `5_WebApps/ShopERP/Services/ShopErpMenuService.cs` |
| Admin pages | `5_WebApps/ShopERP/Components/Pages/Admin/` (~47 trang) |
| Khách/CTV/Shipper | `5_WebApps/KhachLink/Pages/` (~30 trang) |
| API Gateway | `2_Gateway/Controllers/` (~90 controllers) |
| Kế toán core | `3_CoreHub/Services/` (Accounting, Journal, Ledger, HKDBook, CongNo, **Reconcile**) |
| Metering | `8_Metering/` + `docker-compose.metering.yml` |
| Infra | `docker-compose.{gateway,shoperp,khachlink,infra}.yml` |

**Quy mô:** ~47 trang SystemAdmin · ~25 module Owner · ~11 trang booking · ~30 trang KhachLink · ~90 API controllers · 13 check tra soát R1-R12.
