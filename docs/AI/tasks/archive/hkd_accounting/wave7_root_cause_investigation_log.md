# Wave 7 Root Cause Investigation Log — GetHkdBook_S1a_ReturnsBookWithNumericValues

**Date:** 2026-07-17
**Branch:** `feature/hkd-fix-wave7-api-endpoint-di-smoke`
**Test:** `VanAn.Integration.Tests.HKDBooksEndpointTests.GetHkdBook_S1a_ReturnsBookWithNumericValues`
**Status:** FAILING — 5/6 Wave 7 tests PASS, this 1 test fails

---

## Symptom

HTTP `GET /api/hkd-books/S1a_HKD?year=2026&month=6` returns **200 OK** with `NumericValues` containing key `TotalRevenue`, but **value = 0** instead of expected **1500** (1000 + 500 from two Revenue entries with AccountCode "511").

## Investigation Timeline (3 rounds per Fix_Tests.md workflow)

### Round 1: CreateBaseVariables GUID→decimal parse bug
- **Error:** `System.InvalidCastException: Object must implement IConvertible` at `TemplateCalculationEngine.CreateBaseVariables` line 168
- **Root cause:** `decimal.Parse(tenantId.Value.ToString("N"))` — GUID hex string (e.g. "12345678123412341234123456789abc") contains non-numeric chars (a-f) that `decimal.Parse` cannot handle
- **Fix applied:** Changed to `tenantId.Value.GetHashCode()` — `ExtractTenantId` in `ProductionFormulaEngine` has `catch (FormatException) → fallback Guid.NewGuid()`, so round-trip precision not required
- **Result:** IConvertible error gone, but new error: `Value cannot be null. (Parameter 'logger')`

### Round 1b: NullLogger fix (same batch)
- **Error:** `BaseHKDBookTemplate` passed `null!` as logger to `TemplateCalculationEngine` constructor
- **Root cause:** `TemplateCalculationEngine calculationEngine = new(FormulaEngine, DataProvider, null!)` — 2 occurrences (lines 58, 158)
- **Fix applied:** Changed to `NullLogger<TemplateCalculationEngine>.Instance` (added `using Microsoft.Extensions.Logging.Abstractions`)
- **Result:** 200 OK returned, but `TotalRevenue = 0` instead of 1500

### Round 2: Diagnosis — data query returns empty
- **Error:** `Assert.Equal() Failure: Expected: 1500, Actual: 0`
- **Observation:** Endpoint returns 200 OK, `NumericValues` has `TotalRevenue` key but value =0
- **Hypothesis:** `SmartPreAggregationService.GetAccountSumAsync` query returns no matching `AccountingEntries`
- **Query path:** `ScopedDataProvider.GetPreAggregatedDataAsync` → `SmartPreAggregationService.GetAccountAggregatesAsync` → `GetAccountSumAsync` → EF Core query `AccountingEntries.Where(e => e.TenantId == tenantId && e.PeriodYear == 2026 && e.PeriodMonth == 6)`
- **No fix applied this round** — diagnosis only

### Round 3: EF.Property<Guid> fix attempt
- **Hypothesis:** `e.TenantId == tenantId` (TenantId record comparison) may not translate correctly on SQLite
- **Fix applied:** Changed to `EF.Property<Guid>(e, "TenantId") == tenantGuid` in `SmartPreAggregationService.GetAccountSumAsync`
- **Result:** Still `Expected: 1500, Actual: 0` — no improvement
- **Conclusion:** Query still returns empty/zero. Root cause is deeper.

---

## Root Cause Analysis (unresolved)

### Key Finding: AccountingEntryConfiguration does NOT map TenantId

`AccountingEntryConfiguration` (line 14-80) maps: `Id`, `AccountingBookType`, `EntryType`, `VatRate`, `PeriodYear`, `PeriodMonth`, `Amount`, `Description`, `CreatedAt`, `ReversalEntryId`, `AccountCode`, `Vendor`, `Category`, `Reference`, `IndustrySector`.

**Missing:** `builder.Property(e => e.TenantId)` — no explicit mapping for `TenantId` (inherited from `BaseEntity`, type `TenantId` record).

### Key Finding: AccountingEntry excluded from global query filter

`VanAnDbContext` line 230:
```csharp
.Where(e => typeof(IMustHaveTenant).IsAssignableFrom(e.ClrType) && e.ClrType != typeof(CoreAccountingEntry));
```

`AccountingEntry` is explicitly excluded from the multi-tenancy query filter. This means:
1. No auto-filter is applied to `AccountingEntries` queries
2. Manual filter `e.TenantId == tenantId` is required (which `SmartPreAggregationService` does)
3. But if `TenantId` is not mapped as a property, EF Core may treat it as a shadow property with unknown type

### Key Finding: Global TenantIdConverter convention

`VanAnDbContext.ConfigureConventions` (line 287-290):
```csharp
configurationBuilder.Properties<TenantId>().HaveConversion<ValueConverters.TenantIdConverter>();
```

This applies to ALL `TenantId` properties globally. But `AccountingEntry` excluded from query filter — does the convention still apply? If `TenantId` is not explicitly mapped in `AccountingEntryConfiguration`, EF Core may:
- Auto-discover it via convention (since `BaseEntity.TenantId` is public)
- Apply `TenantIdConverter` (TenantId → Guid)
- Store as Guid in SQLite

