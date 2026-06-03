# Sprint 2 — Period Closing & Audit Trail
**Phase:** 2.9.3 + 2.9.4  
**Duration:** 1 tuần (5 ngày)  
**Target:** Kế toán viên có thể đóng sổ tháng/năm và hệ thống ghi lại đầy đủ audit log.

---

## Overview

Sprint 2 tập trung vào hai tính năng cốt lõi cho hệ thống kế toán:
1. **Period Closing Wizard** - Đóng sổ kỳ kế toán với validation và Reversal Entry pattern
2. **Audit Trail System** - Ghi log mọi hành động với 5-year retention policy

---

## Task Breakdown

### Task 2.1 — Period Closing Wizard (Phase 2.9.3)

**Backend:**
- `IPeriodClosingService` interface
- `PeriodClosingService.cs` implementation
  - `ValidatePeriodAsync(periodId)` - Pre-closing validation
  - `ClosePeriodAsync(periodId, userId)` - Automated closing procedures
  - `ReopenPeriodAsync(periodId, userId, reason)` - Reopen capability với Reversal Entry

**Frontend:**
- `PeriodClosing.razor` - Step-by-step wizard UI (3 bước: Validate → Review → Close)
- Components: VanACard, VanAButton, VanAAlert, VanAModal

---

### Task 2.2 — Audit Trail System (Phase 2.9.4)

**Backend:**
- `IAuditTrailService` interface
- `AuditTrailService.cs` implementation
  - `LogActionAsync(action, entityType, entityId, details)`
  - `GetAuditLogsAsync(filter)`
  - `ExportAuditReportAsync(filter)`
- `IAuditTrailRepository` interface + implementation
- 5-year retention policy enforcement

**Frontend (UI Platform Compliant):**
- `AuditTrail.razor` - Audit log table với filters
- Components: VanACard, VanASearchBar, VanATable, VanAButton, VanAAlert
- Follow UI Platform Implementation Guide

---

## TDD Plan (Test-Driven Development)

### Test Order (Priority)

**Phase 1: Domain Tests (Unit)**
1. `PeriodClosingStatus` enum tests
   - Verify all enum values are defined correctly
2. `PeriodClosingCheckResult` record tests
   - `PeriodClosingCheckResult_WithValidData_ReturnsCorrectValues`
   - `PeriodClosingCheckResult_WithErrors_ReturnsInvalid`
3. `AuditLogEntry` record tests
   - `AuditLogEntry_CreatesWithCorrectValues`
   - `AuditLogEntry_TimestampDefaultsToUtcNow`
4. `AuditAction` enum tests
   - Verify all enum values are defined correctly
5. `AuditLogFilter` record tests
   - `AuditLogFilter_WithNullFilters_AllowsAllLogs`
   - `AuditLogFilter_WithDateRange_RestrictsLogs`

**Phase 2: Service Tests (Unit)**
1. `PeriodClosingServiceTests.cs`
   - `ValidatePeriodAsync_WithValidPeriod_ReturnsSuccess`
   - `ValidatePeriodAsync_WithMissingEntries_ReturnsError`
   - `ValidatePeriodAsync_WithUnbalancedEntries_ReturnsError`
   - `ClosePeriodAsync_WhenValid_CreatesClosingEntries`
   - `ClosePeriodAsync_WhenInvalid_ThrowsException`
   - `ClosePeriodAsync_WhenAlreadyClosed_ThrowsException`
   - `ClosePeriodAsync_WhenPeriodHasPendingTransactions_ThrowsException`
   - `ReopenPeriodAsync_CreatesReversalEntry`
   - `ReopenPeriodAsync_WithClosedPeriod_UpdatesStatus`
   - `ReopenPeriodAsync_WithOpenPeriod_ThrowsException`

