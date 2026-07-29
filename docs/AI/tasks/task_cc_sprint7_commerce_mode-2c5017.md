# TASK CARD: Commerce Mode Toggle — Sprint 7 (Marketplace ↔ Reseller)

## 1. GOAL & CONTEXT
- **Mục tiêu cốt lõi:** Bổ sung toggle toàn cục (Global) + override cấp tenant để chuyển đổi giữa mô hình Marketplace (hiện tại — tenant bán trực tiếp) và Reseller (Vạn An mua từ tenant → bán lại cho customer, quyết định phân chia lợi nhuận).
- **Nguyên tắc:** "Mua giúp — Bán dùm". Vạn An mua hàng hóa từ tenant rồi bán lại cho khách hàng cuối. Phân chia thành: chi phí bán hàng (salesman), chi phí giao hàng (shipper), chi phí nền tảng, quỹ phát triển cộng đồng.
- **Nghiệp vụ áp dụng:** Spec v2.0 `commerce-mode-toggle-spec-v2-2c5017.md`
- **Status:** NOT STARTED
- **Branch:** `feature/commerce-mode-toggle-sprint7`
- **Additive:** Không phá Sprint 0-6. Default = Marketplace (existing behavior). Past orders snapshot mode tại creation.

---

## 2. ACTIVE WORKFLOW ROUTING
- **Target Workflow:** `newfeaturebuild.md` (ANALYZE → IMPLEMENT)
- **Execution Mode:** IMPLEMENT
- **Current Phase:** Sprint 7 (post-Community Commerce S0-S6)
- **Dependency:** Sprint 6 COMPLETE (Admin + Fraud + Legal) + Sprint 5 COMPLETE (Wallet + COD)

---

## 3. RELEVANT FILES (CONTEXT BOUNDARY)

### Files cần CREATE
- `docs/AI/tasks/commerce-mode-toggle-spec-v2-2c5017.md` — spec (DONE)
- `1_Shared/Domain/Aggregates/OrderAggregate/CommerceMode.cs` — enum (Marketplace=0, Reseller=1, Inherit=-1)
- `1_Shared/Domain/Aggregates/OrderAggregate/CommissionBase.cs` — enum (OnOrderTotal=0, OnMargin=1)
- `1_Shared/Domain/Aggregates/SystemSettingAggregate/SystemSetting.cs` — key-value config entity
- `3_CoreHub/Services/ICommerceModeService.cs` — interface
- `3_CoreHub/Services/CommerceModeService.cs` — get/set global + tenant override + resolve for order
- `2_Gateway/Controllers/CommunityAdminController.cs` — 3 commerce-mode endpoints (if not exists from Sprint 6)
- `5_WebApps/ShopERP/Components/Pages/Admin/CommerceMode.razor` — admin settings page
- `6_Tests/VanAn.Core.Tests/Community/CommerceModeServiceTests.cs` — 18 unit tests

### Files cần MODIFY
- `1_Shared/Domain.cs` (or Order entity file) — +7 Order fields (CommerceMode, CostPrice, SellPrice, PlatformMargin, DeliveryFee, PlatformFeeRate, CommunityFundRate)
- `1_Shared/Domain/Aggregates/TenantAggregate/TenantSettings.cs` — +CommerceModeOverride field + WithCommerceModeOverride method
- `1_Shared/Domain.cs` (or WalletTransactionType enum) — +3 enum values (PlatformFee=9, CommunityFund=10, DeliveryFee=11)
- `1_Shared/Domain.cs` (or ProductReferralConfig entity) — +CommissionBase field
- `3_CoreHub/Services/WalletService.cs` — dual-mode ConfirmCodAsync + ConfirmAdvanceAsync (branch by order.CommerceMode)
- `3_CoreHub/Services/SalesmanService.cs` — dual-mode CreateCommissionAsync (OnOrderTotal vs OnMargin)
- `3_CoreHub/Infrastructure/VanAnDbContext.cs` — +SystemSettings DbSet
- `3_CoreHub/Infrastructure/Configurations/SystemSettingConfiguration.cs` — EF config (NEW)
- `3_CoreHub/Infrastructure/Configurations/OrderConfiguration.cs` — +7 columns mapping
- `2_Gateway/Program.cs` — +ICommerceModeService DI
- `5_WebApps/ShopERP/Components/Pages/Admin/AdminLayout.razor` — +Commerce Mode nav link
- `5_WebApps/ShopERP/Components/Layout/NavMenu.razor` — +Commerce Mode nav link (SystemAdmin section)
- `5_WebApps/KhachLink/Pages/Wallet.razor` — +mode badge
- `5_WebApps/KhachLink/Pages/DeliveryTracking.razor` — conditional advance button (Marketplace only)
- `5_WebApps/KhachLink/Pages/NearbyProducts.razor` — mode-aware price display
- `5_WebApps/KhachLink/Services/Http/CommunityHttpService.cs` — +GetCommerceModeAsync

