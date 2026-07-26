# TASK CARD: Community Commerce — Sprint 4 — Salesman + Composite QR Referral + Per-Product Commission + App-Install Bonus + Risk Scoring + Fraud Flagging (v1.2)

## 1. GOAL & CONTEXT
- **Mục tiêu cốt lõi:** Salesman thấy sản phẩm tenant trong bán kính + chọn product + composite QR referral code (salesman + product) gắn vào order + commission tracking per-product (2-5% từ ProductReferralConfig) + app-install attribution bonus (UC-12) + **risk scoring + fraud flagging (v1.2 NEW)**.
- **Nghiệp vụ áp dụng:** UC-08 (Nearby Products + chọn product) + UC-09 (Composite QR Referral + risk scoring) + UC-10 (Commission + App-Install Bonus dashboard) + UC-12 (App Install Attribution Bonus + risk scoring) từ requirements spec v1.2.
- **Status:** NOT STARTED
- **Branch:** `feature/community-sprint4-salesman-qr`
- **v1.2 redesign (incremental trên v1.1):**
  - **Risk scoring mandatory:** Mọi SalesReferral + AppInstallAttribution compute RiskScore 0-100 qua IRiskScoringService (đã có từ Sprint 0).
  - **Hold 48h if RiskScore≥60:** CommissionStatus/AttributionStatus=Held, HoldUntil=now+48h, FraudFlag(Status=Pending) tạo.
  - **Auto-reject if RiskScore≥80:** CommissionStatus/AttributionStatus=Rejected, FraudFlag tạo.
  - **Auto-approve after 24h if RiskScore<60:** Cooling period 24h rồi chuyển Pending (ready for payout).
  - **Device fingerprint integration:** app-install/attributed request gửi kèm FingerprintHash + FingerprintSignals (compute client-side bằng FingerprintJS).
  - **Sales Dashboard:** hiển thị status Pending/Held/Rejected tách biệt 2 nguồn (commission + app-install bonus).
  - **IFraudFlagService:** create FraudFlag khi RiskScore≥60, query pending flags.

---

## 2. ACTIVE WORKFLOW ROUTING
- **Target Workflow:** `newfeaturebuild.md` (ANALYZE → IMPLEMENT)
- **Execution Mode:** IMPLEMENT
- **Current Phase:** Sprint 4 of 7 (v1.1: 7 tasks thay vì 5)
- **Dependency:** Sprint 3 COMPLETE (community infrastructure ready) + Sprint 0 migration applied (ProductReferralConfig, AppInstallAttribution tables exist)

---

## 3. RELEVANT FILES (CONTEXT BOUNDARY) — v1.1

