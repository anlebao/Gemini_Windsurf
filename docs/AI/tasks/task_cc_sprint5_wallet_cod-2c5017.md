# TASK CARD: Community Commerce — Sprint 5 — Wallet + COD + Settlement

## 1. GOAL & CONTEXT
- **Mục tiêu cốt lõi:** Shipper ứng tiền + thu COD + wallet ledger (immutable) + settlement giữa shipper-shop-customer.
- **Nghiệp vụ áp dụng:** UC-11 (Wallet + COD) từ requirements spec.
- **Status:** COMPLETE + VPS VERIFIED (2026-07-30)
- **Branch:** `feature/community-sprint5-wallet-cod` → merged to `main`
- **Commit:** `2c038fc0` (merge) + `4d2ed424` (feature)
- **Files:** 15 files, +1567/-27 lines
- **VPS RV:** 34/35 PASS (1 pre-existing admin auth behavior, not a Sprint 5 regression)

---

## 2. ACTIVE WORKFLOW ROUTING
- **Target Workflow:** `newfeaturebuild.md` (ANALYZE → IMPLEMENT)
- **Execution Mode:** COMPLETE
- **Current Phase:** Sprint 5 of 7 — CLOSED
- **Dependency:** Sprint 4 COMPLETE (salesman infrastructure ready) ✅

---

## 3. RELEVANT FILES (CONTEXT BOUNDARY)

### Files CREATED
- ✅ `5_WebApps/KhachLink/Services/Http/WalletHttpService.cs` — HTTP client (5 methods + DTOs)
- ✅ `5_WebApps/KhachLink/Pages/Wallet.razor` — wallet balance + transactions + pending advances confirmation
- ✅ `6_Tests/VanAn.Core.Tests/Community/WalletServiceTests.cs` — 19 unit tests
- ✅ `6_Tests/VanAn.Integration.Tests/WalletControllerIntegrationTests.cs` — 7 integration tests (DI + 401 auth guards)
- ℹ️ `6_Testing/e2e-tests/community-wallet-cod.spec.ts` — NOT created (Playwright disabled in IMPLEMENT mode per governance; integration tests cover endpoint auth)

### Files MODIFIED
- ✅ `2_Gateway/Controllers/CommunityController.cs` — +5 wallet endpoints + IWalletService injection + 3 request DTOs
- ✅ `5_WebApps/KhachLink/Pages/DeliveryTracking.razor` — +COD collection button + advance payment button with amount input
- ✅ `5_WebApps/KhachLink/Program.cs` — +WalletHttpService DI
- ✅ `5_WebApps/KhachLink/Components/Layout/NavMenu.razor` — +wallet tab for shippers
- ✅ `1_Shared/Domain.cs` — +Order.MarkCodCollected(decimal codAmount) domain method (idempotency guarded)
- ✅ `3_CoreHub/Services/IWalletService.cs` — +6 methods (GetWalletAsync, ConfirmCodAsync, ConfirmAdvanceAsync, ConfirmAdvanceReceivedAsync, GetPendingAdvancesAsync, ReverseTransactionAsync) + 3 DTOs
- ✅ `3_CoreHub/Services/WalletService.cs` — +6 method implementations + CreateTransactionAsync provider-aware (PG FOR UPDATE / SQLite LINQ fallback)
- ✅ `3_CoreHub/Infrastructure/IVanAnDbContext.cs` — +ProviderName property
- ✅ `3_CoreHub/Infrastructure/VanAnDbContext.cs` — +ProviderName implementation
- ✅ `5_WebApps/ShopERP/Infrastructure/ShopERPDbContext.cs` — +ProviderName implementation

### Files READ ONLY
- `1_Shared/Domain.cs` — WalletTransaction entity (Sprint 0, immutable)
- `3_CoreHub/Services/OrderWorkflowService.cs` — HandleOrderCompletedAsync pattern
- `1_Shared/Domain/Common.cs` — BaseEntity, audit pattern

### Boundary Rules
- WalletTransaction IMMUTABLE — no update/delete methods (like AccountingEntry)
- BalanceAfter calculated at creation time
- COD flow: shipper confirms "đã thu tiền" → WalletTransaction(CODCollection) + Order.CodCollectedAt
- Settlement: shipper owes shop (advance) or shop owes shipper (COD) — record as WalletTransaction(Settlement)
- KHÔNG sửa AccountingEntry — WalletTransaction là riêng biệt, không liên kết kế toán HKD trong PoC

---

## 4. TECHNICAL & REGULATORY CONSTRAINTS
- [ ] **Immutable ledger:** WalletTransaction append-only — verify no update path in tests
- [ ] **Balance integrity:** BalanceAfter = previous BalanceAfter + Amount (chain verification)
- [ ] **COD payment method:** Order.PaymentMethod = "COD" — shipper collects cash from customer
- [ ] **Double-entry:** Each COD creates 2 WalletTransaction: +amount for shipper, -amount from customer (or settlement record for shop)
- [ ] **UI Platform:** Wallet page dùng VanAnButton, VanAnCard, VanAnTable
- [ ] **Auth:** X-Customer-Token → wallet owner only