2. `AuditTrailServiceTests.cs`
   - `LogActionAsync_LogsEntryCorrectly`
   - `LogActionAsync_WithValidData_PersistsToRepository`
   - `LogActionAsync_WithNullDetails_HandlesGracefully`
   - `GetAuditLogsAsync_WithFilter_ReturnsMatchingLogs`
   - `GetAuditLogsAsync_WithDateRange_ReturnsLogsInRange`
   - `GetAuditLogsAsync_WithUserIdFilter_ReturnsUserLogs`
   - `GetAuditLogsAsync_WithTenantIsolation_DoesNotReturnOtherTenantLogs`
   - `ExportAuditReportAsync_GeneratesCsv`
   - `ExportAuditReportAsync_WithEmptyData_ReturnsEmptyCsv`
   - `RetentionPolicy_DeletesLogsOlderThan5Years`

**Phase 3: Repository Tests (Integration)**
1. `AuditTrailRepositoryTests.cs`
   - `AddAsync_PersistsEntry`
   - `AddAsync_WithValidData_SavesToDatabase`
   - `GetByTenantIdAsync_ReturnsTenantLogs`
   - `GetByTenantIdAsync_WithMultipleTenants_ReturnsCorrectTenantLogs`
   - `GetByDateRangeAsync_ReturnsLogsInRange`
   - `GetByDateRangeAsync_WithInvalidRange_ReturnsEmpty`
   - `GetByUserIdAsync_ReturnsUserLogs`
   - `GetByActionAsync_ReturnsActionLogs`

**Phase 4: E2E Tests (TypeScript Playwright — MANDATORY)**
> File location: `6_Testing/e2e-tests/` — follow pattern của `accounting-flow.spec.ts`

1. `period-closing-flow.spec.ts`
   - `Staff can access Period Closing wizard`
   - `PeriodClosingWizard validates period before closing`
   - `PeriodClosingWizard closes period with confirmation dialog`
   - `PeriodClosingWizard reopens period with reason`
   - `PeriodClosingWizard shows error when period has pending transactions`

2. `audit-trail-flow.spec.ts`
   - `Staff can access Audit Trail page`
   - `AuditTrail loads logs correctly with pagination`
   - `AuditTrail filters logs by date range`
   - `AuditTrail filters logs by user`
   - `AuditTrail exports report as CSV download`
   - `AuditTrail does not show logs from other tenants`

---

## Domain Entities (1_Shared/Domain.cs)

### Period Closing
```csharp
public enum PeriodClosingStatus
{
    Open,
    Validating,
    Closing,
    Closed,
    Reopening
}

public record PeriodClosingCheckResult(bool IsValid, List<string> Errors, List<string> Warnings);

public record ClosingEntry(Guid PeriodId, DateTime ClosingDate, Guid CreatedBy);
```

### Audit Trail
```csharp
public enum AuditAction
{
    Create,
    Read,
    Update,
    Delete,
    Export,
    Login,
    Logout
}

public record AuditLogEntry(
    Guid Id,
    TenantId TenantId,
    Guid UserId,
    AuditAction Action,
    string EntityType,
    Guid EntityId,
    DateTime Timestamp,
    string Details
);

public record AuditLogFilter(
    DateTime? StartDate,
    DateTime? EndDate,
    AuditAction? Action,
    Guid? UserId,
    string? EntityType
);
```

---

## Implementation Plan (5 Days)

> **TDD Rule:** Tests phải được viết TRƯỚC implementation. Red → Green → Refactor.

### Day 1: Domain Entities + Domain Unit Tests (TDD Phase 1)

**Tasks:**
- [ ] Add domain entities to `1_Shared/Domain.cs`
  - [ ] `PeriodClosingStatus` enum
  - [ ] `PeriodClosingCheckResult` record
  - [ ] `ClosingEntry` record
  - [ ] `AuditAction` enum
  - [ ] `AuditLogEntry` record
  - [ ] `AuditLogFilter` record

