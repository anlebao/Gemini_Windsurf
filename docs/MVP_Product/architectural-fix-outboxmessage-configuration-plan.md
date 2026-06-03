# Architectural Fix: OutboxMessage Configuration Violation

**Date:** 2026-05-21  
**Component:** ShopERPDbContext OutboxMessage Configuration  
**Issue:** Direct reference from web layer to infrastructure layer violates Clean Architecture  
**Severity:** High (architectural violation)

---

## Problem Statement

### Current Issue
`ShopERPDbContext` (5_WebApps layer) directly references `CoreHub.Infrastructure.Configurations.OutboxMessageConfiguration()` (3_CoreHub infrastructure layer), violating Clean Architecture layering principles.

### Error Encountered
```
System.InvalidOperationException: Cannot use table 'OutboxMessages' for entity type 'OutboxMessage' 
since it is being used for entity type 'OutboxMessage' and potentially other entity types, 
but there is no linking relationship.
```

### Root Cause
- Two `OutboxMessage` classes exist:
  - `VanAn.CoreHub.Infrastructure.OutboxMessage` (CoreHub) — used by ShopERPDbContext
  - `VanAn.Shared.Domain.OutboxMessage` (Shared) — has configuration file in ShopERP
- ShopERPDbContext uses `CoreOutboxMessage` but applies configuration for `VanAn.Shared.Domain.OutboxMessage`
- Current fix uses direct reference to `CoreHub.Infrastructure.Configurations.OutboxMessageConfiguration()`

---

## Backward Impact Analysis

### Dependencies on ShopERPDbContext

| File | Usage | Impact if Changed |
|------|-------|------------------|
| `5_WebApps/ShopERP/Program.cs` | DI registration | Medium - may need DI adjustment |
| `5_WebApps/ShopERP/Services/OrderQueueService.cs` | Injected dependency | Low - just DI resolution |
| `5_WebApps/ShopERP/Services/SimpleOutboxProcessor.cs` | GetRequiredService | Low - just DI resolution |
| `6_Tests/VanAn.Core.Tests/Services/SimpleOutboxProcessorTests.cs` | Test context | Medium - test setup may need adjustment |
| `6_Tests/VanAn.Core.Tests/Integration/SQLiteConcurrencyIntegrationTests.cs` | Test context | Medium - test setup may need adjustment |
| `6_Tests/VanAn.Core.Tests/Performance/SQLiteConcurrencyPerformanceTests.cs` | Test context | Medium - test setup may need adjustment |

### Dependencies on DbSet<OutboxMessages>

| Context | Entity Type | Impact if Changed |
|---------|-------------|-------------------|
| ShopERPDbContext | `CoreOutboxMessage` (CoreHub.Infrastructure) | Critical - breaks all code using alias |
| VanAnDbContext | `OutboxMessage` (CoreHub.Infrastructure) | High - breaks CoreHub layer |
| IVanAnDbContext interface | `OutboxMessage` | High - breaks interface contract |
| Test files | Via `_context.OutboxMessages` | Medium - test queries may fail |

### Risk Assessment

| Change Type | Impact | Severity | Probability |
|-------------|--------|----------|-------------|
| Remove direct reference to `CoreHub.Infrastructure.Configurations` | Needs alternative configuration method | High | Certain |
| Change entity type (CoreOutboxMessage → Shared.OutboxMessage) | Breaks all code using alias | Critical | N/A (not recommended) |
| Delete wrong configuration file in ShopERP | No impact (file unused) | Low | None |
| Use assembly scanning | May apply unexpected configurations | Medium | Low |

---

## Detailed Coding Plan

### Phase 1: Preparation
**Objective:** Prepare infrastructure for safe refactoring

#### Task 1.1: Create Configuration Abstraction Layer
- **File:** `3_CoreHub/Infrastructure/Configurations/IEntityConfiguration.cs`
- **Action:** Create marker interface for configuration classes
- **Code:**
  ```csharp
  namespace VanAn.CoreHub.Infrastructure.Configurations
  {
      /// <summary>
      /// Marker interface for entity configurations
      /// Used for assembly scanning with filtering
      /// </summary>
      public interface IEntityConfiguration
      {
      }
  }
  ```
- **Estimated Time:** 5 min

#### Task 1.2: Mark CoreHub Configurations
- **File:** `3_CoreHub/Infrastructure/Configurations/OutboxMessageConfiguration.cs`
- **Action:** Implement `IEntityConfiguration` interface
- **Code:**
  ```csharp
  public class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>, IEntityConfiguration
  {
      // existing implementation unchanged
  }
  ```
- **Estimated Time:** 10 min

---

### Phase 2: Refactor ShopERPDbContext
**Objective:** Remove direct reference, use assembly scanning

#### Task 2.1: Delete Wrong Configuration File
- **File:** `5_WebApps/ShopERP/Infrastructure/Configurations/OutboxMessageConfiguration.cs`
- **Action:** Delete this file (configures Shared.Domain.OutboxMessage, not used)
- **Verification:** No code references this class
- **Estimated Time:** 2 min

#### Task 2.2: Replace Direct Reference with Assembly Scanning
- **File:** `5_WebApps/ShopERP/Infrastructure/ShopERPDbContext.cs`
- **Line:** 110
- **Before:**
  ```csharp
  // Apply configurations
  // Note: CoreHub OutboxMessage configuration is applied via CoreHub assembly
  modelBuilder.ApplyConfiguration(new CoreHub.Infrastructure.Configurations.OutboxMessageConfiguration());
  ```
