# TASK CARD: Commerce Mode Toggle — Sprint 7 (Marketplace ↔ Reseller)

## 1. GOAL & CONTEXT
- **Mục tiêu cốt lõi:** Bổ sung toggle toàn cục (Global) + override cấp tenant để chuyển đổi giữa mô hình Marketplace (hiện tại — tenant bán trực tiếp) và Reseller (Vạn An mua từ tenant → bán lại cho customer, quyết định phân chia lợi nhuận).
- **Nguyên tắc:** "Mua giúp — Bán dùm". Vạn An mua hàng hóa từ tenant rồi bán lại cho khách hàng cuối. Phân chia thành: chi phí bán hàng (salesman), chi phí giao hàng (shipper), chi phí nền tảng, quỹ phát triển cộng đồng.
- **Nghiệp vụ áp dụng:** Spec v2.1 `commerce-mode-toggle-spec-v2-2c5017.md` (ANALYZE complete 2026-07-30)
- **Status:** ANALYZE COMPLETE — READY FOR IMPLEMENT (pending user approval)
- **Branch:** `feature/commerce-mode-toggle-sprint7`
- **Additive:** Không phá Sprint 0-6. Default = Marketplace (existing behavior). Past orders snapshot mode tại creation.
- **Scope:** FULL (Q3 community fund management + Q5 non-COD Reseller included per user decision 2026-07-30)

---

## 2. ACTIVE WORKFLOW ROUTING
- **Target Workflow:** `newfeaturebuild.md` (ANALYZE → IMPLEMENT)
- **Execution Mode:** IMPLEMENT
- **Current Phase:** Sprint 7 (post-Community Commerce S0-S6)
- **Dependency:** Sprint 6 COMPLETE (Admin + Fraud + Legal) + Sprint 5 COMPLETE (Wallet + COD)

---

## 3. RELEVANT FILES (CONTEXT BOUNDARY)

### Files cần CREATE
- `docs/AI/tasks/commerce-mode-toggle-spec-v2-2c5017.md` — spec (DONE v2.1)
- `1_Shared/Domain/Aggregates/OrderAggregate/CommerceMode.cs` — enum (Marketplace=0, Reseller=1, Inherit=-1)
- `1_Shared/Domain/Aggregates/OrderAggregate/CommissionBase.cs` — enum (OnOrderTotal=0, OnMargin=1)
- `1_Shared/Domain/Aggregates/SystemSettingAggregate/SystemSetting.cs` — key-value config entity
- `1_Shared/Domain/Aggregates/ProductCostPriceAggregate/ProductCostPrice.cs` — Q1: Van An's negotiated cost per product
- `1_Shared/Domain/Aggregates/CommunityFundAggregate/CommunityFundSpendRecord.cs` — Q3: audit trail for fund disbursement
- `1_Shared/Domain/Common/SystemWalletIds.cs` — reserved GUIDs (PlatformWallet, CommunityFund)
- `3_CoreHub/Services/ICommerceModeService.cs` — interface
- `3_CoreHub/Services/CommerceModeService.cs` — get/set global + tenant override + resolve for order
- `3_CoreHub/Services/ICommunityFundService.cs` — Q3: balance + spend + history
- `3_CoreHub/Services/CommunityFundService.cs` — Q3 implementation
- `2_Gateway/Controllers/CommunityAdminController.cs` — +3 commerce-mode endpoints (add to existing Sprint 6 controller)
- `2_Gateway/Controllers/CommunityFundController.cs` — Q3: 3 endpoints (balance, spend, history)
- `2_Gateway/Controllers/ProductCostPriceController.cs` — Q1: GET + POST cost price
- `5_WebApps/ShopERP/Components/Pages/Admin/CommerceMode.razor` — admin settings page
- `5_WebApps/ShopERP/Components/Pages/Admin/CommunityFund.razor` — Q3: fund management page
- `5_WebApps/ShopERP/Components/Pages/Admin/ProductCostPrice.razor` — Q1: cost price management page
- `5_WebApps/ShopERP/Services/CommerceModeApiClient.cs` — Gateway admin API client
- `5_WebApps/ShopERP/Services/CommunityFundApiClient.cs` — Q3 API client
- `5_WebApps/ShopERP/Services/ProductCostPriceApiClient.cs` — Q1 API client
- `6_Tests/VanAn.Core.Tests/Community/CommerceModeServiceTests.cs` — ~24 unit tests
- `6_Tests/e2e-tests/community-fund-management.spec.ts` — Q3 E2E
- `6_Tests/e2e-tests/reseller-non-cod.spec.ts` — Q5 E2E
- `docs/legal/reseller-policy.md` — (DONE 2026-07-30) Quy chế Reseller "Mua giúp — Bán dùm", 4-split financial, advance, non-COD, toggle mechanism
- `docs/legal/reseller-agreement.md` — (DONE 2026-07-30) Hợp đồng B2B template Vạn An ↔ Tenant (14 điều + 3 phụ lục: CostPrice, Fee Rates, SLA)
- `docs/legal/community-fund-policy.md` — (DONE 2026-07-30) Q3: Quản trị quỹ cộng đồng — guardrail SysAdmin rút tiền, audit trail, transparency, whistleblow
- `docs/legal/anti-fraud-policy-reseller-addendum.md` — (DONE 2026-07-30) Addendum cho anti-fraud-policy.md — 7 fraud vectors Reseller (cost price, margin, advance, COD skimming, fund misappropriation, external payment, settlement)