- [ ] **[TDD]** Viết Domain Unit Tests ngay sau khi định nghĩa entities
  - [ ] `6_Tests/VanAn.Core.Tests/Accounting/PeriodClosingDomainTests.cs`
    - `PeriodClosingStatus_AllValuesAreDefined`
    - `PeriodClosingCheckResult_WithValidData_ReturnsCorrectValues`
    - `PeriodClosingCheckResult_WithErrors_ReturnsInvalid`
  - [ ] `6_Tests/VanAn.Core.Tests/Accounting/AuditTrailDomainTests.cs`
    - `AuditAction_AllValuesAreDefined`
    - `AuditLogEntry_CreatesWithCorrectValues`
    - `AuditLogEntry_TimestampDefaultsToUtcNow`
    - `AuditLogFilter_WithNullFilters_AllowsAllLogs`
    - `AuditLogFilter_WithDateRange_RestrictsLogs`

- [ ] Create EF Core Configuration
  - [ ] `3_CoreHub/Infrastructure/Configurations/AuditLogEntryConfiguration.cs`
    - Index on TenantId + Timestamp
    - Index on UserId
    - Index on Action

**Validation:**
```powershell
dotnet build VanAn.sln
dotnet test 6_Tests/VanAn.Core.Tests/VanAn.Core.Tests.csproj --filter "Category=Domain"
```

---

### Day 2: Service Tests (Failing) → Service Implementation (TDD Phase 2)

**Tasks (viết tests TRƯỚC, sau đó implement):**

- [ ] **[TDD - Red]** Viết failing tests trước:
  - [ ] `6_Tests/VanAn.Core.Tests/Accounting/PeriodClosingServiceTests.cs`
    - `ValidatePeriodAsync_WithValidPeriod_ReturnsSuccess`
    - `ValidatePeriodAsync_WithMissingEntries_ReturnsError`
    - `ValidatePeriodAsync_WithUnbalancedEntries_ReturnsError`
    - `ClosePeriodAsync_WhenValid_CreatesClosingEntries`
    - `ClosePeriodAsync_WhenInvalid_ThrowsException`
    - `ClosePeriodAsync_WhenAlreadyClosed_ThrowsException`
    - `ClosePeriodAsync_WhenPeriodHasPendingTransactions_ThrowsException`
    - `ReopenPeriodAsync_CreatesReversalEntry`
    - `ReopenPeriodAsync_WithClosedPeriod_UpdatesStatus`
    - `ReopenPeriodAsync_WithOpenPeriod_ThrowsException`
  - [ ] `6_Tests/VanAn.Core.Tests/Accounting/AuditTrailServiceTests.cs`
    - `LogActionAsync_LogsEntryCorrectly`
    - `LogActionAsync_WithValidData_PersistsToRepository`
    - `LogActionAsync_WithNullDetails_HandlesGracefully`
    - `GetAuditLogsAsync_WithFilter_ReturnsMatchingLogs`
    - `GetAuditLogsAsync_WithDateRange_ReturnsLogsInRange`
    - `GetAuditLogsAsync_WithUserIdFilter_ReturnsUserLogs`
    - `GetAuditLogsAsync_WithTenantIsolation_DoesNotReturnOtherTenantLogs`
    - `ExportAuditReportAsync_GeneratesCsv`
    - `ExportAuditReportAsync_WithEmptyData_ReturnsEmptyCsv`
    - `RetentionPolicy_DeletesLogsOlderThan5Years`

- [ ] **[TDD - Green]** Implement services để pass tests:
  - [ ] `3_CoreHub/Services/IPeriodClosingService.cs`
  - [ ] `3_CoreHub/Services/PeriodClosingService.cs`
    - ValidatePeriodAsync logic (pending transactions check)
    - ClosePeriodAsync logic (create closing entries, guard already-closed)
    - ReopenPeriodAsync logic (create Reversal Entry, guard state)
  - [ ] `3_CoreHub/Services/IAuditTrailService.cs`
  - [ ] `3_CoreHub/Services/AuditTrailService.cs`
    - LogActionAsync logic
    - GetAuditLogsAsync logic (multi-tenancy filter bắt buộc)
    - ExportAuditReportAsync logic (CSV export)
    - 5-year retention policy enforcement