- **After:**
  ```csharp
  // Apply configurations from CoreHub assembly via assembly scanning
  // This avoids direct reference to CoreHub.Infrastructure.Configurations
  var coreHubAssembly = typeof(VanAn.CoreHub.Infrastructure.OutboxMessage).Assembly;
  modelBuilder.ApplyConfigurationsFromAssembly(coreHubAssembly, 
      t => t.Name.EndsWith("Configuration") && t.GetInterface(typeof(IEntityConfiguration).Name) != null);
  ```
- **Estimated Time:** 10 min

#### Task 2.3: Reduce Using Statements (Optional)
- **File:** `5_WebApps/ShopERP/Infrastructure/ShopERPDbContext.cs`
- **Lines:** 6-8
- **Action:** Review and reduce direct using statements if possible
- **Note:** Keep `CoreOutboxMessage` alias as it's used throughout
- **Estimated Time:** 8 min

---

### Phase 3: Validation
**Objective:** Ensure no breakage of existing functionality

#### Task 3.1: Build Validation
- **Command:** `dotnet build VanAn.sln`
- **Expected:** 0 errors
- **On Failure:** Review compiler errors, rollback if critical
- **Estimated Time:** 5 min

#### Task 3.2: ShopERP Runtime Test
- **Action:** Start ShopERP via Visual Studio
- **Verification:**
  - No EF Core error on startup
  - OutboxMessages table created with correct schema
  - Application runs without errors
- **On Failure:** Check EF Core logs, rollback if schema mismatch
- **Estimated Time:** 10 min

#### Task 3.3: Test Validation
- **Commands:**
  ```bash
  dotnet test 6_Tests/VanAn.Core.Tests/VanAn.Core.Tests.csproj --filter "FullyQualifiedName~SimpleOutboxProcessor"
  dotnet test 6_Tests/VanAn.Core.Tests/VanAn.Core.Tests.csproj --filter "FullyQualifiedName~SQLiteConcurrency"
  ```
- **Expected:** All tests pass
- **On Failure:** Review test failures, check if configuration-related
- **Estimated Time:** 10 min

---

### Phase 4: Cleanup (Optional - Long-term)
**Objective:** Tidy up remaining architectural violations

#### Task 4.1: Review Program.cs Repository Registrations
- **File:** `5_WebApps/ShopERP/Program.cs`
- **Lines:** 91, 97-99
- **Action:** Evaluate if registrations can use assembly scanning
- **Note:** Not urgent, mark for future refactor
- **Estimated Time:** 15 min

#### Task 4.2: Review KhachLink Similar Violations
- **File:** `5_WebApps/KhachLink/Program.cs`
- **Lines:** 53, 77
- **Action:** Apply same pattern if needed
- **Note:** Depends on Phase 2-3 success
- **Estimated Time:** 15 min

---

## Estimation Summary

| Phase | Tasks | Estimated Time |
|-------|-------|----------------|
| Phase 1: Preparation | 2 tasks | 15 min |
| Phase 2: Refactor | 3 tasks | 20 min |
| Phase 3: Validation | 3 tasks | 25 min |
| Phase 4: Cleanup | 2 tasks (optional) | 30 min |
| **Total** | **10 tasks** | **90 min** |

---

## Rollback Plan

If Phase 2 or Phase 3 fails:

1. **Revert ShopERPDbContext.cs line 110**
   ```csharp
   // Restore direct reference
   modelBuilder.ApplyConfiguration(new CoreHub.Infrastructure.Configurations.OutboxMessageConfiguration());
   ```

2. **Restore OutboxMessageConfiguration.cs** if needed (from git)

3. **Build and test again**
   - Run `dotnet build VanAn.sln`
   - Start ShopERP to verify

**Rollback Time:** < 5 minutes

---

## Success Criteria

- [ ] Build succeeds with 0 errors
- [ ] ShopERP starts without EF Core errors
- [ ] OutboxMessages table has correct schema
- [ ] SimpleOutboxProcessor tests pass
- [ ] SQLiteConcurrency tests pass
- [ ] No direct reference to `CoreHub.Infrastructure.Configurations` in ShopERP
- [ ] Assembly scanning used with proper filtering

---

## Related Issues Found During Review

| # | File | Issue | Severity |
|---|------|-------|----------|
| 1 | `ShopERP/Program.cs:91, 97-99` | Direct DI registration of `CoreHub.Infrastructure.Repositories.*` | Medium |
| 2 | `KhachLink/Program.cs:53, 77` | Direct references to `CoreHub.Infrastructure` | Medium |
| 3 | `ShopERPDbContext.cs:6-8` | Direct using statements for `CoreHub.Infrastructure` | Low |

**Note:** These are marked for future refactoring (Phase 4) but not blocking current fix.

---

## References

- **Error Log:** EF Core InvalidOperationException on OutboxMessages table
- **Architecture Principle:** Clean Architecture - Dependency Rule (dependencies point inward)
- **Related Files:**
  - `5_WebApps/ShopERP/Infrastructure/ShopERPDbContext.cs`
  - `3_CoreHub/Infrastructure/Configurations/OutboxMessageConfiguration.cs`
  - `5_WebApps/ShopERP/Infrastructure/Configurations/OutboxMessageConfiguration.cs` (to delete)