### Files cần MODIFY
- `1_Shared/Domain.cs` (Order entity, line 1468) — +7 Order fields (CommerceMode, CostPrice, SellPrice, PlatformMargin, DeliveryFee, PlatformFeeRate, CommunityFundRate)
- `1_Shared/Domain/Aggregates/TenantAggregate/TenantSettings.cs` — +CommerceModeOverride field + WithCommerceModeOverride method
- `1_Shared/Domain.cs` (WalletTransactionType enum, line 3166) — +5 enum values (PlatformFee=7, CommunityFund=8, DeliveryFee=9, ExternalPayment=10, CommunityFundSpend=11)
- `1_Shared/Domain.cs` (ProductReferralConfig entity, line 3569) — +CommissionBase field
- `3_CoreHub/Services/WalletService.cs` — dual-mode ConfirmCodAsync + ConfirmAdvanceAsync + ConfirmExternalPaymentAsync (Q5) + SpendCommunityFundAsync (Q3)
- `3_CoreHub/Services/SalesmanService.cs` — dual-mode CreateCommissionAsync (OnOrderTotal vs OnMargin)
- `3_CoreHub/Services/OrderService.cs` — inject CommerceMode resolution + CostPrice loading + SellPrice computation at order creation (line 609-708)
- `3_CoreHub/Infrastructure/VanAnDbContext.cs` — +SystemSettings +ProductCostPrices +CommunityFundSpendRecords DbSets
- `3_CoreHub/Infrastructure/Configurations/SystemSettingConfiguration.cs` — EF config (NEW)
- `3_CoreHub/Infrastructure/Configurations/ProductCostPriceConfiguration.cs` — EF config (NEW)
- `3_CoreHub/Infrastructure/Configurations/CommunityFundSpendRecordConfiguration.cs` — EF config (NEW)
- `3_CoreHub/Infrastructure/Configurations/OrderConfiguration.cs` — +7 columns mapping
- `2_Gateway/Program.cs` — +ICommerceModeService +ICommunityFundService DI
- `2_Gateway/Controllers/CommunityController.cs` — +confirm-external-payment endpoint (Q5)
- `5_WebApps/ShopERP/Components/Pages/Admin/AdminLayout.razor` — +3 nav links (Commerce Mode, Community Fund, Product Cost Price)
- `5_WebApps/ShopERP/Components/Layout/NavMenu.razor` — +3 nav links (SystemAdmin section)
- `5_WebApps/ShopERP/Program.cs` — +3 API client DI
- `5_WebApps/KhachLink/Pages/Wallet.razor` — +mode badge
- `5_WebApps/KhachLink/Pages/DeliveryTracking.razor` — conditional advance button (Marketplace only)
- `5_WebApps/KhachLink/Pages/NearbyProducts.razor` — mode-aware price display
- `5_WebApps/KhachLink/Services/Http/CommunityHttpService.cs` — +GetCommerceModeAsync
- `6_Tests/VanAn.Architecture.Tests/AuthorizationEnforcementTests.cs` — +3 controllers to W12-G7 exempt list (CommunityFundController, ProductCostPriceController; CommunityAdminController already exempt)

### Files READ ONLY
- `3_CoreHub/Services/WalletService.cs` — existing ConfirmCod/ConfirmAdvance logic (Marketplace baseline)
- `3_CoreHub/Services/SalesmanService.cs` — existing CreateCommission logic
- `1_Shared/Domain/Aggregates/TenantAggregate/TenantSettings.cs` — existing value object pattern