### Files cần CREATE
- `3_CoreHub/Services/ISalesmanService.cs` — interface (v1.1: + composite referral, v1.2: + risk scoring)
- `3_CoreHub/Services/SalesmanService.cs` — nearby products + composite QR + commission per-product + app-install attribution + **risk scoring integration (v1.2)**
- `3_CoreHub/Services/IProductReferralConfigService.cs` (v1.1 NEW) — admin CRUD cho ProductReferralConfig
- `3_CoreHub/Services/ProductReferralConfigService.cs` (v1.1 NEW)
- `3_CoreHub/Services/IAppInstallAttributionService.cs` (v1.1 NEW) — handle app-install attribution + bonus + **risk scoring (v1.2)**
- `3_CoreHub/Services/AppInstallAttributionService.cs` (v1.1 NEW) — **F5a caller: invokes IWalletService.CreateTransactionAsync (Sprint 0 base) để pay app-install bonus commission**
- `3_CoreHub/Services/IFraudFlagService.cs` (v1.2 NEW) — create/query FraudFlag
- `3_CoreHub/Services/FraudFlagService.cs` (v1.2 NEW)
- `2_Gateway/Controllers/ProductReferralConfigController.cs` (v1.1 NEW) — admin CRUD endpoints
- `2_Gateway/Controllers/FraudFlagController.cs` (v1.2 NEW) — admin FraudFlag endpoints (preview, full impl in S6)
- `5_WebApps/KhachLink/Services/Http/SalesmanHttpService.cs` — HTTP client (v1.1: + composite referral, v1.2: + fingerprint header)
- `5_WebApps/KhachLink/Pages/NearbyProducts.razor` — salesman products page (v1.1: + commission/bonus display + "Tạo QR" button per product)
- `5_WebApps/KhachLink/Pages/SalesmanQR.razor` — QR code display page (v1.1: composite code, yêu cầu productId)
- `5_WebApps/KhachLink/Pages/SalesDashboard.razor` — commission + app-install bonus dashboard (v1.1: tách biệt 2 nguồn, v1.2: + Held/Rejected status)
- `5_WebApps/ShopERP/Pages/Admin/ProductReferralConfigs.razor` (v1.1 NEW) — admin UI CRUD
- `5_WebApps/KhachLink/wwwroot/js/qrcode.js` — QR generation JS interop
- `5_WebApps/KhachLink/wwwroot/js/app-install-tracker.js` (v1.1 NEW — PWA install event handler, v1.2: + fingerprint send)
- `6_Tests/VanAn.Core.Tests/SalesmanServiceTests.cs` (v1.1: + composite referral, v1.2: + risk scoring)
- `6_Tests/VanAn.Core.Tests/AppInstallAttributionServiceTests.cs` (v1.1 NEW, v1.2: + risk scoring)
- `6_Tests/VanAn.Core.Tests/ProductReferralConfigServiceTests.cs` (v1.1 NEW)
- `6_Tests/VanAn.Core.Tests/FraudFlagServiceTests.cs` (v1.2 NEW)
- `6_Testing/e2e-tests/community-salesman.spec.ts` (v1.1: + app-install attribution flow, v1.2: + risk scoring hold)

### Files cần MODIFY
- `2_Gateway/Controllers/CommunityController.cs` — add salesman endpoints (v1.1: + composite QR, + app-install/attributed)
- `2_Gateway/Controllers/OrdersController.cs` — accept `referralCode` (composite) in CreateOrder → resolve salesmanId + productId (v1.1)
- `5_WebApps/KhachLink/Program.cs` — DI for SalesmanHttpService + app-install event wiring (v1.1)
- `5_WebApps/KhachLink/Components/QRScanner.razor` — handle composite referral URL `/r/{salesmanCode}|{productShortCode}` (v1.1)
- `5_WebApps/KhachLink/Pages/Login.razor` — check localStorage for composite referral code (v1.1)
- `5_WebApps/KhachLink/wwwroot/sw.js` hoặc `pwa.js` — register `appinstalled` event handler → POST `/api/community/app-install/attributed` (v1.1 NEW)
- `1_Shared/Domain.cs` — Order: add `AssignSalesman(Guid salesmanId, string referralCode, Guid referralProductId)` method (F2 fix — SalesmanId/ReferralCode/ReferralProductId fields có từ Sprint 0 nhưng chưa có domain method để set. Composite referral flow cần set cả 3 fields atomic)
- `3_CoreHub/Services/WalletService.cs` — **F5a caller: extend Sprint 0 base với ConfirmAppInstallBonusAsync** (KHÔNG tạo mới — Sprint 0 đã tạo IWalletService + WalletService base. Sprint 4 extends với app-install bonus payout method)

### Files READ ONLY
- `2_Gateway/Controllers/FeaturedProductsController.cs` — FeaturedProducts query pattern
- `1_Shared/Domain/Aggregates/TenantAggregate/TenantSettings.cs` — lat/lng for distance
- `5_WebApps/KhachLink/Components/GoogleMaps.razor` — QR pattern reference
- `1_Shared/Domain.cs` — SalesReferral (v1.1 redesign), ProductReferralConfig, AppInstallAttribution entities (Sprint 0)

