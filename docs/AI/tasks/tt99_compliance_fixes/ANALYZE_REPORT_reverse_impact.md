# ANALYZE REPORT — TT 99 Compliance Fixes (Reverse Impact Review)

> **Status:** ✅ COMPLETE — 6 subagents verified all 6 task cards against codebase
> **Date:** 2026-08-03
> **Mode:** ANALYZE (read-only, no code changes)

---

## EXECUTIVE SUMMARY

6 task cards reviewed against actual codebase via parallel subagent investigation. **All 6 task cards are fundamentally accurate** but require updates to reflect verified reality:

| Phase | Accuracy | Critical Findings | Files Impact |
|-------|----------|-------------------|--------------|
| Phase 1 (Rename) | ✅ Accurate | 4 MISSING files (Sitemap + tests) | 7 files (was 3) |
| Phase 2 (Auto-standard) | ✅ Accurate | `IVasFeatureFlagService.GetTenantTypeAsync()` ALREADY EXISTS — no DTO change needed | 5 files |
| Phase 3 (Indirect method) | ✅ Accurate | Must inject IBalanceSheetService + IIncomeStatementService into CashFlowStatementService | 10 files |
| Phase 4 (Template structure) | ✅ Accurate | TT58 intentionally NOT seeded (correct); 11+ tests per service to update | 9+ files |
| Phase 5 (B 09-DN) | ⚠️ BLOCKER | Tenant missing LegalForm/BusinessField/CharterCapital — need TenantSettings extension | 8+ files |
| Phase 6 (BĐSĐT) | ⚠️ DRIFT | TK 5117/6327 MISSING from seeder; Mã số "75" is GUESS | 3 files |

---

## PHASE 1 — Rename B 01-DN: 4 FILES MISSING FROM TASK CARD

### Verified Accurate
- All 7 line numbers in task card are CORRECT
- All text strings match exactly
- `FinancialReportExportService.cs` does NOT hardcode title — accepts as parameter (task card was right)

### DRIFT: 4 additional files contain "Bảng Cân Đối Kế Toán"
| File | Line | Context |
|------|------|---------|
| `5_WebApps/ShopERP/Components/Pages/Sitemap.razor` | 154 | `<span>Bảng Cân Đối Kế Toán (B01-DN)</span>` — user-facing nav |
| `6_Tests/VanAn.ShopERP.Tests/Components/VasReports/BalanceSheetPageTests.cs` | 13, 22, 35 | Unit test assertions |
| `6_Tests/VanAn.ShopERP.Tests/Components/VasReports/FinancialReportsHubPageTests.cs` | 30 | Hub link text assertion |
| `6_Testing/e2e-tests/vas-export.spec.ts` | 26 | E2E heading assertion |

**Action:** Update task card to include these 4 files. Total: 7 files (was 3).

---

## PHASE 2 — Auto-Standard: SIMPLER THAN EXPECTED

### Verified Accurate
- All 4 report pages have `selectedStandard = TT133_2016` at claimed lines
- All 4 dropdowns have only 2 options (TT133 + TT99), no TT58
- `Tenant.Type` exists as `TenantType? Type` (nullable, private set)
- `TenantType` enum has 4 values matching task card
- `FinancialReports.razor` has 4 links in 1 card (needs split into 2 sections)

### KEY FINDING: `IVasFeatureFlagService.GetTenantTypeAsync()` ALREADY EXISTS
```csharp
// VasFeatureFlagService.cs lines 18-29
public interface IVasFeatureFlagService
{
    Task<bool> CanAccessVasReportsAsync(TenantId tenantId, CancellationToken ct = default);
    Task<TenantType?> GetTenantTypeAsync(TenantId tenantId, CancellationToken ct = default);  // ← USE THIS
    Task<bool> IsReadOnlyAsync(TenantId tenantId, CancellationToken ct = default);
}
```

**Impact:** Task card's Option A (add `TenantType` to `TenantApiDto`) is **NOT NEEDED**. Report pages can inject `IVasFeatureFlagService` directly (same as `AccountingLayout.razor` already does). This eliminates Gateway controller + DTO changes.

### CRITICAL: TT58 Intentionally NOT Seeded
`AccountChartSeeder.cs` line 12: "TT 58/2026 NOT seeded — TT 58 'bỏ hoàn toàn hệ thống tài khoản kế toán, thay bằng sổ theo dõi đơn giản hóa' (C5)"

**Impact:** Adding TT58 to dropdown is possible (enum exists) but **service calls with TT58_2026 will return empty** because no accounts are seeded. Two options:
- **Option A (recommended):** Don't add TT58 option — TT58 uses simplified tracking, not account-based reports. Display message "TT 58/2026 sử dụng sổ theo dõi đơn giản hóa, không áp dụng BCTC mẫu" instead.
- **Option B:** Seed TT58 accounts — but TT 99/2025 explicitly abandons account system for DN siêu nhỏ, so seeding would be semantically wrong.