### Files READ ONLY
- `3_CoreHub/Services/WalletService.cs` — existing ConfirmCod/ConfirmAdvance logic (Marketplace baseline)
- `3_CoreHub/Services/SalesmanService.cs` — existing CreateCommission logic
- `1_Shared/Domain/Aggregates/TenantAggregate/TenantSettings.cs` — existing value object pattern

### Boundary Rules
- **Additive only:** Không xóa/sửa existing enum values, không sửa existing Order field semantics
- **Snapshot at creation:** Order.CommerceMode set khi tạo, KHÔNG thay đổi khi toggle
- **Marketplace = existing behavior:** Khi mode=Marketplace, tất cả logic giống Sprint 0-6
- **SystemWalletIds:** 2 reserved GUID (PlatformWallet, CommunityFund) — không tạo Customer entity
- **Financial balance invariant:** Reseller COD: tổng tất cả tx amounts = COD collected
- **Domain Modification:** Cần approval — 7 Order fields + 2 enum + 3 WalletTransactionType + 1 ProductReferralConfig field + 1 TenantSettings field + 1 SystemSetting entity

---

## 4. TECHNICAL & REGULATORY CONSTRAINTS
- [ ] **Additive migration:** Tất cả columns mới nullable (trừ CommerceMode default 0). Existing data intact.
- [ ] **Toggle runtime:** SystemSetting entity (không restart) — admin UI toggle
- [ ] **Tenant override priority:** TenantSettings.CommerceModeOverride ≠ Inherit → override. = Inherit → global.
- [ ] **Order snapshot:** CommerceMode snapshot tại creation. Toggle affect future orders only.
- [ ] **Dual-mode WalletService:** ConfirmCodAsync + ConfirmAdvanceAsync branch by order.CommerceMode. Marketplace path = existing. Reseller path = new.
- [ ] **Commission base:** ProductReferralConfig.CommissionBase — OnOrderTotal (Marketplace) vs OnMargin (Reseller). Không overload CommissionRate semantics.
- [ ] **Platform wallet:** SystemWalletIds.PlatformWallet + SystemWalletIds.CommunityFund — reserved GUIDs, không Customer entity.
- [ ] **UI Platform:** CommerceMode.razor dùng VanAnButton, VanAnCard, VanAnTable, VanAnBadge.
- [ ] **Auth:** Admin endpoints = SystemAdmin JWT. Customer endpoint = X-Customer-Token.

---