### Boundary Rules (v1.2 updated)
- Nearby Products: query FeaturedProducts (PG) chỉ — không query per-tenant SQLite. JOIN ProductReferralConfig (PG) để lấy commission rate + app-install bonus (v1.1).
- QR generation: client-side (qrcode.js library, CDN). Composite code format `{salesmanCode}|{productShortCode}` (v1.1).
- Referral flow: Scan QR → localStorage lưu composite code → order creation gửi composite → resolve salesmanId + productId → set Order.SalesmanId + Order.ReferralProductId (v1.1).
- Commission: tính khi Order.Completed — `orderTotal * ProductReferralConfig.CommissionRate` (per-product 2-5%, KHÔNG hardcode) (v1.1).
- App-install bonus: customer cài app qua referral → POST `/api/community/app-install/attributed` → AppInstallAttribution + WalletTransaction type=Commission (v1.1 NEW).
- 1 customer 1 attribution (unique constraint AppInstallAttribution.CustomerId) (v1.1).
- Admin UI: ProductReferralConfig CRUD — sysadmin set commission rate + app-install bonus per product (v1.1 NEW).
- KHÔNG sửa Domain.cs — SalesReferral, ProductReferralConfig, AppInstallAttribution, DeviceRegistration, FraudFlag entities đã có từ Sprint 0.
- UI Platform: All pages dùng VanAnButton, VanAnCard.
- **v1.2 NEW — Risk scoring mandatory:** Mọi SalesReferral + AppInstallAttribution MUST compute RiskScore qua IRiskScoringService khi tạo. KHÔNG bỏ qua.
- **v1.2 NEW — Hold 48h if RiskScore≥60:** CommissionStatus/AttributionStatus=Held + HoldUntil=now+48h + FraudFlag(Status=Pending) tạo. KHÔNG auto-pay.
- **v1.2 NEW — Auto-reject if RiskScore≥80:** CommissionStatus/AttributionStatus=Rejected + FraudFlag tạo.
- **v1.2 NEW — Auto-approve after 24h if RiskScore<60:** Cooling period 24h rồi chuyển Pending (ready for payout).
- **v1.2 NEW — Device fingerprint in app-install request:** POST `/api/community/app-install/attributed` body phải có `fingerprintHash` + `fingerprintSignals` (compute client-side bằng FingerprintJS từ Sprint 0).
- **v1.2 NEW — IFraudFlagService:** Service tạo FraudFlag khi RiskScore≥60, query pending flags (Sprint 6 admin UI sẽ consume).

---

## 4. TECHNICAL & REGULATORY CONSTRAINTS — v1.2
- [ ] **Product scope:** PoC chỉ hiển thị FeaturedProducts (PG) — không full catalog
- [ ] **QR format (v1.1):** URL `https://{domain}/r/{salesmanCode}|{productShortCode}` — composite code, redirect to KhachLink với cả 2 phần
- [ ] **Referral flow (v1.1):** Scan QR → localStorage (composite code) → order creation → resolve salesmanId + productId → Order.SalesmanId + Order.ReferralProductId + SalesReferral
- [ ] **Commission trigger (v1.1):** Order.Completed → calculate commission = `orderTotal * ProductReferralConfig.CommissionRate` (per-product 2-5%, KHÔNG hardcode) → create SalesReferral with CommissionAmount + CommissionRate snapshot
- [ ] **App-install bonus (v1.1 NEW):** PWA `appinstalled` event → POST `/api/community/app-install/attributed` → resolve referralCode → create AppInstallAttribution (unique per CustomerId) + WalletTransaction type=Commission (amount = ProductReferralConfig.AppInstallBonus)
- [ ] **Admin config (v1.1 NEW):** ProductReferralConfig CRUD — sysadmin set CommissionRate (2-5%) + AppInstallBonus + ProductShortCode per product
- [ ] **UI Platform:** All pages dùng VanAnButton, VanAnCard
- [ ] **Auth:** X-Customer-Token → check CommunityRole(Salesman, Active). Admin endpoints dùng SystemAdmin policy.
- [ ] **v1.2 NEW — Risk scoring mandatory:** Mọi SalesReferral + AppInstallAttribution MUST compute RiskScore qua IRiskScoringService (8 factors deterministic). Score≥60 → hold 48h + FraudFlag. Score≥80 → auto-reject + FraudFlag.
- [ ] **v1.2 NEW — Device fingerprint in app-install request:** POST `/api/community/app-install/attributed` body include `fingerprintHash` + `fingerprintSignals` (FingerprintJS từ Sprint 0).
- [ ] **v1.2 NEW — IFraudFlagService:** Create FraudFlag khi RiskScore≥60. Query pending flags for Sprint 6 admin UI.
- [ ] **v1.2 NEW — Cooling period 24h:** RiskScore<60 → CommissionStatus/AttributionStatus=Pending (auto-approve sau 24h). KHÔNG pay ngay.