**Validation:**
```powershell
dotnet build VanAn.sln
dotnet test 6_Tests/VanAn.Core.Tests/VanAn.Core.Tests.csproj --filter "FullyQualifiedName~PeriodClosingService|FullyQualifiedName~AuditTrailService"
```

---

### Day 3: Repository Tests (Failing) → Repository Implementation (TDD Phase 3)

**Tasks (viết tests TRƯỚC, sau đó implement):**

- [ ] **[TDD - Red]** Viết failing repository tests trước:
  - [ ] `6_Tests/VanAn.Core.Tests/Accounting/AuditTrailRepositoryTests.cs`
    - `AddAsync_PersistsEntry`
    - `AddAsync_WithValidData_SavesToDatabase`
    - `GetByTenantIdAsync_ReturnsTenantLogs`
    - `GetByTenantIdAsync_WithMultipleTenants_ReturnsCorrectTenantLogs`
    - `GetByDateRangeAsync_ReturnsLogsInRange`
    - `GetByDateRangeAsync_WithInvalidRange_ReturnsEmpty`
    - `GetByUserIdAsync_ReturnsUserLogs`
    - `GetByActionAsync_ReturnsActionLogs`

- [ ] **[TDD - Green]** Implement repository để pass tests:
  - [ ] `3_CoreHub/Infrastructure/Repositories/IAuditTrailRepository.cs`
  - [ ] `3_CoreHub/Infrastructure/Repositories/AuditTrailRepository.cs`

**Validation:**
```powershell
dotnet build VanAn.sln
dotnet test 6_Tests/VanAn.Core.Tests/VanAn.Core.Tests.csproj --filter "FullyQualifiedName~AuditTrailRepository"
```

---

### Day 4: Frontend Implementation

**Tasks:**
- [ ] Create Period Closing Wizard
  - [ ] `5_WebApps/ShopERP/Components/Pages/Accounting/PeriodClosing.razor`
    - Step 1: Validate (show checklist)
    - Step 2: Review (show summary)
    - Step 3: Close (confirmation dialog)
    - Use VanACard, VanAButton, VanAAlert, VanAModal
    - Namespace directives per UI Platform guide

- [ ] Create Audit Trail Page (UI Platform Compliant)
  - [ ] `5_WebApps/ShopERP/Components/Pages/Accounting/AuditTrail.razor`
    - VanASearchBar for search/filter
    - VanATable for audit log display
    - VanAButton for Export Report
    - VanAAlert for notifications
    - Namespace directives:
      ```razor
      @using VanAn.UI.Platform.Components.Atomic
      @using VanAn.UI.Platform.Components.Composite
      @using VanAn.UI.Platform.Components.Data
      @using VanAn.UI.Platform.Models
      @using VanAn.CoreHub.Services
      @using VanAn.Shared.Domain
      ```

- [ ] Update ShopERP Navigation
  - [ ] Add "Period Closing" menu item
  - [ ] Add "Audit Trail" menu item
  - [ ] Routes: `/accounting/period-closing`, `/accounting/audit-trail`

- [ ] DI Registration
  - [ ] Register `IPeriodClosingService` in `ShopERP/Program.cs`
  - [ ] Register `IAuditTrailService` in `ShopERP/Program.cs`
  - [ ] Register `IAuditTrailRepository` in `3_CoreHub/Program.cs`

**Validation:**
```powershell
dotnet build VanAn.sln
```

---

### Day 5: E2E Tests + Integration & Validation

**Tasks:**

