# W3 FIX PLAN — Detailed Coding Plan for Review

> **Date:** 2026-07-04
> **Branch:** `feature/vas-wave3-account-code-map`
> **Status:** PLAN — awaiting user approval before IMPLEMENT
> **Trigger:** W3 review found 5 CRITICAL + 4 GAPS + 3 MINOR issues

---

## 0. Issue Summary (from Review + Research)

### CRITICAL (Blocks production)

| # | Issue | Root Cause | Impact |
|---|-------|-----------|--------|
| C1 | Seeder not called from startup | `AccountChartSeeder.SeedAsync()` defined but never invoked | AccountCharts table empty → all lookups return fallback/null |
| C2 | AccountChartService depends on `VanAnDbContext` (not registered in ShopERP DI) | ShopERP only registers `ShopERPDbContext` + `IVanAnDbContext` | DI resolution throws at runtime when IAccountChartService is requested |
| C3 | `IVanAnDbContext` + `ShopERPDbContext` missing `AccountCharts` DbSet | W3 only added DbSet to `VanAnDbContext`, not to interface or ShopERP impl | No query access from ShopERP runtime |
| C4 | TT 133 has 4 WRONG accounts | 311 (removed in TT 133), 213 (is 2113 sub-account in TT 133), 641 (is 6421 sub-account in TT 133), 521 (removed in TT 133 — discounts go to 511) | Incorrect chart for DN vừa/nhỏ |
| C5 | TT 58/2026 doesn't have a formal chart of accounts | TT 58 "bỏ hoàn toàn hệ thống tài khoản kế toán, thay bằng sổ theo dõi đơn giản hóa" (source: fast.com.vn, amis.misa.vn) | TT 58 seeder is fabricated data |

### GAPS (Code doesn't meet task card spec)

| # | Issue | Spec | Current |
|---|-------|------|---------|
| G1 | No unit tests | Task card has 9 verification checkboxes | 0 tests |
| G2 | TT 133 incomplete | 47 level-1 accounts per TT 133/2016 | 35 (4 wrong + 18 missing) |
| G3 | TT 99 missing TK 332 | F8: "include new TK 33x" — verified TK 332 "Phải trả cổ tức, lợi nhuận" (NEW, split from 338) | 70 accounts (missing 332) |
| G4 | Mapper/Chart granularity mismatch | Mapper outputs 3331, 1331 (level-2); Chart only has 333, 133 (level-1) | W8 lookup would return null |

### MINOR