---

## 5. SUCCESS CRITERIA — v1.1
- [ ] **SC1:** GET `/api/community/nearby-products?lat={lat}&lng={lng}&radiusKm=10` trả FeaturedProducts trong bán kính + commission rate + app-install bonus từ ProductReferralConfig (v1.1)
- [ ] **SC2:** Mỗi product: name, price, shopName, distanceKm, **commissionRate**, **appInstallBonus** (v1.1: +2 fields từ ProductReferralConfig, "Chưa thiết lập" nếu không có config)
- [ ] **SC3 (v1.1):** GET `/api/community/salesman/qr?productId={productId}` trả composite code `{salesmanCode}|{productShortCode}` (yêu cầu productId)
- [ ] **SC4:** SalesmanQR page hiển thị QR code chứa composite code (client-side generation, v1.1)
- [ ] **SC5 (v1.1):** QR scan → lưu composite referral code trong localStorage (cả salesmanCode + productShortCode)
- [ ] **SC6 (v1.1):** Order creation với composite referralCode → Order.AssignSalesman() set SalesmanId + ReferralCode + ReferralProductId atomic + SalesReferral created (F2 fix — Sprint 0 tạo 3 fields nhưng thiếu domain method)
- [ ] **SC7 (v1.1):** GET `/api/community/salesman/{id}/commissions` trả list + tổng — tách biệt commission chốt đơn + app-install bonus
- [ ] **SC8 (v1.1):** Commission tính theo `ProductReferralConfig.CommissionRate` (per-product 2-5%, KHÔNG hardcode), status Pending
- [ ] **SC9 (v1.1):** SalesDashboard hiển thị doanh số + commission chốt đơn + app-install bonus (tách biệt 2 nguồn)
- [ ] **SC10 (v1.1 NEW):** POST `/api/community/app-install/attributed` tạo AppInstallAttribution + WalletTransaction type=Commission cho salesman (F5a caller — Sprint 0 WalletService base được invoke lần đầu. IWalletService.CreateTransactionAsync不再是 dead code)
- [ ] **SC11 (v1.1 NEW):** 1 customer chỉ attribute 1 lần — double attribution → 409 Conflict
- [ ] **SC12 (v1.1 NEW):** Admin UI: GET/POST/PUT/DELETE `/api/admin/products/{productId}/referral-config` — sysadmin set commission rate + app-install bonus per product
- [ ] **SC13 (v1.1 NEW):** PWA `appinstalled` event trigger POST `/api/community/app-install/attributed` (app-install-tracker.js)
- [ ] **SC14:** Unit tests ≥20 cases pass (v1.2: tăng từ 15 — +risk scoring integration, +FraudFlag service, +cooling period)
- [ ] **SC15:** `dotnet build` 0 errors + `guard-check.ps1` pass
- [ ] **SC16 (v1.1):** E2E test: scan QR → order → commission + app-install → bonus (community-salesman.spec.ts)
- [ ] **SC17 (v1.2 NEW):** RiskScore computed deterministic cho mọi SalesReferral + AppInstallAttribution (F4 caller — Sprint 0 RiskScoringService được invoke lần đầu. IRiskScoringService.CalculateScore不再是 dead code)
- [ ] **SC18 (v1.2 NEW):** RiskScore ≥ 60 → CommissionStatus/BonusStatus=Held, HoldUntil=now+48h, FraudFlag(Status=Pending) tạo
- [ ] **SC19 (v1.2 NEW):** RiskScore ≥ 80 → CommissionStatus/BonusStatus=Rejected, FraudFlag tạo
- [ ] **SC20 (v1.2 NEW):** RiskScore < 60 → CommissionStatus=Pending, auto-approve sau 24h cooling period
- [ ] **SC21 (v1.2 NEW):** Device fingerprint (FingerprintJS) gửi kèm app-install attribution request
- [ ] **SC22 (v1.2 NEW):** Anti-fraud signals check: salesmanFingerprint==customerFingerprint, same IP 24h, customerAgeDays<7, deviceFirstSeen<24h, appInstallTime<30s, blacklistedFingerprint