- [ ] **[E2E - MANDATORY]** Viết Playwright TypeScript E2E tests
  - [ ] `6_Testing/e2e-tests/period-closing-flow.spec.ts`
    - `Staff can access Period Closing wizard`
    - `PeriodClosingWizard validates period before closing`
    - `PeriodClosingWizard closes period with confirmation dialog`
    - `PeriodClosingWizard reopens period with reason`
    - `PeriodClosingWizard shows error when period has pending transactions`
  - [ ] `6_Testing/e2e-tests/audit-trail-flow.spec.ts`
    - `Staff can access Audit Trail page`
    - `AuditTrail loads logs correctly with pagination`
    - `AuditTrail filters logs by date range`
    - `AuditTrail filters logs by user`
    - `AuditTrail exports report as CSV download`
    - `AuditTrail does not show logs from other tenants`

- [ ] Build Validation
  ```powershell
  dotnet clean
  dotnet build VanAn.sln --no-incremental
  ```

- [ ] Guard Check
  ```powershell
  ./guard-check.ps1
  ```

- [ ] Full Test Suite
  ```powershell
  dotnet test 6_Tests/VanAn.Core.Tests/VanAn.Core.Tests.csproj
  npx playwright test 6_Testing/e2e-tests/period-closing-flow.spec.ts
  npx playwright test 6_Testing/e2e-tests/audit-trail-flow.spec.ts
  ```

---

## Namespace Strategy

### Domain Layer
```csharp
namespace VanAn.Shared.Domain;
```

### Services
```csharp
namespace VanAn.CoreHub.Services;
```

### Repositories
```csharp
namespace VanAn.CoreHub.Infrastructure.Repositories;
```

### Configurations
```csharp
namespace VanAn.CoreHub.Infrastructure.Configurations;
```

### Frontend
```razor
@using VanAn.UI.Platform.Components.Atomic      // VanAButton, VanAAlert, VanACard
@using VanAn.UI.Platform.Components.Composite   // VanASearchBar, VanATable
@using VanAn.UI.Platform.Components.Data        // TableColumn
@using VanAn.UI.Platform.Models                 // ComponentModels
@using VanAn.CoreHub.Services                   // IAuditTrailService, IPeriodClosingService
@using VanAn.Shared.Domain                      // TenantId, AccountingPeriod, AuditAction
```

---

## UI Platform Compliance (Task 2.2)

### Required Components
- ✅ `VanACard` - Container for audit log page
- ✅ `VanASearchBar` - Search + filter bar (already exists in UI.Platform)
- ✅ `VanATable` - Audit log data table
- ✅ `VanAButton` - Export Report, Clear Filters buttons
- ✅ `VanAAlert` - Success/error notifications

### Anti-Patterns (DO NOT)
- ❌ Custom `<input>` for search → Use `VanASearchBar`
- ❌ Custom `<table>` → Use `VanATable`
- ❌ Custom `<button>` → Use `VanAButton`
- ❌ Custom `<div class="card">` → Use `VanACard`
- ❌ Custom `<div class="alert">` → Use `VanAAlert`

### Design Tokens
- Use `ThemeProvider.CurrentTheme` for theming
- Use CSS variables for colors, spacing, typography
- Mobile-first responsive design

---

## Architectural Constraints (NON-NEGOTIABLE)

- **Domain Layer:** Tất cả new entities phải vào `1_Shared/Domain.cs`
- **AccountingEntry:** Immutable — đóng sổ dùng Reversal Entry pattern
- **Multi-tenancy:** Mọi query phải filter theo `TenantId`
- **UI Platform:** Dùng UI Platform components — KHÔNG custom HTML/CSS
- **Build gate:** `guard-check.ps1` + `dotnet build VanAn.sln` phải PASS sau mỗi task

---

## Dependencies

**Existing (✅):**
- `IAccountingEntryService` (from Sprint 1)
- AccountingEntry immutable pattern
- Multi-tenancy infrastructure
- UI Platform components (VanACard, VanASearchBar, VanATable, etc.)

**New:**
- Period Closing logic
- Audit Trail repository
- Reversal Entry pattern for period reopening

---

## Risks & Mitigations