**Action:** Update task card — remove TT58 dropdown option, replace with info message. Simpler implementation.

---

## PHASE 3 — Indirect Method: 10 FILES IMPACT + DEPENDENCY INJECTION

### Verified Accurate
- `CashFlowStatement` record at Domain.cs:3350-3356 — exact match
- `CashFlowStatementService` only has `GenerateAsync` (direct) — confirmed
- Service does NOT inject `IBalanceSheetService` or `IIncomeStatementService` — confirmed
- No UI toggle exists — confirmed
- `IncomeStatement` exposes `NetProfitEnding`/`NetProfitOpening` — confirmed

### DRIFT: Service Dependency Gap
Task card didn't specify that `CashFlowStatementService` constructor must be changed to inject:
- `IBalanceSheetService` — for account-level deltas (AR, Inventory, AP)
- `IIncomeStatementService` — for NetProfit starting point

### Reverse Impact: 10 files
| Priority | File | Change |
|----------|------|--------|
| 🔴 CRITICAL | `CashFlowStatementService.cs:120` | Add `Method: CashFlowMethod.Direct` to constructor |
| 🔴 CRITICAL | `VasReportPageTestBase.cs:132` | Add `Method: CashFlowMethod.Direct` to test mock |
| 🟡 REQUIRED | `ICashFlowStatementService.cs` | Add `GenerateIndirectAsync` signature |
| 🟡 REQUIRED | `CashFlowStatementService.cs` constructor | Inject `IBalanceSheetService` + `IIncomeStatementService` |
| 🟡 REQUIRED | `CashFlowStatement.razor` | Add method toggle |
| 🟢 OPTIONAL | `CashFlowStatementsController.cs` | Add `method` query param |
| 🟢 OPTIONAL | `FinancialReportExportService.cs` | Handle `IndirectAdjustments` |
| 🟢 TESTS | `CashFlowStatementServiceTests.cs` | Add indirect method tests |
| 🟢 TESTS | `CashFlowStatementPageTests.cs` | Add toggle tests |
| 🟢 TESTS | `VasReportsEndpointTests.cs` | Add API tests |
| 🟢 TESTS | `ArchitectureRulesTests.cs` | Verify new dependencies |

**Action:** Update task card with dependency injection + full 10-file impact list.

---

## PHASE 4 — Template Structure: ACCURATE, LARGE REFACTOR

### Verified Accurate
- All 3 services use **flat account list** (not TT99 template) — confirmed
- `BalanceSheetService`: flat per TK, AccountChart classification
- `IncomeStatementService`: flat per TK, AccountChart classification
- `CashFlowStatementService`: flat per offset account, hardcoded prefix rules
- TK 215, 332, 128 all seeded in TT99 — confirmed
- No `Tt99Template` or template mapping exists — confirmed
- TT58 intentionally NOT seeded (see Phase 2)

### Reverse Impact: Callers
- 3 UI pages (BalanceSheet.razor:190, IncomeStatement.razor:138, CashFlowStatement.razor:198)
- 3 API controllers (BalanceSheetsController:54, IncomeStatementsController:52, CashFlowStatementsController:51)
- `FinancialReportExportService` does NOT call services directly (receives data from UI)
- **11+ existing tests per service** (W4 + W7 series) — will need expected structure updates

### Test Inventory (will need updates)
- `BalanceSheetServiceTests.cs` — 11 tests (W4_BS1-6, W7_BS1-5)
- `IncomeStatementServiceTests.cs` — 11 tests (W4_IS1-6, W7_IS1-5)
- `CashFlowStatementServiceTests.cs` — 11 tests (W4_CF1-6, W7_CF1-5)
- `VasMultiTenantTests.cs` — covers all 3
- `ArchitectureRulesTests.cs:307-309` — references all 3
- 3 UI page tests

**Action:** Task card is accurate. Add test inventory + note that W7 tests (specific value assertions) will break.

---

## PHASE 5 — B 09-DN: 🔴 BLOCKER — TENANT MISSING FIELDS

### Verified Accurate
- `FinancialStatementNotes` record does NOT exist — confirmed (0 matches)
- `FinancialStatementNotesService` does NOT exist — confirmed
- `FinancialStatementNotes.razor` does NOT exist — confirmed (13 .razor files, none match)
- `ITenantManagementService.GetTenantByIdAsync` exists, returns `Task<Tenant?>` — confirmed
- `FinancialReportExportService` uses per-report methods (Docx+Xlsx pairs) — confirmed
- Program.cs DI pattern: `AddScoped<I, Impl>()` — confirmed
- `AccountingLayout.razor` NO menu change needed (hub already exists) — confirmed
- `FinancialReports.razor` is the hub — confirmed

