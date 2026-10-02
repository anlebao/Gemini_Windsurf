# TASK CARD: Phase 4a — Phiếu thu/chi theo chuẩn tenant (TK HTX)

> **Master plan:** `docs/AI/plans/tt71-htx-accounting-master-plan.md`
> **Status:** ✅ COMPLETE 2026-10-02 (`0dfb848f` — pushed + CD Multi-VPS SUCCESS + RV markers PASS) · Q5 = CÓ (mẫu in → Phase 4b riêng: `task_phase4b_voucher_print.md`)

## 1. OBJECTIVE

`RevenueEntry.razor` + `ExpenseEntry.razor` dùng account options theo chuẩn kế toán của tenant (không hardcode DN).

## 2. HIỆN TRẠNG (mâu thuẫn với TT 71)

- `RevenueEntry.razor` `GetRevenueAccounts()`: **511/512/515/711** — 515, 711 KHÔNG tồn tại trong TT 71
- `ExpenseEntry.razor` `GetExpenseAccounts()`: **621/622/627/641/642** — TT 71 chỉ có 642 (+658, 611/612)

## 3. CHANGES

| File | Change |
|---|---|
| `5_WebApps/ShopERP/Components/Pages/Accounting/RevenueEntry.razor` | `GetRevenueAccounts()`: branch theo tenant standard/type — HTX: 511 (bên ngoài), 512 (nội bộ), 558 (thu nhập khác); DN: giữ 511/512/515/711; HKD: giữ hiện tại |
| `5_WebApps/ShopERP/Components/Pages/Accounting/ExpenseEntry.razor` | `GetExpenseAccounts()`: HTX: 642 (CP QLKD), 658 (chi phí khác) [+611/612 nếu duyệt]; DN/HKD: giữ |
| (MỚI — dùng chung) `5_WebApps/ShopERP/Services/Accounting/AccountingAccountProvider.cs` | Helper `GetRevenueAccounts(TenantType/AccountingStandard)` + `GetExpenseAccounts(...)` — tránh duplicate logic 2 page |

### Cách lấy standard của tenant

- `ITenantProvider` (tenant id) → `TenantManagementService.GetTenantByIdAsync` → `Type`/`AccountingStandard` (Phase 1 xong sẽ có HTX→TT71)
- Fallback nếu chưa có: `TenantType.HKD` → HKD (giữ); `Enterprise_*` → TT 133/99; HTX → TT 71
- Cache nhẹ (1 lần/load form) — không query lại mỗi render

## 4. UI PLATFORM

- Giữ `DynamicFormFields` + VanAButton/VanACard/VanAAlert — KHÔNG custom HTML/CSS
- Account select options thay đổi theo tenant là đủ (không đổi layout)
- HelpText của field "Tài Khoản" có thể thêm note chuẩn áp dụng (VD: "Theo TT 71/2024 — Chế độ kế toán HTX")

## 5. ACCEPTANCE

- [x] Tenant HTX: phiếu thu chỉ 511/512/558; phiếu chi chỉ 642/658 (T6 tests — không chứa 515/711/621/622/627/641)
- [x] Tenant DN/HKD: options KHÔNG đổi (T6 Enterprise tests — không regress)
- [x] ShopERP.Tests component render 2 page PASS (116 full suite)
- [x] Mẫu in chứng từ 01-TT/02-TT → Phase 4b (`task_phase4b_voucher_print.md`)

## 6. KẾT QUẢ (2026-10-02)

- `5_WebApps/ShopERP/Services/Accounting/AccountingAccountProvider.cs` (mới) — single source: `GetRevenueAccounts/GetExpenseAccounts/GetAccountHelpText(TenantType?)`; HTX → thu 511/512/558 · chi 642/658; DN/HKD giữ nguyên
- `RevenueEntry.razor`/`ExpenseEntry.razor`: inject `IVasFeatureFlagService` + `OnInitializedAsync` → `_tenantType` → options động + HelpText "Theo TT 71/2024 (Chế độ kế toán HTX)"
- 611/612 (giá vốn nội/ngoài) chưa đưa vào phiếu chi HTX — chờ user duyệt (D6 "nếu duyệt")

## 6. VERIFICATION

```powershell
dotnet build VanAn.sln
dotnet test 6_Tests\VanAn.ShopERP.Tests --filter "FullyQualifiedName~RevenueEntry|FullyQualifiedName~ExpenseEntry"
```