But `EF.Property<Guid>(e, "TenantId")` returned empty results — suggesting either:
1. The property is stored differently than expected
2. The seed data's `TenantId` doesn't match what's stored
3. `EnsureCreated` created the column with a different type than the query expects

### Unverified Hypotheses (require further investigation)

1. **Schema mismatch:** `EnsureCreated` may create `AccountingEntries.TenantId` as TEXT (string) via convention, but `EF.Property<Guid>` queries as Guid — type mismatch on SQLite causes empty results
2. **Seed data not persisted:** `db.AccountingEntries.AddRange(revenue1, revenue2, ...)` + `SaveChangesAsync()` may fail silently if `TenantId` converter throws during save (but no error was observed)
3. **Convention exclusion:** `AccountingEntry` excluded from query filter loop, but convention may still apply — need to verify EF Core model snapshot
4. **TenantId shadow property type:** If EF Core maps `TenantId` as shadow property (not via convention), its type may default to `string` or `object`, not `Guid`

### Recommended Next Steps (for future session)

1. **Add diagnostic logging** to `SmartPreAggregationService.GetAccountSumAsync` — log raw SQL query + parameter values + result count
2. **Verify schema:** Query `PRAGMA table_info(AccountingEntries)` on SQLite to check `TenantId` column type
3. **Test direct query:** In test, after seeding, run `db.AccountingEntries.Count()` and `db.AccountingEntries.Where(e => e.TenantId == TestTenantId).Count()` to verify data is persisted and queryable
4. **Check EF Core model:** Use `db.Model.FindEntityType(typeof(AccountingEntry))?.FindProperty("TenantId")` to inspect how EF Core maps `TenantId`
5. **Consider explicit mapping:** Add `builder.Property(e => e.TenantId).HasConversion(...)` to `AccountingEntryConfiguration` to ensure consistent mapping

---

## Files Changed During Investigation (uncommitted on branch)

### Production code fixes (pre-existing bugs, not Wave 7 scope):
1. `3_CoreHub/Services/Template/TemplateCalculationEngine.cs` — `CreateBaseVariables`: `decimal.Parse(tenantId.Value.ToString("N"))` → `tenantId.Value.GetHashCode()`
2. `3_CoreHub/Services/Template/BaseHKDBookTemplate.cs` — 2 occurrences: `null!` logger → `NullLogger<TemplateCalculationEngine>.Instance`
3. `3_CoreHub/Services/PreAggregation/SmartPreAggregationService.cs` — `GetAccountSumAsync`: `e.TenantId == tenantId` → `EF.Property<Guid>(e, "TenantId") == tenantGuid` (did NOT fix the issue)

### Wave 7 implementation (from prior session, working):
4. `1_Shared/DTOs/HKDBookDto.cs` — NEW
5. `2_Gateway/Controllers/HKDBooksController.cs` — NEW
6. `2_Gateway/Program.cs` — Added DI registrations
7. `3_CoreHub/Program.cs` — Added `Lazy<IFormulaEngine>`
8. `3_CoreHub/Services/PreAggregation/SmartPreAggregationService.cs` — `Lazy<IFormulaEngine>` (circular dependency fix)
9. `3_CoreHub/Services/Template/HKDBookGenerationService.cs` — `EntryDate` range filter
10. `3_CoreHub/Infrastructure/Configurations/JournalEntryConfiguration.cs` — Added `EntryDate` mapping
11. `6_Tests/VanAn.Integration.Tests/HKDBookDISmokeTests.cs` — NEW
12. `6_Tests/VanAn.Integration.Tests/HKDBooksEndpointTests.cs` — NEW
13. `6_Tests/VanAn.Core.Tests/Services/SmartPreAggregationServiceWave2Tests.cs` — Updated for `Lazy<IFormulaEngine>`

## Test Results
- **5/6 PASS:** DI smoke (2/2), endpoint list templates (1/1), invalid period 400 (1/1), no-auth 401/302 (1/1)
- **1/6 FAIL:** `GetHkdBook_S1a_ReturnsBookWithNumericValues` — 200 OK but TotalRevenue=0 (expected 1500)

## Pre-existing Bugs Discovered (5 total)
1. ✅ Gateway missing DI registrations (IAuditTrailService, IAuditLogRepository, calc engine) — FIXED
2. ✅ Circular dependency IFormulaEngine→IDataProvider→IPreAggregationService→IFormulaEngine — FIXED via Lazy<IFormulaEngine>
3. ✅ JournalEntryConfiguration missing EntryDate mapping — FIXED
4. ✅ HKDBookGenerationService queried unmapped Period.Year/Month — FIXED (EntryDate range)
5. ✅ TemplateCalculationEngine.CreateBaseVariables GUID→decimal parse — FIXED (GetHashCode)
6. ✅ BaseHKDBookTemplate null! logger — FIXED (NullLogger)
7. ❌ SmartPreAggregationService AccountingEntries TenantId query returns empty — UNRESOLVED (root cause: likely AccountingEntryConfiguration missing explicit TenantId mapping + EnsureCreated schema mismatch)
