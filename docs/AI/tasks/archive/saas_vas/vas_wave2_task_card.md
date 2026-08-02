# TASK CARD — VAS Wave 2: Domain Records

> **Status:** INVESTIGATE DONE → PLAN APPROVED PENDING → IMPLEMENT (await user approval)
> **Prerequisite:** W1 merged to main (`93a5e7e`) · **Domain modification APPROVED (D5)**
> **Branch:** `feature/vas-wave2-domain-records` (to create from `main`)
> **Estimated sessions:** 1
> **Last INVESTIGATE:** 2026-07-04 — all 9 facts verified, 0 drift

## Objective
Add Domain records cho 4 BCTC + OpeningBalance + AccountChart + TenantType enum + **HKD↔DN conversion fields (D9 — Option B: New Tenant + Link)**.

## Prerequisites (verified 2026-07-04)
- [x] W1 merged to main (commit `93a5e7e`, seed data available)
- [x] `1_Shared/Domain.cs` exists — 2024 lines, namespace `VanAn.Shared.Domain`, EF Core/DataAnnotations already commented out (purity intact)
- [x] `TrialBalance` record exists at Domain.cs:1518 (NO `TenantId` field — keep as-is, W4 service will wrap)
- [x] **No collision** — grep confirms 0 matches for `TenantType`/`AccountingStandard`/`AccountType`/`BalanceSheet`/`IncomeStatement`/`CashFlow`/`OpeningBalance`/`AccountChartEntry` in Domain.cs
- [x] `Tenant.cs` (Rich Domain Model, 135 lines) — no conversion fields yet
- [x] `TenantStatus` enum: `Active=1, Suspended=2, Inactive=3` — needs `Converted=4`
- [x] `TenantEvents.cs` has `TenantCreatedEvent`/`Suspended`/`Deactivated` — needs `TenantConvertedEvent`
- [x] Arch tests: `VA-DDD-002` only scans `3_CoreHub/Domain` (doesn't exist → skip). `Rule D` only checks Order/Customer/Product/Invoice tenant — adding new records won't break
- [x] `TenantId` + `AccountingPeriod` in parent namespace `VanAn.Shared.Domain` — visible from Tenant aggregate

## Open Questions — RESOLVED (Q1-Q5)
| Q | Decision | Rationale |
|---|----------|-----------|
| Q1 | **Append to `1_Shared/Domain.cs`** (NOT new `VASReports.cs`) | Governance: SSoT = `1_Shared/Domain.cs`. New file fragments the truth. |
| Q2 | **`TrialBalance` keep as-is** | Already exists at line 1518. W4 service will wrap with TenantId. No double-definition. |
| Q3 | **`AccountChartEntry` = in-memory record in W2** | Storage decision (DB table vs dictionary) deferred to W3. W2 only defines the shape. |
| Q4 | **`TenantConvertedEvent` = Domain event only in W2** | Outbox handler / event consumer is W8 scope. W2 only raises the event. |
| Q5 | **Add `Converted=4` as separate enum value** | Semantics differ from `Inactive`: Converted = read-only historical reports still accessible; Inactive = archived, no access. |

## Review Feedback — INTEGRATED (2026-07-04)
External review raised 4 critiques + 1 scope addition. Assessment after code verification:

| # | Critique | Verdict | Evidence |
|---|----------|---------|----------|
| 1 | BCTC uses `ReportItemCode` + 2 columns (Ending/Opening), NOT `AccountCode` + single `Amount` | ✅ ACCEPTED | Mẫu B01-DN/B02-DN/B03-DN use mã chỉ tiêu (100, 110, 01...). TT 200 + TT 133 require 2 comparative columns. Original spec confused BCTC with Bảng cân đối số phát sinh. |
| 2 | `bool IsBalanced` violates DDD invariant | ✅ ACCEPTED | Balance Sheet must balance by definition. If unbalanced → factory throws, not store a flag. Flag implicitly allows storing invalid reports. |
| 3a | `ConvertedToStandard` → general `AccountingStandard` for all Tenants | ✅ ACCEPTED | Every DN follows a standard (TT 99/133/58), not just converted ones. HKD = null (TT 152 implied by TenantType). Cleaner modeling. |
| 3b | `tenant.SetTenantId(newId)` is "redundant" after `Id = newId` | ❌ REJECTED | `Tenant.Id` (line 13, `new TenantId`) HIDES `BaseEntity.Id` (Guid, Common.cs:77). `SetTenantId` sets `BaseEntity.TenantId` (Common.cs:79) — DIFFERENT property. Both `CreateCompany` + `CreateHouseholdBusiness` use this pattern (line 46, 64). Removing it breaks multi-tenancy filtering. |
| 4 | `AccountType.Contra` should not be peer of Asset/Liability | ✅ ACCEPTED | TK 214 (Hao mòn TSCĐ) is in 2xx group (Asset) but has normal credit balance. Contra is a balance attribute, not an account group. Replace with `IsNormalCredit` flag on `AccountChartEntry`. |
| Extra | Add `TT88_2021` to `AccountingStandard` enum | ❌ REJECTED — SCOPE CREEP | D1 (approved) only authorizes 3 standards: TT 99 + TT 133 + TT 58. TT 88/2021 is not in scope. Adding it requires separate user approval (governance: no scope expansion without approval). |

**Net changes from review:**
- BCTC records restructured: `FinancialStatementLine` with `ReportItemCode` + 2 columns (Ending/Opening) + `Level` + `IsNormalNegative`
- `BalanceSheet`: removed `IsBalanced`, added 2-column totals (Ending/Opening)
- `IncomeStatement`: restructured with 2-column totals + `FinancialStatementLine` lines
- `CashFlowStatement`: 3 activity sections as `IEnumerable<FinancialStatementLine>` (detail-level, totals derivable)
- `AccountType`: removed `Contra` (5 values now)
- `AccountChartEntry`: added `IsNormalCredit` flag
- `Tenant`: `ConvertedToStandard` → general `AccountingStandard` (nullable, HKD=null)
- KEPT `SetTenantId(newId)` in factory (NOT redundant — sets BaseEntity.TenantId for multi-tenancy)

## Files to Modify (FINAL — 4 files)
| File | Action | Lines |
|------|--------|-------|
| `1_Shared/Domain.cs` | APPEND new section before closing `}` (~line 2023) | +~60 lines |
| `1_Shared/Domain/Aggregates/TenantAggregate/TenantStatus.cs` | ADD `Converted = 4` + XML doc | +4 lines |
| `1_Shared/Domain/Aggregates/TenantAggregate/TenantEvents.cs` | ADD `TenantConvertedEvent` record | +10 lines |
| `1_Shared/Domain/Aggregates/TenantAggregate/Tenant.cs` | ADD 4 fields + factory + method + 2 query helpers | +~40 lines |

## Detailed Task List

### W2-T1: Add Domain records (append to `1_Shared/Domain.cs`, before closing `}`)
Insert new section "VAS ENTERPRISE REPORTS — Domain Records (Wave 2)" with:

```csharp
// ====================== VAS ENTERPRISE REPORTS — Domain Records (Wave 2) ======================
// D5 approved: Domain modification for VAS Enterprise Financial Reports (TT 99/2025 + TT 133/2016 + TT 58/2026)
// D9 approved: HKD↔DN conversion = Option B (New Tenant + Link)
// Review 2026-07-04: BCTC records use ReportItemCode (Mã chỉ tiêu) + 2-column comparative (Ending/Opening)
//   per Vietnamese accounting law (Mẫu B01-DN/B02-DN/B03-DN). NOT AccountCode-based (that's Trial Balance).
// Domain Purity: NO EF Core, NO DbContext, NO DataAnnotations — records only

/// <summary>
/// Tenant business type — determines which accounting standard applies.
/// Wave 2 (D9): drives feature flag routing in W8.
/// </summary>
public enum TenantType
{
    HKD = 1,                    // Hộ kinh doanh (TT 152/2025/TT-BTC)
    Enterprise_SuperSmall = 2,  // DN siêu nhỏ (TT 58/2026)
    Enterprise_SME = 3,         // DN vừa và nhỏ (TT 133/2016)
    Enterprise_Large = 4        // DN lớn (TT 99/2025)
}

/// <summary>
/// Account classification for chart-of-accounts mapping (W3).
/// Contra accounts (e.g., TK 214) are typed as their parent group (Asset) with IsNormalCredit=true on AccountChartEntry.
/// </summary>
public enum AccountType { Asset, Liability, Equity, Revenue, Expense }

/// <summary>Vietnamese accounting standards supported by VAS module (D1 approved scope).</summary>
public enum AccountingStandard { TT99_2025, TT133_2016, TT58_2026 }

// ── 1. BẢNG CÂN ĐỐI KẾ TOÁN (Mẫu B01-DN / B01-DNN) ──────────────────────────────────
// Invariant: TotalAssetsEnding == TotalLiabilitiesAndEquityEnding (enforced at factory/service in W4).
// No IsBalanced flag — unbalanced data throws, never stored.
public record BalanceSheet(
    TenantId TenantId, AccountingPeriod Period, DateTime GeneratedAt,
    IEnumerable<FinancialStatementLine> Assets,
    IEnumerable<FinancialStatementLine> Liabilities,
    IEnumerable<FinancialStatementLine> Equity,
    decimal TotalAssetsEnding, decimal TotalAssetsOpening,
    decimal TotalLiabilitiesAndEquityEnding, decimal TotalLiabilitiesAndEquityOpening
);

// ── 2. BÁO CÁO KẾT QUẢ HOẠT ĐỘNG KINH DOANH (Mẫu B02-DN / B02-DNN) ────────────────
public record IncomeStatement(
    TenantId TenantId, AccountingPeriod Period, DateTime GeneratedAt,
    decimal TotalRevenueEnding, decimal TotalRevenueOpening,
    decimal NetProfitEnding, decimal NetProfitOpening,
    IEnumerable<FinancialStatementLine> Lines
);

// ── 3. BÁO CÁO LƯU CHUYỂN TIỀN TỆ (Mẫu B03-DN / B03-DNN) ──────────────────────────
public record CashFlowStatement(
    TenantId TenantId, AccountingPeriod Period, DateTime GeneratedAt,
    decimal OpeningCash, decimal ClosingCash, decimal NetChange,
    IEnumerable<FinancialStatementLine> OperatingActivities,
    IEnumerable<FinancialStatementLine> InvestingActivities,
    IEnumerable<FinancialStatementLine> FinancingActivities
);

/// <summary>
/// Chuẩn cấu trúc một dòng Chỉ tiêu Báo cáo Tài chính theo pháp luật Việt Nam.
/// Sử dụng Mã chỉ tiêu (ReportItemCode), KHÔNG dùng AccountCode.
/// Bắt buộc có 2 cột số liệu để đối chiếu thời kỳ (Số cuối kỳ / Số đầu năm).
/// </summary>
public record FinancialStatementLine(
    string ReportItemCode,      // Mã chỉ tiêu (VD: "100", "110", "01", "20")
    string ReportItemName,      // Tên chỉ tiêu (VD: "Tài sản ngắn hạn", "Doanh thu bán hàng")
    decimal EndingAmount,       // Số cuối kỳ / Năm nay
    decimal OpeningAmount,      // Số đầu năm / Năm trước (So sánh)
    int Level,                  // Cấp bậc phân cấp cha-con trình bày UI
    bool IsNormalNegative       // Hiển thị số âm trong ngoặc đơn VD: (20,000,000)
);

// TrialBalance already exists at Domain.cs:1518 — keep as-is, W4 service wraps with TenantId.

// ── 4. SỐ DƯ ĐẦU KỲ (Mở sổ / Khởi tạo dữ liệu) ─────────────────────────────────────
public record OpeningBalance(
    TenantId TenantId, AccountingPeriod Period,
    IEnumerable<OpeningBalanceLine> Lines
);
public record OpeningBalanceLine(string AccountCode, decimal DebitOpening, decimal CreditOpening);

/// <summary>
/// In-memory chart-of-accounts entry. Storage decision (DB vs dictionary) deferred to W3.
/// IsNormalCredit: true for contra accounts (e.g., TK 214 Hao mòn TSCĐ) — normal credit balance.
/// </summary>
public record AccountChartEntry(
    string AccountCode, string AccountName, AccountType Type,
    AccountingStandard Standard, bool IsNormalCredit
);
```

### W2-T2: Add `Converted` status to `TenantStatus.cs`
```csharp
/// <summary>HKD đã chuyển đổi thành DN — read-only, historical reports vẫn truy cập (D9 Option B).</summary>
Converted = 4
```
Semantics: `Converted` ≠ `Inactive`. Converted tenant retains read-only historical report access; successor DN tenant is the active entity going forward.

### W2-T3: Add `TenantConvertedEvent` to `TenantEvents.cs`
```csharp
/// <summary>
/// Raised when an HKD tenant is converted to a DN tenant (D9 Option B).
/// Wave 2: Domain event only — outbox handler/consumer is W8 scope.
/// </summary>
public sealed record TenantConvertedEvent(
    Guid TenantId,           // HKD tenant being converted
    Guid SuccessorTenantId,  // New DN tenant created from conversion
    DateTime OccurredAt) : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
}
```

### W2-T4: Add conversion fields + factory + method to `Tenant.cs`
Add 4 nullable fields after `DefaultIndustrySector` (line 21):
```csharp
// ── D9: HKD↔DN Conversion Link (Option B — New Tenant + Link) ─────────
// Predecessor: Tenant cũ (HKD) mà DN này được convert từ (set on new DN tenant).
public TenantId? PredecessorTenantId { get; private set; }
// Successor: Tenant mới (DN) mà HKD này đã convert sang (set on old HKD tenant).
public TenantId? SuccessorTenantId { get; private set; }
public DateTime? ConvertedAt { get; private set; }
// Accounting standard applies to ALL tenants (not just converted ones).
// HKD = null (TT 152 implied by TenantType=HKD). DN = TT99/133/58.
// Review 2026-07-04: replaced ConvertedToStandard with general AccountingStandard.
public AccountingStandard? AccountingStandard { get; private set; }
```

Add factory method (after `CreateHouseholdBusiness`, ~line 67):
```csharp
/// <summary>
/// D9 Option B: Create a new DN tenant from HKD conversion.
/// The new tenant links back to its HKD predecessor via PredecessorTenantId.
/// Raises TenantCreatedEvent (standard lifecycle) — successor link set by caller via MarkConvertedTo.
/// Note: SetTenantId sets BaseEntity.TenantId (Guid, for multi-tenancy filtering),
///       distinct from Tenant.Id (TenantId, strongly-typed) — NOT redundant.
/// </summary>
public static Tenant CreateFromConversion(
    TenantId newId, string name, TenantType newType,
    TenantId predecessorTenantId, AccountingStandard standard,
    TenantSettings? settings = null)
{
    ArgumentException.ThrowIfNullOrWhiteSpace(name);
    var tenant = new Tenant
    {
        Id = newId,
        Name = name,
        BusinessType = BusinessType.Company,  // DN is always Company
        Status = TenantStatus.Active,
        Settings = settings ?? TenantSettings.Empty(),
        PredecessorTenantId = predecessorTenantId,
        ConvertedAt = DateTime.UtcNow,
        AccountingStandard = standard
    };
    tenant.SetTenantId(newId); // sets BaseEntity.TenantId (multi-tenancy) — distinct from Tenant.Id
    tenant.AddDomainEvent(new TenantCreatedEvent(newId.Value, name, settings?.ContactEmail, DateTime.UtcNow));
    return tenant;
}
```

Add domain method (after `SetDefaultIndustrySector`, ~line 127):
```csharp
/// <summary>
/// D9 Option B: Mark an HKD tenant as converted to a DN successor.
/// Sets Status=Converted (read-only historical) + links successor.
/// Raises TenantConvertedEvent. Cannot convert an inactive tenant.
/// </summary>
public void MarkConvertedTo(TenantId successorTenantId)
{
    if (Status == TenantStatus.Inactive)
        throw new InvalidOperationException("Cannot convert an inactive tenant.");
    if (Status == TenantStatus.Converted)
        throw new InvalidOperationException("Tenant is already converted.");
    Status = TenantStatus.Converted;
    SuccessorTenantId = successorTenantId;
    UpdateAudit();
    AddDomainEvent(new TenantConvertedEvent(Id.Value, successorTenantId.Value, DateTime.UtcNow));
}
```

Add query helpers (after `IsCompany()`, line 133):
```csharp
public bool IsConverted() => Status == TenantStatus.Converted;
public bool IsConversionOf(TenantId predecessor) => PredecessorTenantId == predecessor;
```

### W2-T5: Build + guard pass
- `dotnet build VanAn.sln` (Release) — 0 errors
- `guard-check.ps1` — pass
- No service changes, just Domain records + 4 Tenant fields + 1 event

### W2-T6: Architecture tests verify
- `dotnet test VanAn.Architecture.Tests` — all pass (Domain purity intact: no EF Core, no DbContext, no DataAnnotations in new records)
- Existing tests `VA-DDD-002`, `Rule D`, `Rule E`, `Rule F`, `Rule G` unaffected (new records don't match scanned patterns)

### W2-T7: Add unit tests (lock behavior)
Add to `6_Tests/VanAn.Core.Tests/` (Tenant test folder — verify path in IMPLEMENT):
- `Tenant_CreateFromConversion_SetsPredecessorAndStandard`
- `Tenant_MarkConvertedTo_SetsStatusConvertedAndSuccessor`
- `Tenant_MarkConvertedTo_ThrowsOnInactive`
- `Tenant_MarkConvertedTo_ThrowsOnAlreadyConverted`

## Verification Checklist
- [ ] Build 0 errors (Release)
- [ ] guard-check.ps1 pass
- [ ] Architecture tests pass (all existing + new records pure)
- [ ] Domain records have NO EF Core / DbContext / DataAnnotations references
- [ ] BCTC records use `FinancialStatementLine` with `ReportItemCode` + 2 columns (Ending/Opening) — NOT AccountCode
- [ ] `BalanceSheet` has NO `IsBalanced` flag (invariant enforced at W4 factory, not stored)
- [ ] `AccountType` enum has 5 values (no `Contra`) — contra handled via `AccountChartEntry.IsNormalCredit`
- [ ] `AccountingStandard` enum has 3 values only (TT99/133/58) — NO TT88_2021 (out of D1 scope)
- [ ] `Tenant.AccountingStandard` is general property (nullable, HKD=null, DN=TT99/133/58)
- [ ] `Tenant.CreateFromConversion` sets `PredecessorTenantId` + `ConvertedAt` + `AccountingStandard` + calls `SetTenantId`
- [ ] `Tenant.MarkConvertedTo` sets `Status=Converted` + `SuccessorTenantId` + raises `TenantConvertedEvent`
- [ ] `Tenant.MarkConvertedTo` throws `InvalidOperationException` on Inactive/AlreadyConverted
- [ ] 4 new unit tests pass

## Rollback
- Git revert on `feature/vas-wave2-domain-records` (Domain records + conversion fields only — no service dependency yet, no EF mapping changes)

## Risk Assessment
| # | Risk | Severity | Mitigation |
|---|------|----------|------------|
| R6 | Domain mod break arch tests | LOW | Arch tests only scan `3_CoreHub/Domain` (doesn't exist) + `Rule D` only checks Order/Customer/Product/Invoice. New records don't match scanned patterns. |
| R-blast | Blast radius | LOW | Only adds records + 4 fields + 1 event. No service changes, no EF mapping changes. `TenantConfiguration` update deferred to W8 (conversion service). |
| R-purity | Domain purity violation | LOW | All new code is records + enum + 1 event class. No EF Core, no DbContext, no DataAnnotations. Header comment in Domain.cs already enforces this. |
| R-invariant | BalanceSheet unbalanced at runtime | MEDIUM | W2 only defines record shape (no `IsBalanced` flag). W4 factory/service MUST enforce `TotalAssets == TotalLiabilities + TotalEquity` invariant — throw if unbalanced. Tracked in W4 task card. |
| R-standard | `AccountingStandard` general property affects existing factories | LOW | `CreateCompany`/`CreateHouseholdBusiness` keep current signatures (standard defaults to null). Only `CreateFromConversion` sets it. W8 may add `SetAccountingStandard()` method for non-converted DN tenants. |

## Out of Scope (deferred)
- EF Core mapping for new Tenant fields (`PredecessorTenantId`, `SuccessorTenantId`, `ConvertedAt`, `AccountingStandard`) → **W8** (conversion service + TenantConfiguration update)
- `TenantConvertedEvent` outbox handler / consumer → **W8**
- `AccountChartEntry` storage (DB table vs in-memory dictionary) → **W3**
- `IHkdToEnterpriseAccountMapper` interface + mapping data → **W3**
- Feature flag routing on `TenantType` → **W8**
- `SetAccountingStandard()` method for non-converted DN tenants → **W8** (if needed)
- BalanceSheet invariant enforcement (factory throws if unbalanced) → **W4** (service layer)
- TT 88/2021 accounting standard → **NOT APPROVED** (outside D1 scope, requires separate user approval)