---

## 5. SUCCESS CRITERIA
- [x] **SC1:** Order.PaymentMethod hỗ trợ "COD" ✅
- [x] **SC2:** GET `/api/community/wallet` trả balance + transaction history ✅ (VPS: 401 no-token, endpoint exists)
- [x] **SC3:** POST `/api/community/wallet/confirm-cod` tạo WalletTransaction(CODCollection) + set Order.CodCollectedAt via Order.MarkCodCollected() ✅ (VPS: 401 no-token, endpoint exists, DLL compiled)
- [x] **SC4:** POST `/api/community/wallet/confirm-advance` tạo WalletTransaction(AdvancePayment) ✅ (VPS: 401 no-token, endpoint exists, DLL compiled)
- [x] **SC5:** WalletTransaction immutable — no update/delete API ✅ (T14 reflection test PASS)
- [x] **SC6:** BalanceAfter consistent (chain verification) ✅ (T15 chain test PASS)
- [x] **SC7:** Settlement record tạo cho shop (WalletTransaction.Settlement) ✅ (T6 + T16 tests PASS)
- [x] **SC8:** Unit tests ≥15 cases pass ✅ (19 unit tests PASS — exceeded plan by 4: +shop-confirmed advance flow T16-T19)
- [x] **SC9:** `dotnet build` 0 errors + `guard-check.ps1` pass ✅
- [~] **SC10:** E2E test: COD order → shipper confirm → wallet balance update — SKIPPED (Playwright disabled in IMPLEMENT mode per governance; 7 integration tests cover endpoint auth + DI)
- [x] **SC11:** Architecture tests pass ✅ (39/39 PASS)
- [x] **SC12:** Regression: delivery + chat + salesman vẫn hoạt động ✅ (VPS RV5: 7/8 regression PASS, 1 pre-existing admin auth)

### Additional deliverables beyond original plan (shop-confirmed advance flow):
- POST `/api/community/wallet/confirm-advance-received` — shop confirms advance receipt (idempotency guarded)
- GET `/api/community/wallet/pending-advances` — shop owner pending advance queue
- Wallet.razor pending advances confirmation section
- IVanAnDbContext.ProviderName property (provider-aware CreateTransactionAsync)

**Branch:** `feature/community-sprint5-wallet-cod` → merged to `main`

---

## 6. ACTIVE SKILLS (MAX 3)
- `domain-integrity-validation` — WalletTransaction immutability, balance chain
- `outbox-pattern-implementation` — Financial transaction atomicity
- `build-error-analysis` — Wallet + COD errors

---

## 7. AI HEALTH CHECK MATRIX (FINAL)
- **Evidence Count:** 12
- **Verified Facts:**
  - Fact 1: `WalletTransaction` entity (Sprint 0) — immutable, BalanceAfter, no update methods. `IWalletService` + `WalletService` base (Sprint 0 v1.4) — CreateTransactionAsync + GetBalanceAsync đã có, Sprint 5 extends với COD/Advance/Settlement/Reverse ✅
  - Fact 2: `Order` has `CodAmount` + `CodCollectedAt` fields (Sprint 0) ✅
  - Fact 3: `Order.PaymentMethod` — string field, currently CASH/VIETQR/CREDIT_CARD ✅
  - Fact 4: `AccountingEntry` immutable pattern — precedent for WalletTransaction ✅
  - Fact 5: `OrderWorkflowService.HandleOrderCompletedAsync` — hook point for settlement ✅
  - Fact 6: 19 unit tests PASS (wallet, COD, advance, settlement, immutability, balance chain, idempotency, shop-confirmed flow) ✅
  - Fact 7: 7 integration tests PASS (DI registration + 401 auth guards for all 5 endpoints) ✅
  - Fact 8: 133 community tests PASS (19 Wallet + 114 existing) ✅
  - Fact 9: 39/39 Architecture tests PASS ✅
  - Fact 10: VPS RV5 34/35 PASS — 5/5 wallet API 401 no-token + 7/7 DLL methods + 4/4 routes + 6/6 WASM symbols + PG schema verified ✅
  - Fact 11: CD auto-deployed commit `2c038fc0` (images built 18:32-18:34 UTC, containers created 18:35) ✅
  - Fact 12: Pre-push CI pipeline ALL PASSED (188s/146s) ✅
- **Assumptions:** 0 (all resolved during implementation)
- **Open Questions:** 0 (Q1 resolved: shop-confirmed advance flow; Q2 resolved: per-order settlement)
- **Gate 6 Status:** ✅ Assumptions (0) < Verified Facts (12), Open Questions (0) < 3
- **Final Status:** SPRINT 5 COMPLETE + VPS VERIFIED