| # | Issue | Fix |
|---|-------|-----|
| M4 | TT 133 duplicate labels (311 + 341 both "Vay và nợ thuê tài chính") | Remove 311 (C4 fix) |
| M5 | TT 133 155 label "Sản phẩm" (that's TT 99) | Change to "Thành phẩm" |
| M6 | TT 133 411 label "Vốn đầu tư của chủ sở hữu" (that's TT 99) | Change to "Nguồn vốn kinh doanh" |

---

## 1. Fix Plan — Task by Task

### FIX-1: IVanAnDbContext + ShopERPDbContext + AccountChartService (C2 + C3)

**Files to modify:**
- `3_CoreHub/Infrastructure/IVanAnDbContext.cs` — ADD `DbSet<AccountChartEntity> AccountCharts { get; }`
- `5_WebApps/ShopERP/Infrastructure/ShopERPDbContext.cs` — ADD `DbSet<AccountChartEntity> AccountCharts { get; set; }`
- `3_CoreHub/Services/AccountChartService.cs` — CHANGE `VanAnDbContext` → `IVanAnDbContext`
- `3_CoreHub/Services/IAccountChartService.cs` — no change (interface already correct)

**Details:**

`IVanAnDbContext.cs` — add after `PushSubscriptions`:
```csharp
// W3: VAS Account Chart reference data
DbSet<VanAn.CoreHub.Infrastructure.Entities.AccountChartEntity> AccountCharts { get; }
```

`ShopERPDbContext.cs` — add after `PushSubscriptions`:
```csharp
// W3: VAS Account Chart reference data
public DbSet<VanAn.CoreHub.Infrastructure.Entities.AccountChartEntity> AccountCharts { get; set; }
```

`AccountChartService.cs` — change constructor:
```csharp
// BEFORE:
private readonly VanAnDbContext _dbContext;
public AccountChartService(VanAnDbContext dbContext, ...)

// AFTER:
private readonly IVanAnDbContext _dbContext;
public AccountChartService(IVanAnDbContext dbContext, ...)
```

**Why:** ShopERP uses `ShopERPDbContext` (implements `IVanAnDbContext`), NOT `VanAnDbContext`. All CoreHub services consumed by ShopERP must depend on `IVanAnDbContext`. Precedent: `SmartPreAggregationService`, `HKDBookGenerationService`, `TenantOnboardingService`.

**Schema impact:** `ShopERPDbContext` uses `EnsureCreatedAsync()` → table auto-created from model. `AccountChartConfiguration` is auto-discovered via `ApplyConfigurationsFromAssembly` (already configured in ShopERPDbContext.OnModelCreating line 149).

---

### FIX-2: AccountChartSeeder — use IVanAnDbContext + add CleanupAsync (C1)

**Files to modify:**
- `3_CoreHub/Infrastructure/Seed/AccountChartSeeder.cs` — CHANGE `VanAnDbContext` → `IVanAnDbContext` + ADD `CleanupAsync`
- `5_WebApps/ShopERP/Program.cs` — ADD seeder call in startup scope

**Seeder changes:**
```csharp
// Change all VanAnDbContext → IVanAnDbContext
public static async Task<int> SeedAsync(IVanAnDbContext dbContext, ...)

// ADD CleanupAsync (clear-old-seed-data mechanism per user request):
public static async Task CleanupAsync(IVanAnDbContext db, CancellationToken ct = default)
{
    var all = await db.AccountCharts.ToListAsync(ct);
    db.AccountCharts.RemoveRange(all);
    await db.SaveChangesAsync(ct);
}
```

**Program.cs startup hook** (add after existing seed block, ~line 386):
```csharp
// W3: Seed AccountChart reference data (clear + reseed to ensure chart matches code)
CoreHub.Infrastructure.IVanAnDbContext vanAnContext = scope.ServiceProvider.GetRequiredService<CoreHub.Infrastructure.IVanAnDbContext>();
await CoreHub.Infrastructure.Seed.AccountChartSeeder.CleanupAsync(vanAnContext);
int accountChartCount = await CoreHub.Infrastructure.Seed.AccountChartSeeder.SeedAsync(vanAnContext);
Console.WriteLine($"W3: AccountChart reference data seeded — {accountChartCount} accounts across 3 standards");
```

**Clear + Reseed rationale (per user request):**
- Reference data MUST match code (not user-editable)
- Clear+Reseed ensures label corrections / account additions propagate on every startup
- Idempotent skip (current approach) would NOT fix wrong labels or remove deleted accounts
- Transaction not needed — happens before any HTTP request, and AccountCharts has no FK dependencies

---

### FIX-3: TT 133 chart corrections (C4 + G2 + M4 + M5 + M6)

**File:** `3_CoreHub/Infrastructure/Seed/AccountChartSeeder.cs` — `GetTt133Accounts()`

**REMOVE (4 accounts — not in TT 133):**
| Code | Reason |
|------|--------|
| 311 | Removed in TT 133 (replaced by 341) |
| 213 | Is 2113 sub-account in TT 133 (not level-1) |
| 641 | Is 6421 sub-account in TT 133 (not level-1; TT 133 gộp 641+642 → 642) |
| 521 | Removed in TT 133 (discounts go to 511 directly — W1 seeder note #3) |

**ADD (18 missing accounts — per TT 133/2016 Phụ lục II, source: baocaotaichinh.vn):**
| Code | Name | Type | IsNormalCredit | Group |
|------|------|------|---------------|-------|
| 121 | Chứng khoán kinh doanh | Asset | false | Đầu tư TC |
| 128 | Đầu tư nắm giữ đến ngày đáo hạn | Asset | false | Đầu tư TC |
| 151 | Hàng mua đang đi đường | Asset | false | Hàng tồn kho |
| 154 | Chi phí SXKD dở dang | Asset | false | Hàng tồn kho |
| 155 | Thành phẩm | Asset | false | Hàng tồn kho (M5: fix label) |
| 157 | Hàng gửi đi bán | Asset | false | Hàng tồn kho |
| 217 | Bất động sản đầu tư | Asset | false | TSCĐ |
| 228 | Đầu tư khác | Asset | false | Đầu tư vốn |
| 336 | Phải trả nội bộ | Liability | true | Khoản phải trả |
| 338 | Phải trả, phải nộp khác | Liability | true | Khoản phải trả |
| 352 | Dự phòng phải trả | Liability | true | Quỹ |
| 353 | Quỹ khen thưởng, phúc lợi | Liability | true | Quỹ |
| 356 | Quỹ phát triển KH&CN | Liability | true | Quỹ |
| 413 | Chênh lệch tỷ giá hối đoái | Equity | true | Vốn CSH |
| 418 | Các quỹ khác thuộc VCSH | Equity | true | Vốn CSH |
| 419 | Cổ phiếu quỹ | Equity | true | Vốn CSH |
| 611 | Mua hàng | Expense | false | Chi phí SXKD |
| 631 | Giá thành sản xuất | Expense | false | Chi phí SXKD |

**FIX LABELS (2 accounts):**
| Code | Current (WRONG) | Correct | Reason |
|------|-----------------|---------|--------|
| 155 | "Sản phẩm" | "Thành phẩm" | M5: "Sản phẩm" is TT 99 name; TT 133 uses "Thành phẩm" |
| 411 | "Vốn đầu tư của chủ sở hữu" | "Nguồn vốn kinh doanh" | M6: TT 133 name is "Nguồn vốn kinh doanh" |

**Result:** TT 133 = 47 level-1 accounts (matches TT 133/2016 Phụ lục II)

---

### FIX-4: TT 99 chart correction — add TK 332 (G3)

**File:** `3_CoreHub/Infrastructure/Seed/AccountChartSeeder.cs` — `GetTt99Accounts()`

**ADD (1 account — NEW in TT 99/2025, split from 338):**
| Code | Name | Type | IsNormalCredit | Source |
|------|------|------|---------------|--------|
| 332 | Phải trả cổ tức, lợi nhuận | Liability | true | ketoan.vn — "TK 332 là tài khoản mới được quy định trong TT 99/2025/TT-BTC, tách từ TK 338" |

**Position:** After 331, before 333 (natural ordering in 33x group)

**Result:** TT 99 = 71 level-1 accounts (matches TT 99/2025 spec)

---

### FIX-5: TT 58 chart removal (C5)

**File:** `3_CoreHub/Infrastructure/Seed/AccountChartSeeder.cs`

**Action:** REMOVE `GetTt58Accounts()` method + remove TT 58 from `SeedAsync` loop.

**Rationale:**
- TT 58/2026 "bỏ hoàn toàn mô hình Nhật ký sổ cái (S01) và hệ thống tài khoản kế toán, thay bằng các sổ theo dõi đơn giản hóa gắn trực tiếp với nghĩa vụ thuế" (source: fast.com.vn, amis.misa.vn)
- TT 58 DN siêu nhỏ use simplified tax books, NOT a double-entry chart of accounts
- DN siêu nhỏ that need BCTC use TT 133's chart (TT 133 applies to "doanh nghiệp nhỏ và vừa bao gồm cả doanh nghiệp siêu nhỏ")
- `AccountingStandard.TT58_2026` enum value stays (for feature flagging/report format), but no AccountChart entries
- `AccountChartService.GetAllAccountsAsync(TT58_2026)` returns empty list (correct — no chart)

**Result:** Seeder seeds 2 standards (TT 133 + TT 99) = 47 + 71 = 118 accounts

---

### FIX-6: Mapper/Chart granularity reconciliation (G4)

**File:** `3_CoreHub/Infrastructure/Seed/AccountChartSeeder.cs`

**Action:** Add level-2 accounts 3331 + 1331 to BOTH TT 133 and TT 99 seeders.

**Accounts to add:**
| Code | Name | Type | IsNormalCredit | Standards | Reason |
|------|------|------|---------------|-----------|--------|
| 3331 | Thuế GTGT đầu ra | Liability | true | TT 133 + TT 99 | Mapper outputs "3331" for TaxOutput |
| 1331 | Thuế GTGT đầu vào | Asset | false | TT 133 + TT 99 | Mapper outputs "1331" for TaxInput |

**Rationale:**
- `HkdToEnterpriseAccountMapper` maps TaxOutput→"3331" and TaxInput→"1331" (level-2 codes)
- Without these in the chart, `GetAccountAsync("3331", ...)` returns null
- W8 conversion service would fail to look up account names for tax accounts
- These are universally used across all DN standards (VAT is mandatory)

**Result:** TT 133 = 49 accounts, TT 99 = 73 accounts, Total = 122 accounts

---

### FIX-7: Unit tests (G1)

**Files to create:**
- `6_Tests/VanAn.Core.Tests/Services/AccountChartServiceTests.cs` — 5 tests
- `6_Tests/VanAn.Core.Tests/Seed/AccountChartSeederTests.cs` — 4 tests
- `6_Tests/VanAn.Core.Tests/Services/HkdToEnterpriseAccountMapperTests.cs` — 3 tests

**Test specs:**

#### AccountChartServiceTests.cs (5 tests — covers task card verification checkboxes 1-5)
```
W3-AC1: GetAccountNameAsync("511", TT133_2016) returns "Doanh thu bán hàng và cung cấp dịch vụ"
W3-AC2: GetAccountTypeAsync("511", TT133_2016) returns Revenue
W3-AC3: GetAccountAsync("214", TT133_2016) returns entry with Type=Asset, IsNormalCredit=true (J1+J2)
W3-AC4: GetAccountAsync("521", TT99_2025) returns entry with Type=Revenue, IsNormalCredit=false (F9 contra-revenue)
        NOTE: 521 is in TT 99 (NOT TT 133 — removed in TT 133)
W3-AC5: GetAccountNameAsync("999", TT133_2016) returns fallback "Tài khoản 999" (not found)
```

#### AccountChartSeederTests.cs (4 tests — covers seeder + cleanup)
```
W3-SE1: SeedAsync creates expected account counts (TT133=49, TT99=73, total=122)
W3-SE2: CleanupAsync + SeedAsync is idempotent (clear+reseed produces same count)
W3-SE3: SeedAsync seeds TT 133 first (R3 priority — verify order via logger mock or sequential check)
W3-SE4: No duplicate AccountCode per Standard (unique constraint verified)
```

#### HkdToEnterpriseAccountMapperTests.cs (3 tests — covers task card verification checkboxes 6-7)
```
W3-MP1: MapToEnterpriseAccount("Revenue", TT133_2016) returns "511"
W3-MP2: MapToEnterpriseAccount("Depreciation", TT133_2016) returns "214"
W3-MP3: MapToEnterpriseAccount("Unknown", TT133_2016) throws KeyNotFoundException
```

**Pattern:** Follow `VasSampleDataSeederTests.cs` — use `VanAnDbContextTestFactory.Create()`, seed before service tests.

---

### FIX-8: Update task card with findings

**File:** `docs/AI/tasks/vas_wave3_task_card.md`

**Updates:**
- Verification section: update checkboxes with test IDs (W3-AC1 through W3-MP3)
- F9 note: clarify 521 is in TT 99 only (NOT TT 133 — removed)
- F8 note: update "TK 33x" → "TK 332 (Phải trả cổ tức, lợi nhuận — verified)"
- Add C5 finding: TT 58 has no chart of accounts
- Add C2/C3 finding: IVanAnDbContext dependency
- Update account counts: TT 133=49, TT 99=73, TT 58=0 (no chart)

---

## 2. Execution Order

```
FIX-1 (IVanAnDbContext + ShopERPDbContext + Service) ─┐
                                                       ├─ FIX-3 (TT 133 corrections)
FIX-2 (Seeder IVanAnDbContext + CleanupAsync)         ─┤   FIX-4 (TT 99 add 332)
                                                       ├─ FIX-5 (TT 58 removal)
                                                       ├─ FIX-6 (Add 3331+1331)
                                                       └─ FIX-7 (Unit tests)
                                                            FIX-8 (Task card update)
```

FIX-1 and FIX-2 are prerequisites (DI + DbContext must work before seeder can run).
FIX-3 through FIX-6 are independent seeder data corrections (can be done in one pass).
FIX-7 depends on all data fixes being complete.
FIX-8 is documentation (last).

---

## 3. Verification Gate (after all fixes)

1. `dotnet build VanAn.sln` → 0 errors
2. `guard-check.ps1` → ALL CHECKS PASSED
3. `dotnet test 6_Tests/VanAn.Core.Tests` → all pass (854 existing + 12 new = 866)
4. `dotnet test 6_Tests/VanAn.Architecture.Tests` → 31/31 pass
5. Manual: run ShopERP, verify console output "W3: AccountChart reference data seeded — 122 accounts"
6. Manual: query AccountCharts table, verify TT 133 has 49 rows, TT 99 has 73 rows, TT 58 has 0 rows

---

## 4. Risk Assessment

| Risk | Likelihood | Mitigation |
|------|-----------|------------|
| ShopERPDbContext EnsureCreated doesn't create AccountCharts table | Low | DbSet added + Configuration auto-discovered via ApplyConfigurationsFromAssembly |
| Clear+Reseed causes data loss on production | Very Low | AccountCharts is reference data (no user edits), clear+reseed is safe |
| TT 133 account list has errors | Low | Sourced from baocaotaichinh.vn (official TT 133/2016 Phụ lục II) |
| TT 58 removal breaks W4 | Low | W4 not started yet; TT 58 report format is separate from chart lookup |
| IVanAnDbContext change breaks existing tests | Low | Tests use VanAnDbContext directly (not via DI), only service tests need IVanAnDbContext mock |

---

## 5. Files Changed Summary

| File | Action | Fix # |
|------|--------|-------|
| `3_CoreHub/Infrastructure/IVanAnDbContext.cs` | MODIFY — add AccountCharts DbSet | FIX-1 |
| `5_WebApps/ShopERP/Infrastructure/ShopERPDbContext.cs` | MODIFY — add AccountCharts DbSet | FIX-1 |
| `3_CoreHub/Services/AccountChartService.cs` | MODIFY — VanAnDbContext → IVanAnDbContext | FIX-1 |
| `3_CoreHub/Infrastructure/Seed/AccountChartSeeder.cs` | MODIFY — IVanAnDbContext + CleanupAsync + TT 133 fixes + TT 99 332 + TT 58 removal + 3331/1331 | FIX-2,3,4,5,6 |
| `5_WebApps/ShopERP/Program.cs` | MODIFY — add seeder call in startup | FIX-2 |
| `6_Tests/VanAn.Core.Tests/Services/AccountChartServiceTests.cs` | CREATE — 5 tests | FIX-7 |
| `6_Tests/VanAn.Core.Tests/Seed/AccountChartSeederTests.cs` | CREATE — 4 tests | FIX-7 |
| `6_Tests/VanAn.Core.Tests/Services/HkdToEnterpriseAccountMapperTests.cs` | CREATE — 3 tests | FIX-7 |
| `docs/AI/tasks/vas_wave3_task_card.md` | MODIFY — update findings + verification | FIX-8 |

**Total: 4 modified + 3 created + 1 doc = 8 files**