| Risk | Mitigation |
|------|------------|
| Period closing affects open periods | Add validation to prevent closing with pending transactions |
| Audit log table growth (5-year retention) | Add indexing on TenantId, Timestamp, UserId |
| Reopening period creates data inconsistency | Use immutable Reversal Entry pattern (already established) |
| Performance degradation with large audit logs | Implement pagination, filtering, and archiving strategy |

---

## Progress Tracking

### Overall Progress
- [ ] Day 1: Domain Entities + Domain Unit Tests (0/3 tasks)
- [ ] Day 2: Service Tests → Service Implementation (0/2 tasks)
- [ ] Day 3: Repository Tests → Repository Implementation (0/2 tasks)
- [ ] Day 4: Frontend Implementation (0/4 tasks)
- [ ] Day 5: E2E Tests + Integration & Validation (0/4 tasks)

### Detailed Progress
- [ ] Domain entities added to `1_Shared/Domain.cs`
- [ ] Domain unit tests written and passing (Day 1)
- [ ] Service tests written failing — Red (Day 2)
- [ ] `IPeriodClosingService` + implementation created — Green (Day 2)
- [ ] `IAuditTrailService` + implementation created — Green (Day 2)
- [ ] Service tests passing (Day 2)
- [ ] Repository tests written failing — Red (Day 3)
- [ ] `AuditLogEntryConfiguration.cs` created (Day 3)
- [ ] `IAuditTrailRepository` + implementation created — Green (Day 3)
- [ ] Repository tests passing (Day 3)
- [ ] `PeriodClosing.razor` created (Day 4)
- [ ] `AuditTrail.razor` created — UI Platform compliant (Day 4)
- [ ] DI registration completed (Day 4)
- [ ] E2E specs written and passing (Day 5)
- [ ] `dotnet build VanAn.sln` passes
- [ ] `./guard-check.ps1` passes

---

## Deliverables

**After Sprint 2:**
- ✅ Period Closing Wizard working in ShopERP
- ✅ Audit Trail page with filter/export functionality
- ✅ Domain unit tests passing (Day 1)
- ✅ Service unit tests passing — 20 test cases (Day 2)
- ✅ Repository integration tests passing — 8 test cases (Day 3)
- ✅ E2E Playwright tests passing — 11 test cases (Day 5)
- ✅ Build validation passing
- ✅ Guard check passing

**Files Created:**
- `1_Shared/Domain.cs` (updated with new entities)
- `3_CoreHub/Infrastructure/Configurations/AuditLogEntryConfiguration.cs`
- `3_CoreHub/Infrastructure/Repositories/IAuditTrailRepository.cs`
- `3_CoreHub/Infrastructure/Repositories/AuditTrailRepository.cs`
- `3_CoreHub/Services/IPeriodClosingService.cs`
- `3_CoreHub/Services/PeriodClosingService.cs`
- `3_CoreHub/Services/IAuditTrailService.cs`
- `3_CoreHub/Services/AuditTrailService.cs`
- `5_WebApps/ShopERP/Components/Pages/Accounting/PeriodClosing.razor`
- `5_WebApps/ShopERP/Components/Pages/Accounting/AuditTrail.razor`
- `6_Tests/VanAn.Core.Tests/Accounting/PeriodClosingDomainTests.cs`
- `6_Tests/VanAn.Core.Tests/Accounting/AuditTrailDomainTests.cs`
- `6_Tests/VanAn.Core.Tests/Accounting/PeriodClosingServiceTests.cs`
- `6_Tests/VanAn.Core.Tests/Accounting/AuditTrailServiceTests.cs`
- `6_Tests/VanAn.Core.Tests/Accounting/AuditTrailRepositoryTests.cs`
- `6_Testing/e2e-tests/period-closing-flow.spec.ts`
- `6_Testing/e2e-tests/audit-trail-flow.spec.ts`

---

**Created:** May 7, 2026  
**Next Review:** End of Day 1  
**Owner:** Vạn An Development Team