## 5. SUCCESS CRITERIA
- [ ] **SC1:** CommerceMode enum + Order fields + SystemSetting entity + TenantSettings.CommerceModeOverride + ProductReferralConfig.CommissionBase added to Domain
- [ ] **SC2:** ICommerceModeService + CommerceModeService — get/set global + tenant override + resolve for order
- [ ] **SC3:** WalletService.ConfirmCodAsync dual-mode — Marketplace (2 tx, existing) + Reseller (6 tx, new)
- [ ] **SC4:** WalletService.ConfirmAdvanceAsync dual-mode — Marketplace (shipper ứng) + Reseller (Vạn An ứng)
- [ ] **SC5:** SalesmanService.CreateCommissionAsync dual-mode — OnOrderTotal (Marketplace) + OnMargin (Reseller)
- [ ] **SC6:** CommunityAdminController — 3 endpoints (GET global, POST global, POST tenant override)
- [ ] **SC7:** GET /api/community/commerce-mode — customer-facing mode query
- [ ] **SC8:** CommerceMode.razor admin page — toggle + rate settings + tenant overrides table
- [ ] **SC9:** KhachLink mode-aware UI — Wallet badge + DeliveryTracking advance button conditional + NearbyProducts price
- [ ] **SC10:** ShopERP AdminLayout + NavMenu — nav link to /admin/commerce-mode
- [ ] **SC11:** 18 unit tests PASS
- [ ] **SC12:** `dotnet build` 0 errors + guard-check pass
- [ ] **SC13:** Architecture tests pass
- [ ] **SC14:** VPS RV7-1 to RV7-12 ALL PASS
- [ ] **SC15:** Regression Sprint 0-6 — all existing behavior unchanged when mode=Marketplace (default)
- [ ] **SC16:** Financial balance invariant — Reseller COD: sum(tx amounts) = COD collected
- [ ] **SC17:** Order snapshot — past orders unaffected by toggle
- [ ] **SC18:** Migration additive — existing data intact, no data loss

**Branch:** `feature/commerce-mode-toggle-sprint7`

---

## 6. ACTIVE SKILLS (MAX 3)
- `domain-integrity-validation` — Order snapshot, WalletTransaction immutability, financial balance invariant
- `accounting-ui-implementation` — CommerceMode admin page + mode-aware KhachLink UI
- `build-error-analysis` — Dual-mode branch errors

---

## 7. AI HEALTH CHECK MATRIX (INITIAL)
- **Evidence Count:** 8
- **Verified Facts:**
  - Fact 1: `WalletService.cs` ConfirmCodAsync (line 155) — existing Marketplace logic: 2 tx (CODCollection + Settlement). Verified.
  - Fact 2: `WalletService.cs` ConfirmAdvanceAsync (line 214) — existing Marketplace logic: shipper -amount. Verified.
  - Fact 3: `SalesmanService.cs:255` — `referral.AttachToOrder(orderId, ..., order.TotalAmount, config.CommissionRate)` — commission on orderTotal. Verified.
  - Fact 4: `TenantSettings.cs` — value object with `With*` immutable update pattern (WithSlug, WithTheme, etc.). Verified.
  - Fact 5: `WalletTransactionType` enum — 8 values (CODCollection=1 through SmsOtpFee=8). Verified.
  - Fact 6: `Order` entity — has TotalAmount, PaymentMethod, CodAmount, CodCollectedAt. No CostPrice/Margin fields. Verified.
  - Fact 7: `ProductReferralConfig` entity — has CommissionRate, AppInstallBonus, ProductShortCode. No CommissionBase field. Verified.
  - Fact 8: Sprint 5 deployed + VPS verified (RV5 34/35 PASS). Sprint 6 task card updated. Wallet + COD + Settlement working in Marketplace mode.
- **Assumptions:**
  - SystemSetting entity chưa tồn tại (spec v1.5 mention nhưng chưa implement) — cần tạo mới
  - CostPrice lấy từ tenant — PoC: tenant nhập manual qua admin API. Scale: NATS query.
- **Open Questions:**
  - Q1: CostPrice lấy từ đâu? (PoC: manual. Scale: NATS query product price.)
  - Q2: Platform fee rate: global only hay per-tenant? (Spec: global. Có cần per-tenant?)
  - Q3: Community fund wallet management (withdraw + spend)? (Defer to Sprint 8+.)
  - Q4: Reseller COD: shipper thu = SellPrice + DeliveryFee, hay chỉ SellPrice?
  - Q5: Reseller + non-COD payment (VietQR): Vạn An nhận trực tiếp, flow thế nào?
- **Recommended Action:** PROCEED — Assumptions (2) < Verified Facts (8), Open Questions (5) ≥ 3 → **NEED RESOLUTION before IMPLEMENT**. Resolve Q1-Q5 in ANALYZE phase.