**Branch:** `feature/community-sprint4-salesman-qr`

---

## 6. ACTIVE SKILLS (MAX 3)
- `domain-integrity-validation` — SalesReferral, commission calculation
- `accounting-ui-implementation` — Salesman UI pages
- `build-error-analysis` — QR + referral flow errors

---

## 7. AI HEALTH CHECK MATRIX (INITIAL) — v1.1
- **Evidence Count:** 9
- **Verified Facts:**
  - Fact 1: `FeaturedProductsController.cs` — PG query for FeaturedProducts, SystemAdmin auth
  - Fact 2: `TenantSettings` has Latitude/Longitude for distance calculation
  - Fact 3 (v1.1): `SalesReferral` entity redesigned (Sprint 0 v1.1) — có ProductId, ProductShortCode, CommissionRate snapshot, AppInstallBonusAmount, AttachToOrder với per-product commission, AttachAppInstallBonus
  - Fact 4: `CommunityRole` has SalesmanCode (Sprint 0, 6-8 chars unique)
  - Fact 5: `QRScanner.razor` exists — scans tenant QR, can be extended cho composite code
  - Fact 6: `OrdersController.CreateOrder` — accepts CreateOrderCommand (can extend với ReferralCode composite)
  - Fact 7 (v1.1 NEW): `ProductReferralConfig` entity (Sprint 0 v1.1) — CommissionRate 2-5%, AppInstallBonus, ProductShortCode, unique per ProductId
  - Fact 8 (v1.1 NEW): `AppInstallAttribution` entity (Sprint 0 v1.1) — unique per CustomerId, snapshot BonusAmount
  - Fact 9 (v1.1 NEW): `WalletTransaction` entity (Sprint 0 v1.1) — Type=Commission cho app-install bonus, immutable
- **Assumptions:**
  - FeaturedProducts has TenantId → join TenantSettings for lat/lng
  - CreateOrderCommand can be extended with ReferralCode field (composite format)
  - PWA `appinstalled` event có thể trigger JS → POST API (browser support)
- **Open Questions:**
  - Q1 (v1.1 resolved): Commission rate config — **per-product** qua ProductReferralConfig (KHÔNG global, KHÔNG hardcode).
  - Q2 (v1.1 resolved): QR scan referral — auto-apply to **single order** (composite code gửi khi order creation, không auto-apply all future orders).
  - Q3 (v1.1 NEW): ProductShortCode — sysadmin set manual hay auto-generate? (Default: sysadmin set manual trong ProductReferralConfig CRUD, fallback ProductId nếu không có short code).
  - Q4 (v1.1 NEW): App-install attribution — customer đã cài app trước khi scan QR có qualify không? (No — AC-12.7: check install history, chỉ qualify nếu customer chưa cài app trước đó).
- **Recommended Action:** PROCEED — Assumptions (3) < Verified Facts (9), Open Questions (4) = 4 (borderline, nhưng Q1/Q2 đã resolved, Q3/Q4 có default rõ ràng)
