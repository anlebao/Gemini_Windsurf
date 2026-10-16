# TASK CARD: P1.3 — CẢNH BÁO ÂM QUỸ TK 111 / ÂM KHO TK 152,156 TẠI THỜI ĐIỂM XUẤT

> Created: 2026-10-16 (user directive master plan P0-P2)
> Master plan: `docs/AI/plans/accounting-legal-compliance-master-plan.md` (Phase P1.3 — dep: sau P0.2)
> Branch: `main`
> Status: 📋 PLANNED

---

## 1. GOAL & CONTEXT

- **Mục tiêu:** đóng lỗ hổng checklist **1.4** — tự động phát hiện và cảnh báo giao dịch làm **âm quỹ tiền mặt (TK 111)** hoặc **âm kho (TK 152, 156)** tại thời điểm xuất (phiếu chi / bán hàng / nhập xuất kho).
- **Phát hiện review (2026-10-16):** không có cơ chế cảnh báo số dư âm; hệ thống có `PaymentMethodConstants` (111/112) nhưng không tính số dư động.
- **Đã xác minh code:** nguồn số dư 111 = `JournalEntry` (PG — sau P0.2 có `IGeneralLedgerService.GetBalancesAsync`) · tồn kho 152/156 = SQLite ShopERP (`InventoryExcelReport`, ingredient/product stock) · hook điểm xuất: `OrderService` (POS/checkout), `ExpenseEntry`/`ManualEntryJournalBridge` (phiếu chi), nhập/xuất kho (`Ingredients.razor` CRUD).

## 2. ACTIVE WORKFLOW ROUTING

- **Workflow:** `newfeaturebuild.md` — Wave 3 (sau P0.2 cung cấp GetBalancesAsync).
- **Execution Mode:** IMPLEMENT.
- **Skills (max 3):** `domain-integrity-validation` + `accounting-ui-implementation` + `pattern-based-fixing`.

## 3. SCOPE

### 3.1 Service (`3_CoreHub/Services/GeneralLedger/BalanceAlertService.cs`)
- [ ] `IBalanceAlertService`:
  - `CheckCashBalanceAsync(tenantId, projectedAmount, date)` — số dư 111 (từ `GetBalancesAsync` P0.2) − projectedAmount < 0 → alert.
  - `CheckInventoryBalanceAsync(tenantId, productId, quantity)` — tồn kho SQLite (đọc qua repo ShopERP — nếu service chạy CoreHub → đọc qua Gateway/API hiện có) − quantity < 0 → alert.
  - `EvaluateAsync(...)` → `BalanceAlertResult { AlertType: Cash|Inventory, CurrentBalance, Projected, IsNegative }`.
- [ ] Hook: `OrderService` (POS payment CASH/COD) · phiếu chi (`ExpenseEntry` path — khi account 111) · xuất kho (nếu có luồng xuất) — **CẢNH BÁO, không chặn** (config on/off flag `Balance_Alert_Enabled`).
- [ ] Multi-tenancy filter mọi query.

### 3.2 UI
- [ ] Toast/banner cảnh báo (UI Platform) khi phát hiện âm quỹ/âm kho tại điểm xuất — hiển thị số dư hiện tại + số âm dự kiến.
- [ ] Cột/chỉ thị trong Sổ Cái 111 (P0.2) khi số dư âm (nếu P0.2 đã ra — tối thiểu).

### 3.3 Tests
- [ ] Core.Tests: số dư đủ → no alert · chi vượt số dư → alert · âm kho khi xuất > tồn → alert · tenant isolation · flag off → không alert.
- [ ] bUnit: banner render.

## 4. DECISIONS

- Mặc định: **cảnh báo không chặn** (tránh chặn nghiệp vụ — quỹ âm có thể do bù trừ cuối ngày); số liệu để kế toán kiểm soát.

## 5. CONSTRAINTS (governance)

- KHÔNG đụng Domain.cs · KHÔNG entity/migration mới (tính động) · Multi-tenancy (lesson a21f97f2) · UI Platform 100% · Playwright DISABLED trong IMPLEMENT · guard + build + tests MUST PASS.

## 6. SUCCESS CRITERIA (DoD)

- [ ] Cảnh báo âm quỹ 111 + âm kho 152/156 tại điểm xuất (POS + phiếu chi + xuất kho)
- [ ] Flag bật/tắt + test tenant isolation
- [ ] guard + build + tests PASS · Deploy + RV PASS → đóng

## 7. DEFERRED

- Chặn cứng khi âm (chính sách từng tenant) · cảnh báo âm các TK khác (112, 131...) · đối chiếu quỹ cuối ngày tự động