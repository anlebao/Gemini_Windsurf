# HƯỚNG DẪN SỬ DỤNG COMMUNITY COMMERCE — VẠN AN ECOSYSTEM

> **Phiên bản:** PoC v1.5 + Sprint 7 Commerce Mode Toggle + KhachLink Multi-Profile + Customer Onboarding + GTM Drill Machine — cập nhật 2026-09-09
> **Áp dụng:** Community Commerce Sprint 0-7 (đã deploy + VPS verified) + KhachLink Profile Transition UX Sprint 1-3 (PR #170) + Customer Onboarding via Google Login (PR #171) + Admin/Owner Override (PR #172) + GTM Drill Machine W1-W2.
> **Phạm vi:** Module Shipper/Salesman (Community Commerce) + Commerce Mode Toggle (Marketplace ↔ Reseller) + KhachLink Multi-Profile (FullCommerce ↔ Directory) + GTM Drill Machine — "Mua giúp — Bán dùm".
> **Nền tảng:** KhachLink PWA (Blazor WebAssembly, `diemthuong2.khachvip.online` — FullCommerce, `commienphi.timlathay.com` — Reseller) + KhachLink Directory (Blazor SSR, `timlathay.com` — Directory profile) + ShopERP Admin (Blazor Server, `khachvip.online`). Multi-VPS: `vanan-gateway` + `vanan-shop-a` + `vanan-khachlink`.

---

## MỤC LỤC

| # | Vai trò | File | Nền tảng truy cập |
|---|---|---|---|
| 1 | [System Admin — Quản trị hệ thống](./01-systemadmin.md) | `01-systemadmin.md` | ShopERP Admin (`/admin/*`) + Gateway Admin API |
| 2 | [Shop Owner — Chủ cửa hàng](./02-owner.md) | `02-owner.md` | ShopERP Admin (`/admin/*`) — tenant của mình |
| 3 | [Salesman — Cộng tác viên bán hàng](./03-salesman.md) | `03-salesman.md` | KhachLink PWA — tab "Sản phẩm gần", "Mã QR", "Doanh số" |
| 4 | [Shipper — Cộng tác viên giao hàng](./04-shipper.md) | `04-shipper.md` | KhachLink PWA — tab "Đơn hàng gần", "Đang giao", "Ví" |
| 5 | [Staff — Nhân viên quầy/POS](./05-staff.md) | `05-staff.md` | ShopERP Admin — POS + Order management |
| 6 | [Bếp / Kitchen](./06-kitchen.md) | `06-kitchen.md` | ShopERP Admin — Kitchen Display + KitchenHub |
| 7 | [Customer — Khách hàng mua hàng](./07-customer.md) | `07-customer.md` | KhachLink PWA — đăng nhập, đặt hàng, tracking, chat, ví, tích điểm |

---

## 1. TỔNG QUAN KIẾN TRÚC

```
┌─────────────────────────────────────────────────────────────────────┐
│  KhachLink PWA (5002) — Blazor WebAssembly                          │
│  Customer + Salesman + Shipper (cùng 1 app, role quyết định tab)    │
│  Auth: X-Customer-Token (token-based, KHÔNG cookie)                 │
│  GPS: PWA polling 10s/30s (adaptive) khi tab active                 │
└───────────────┬─────────────────────────────────────────────────────┘
                │  HTTP + X-Customer-Token header
                ▼
┌─────────────────────────────────────────────────────────────────────┐
│  Gateway (5001) — YARP + Order Creator (Option C)                   │
│  PG source of truth: Orders + Accounting + Tenants + ShopInstances  │
│                     + Users + FeaturedProducts + Community entities │
│  Community entities (PG ONLY): CommunityRole, DeliveryTask,         │
│    DeliveryTracking, Conversation, Message, SalesReferral,          │
│    WalletTransaction, ProductReferralConfig, AppInstallAttribution, │
│    DeviceRegistration, FraudFlag, SystemSetting, ProductCostPrice,  │
│    CommunityFundSpendRecord                                         │
└───────────────┬─────────────────────────────────────────────────────┘
                │  NATS (routed by ShopInstanceId)
                ▼
┌─────────────────────────────────────────────────────────────────────┐
│  ShopERP (5003) — Blazor Server, per-tenant SQLite                  │
│  Owner + Staff + Kitchen (cookie auth, role claims)                 │
│  Business data: Products, Kitchen, POS, Accounting (per-tenant)     │
│  Admin pages: /admin/* (SystemAdmin thấy tất cả, Owner thấy tenant) │
└─────────────────────────────────────────────────────────────────────┘
```

**Auth model:**
- **Customer/Salesman/Shipper:** `X-Customer-Token` (custom token, không cookie) — 1 user có thể đồng thời là Buyer + Salesman + Shipper.
- **Owner/Staff/Kitchen:** Cookie auth + role claims (Owner/StoreKeeper/Guard/Staff/Masterchef) — tenant-scoped.
- **System Admin:** Cookie auth + role = SystemAdmin — cross-tenant.

---

## 2. HAI MÔ HÌNH THƯƠNG MẠI (Sprint 7 — Commerce Mode Toggle)

| Khía cạnh | Marketplace (mặc định — Sprint 0-6) | Reseller (mới — Sprint 7) |
|---|---|---|
| **Vai trò Vạn An** | Nền tảng giới thiệu + giao hàng | Bên mua từ tenant → bán lại cho customer |
| **Ai định giá** | Tenant tự định giá | Vạn An định giá bán (cost price từ tenant + margin) |
| **Dòng tiền COD** | Shipper thu hộ → shop nhận trực tiếp | Customer trả → Vạn An nhận → Vạn An phân phối |
| **Commission base** | `% orderTotal` | `% margin` (SellPrice - CostPrice) |
| **Advance payment** | Shipper ứng tiền cho shop | Vạn An ứng tiền cho shop (mua trước) |
| **Settlement** | Shipper ↔ Shop trực tiếp | Tất cả qua Vạn An wallet |
| **Platform fee** | Không có | Vạn An giữ margin → phân chia 4 khoản |
| **Community fund** | Không có | % margin vào quỹ phát triển cộng đồng |

**Toggle mechanism:** Global setting (SystemAdmin) + override cấp tenant. Mỗi Order snapshot `CommerceMode` tại creation time — toggle chỉ ảnh hưởng đơn hàng mới.

**Quy tắc ưu tiên mode:**
1. `TenantSettings.CommerceModeOverride` ≠ `Inherit` → dùng override
2. `TenantSettings.CommerceModeOverride` == `Inherit` → dùng `GlobalCommerceMode`
3. Past orders giữ mode snapshot — KHÔNG thay đổi khi toggle

---

## 3. ĐIỀU KIỆN KÍCH HOẠT VAI TRÒ CỘNG TÁC VIÊN

Một Customer có thể trở thành Salesman hoặc Shipper khi đạt điều kiện. **System Admin** là người kích hoạt.

### 3.1. Khi `CollaboratorSmsVerificationEnabled = OFF` (mặc định — early stage)
- **Shipper/Salesman:** `IdentityLevel ≥ DeviceVerified` (device fingerprint pass) **HOẶC** `IdentityLevel ≥ Verified` (SMS OTP) + `LoyaltyPoints ≥ 1000`
- Mục tiêu: friction thấp, tối đa hóa số cộng tác viên

### 3.2. Khi `CollaboratorSmsVerificationEnabled = ON` (scale đủ)
- **Shipper/Salesman:** `IdentityLevel ≥ Verified` (SMS OTP) **BẮT BUỘC** + `LoyaltyPoints ≥ 1000` + `CommunityRole.IsPhoneVerified = true` + Deposit wallet ≥ phí SMS OTP
- **Owner:** SMS OTP verify SĐT khi onboarding tenant mới
- Phí SMS OTP trừ deposit wallet mỗi lần verify

> **Lưu ý:** Customer KHÔNG bị ảnh hưởng bởi toggle — customer luôn optional SMS OTP (device fingerprint là primary).

### 3.3. IdentityLevel (thứ bậc định danh)
| Level | Giá trị | Ý nghĩa |
|---|---|---|
| Guest | 0 | Khách vãng lai (không token, checkout as guest) |
| Social | 1 | Đăng nhập Google/Facebook |
| Verified | 2 | SMS OTP verified |
| Full | 3 | Full KYC |
| DeviceVerified | 4 | Device fingerprint + behavioral check passed (KHÔNG cần SMS) |

---

## 4. ANTI-FRAUD — 5 LỚP PHÒNG THỦ (Self-hosted, zero external dependency)

| Lớp | Technique | Phạm vi |
|---|---|---|
| 1 | **Device Fingerprint** — FingerprintJS (MIT, self-host) — 15+ signals → SHA256 hash | 80% fraud |
| 2 | **Device Token** — server-signed UUIDv7+HMAC, max 3 devices/customer | +10% |
| 3 | **Behavioral rules** — same fingerprint, same IP 24h, >3 accounts/device, app-install <30s | +5% |
| 4 | **Risk Scoring** — deterministic 0-100. Score≥60 → hold 48h. Score≥80 → auto-reject | Manual review |
| 5 | **Native App Attestation** (post-PoC, OPTIONAL) — iOS App Attest + Android Play Integrity | +5% VM farm |

**Target fraud rate:** <0.5%. **Max 3 active devices per Customer** — device thứ 4 yêu cầu admin approval.

**RiskScore factors:** salesmanFingerprint==customerFingerprint (+50), same IP 24h (+30), customerAgeDays<7 (+30), deviceFirstSeen<24h (+25), ordersFromDeviceToday>3 (+20), referralBonusAmount>50K (+15), appInstallTime<30s (+40), blacklistedFingerprint (+60).

**Payout policy:** Hold commission/bonus 48h if RiskScore≥60. Auto-reject if ≥80. KYC bank account required. Min payout 500K VND. 3-strike ban.

---

## 5. SALESMAN EARNING MODEL (2 nguồn thu)

Cả 2 nguồn do **SystemAdmin thiết lập per-product** qua `ProductReferralConfig` (KHÔNG hardcode):

1. **Commission chốt đơn:** 2-5% giá trị đơn hàng (Marketplace: % orderTotal; Reseller: % margin) khi customer đặt hàng qua referral code.
2. **App-install bonus:** Thưởng cố định khi salesman thuyết phục customer cài KhachLink PWA. Attribution: customer có referralCode trong localStorage khi trigger PWA install event.

**Composite referral code:** Format `{salesmanCode}|{productShortCode}` (vd `ABC123|TR-001`). Customer scan QR 1 lần → lưu cả 2 vào localStorage → gửi khi order creation → server resolve salesmanId + productId.

---

## 6. WALLET & DÒNG TIỀN

### 6.1. WalletTransaction types (immutable ledger + Reversal pattern)
| Type | Mode | Ý nghĩa |
|---|---|---|
| CODCollection (1) | Cả 2 | Shipper thu hộ tiền COD |
| AdvancePayment (2) | Marketplace | Shipper ứng tiền cho shop |
| Commission (3) | Cả 2 | Hoa hồng salesman |
| Withdrawal (4) | Cả 2 | Salesman/Shipper rút tiền |
| Settlement (5) | Cả 2 | Thanh toán giữa các bên |
| Reversal (6) | Cả 2 | Hoàn giao dịch sai (append-only) |
| PlatformFee (7) | Reseller | Vạn An giữ margin |
| CommunityFund (8) | Reseller | Quỹ phát triển cộng đồng |
| DeliveryFee (9) | Reseller | Phí giao hàng trả shipper |
| ExternalPayment (10) | Reseller | Customer trả Vạn An qua VietQR/card (non-COD) |
| CommunityFundSpend (11) | Reseller | SysAdmin rút quỹ tái đầu tư |

### 6.2. Reseller COD Flow (6 transactions)
Khi shipper confirm COD trong mode Reseller, Vạn An tạo 6 transactions:
1. CODCollection (+COD amount vào shipper wallet)
2. Settlement (-COD amount, chuyển cho shop)
3. DeliveryFee (+delivery fee vào shipper wallet)
4. Commission (+commission vào salesman wallet, nếu có)
5. PlatformFee (+platform fee vào platform wallet)
6. CommunityFund (+community fund vào community fund wallet)

**Financial balance invariant:** Tổng tất cả tx amounts = COD amount collected.

---

## 7. CÂU HỎI THƯỜNG GẶP (FAQ RÚT GỌN)

**Q: Tôi có thể vừa là Customer vừa là Salesman/Shipper không?**
A: CÓ. Một user có thể đồng thời là Buyer + Salesman + Shipper. Role cộng tác viên do System Admin kích hoạt khi đủ điều kiện (xem Section 3).

**Q: GPS tracking có hoạt động khi tắt app không?**
A: KHÔNG. PWA chỉ track GPS khi tab active. Thông báo "Giữ app mở để cập nhật vị trí". Post-PoC sẽ đánh giá native app cho background GPS.

**Q: Đổi mode Marketplace ↔ Reseller có ảnh hưởng đơn cũ không?**
A: KHÔNG. Mỗi Order snapshot mode tại creation time. Toggle chỉ ảnh hưởng đơn hàng mới.

**Q: Customer có bị thu phí SMS OTP không?**
A: KHÔNG. Customer luôn optional SMS OTP (device fingerprint là primary). Chỉ Collaborator (Salesman/Shipper/Owner) mới bị thu phí khi toggle ON.

**Q: Salesman kiếm tiền từ đâu?**
A: 2 nguồn — commission chốt đơn (2-5%) + app-install bonus. Cả 2 do SystemAdmin set per-product.

---

## 8. KHACHLINK MULTI-PROFILE (FullCommerce ↔ Directory) — PR #170

KhachLink chạy 2 profile khác nhau tùy tenant:

| Khía cạnh | FullCommerce (WASM) | Directory (SSR) |
|---|---|---|
| **Nền tảng** | Blazor WebAssembly PWA | Blazor Server SSR |
| **Domain** | `diemthuong2.khachvip.online` (FullCommerce), `commienphi.timlathay.com` (Reseller) | `timlathay.com` |
| **Cart / Checkout** | CÓ | KHÔNG |
| **Order tracking / Chat shipper** | CÓ | KHÔNG |
| **Wallet / Loyalty** | CÓ | KHÔNG |
| **Mục đích** | Cửa hàng F&B full commerce | Directory tìm cửa hàng + lead generation |

**Profile Transition UX (Sprint 1-3, PR #170):**
- **ProfileGuard.razor** — route guard 7 trang commerce (cart, checkout, tracking, wallet, v.v.) → redirect về Home nếu tenant là Directory.
- **Profile indicator footer** — hiển thị profile hiện tại (FullCommerce/Directory) trên KhachLinkLayout.
- **What's New banner** — thông báo 1 chiều khi admin đổi profile (FullCommerce→Directory: "tính năng commerce sẽ bị ẩn"; Directory→FullCommerce: "tính năng commerce đã mở").
- **Cart preservation modal** — khi FullCommerce→Directory + cart có items → modal cảnh báo trước khi chuyển.
- **Onboarding tour** (driver.js, vendored) — 3-step tour (cart→rewards→stores) khi Directory→FullCommerce, 1× per profile.
- **Audit trail** — `KhachLinkInstanceAudit.razor` (`/admin/khachlink-instances/{id}/audit`) + AuditTrail.razor summary cards. Hybrid persist: Accounting SYNC, Security+KhachLink ASYNC (AuditLogQueue + AuditLogBackgroundWriter).

**Owner/Admin đổi profile:** qua `/admin/khachlink-instances` (SystemAdmin) — set `ProfileType` enum. Customer thấy thay đổi sau SW update (`vananTriggerSWUpdate()`).

---

## 9. CUSTOMER ONBOARDING VIA GOOGLE LOGIN — PR #171

Customer đăng nhập Google lần đầu → tự động được gán đúng tenant theo domain:

1. KhachLink `Login.razor` truyền `klOrigin` (origin hiện tại) vào OAuth state.
2. `SocialAuthController` encode `redirectTo|klOrigin` trong OAuth state param.
3. Google callback → server resolve `KhachLinkInstance.OwnerTenantId` theo domain origin.
4. Customer mới tạo → gán vào tenant tương ứng (fallback default tenant cho platform-level domains không có owner).
5. Customer cũ (email đã tồn tại) → reuse global identity (KHÔNG tạo duplicate customer trên tenant khác).

**RV (2026-09-09):** L1-L2 PASS. L3-L5 manual pending (cần Google login thật trên `commienphi.timlathay.com`).

---

## 10. ADMIN/OWNER OVERRIDE — PR #172

Kích hoạt role cộng tác viên có 2 path + bypass eligibility:

| Path | URL | Role | Scope |
|---|---|---|---|
| Admin Panel | `/admin/community/admin-panel` | SystemAdmin | Cross-tenant |
| Owner Panel | `/community/owner-panel` | Owner | Tenant-scoped |

**Tính năng (cả 2 panel):**
- Toggle **"Hiển thị tất cả khách hàng"** (`includeIneligible=true`) — xem cả customer chưa đủ điều kiện.
- **Bypass eligibility** — body `{"role":"Shipper","bypassEligibility":true}` → kích hoạt bỏ qua check IdentityLevel/LoyaltyPoints.
- **Confirm dialog** trước khi activate.
- API: `GET /api/admin/community/eligible?includeIneligible=true` + `POST /api/admin/community/{id}/activate-role` (SystemAdmin) hoặc `GET /api/v1/tenant-community/eligible` + `POST /api/v1/tenant-community/{id}/activate-role` (Owner tenant-scoped).

---

## 11. GTM DRILL MACHINE (W1-W2) — Lead Generation

**W1 — Merchant Audit:** Directory `/kiem-tra-cua-hang` (Blazor SSR) + Gateway `GET /api/v1/growth/audit?name=&mst=` (rate-limit 10/IP/h) — tenant check trạng thái active/pending privacy.

**W2 — Interactive Demo + Registration:**
- KhachLink `/demo` — standalone storefront mock (session-only, không persist).
- KhachLink `/claim` (`Register.razor`) — form đăng ký tenant mới + Turnstile + honeypot.
- `POST /api/v1/tenant-registrations` (AllowAnonymous, rate-limit 5/IP/24h) → tạo `TenantRegistration` entity (PG migration `20260908023803`).
- E2E `gtm-demo.spec.ts` 6/6 PASS trên production.

**W3 NEXT:** Revenue Proof (counters trên tenant GrowthDashboard) → W4 Referral → W5 consent + flag `GrowthMachine:Enabled`.

---

> **Xem chi tiết từng vai trò:** Click vào file tương ứng trong bảng MỤC LỤC ở đầu trang.
> **Tài liệu kỹ thuật:** `docs/AI/tasks/community-commerce-requirements-spec-2c5017.md` (spec) + `community-commerce-master-plan-2c5017.md` (plan) + `commerce-mode-toggle-spec-v2-2c5017.md` (Sprint 7) + `docs/user-guide/KhachLink_Multi_Profile_Usage_Guide.md` (Multi-Profile) + `docs/AI/tasks/gtm_drill_mvp/` (GTM).
