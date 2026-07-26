# TASK CARD: Community Commerce — Sprint 5 — Wallet + COD + Settlement

## 1. GOAL & CONTEXT
- **Mục tiêu cốt lõi:** Shipper ứng tiền + thu COD + wallet ledger (immutable) + settlement giữa shipper-shop-customer.
- **Nghiệp vụ áp dụng:** UC-11 (Wallet + COD) từ requirements spec.
- **Status:** NOT STARTED
- **Branch:** `feature/community-sprint5-wallet-cod`

---

## 2. ACTIVE WORKFLOW ROUTING
- **Target Workflow:** `newfeaturebuild.md` (ANALYZE → IMPLEMENT)
- **Execution Mode:** IMPLEMENT
- **Current Phase:** Sprint 5 of 7
- **Dependency:** Sprint 4 COMPLETE (salesman infrastructure ready)

---

## 3. RELEVANT FILES (CONTEXT BOUNDARY)

### Files cần CREATE
- `5_WebApps/KhachLink/Services/Http/WalletHttpService.cs` — HTTP client
- `5_WebApps/KhachLink/Pages/Wallet.razor` — wallet balance + transactions
- `6_Tests/VanAn.Core.Tests/WalletServiceTests.cs`
- `6_Testing/e2e-tests/community-wallet-cod.spec.ts`

### Files cần MODIFY
- `2_Gateway/Controllers/CommunityController.cs` — add wallet + COD endpoints
- `5_WebApps/KhachLink/Pages/DeliveryTracking.razor` — add COD confirm button
- `5_WebApps/KhachLink/Program.cs` — DI for WalletHttpService
- `1_Shared/Domain.cs` — Order: add `MarkCodCollected(decimal codAmount)` method (F2 fix — CodAmount/CodCollectedAt fields có từ Sprint 0 nhưng chưa có domain method. Set atomic khi shipper confirm COD), `PaymentMethod` accept "COD"
- `3_CoreHub/Services/IWalletService.cs` — **F5a caller: extend Sprint 0 base interface** với ConfirmCodAsync/ConfirmAdvanceAsync/ReverseTransactionAsync/SettleAsync (KHÔNG tạo mới — Sprint 0 đã tạo IWalletService + WalletService base với CreateTransactionAsync + GetBalanceAsync)
- `3_CoreHub/Services/WalletService.cs` — **F5a caller: extend Sprint 0 base impl** với COD/Advance/Settlement/Reverse methods (KHÔNG tạo mới — Sprint 0 đã tạo WalletService base. Sprint 5 extends với full wallet operations)

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
- [ ] **SC1:** Order.PaymentMethod hỗ trợ "COD"
- [ ] **SC2:** GET `/api/community/wallet` trả balance + transaction history
- [ ] **SC3:** POST `/api/community/wallet/confirm-cod` tạo WalletTransaction(CODCollection) + set Order.CodCollectedAt via Order.MarkCodCollected() (F2 fix — Sprint 0 tạo CodAmount/CodCollectedAt fields nhưng thiếu domain method. F5a caller — Sprint 0 WalletService.CreateTransactionAsync được invoke cho COD flow)
- [ ] **SC4:** POST `/api/community/wallet/confirm-advance` tạo WalletTransaction(AdvancePayment) (F5a caller — Sprint 0 WalletService base extended với AdvancePayment method)
- [ ] **SC5:** WalletTransaction immutable — no update/delete API
- [ ] **SC6:** BalanceAfter consistent (chain verification)
- [ ] **SC7:** Settlement record tạo cho shop (WalletTransaction.Settlement)
- [ ] **SC8:** Unit tests ≥15 cases pass (wallet, COD, settlement, immutability, balance)
- [ ] **SC9:** `dotnet build` 0 errors + `guard-check.ps1` pass
- [ ] **SC10:** E2E test: COD order → shipper confirm → wallet balance update
- [ ] **SC11:** Architecture tests pass
- [ ] **SC12:** Regression: delivery + chat + salesman vẫn hoạt động

**Branch:** `feature/community-sprint5-wallet-cod`

---

## 6. ACTIVE SKILLS (MAX 3)
- `domain-integrity-validation` — WalletTransaction immutability, balance chain
- `outbox-pattern-implementation` — Financial transaction atomicity
- `build-error-analysis` — Wallet + COD errors

---

## 7. AI HEALTH CHECK MATRIX (INITIAL)
- **Evidence Count:** 5
- **Verified Facts:**
  - Fact 1: `WalletTransaction` entity (Sprint 0) — immutable, BalanceAfter, no update methods. `IWalletService` + `WalletService` base (Sprint 0 v1.4) — CreateTransactionAsync + GetBalanceAsync đã có, Sprint 5 extends với COD/Advance/Settlement/Reverse
  - Fact 2: `Order` has `CodAmount` + `CodCollectedAt` fields (Sprint 0)
  - Fact 3: `Order.PaymentMethod` — string field, currently CASH/VIETQR/CREDIT_CARD
  - Fact 4: `AccountingEntry` immutable pattern — precedent for WalletTransaction
  - Fact 5: `OrderWorkflowService.HandleOrderCompletedAsync` — hook point for settlement
- **Assumptions:**
  - Wallet balance = SUM(WalletTransaction.Amount WHERE OwnerId) — no separate balance table
  - Settlement simplified: 1 record per COD transaction (not full double-entry in PoC)
- **Open Questions:**
  - Q1: Shipper advance payment — shipper pays shop before pickup? How to verify?
  - Q2: Settlement frequency — per-order or batch (daily/weekly)?
- **Recommended Action:** PROCEED — Assumptions (2) < Facts (5), Open Questions (2) < 3
