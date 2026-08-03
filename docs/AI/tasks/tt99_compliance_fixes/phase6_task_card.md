# TASK CARD — Phase 6: B 03-DN Phân Loại Dòng Tiền BĐSĐT (REVISED)

> **Status:** 🟡 PLANNED (REVISED 2026-08-03 after official template verification)
> **Priority:** P2 — TT 99 compliance
> **Branch:** `feature/tt99-fix-phase6-bdsdt-classification`
> **Estimated sessions:** 1
> **Mode:** IMPLEMENT
> **Domain modification:** NO (service logic only)
> **Reference:** `REFERENCE_B03DN_official.md` — VERIFIED from Phụ lục IV TT 99

## Objective (REVISED)

**Original task card (WRONG):** Add BĐSĐT indicator with Mã "75" to B 03-DN operating activities.

**Verified reality:** TT 99 Phụ lục IV does NOT have Mã 75 in B 03-DN. BĐSĐT is handled via:
1. **B 02-DN Mã 21** — "Lãi/lỗ của hoạt động bán, thanh lý BĐS đầu tư" (separate line in income statement, NOT cash flow)
2. **B 03-DN indirect Mã 02** — "Khấu hao TSCĐ **và BĐSĐT**" (depreciation adjustment includes BĐSĐT)
3. **B 03-DN direct Mã 21/22** — Investing activity: "Tiền chi mua sắm, xây dựng TSCĐ và TSDH khác" / "Tiền thu từ thanh lý, nhượng bán TSCĐ và TSDH khác" (BĐSĐT flows through here as long-term asset)
4. **B 01-DN Mã 240** — Balance sheet: "Bất động sản đầu tư" (separate section, not under TSCĐ)

**Revised objective:** Ensure `CashFlowStatementService` correctly classifies BĐSĐT cash flows:
- **BĐSĐT purchases/sales → INVESTING activity** (Mã 21/22), NOT operating
- **BĐSĐT depreciation → indirect method Mã 02** (already covered by TK 214)
- **BĐSĐT revenue/cost → B 02-DN Mã 21** (income statement, handled in Phase 4 template)

## Prerequisites
- [ ] Verify `CashFlowStatementService.ClassifyAccount()` currently classifies TK 217 as Investing (lines 137-140 — confirmed via ANALYZE)
- [ ] Verify TK 5117/6327 seeded in AccountChartSeeder (Phase 6 prerequisite: add if missing)
- [ ] Verify TT 99 Phụ lục IV B 03-DN template (`REFERENCE_B03DN_official.md`)

## Files to Modify
| File | Changes |
|------|---------|
| `3_CoreHub/Infrastructure/Seed/AccountChartSeeder.cs` | **ADD**: TK 5117, TK 6327 (BĐSĐT revenue/cost sub-accounts) |
| `3_CoreHub/Services/CashFlowStatementService.cs` | Verify TK 217 → Investing (already correct); add TK 5117/6327 → Operating (revenue/expense, not BĐSĐT asset) |
| `5_WebApps/ShopERP/Components/Pages/Accounting/CashFlowStatement.razor` | No change (auto-display via template) |

## Detailed Changes

### Change 1: Seed TK 5117 + 6327 (BĐSĐT sub-accounts)
```csharp
// AccountChartSeeder.GetTt99Accounts() — add after existing revenue/expense accounts
// TK 5117: Doanh thu kinh doanh BĐS đầu tư (sub-account of 511)
yield return ("5117", "Doanh thu kinh doanh BĐS đầu tư", AccountType.Revenue, true);
// TK 6327: Giá vốn BĐS đầu tư (sub-account of 632)
yield return ("6327", "Giá vốn BĐS đầu tư", AccountType.Expense, false);
```

### Change 2: Verify CashFlowStatementService classification
```csharp
// CashFlowStatementService.ClassifyAccount() — current (lines 134-155):
// TK 217 → already classified as INVESTING (via "217" prefix match + "21" catch-all)
// This is CORRECT per TT 99 — BĐSĐT is a long-term asset, cash flows go to investing

// ADD verification comment:
// TT 99 B 03-DN: BĐSĐT (TK 217) cash flows → Investing (Mã 21/22)
// BĐSĐT revenue (TK 5117) + cost (TK 6327) → Operating (revenue/expense accounts)
// BĐSĐT depreciation (TK 2147) → Indirect method Mã 02 adjustment (via TK 214 catch-all)
// NOTE: No separate BĐSĐT line in B 03-DN — flows through standard Mã 21/22 (investing) + Mã 02 (indirect depreciation)
```

### Change 3: No UI change needed
`CashFlowStatement.razor` renders via `FinancialStatementLine` collection — if BĐSĐT cash flows are correctly classified into Investing activities (Mã 21/22), they auto-display. No new line needed.

## Verification
- [ ] `dotnet build VanAn.sln` Release — 0 errors
- [ ] TK 5117 + 6327 seeded in TT 99 accounts
- [ ] TK 217 cash flows appear in Investing Activities (Mã 21/22), NOT Operating
- [ ] TK 5117/6327 cash flows appear in Operating Activities (revenue/expense)
- [ ] No Mã "75" anywhere (confirmed: TT 99 does NOT have this Mã)

## Rollback
`git revert <commit>` — seeder + service logic only.

## Notes
- **Original task card was WRONG**: Mã "75" does not exist in TT 99 B 03-DN.
- **BĐSĐT handling is distributed** across 3 reports:
  - B 01-DN Mã 240 (balance sheet — asset)
  - B 02-DN Mã 21 (income statement — revenue/cost)
  - B 03-DN Mã 21/22 (cash flow — investing) + Mã 02 indirect (depreciation)
- **Phase 4 template refactor** (B 02-DN + B 03-DN) will handle BĐSĐT Mã correctly via template definitions.
- **This phase** focuses only on: (1) seeding TK 5117/6327, (2) verifying classification is correct.