### Boundary Rules
- **Additive only:** Không xóa/sửa existing enum values, không sửa existing Order field semantics
- **Snapshot at creation:** Order.CommerceMode set khi tạo, KHÔNG thay đổi khi toggle
- **Marketplace = existing behavior:** Khi mode=Marketplace, tất cả logic giống Sprint 0-6
- **SystemWalletIds:** 2 reserved GUID (PlatformWallet, CommunityFund) — không tạo Customer entity
- **Financial balance invariant:** Reseller COD: tổng tất cả tx amounts = COD collected. Non-COD: tổng = external payment amount.
- **Domain Modification APPROVED 2026-07-30:** 7 Order fields + 2 enum + 5 WalletTransactionType + 1 ProductReferralConfig field + 1 TenantSettings field + 3 new entities (SystemSetting, ProductCostPrice, CommunityFundSpendRecord) + 1 static class (SystemWalletIds)
- **Q1 CostPrice:** Manual via Gateway admin API (ProductCostPrice table in PG). Tenant negotiates offline.
- **Q2 Fee rate:** Global only (SystemSetting). No per-tenant fee rate in Sprint 7.
- **Q3 Community fund:** Full UC — balance + spend + history. CommunityFundSpendRecord audit entity.
- **Q5 Non-COD:** confirm-external-payment endpoint. ExternalPayment=10 enum. 5-split (no CODCollection).

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
- [ ] **SC1:** CommerceMode enum + CommissionBase enum + Order 7 fields + SystemSetting entity + ProductCostPrice entity + CommunityFundSpendRecord entity + SystemWalletIds + TenantSettings.CommerceModeOverride + ProductReferralConfig.CommissionBase added to Domain
- [ ] **SC2:** ICommerceModeService + CommerceModeService — get/set global + tenant override + resolve for order
- [ ] **SC3:** WalletService.ConfirmCodAsync dual-mode — Marketplace (2 tx, existing) + Reseller (6 tx, new)
- [ ] **SC4:** WalletService.ConfirmAdvanceAsync dual-mode — Marketplace (shipper ứng) + Reseller (Vạn An ứng)
- [ ] **SC5:** SalesmanService.CreateCommissionAsync dual-mode — OnOrderTotal (Marketplace) + OnMargin (Reseller)
- [ ] **SC6:** CommunityAdminController — +3 commerce-mode endpoints (GET global, POST global, POST tenant override)
- [ ] **SC7:** GET /api/community/commerce-mode — customer-facing mode query
- [ ] **SC8:** CommerceMode.razor admin page — toggle + rate settings + tenant overrides table
- [ ] **SC9:** KhachLink mode-aware UI — Wallet badge + DeliveryTracking advance button conditional + NearbyProducts price
- [ ] **SC10:** ShopERP AdminLayout + NavMenu — +3 nav links (Commerce Mode, Community Fund, Product Cost Price)
- [ ] **SC11:** ~24 unit tests PASS
- [ ] **SC12:** `dotnet build` 0 errors + guard-check pass
- [ ] **SC13:** Architecture tests pass (W12-G7 exempt list updated)
- [ ] **SC14:** VPS RV7-1 to RV7-18 ALL PASS
- [ ] **SC15:** Regression Sprint 0-6 — all existing behavior unchanged when mode=Marketplace (default)
- [ ] **SC16:** Financial balance invariant — Reseller COD: sum(tx amounts) = COD collected. Non-COD: sum = external payment.
- [ ] **SC17:** Order snapshot — past orders unaffected by toggle
- [ ] **SC18:** Migration additive — existing data intact, no data loss
- [ ] **SC19 (Q1):** ProductCostPrice entity + POST /api/admin/product-cost-price + ProductCostPrice.razor admin page
- [ ] **SC20 (Q3):** CommunityFundService — balance + spend + history. CommunityFundSpendRecord audit. CommunityFund.razor admin page. 3 endpoints.
- [ ] **SC21 (Q5):** WalletService.ConfirmExternalPaymentAsync — Reseller non-COD 5-split. POST /api/community/wallet/confirm-external-payment. Rejects Marketplace orders.
- [ ] **SC22:** 2 E2E specs (community-fund-management, reseller-non-cod) PASS
- [ ] **SC23 (Legal — Reseller doc set):** 4 legal doc Reseller draft hoàn thành — `docs/legal/reseller-policy.md` (quy chế Reseller "Mua giúp — Bán dùm" + 4-split financial + COD/advance/non-COD flow + toggle mechanism) + `docs/legal/reseller-agreement.md` (hợp đồng B2B template Vạn An ↔ Tenant, 14 điều + 3 phụ lục) + `docs/legal/community-fund-policy.md` (Q3 quản trị quỹ cộng đồng + guardrail SysAdmin rút tiền + audit trail + transparency + whistleblow) + `docs/legal/anti-fraud-policy-reseller-addendum.md` (addendum cho `anti-fraud-policy.md` — 7 fraud vectors Reseller: cost price manipulation, margin manipulation, advance fraud, COD skimming variant, community fund misappropriation, external payment fraud Q5, settlement fraud + 3-strike extension tenant/SysAdmin). **DONE 2026-07-30** (draft — cần luật sư + kế toán review trước khi publish/ký). Additive — không sửa 4 legal doc Marketplace hiện có (community-terms-of-service, community-privacy-policy, marketplace-policy, anti-fraud-policy).