### 🔴 BLOCKER: Tenant Missing 3 Fields for Phần I
Task card lines 136-138 reference `tenant.LegalForm`, `tenant.BusinessField`, `tenant.CharterCapital` — **these properties DO NOT EXIST** on Tenant entity.

**Current Tenant properties:**
- `Name`, `BusinessType`, `HKDGroup?`, `IndustrySector?`, `Type?`, `Status`, `Settings`
- `Settings` (TenantSettings value object): `ContactEmail`, `ContactPhone`, `Address`, `TaxCode`, `Slug`, `BrandStory`, etc.

**Missing:**
- `LegalForm` (Hình thức pháp lý) — e.g., "Công ty TNHH"
- `BusinessField` (Lĩnh vực kinh doanh) — e.g., "F&B"
- `CharterCapital` (Vốn điều lệ) — decimal VND

**Recommended Fix: Option B — Add to TenantSettings (owned value object, NO migration)**
```csharp
// TenantSettings — add 3 properties
public string? LegalForm { get; set; }
public string? BusinessField { get; set; }
public decimal? CharterCapital { get; set; }
```

**Action:** Add Phase 5a prerequisite task: extend TenantSettings. Update task card code snippet to use `tenant.Settings.LegalForm` etc.

---

## PHASE 6 — BĐSĐT: TK 5117/6327 MISSING + MÃ S UNVERIFIED

### Verified Accurate
- `CashFlowStatementService` has NO BĐSĐT handling — confirmed
- TK 217 exists in seeder (line 223 TT99) — confirmed
- `CashFlowStatement` record does NOT need change — confirmed
- `NetChange` = `ClosingCash - OpeningCash` (NOT sum of activities) — adding line has NO impact on totals — confirmed

### DRIFT: TK 5117 + 6327 MISSING from Seeder
Task card assumes these exist. They do NOT. Web search confirms they exist in official TT 99:
- TK 5117: "Doanh thu kinh doanh BĐSĐT" — MISSING
- TK 6327: "Giá vốn BĐSĐT" — MISSING
- TK 2147: "Hao mòn BĐSĐT" — MISSING (consider adding)

### OPEN QUESTION: Mã số "75" is a GUESS
Cannot verify without official Phụ lục IV file. Web search did not reveal exact Mã số.

**Action:** Update task card:
1. Add prerequisite: seed TK 5117, 6327 in `AccountChartSeeder.GetTt99Accounts()`
2. Flag Mã số "75" as UNVERIFIED — must confirm from official Phụ lục IV before implementation
3. Note: `NetChange` unaffected (safe to add line)

---

## CONSOLIDATED REVERSE IMPACT MATRIX

| Phase | Files to Modify | Files to Create | Tests to Update | New Tests |
|-------|----------------|----------------|----------------|-----------|
| Phase 1 | 7 | 0 | 3 | 0 |
| Phase 2 | 5 | 0 | 2 | 0 |
| Phase 3 | 6 | 0 | 4 | 3+ |
| Phase 4 | 6+ | 1 (Tt99Templates) | 11+ | 3+ |
| Phase 5 | 4 | 3 (record, service, page) | 0 | 5+ |
| Phase 6 | 3 | 0 | 1 | 2+ |
| **Phase 5a (NEW)** | 1 (TenantSettings) | 0 | 1 | 0 |
| **TOTAL** | **32+** | **4** | **22+** | **13+** |

---

## RECOMMENDED EXECUTION ORDER (REVISED)

```
Phase 5a (NEW — TenantSettings extension) ──┐
Phase 1 (Rename B 01-DN)                    │  Independent, parallel
Phase 2 (Auto-standard + split TrialBalance)│
Phase 6 (BĐSĐT — needs seeder update first) │
                                             ┘
Phase 3 (Indirect method) ← needs service DI change
Phase 4 (Template structure) ← depends on Phase 1+2 (naming + standard)
Phase 5 (B 09-DN) ← depends on Phase 5a (Tenant fields) + Phase 4 (template for Phần IV)
```

**Phase 5a is NEW prerequisite** for Phase 5 — must extend TenantSettings before B 09-DN can populate Phần I.

---

## OPEN QUESTIONS (REQUIRE USER INPUT)

1. **TT58 dropdown:** Add option with "not applicable" message, or skip entirely? (TT58 intentionally has no account system)
2. **BĐSĐT Mã số:** Can you provide official Phụ lục IV file, or should we use "75" as placeholder pending verification?
3. **TenantSettings extension (Phase 5a):** Approve adding `LegalForm`, `BusinessField`, `CharterCapital` to `TenantSettings` value object? (Option B — no migration, fits existing pattern)
4. **Phase 4 test strategy:** W7 tests have specific value assertions (e.g., `TotalAssetsEnding_HasSpecificValue`). Template refactor will change line structure but totals should remain. Acceptable to update W7 tests for new structure?