**Branch:** `feature/commerce-mode-toggle-sprint7`

---

## 6. ACTIVE SKILLS (MAX 3)
- `domain-integrity-validation` — Order snapshot, WalletTransaction immutability, financial balance invariant
- `accounting-ui-implementation` — CommerceMode admin page + mode-aware KhachLink UI
- `build-error-analysis` — Dual-mode branch errors

---

## 7. AI HEALTH CHECK MATRIX (POST-ANALYZE 2026-07-30)
- **Evidence Count:** 19
- **Verified Facts (ANALYZE phase 2026-07-30, 2 subagents):**
  - Fact 1: `WalletService.cs` ConfirmCodAsync (line 155) — existing Marketplace logic: 2 tx (CODCollection + Settlement). Verified.
  - Fact 2: `WalletService.cs` ConfirmAdvanceAsync (line 214) — existing Marketplace logic: shipper -amount. Verified.
  - Fact 3: `SalesmanService.cs:255` — `referral.AttachToOrder(orderId, ..., order.TotalAmount, config.CommissionRate)` — commission on orderTotal. Verified.
  - Fact 4: `TenantSettings.cs` — value object with 9 `With*` immutable update methods (WithSlug, WithTheme, etc.). Verified.
  - Fact 5: `WalletTransactionType` enum — **6 values** (CODCollection=1 through Reversal=6). v2.0 spec claim of 8 values was WRONG. Verified.
  - Fact 6: `Order` entity (Domain.cs:1468) — has TotalAmount, PaymentMethod, CodAmount, CodCollectedAt, ShippingFee (line 1498). No CommerceMode/CostPrice/Margin/DeliveryFee fields. Verified.
  - Fact 7: `ProductReferralConfig` entity (Domain.cs:3569) — has CommissionRate, AppInstallBonus, ProductShortCode. No CommissionBase field. Verified.
  - Fact 8: Sprint 5+6 deployed + VPS verified. Wallet + COD + Settlement working in Marketplace mode.
  - Fact 9: `SystemSetting` entity does NOT exist. Closest pattern: `ShopFeatureSettingsEntity` (per-tenant). Verified.
  - Fact 10: `WalletTransaction.OwnerId` is plain `Guid` (Domain.cs:3540) — reserved GUIDs work without Customer entity. Verified.
  - Fact 11: `BaseEntity.Id` is `Guid` (Common.cs:77), Single-Identity Pattern compliant. Verified.
  - Fact 12: `SystemWalletIds` does NOT exist — needs creation. Verified.
  - Fact 13: `CommunityAdminController` (2_Gateway) — 3 Sprint 6 endpoints (eligible, activate-role, deactivate-role), SystemAdmin JWT. Verified.
  - Fact 14: `CommunityController` confirm-cod at line 674. No existing commerce-mode endpoints. Verified.
  - Fact 15: Gateway Program.cs DI at lines 251-253 (Sprint 6 added ICommunityAdminService + IFraudReviewService). Verified.
  - Fact 16: ShopERP AdminLayout.razor + NavMenu.razor — Sprint 6 added +4 nav links under SystemAdmin. Verified.
  - Fact 17: KhachLink Wallet.razor (no mode badge), DeliveryTracking.razor (advance button unconditional :158-174), NearbyProducts.razor (price unconditional :66-86). Verified.
  - Fact 18: Order creation at `OrderService.CreateOrderFromCommandAsync` (lines 609-708). Verified.
  - Fact 19: 27 migrations exist, latest `20260726105331_CommunitySprint0`. W12-G7 exempt list at `AuthorizationEnforcementTests.cs:119-159`. `GatewayAdminApiClientBase` at ShopERP/Services. Verified.
- **Assumptions:** 0
- **Open Questions:** 0 (all 5 resolved — see spec §12)
- **Recommended Action:** PROCEED TO IMPLEMENT — Assumptions (0) < Verified Facts (19), Open Questions (0) < 3. Gate 1 + Gate 6 PASSED. Domain Modification APPROVED 2026-07-30.
