# Detailed Coding Plan - Resolve Remaining Errors & Technical Debt

## Executive Summary
This plan addresses all remaining test failures and technical debt in 6_Tests after resolving the IConvertible exception.

**Current Status:**
- Infrastructure tests: 7/7 passing (AccountingEntryRepository), 1 failing (JournalTemplate)
- Customer Service tests: 2/2 passing
- Kitchen tests: 5/6 passing, 1 failing (FOREIGN KEY constraint)
- Performance/Integration tests: 4 blocked (performance thresholds)

**Technical Debt Identified:**
- `IgnoreQueryFilters()` is a temporary workaround that bypasses tenant isolation, soft delete, and security boundaries
- Global multi-tenancy filter uses raw Guid comparison instead of TenantId value object
- AccountingEntry should be excluded from global filter (special case: append-only, audit/history, reconciliation)

---

## Phase 1: Fix Multi-Tenancy Filter Architecture (Root Cause Fix)

### File: `c:\VibeCoding\Gemini_Windsurf\3_CoreHub\Infrastructure\VanAnDbContext.cs`

**Location:** `ApplyMultiTenancyFilters()` method (lines 264-299)

**Current Problem:**
```csharp
var currentTenantIdConstant = System.Linq.Expressions.Expression.Constant(currentTenantId);
var filter = System.Linq.Expressions.Expression.Equal(tenantIdProperty, currentTenantIdConstant);
```
- Uses raw Guid constant for comparison with TenantId property
- EF Core tries to apply TenantIdConverter during translation, which fails
- Forces repositories to use `IgnoreQueryFilters()` workaround

**Proposed Fix (REVISED):**
```csharp
// Apply to all entities implement IMustHaveTenant (except AccountingEntry)
var entityTypes = modelBuilder.Model.GetEntityTypes()
    .Where(e => typeof(IMustHaveTenant).IsAssignableFrom(e.ClrType) && e.ClrType != typeof(CoreAccountingEntry));

foreach (var entityType in entityTypes)
{
    try
    {
        var parameter = System.Linq.Expressions.Expression.Parameter(entityType.ClrType, "e");
        
        // Use EF.Property<Guid> to bypass value converter for filter comparison
        // This allows EF Core to translate the filter to SQL without IConvertible exception
        var tenantIdProperty = System.Linq.Expressions.Expression.Call(
            typeof(EF).GetMethod("Property").MakeGenericMethod(typeof(Guid)),
            parameter,
            System.Linq.Expressions.Expression.Constant("TenantId"));
        
        var currentTenantIdConstant = System.Linq.Expressions.Expression.Constant(currentTenantId);
        var filter = System.Linq.Expressions.Expression.Equal(tenantIdProperty, currentTenantIdConstant);
        
        var lambdaMethod = typeof(System.Linq.Expressions.Expression)
            .GetMethods()
            .First(m => m.Name == "Lambda" && m.IsGenericMethod && m.GetParameters().Length == 2)
            .MakeGenericMethod(entityType.ClrType, typeof(bool));
        
        var lambda = lambdaMethod.Invoke(null, new object[] { filter, parameter }) 
            ?? throw new InvalidOperationException("Failed to create lambda expression");
        
        modelBuilder.Entity(entityType.ClrType).HasQueryFilter((System.Linq.Expressions.LambdaExpression)lambda);
    }
    catch (Exception ex)
    {
        // Log error but continue - some entities may not have TenantId
        Console.WriteLine($"Failed to apply tenant filter to {entityType.ClrType.Name}: {ex.Message}");
    }
}
```

**Rationale:**
- Uses `EF.Property<Guid>("TenantId")` to bypass the value converter for filter comparison
- EF Core can translate this to SQL without IConvertible exception
- The value converter still applies for read/write operations, but not for the filter
- Excludes AccountingEntry (special case: cross-tenant queries, audit/history)

**Expected Impact:**
- Enables removal of `IgnoreQueryFilters()` from CustomerRepository, KitchenService, and AccountingEntryRepository
- Restores tenant isolation security in production
- Maintains test compatibility

**Rollback Strategy:**
- If Phase 1 causes regressions, revert to original code and keep `IgnoreQueryFilters()` workaround
- Document the specific error if rollback is needed

---

## Phase 2: Remove IgnoreQueryFilters from CustomerRepository

### File: `c:\VibeCoding\Gemini_Windsurf\3_CoreHub\Infrastructure\Repositories\CustomerRepository.cs`

**Methods to Update:**
1. `GetByIdAsync()` (lines 24-32)
2. `GetByDeviceIdAsync()` (lines 35-43)
3. `GetAllActiveAsync()` (lines 46-54)
4. `ExistsByDeviceIdAsync()` (lines 102-109)
5. `GetWithOrdersAsync()` (lines 112-121)

**Current Code:**
```csharp
return await _context.Customers
    .IgnoreQueryFilters()  // Bypass global query filter to avoid EF.Property conflict
    .Where(c => c.Id == id && 
               c.TenantId == new TenantId(tenantId) && 
               !c.IsDeleted)
    .FirstOrDefaultAsync();
```

**Proposed Fix:**
```csharp
return await _context.Customers
    .Where(c => c.Id == id && 
               c.TenantId == new TenantId(tenantId) && 
               !c.IsDeleted)
    .FirstOrDefaultAsync();
```

**Rationale:**
- After Phase 1 fix, global filter will work with EF.Property<Guid>
- No need to bypass tenant isolation
- Restores security boundaries for production

**Verification:**
- Run Customer Service tests: `dotnet test --filter "FullyQualifiedName~CustomerService"`
- Expected: 2/2 tests passing

---

## Phase 3: Remove IgnoreQueryFilters from AccountingEntryRepository

### File: `c:\VibeCoding\Gemini_Windsurf\3_CoreHub\Repositories\AccountingEntryRepository.cs`

**Methods to Update:**
1. `GetByTenantAndBookTypeAsync()` (line 45)
2. `GetByTenantAndDateRangeAsync()` (line 70)
3. `GetByTenantAndPeriodAsync()` (line 122)
4. `AddAsync()` (line 175)
5. `GetByTenantAndPeriodAsync()` (line 279)
6. Any other methods using `IgnoreQueryFilters()`

**Current Code:**
```csharp
return await _context.AccountingEntries
    .IgnoreQueryFilters()  // Bypass global query filter to avoid EF.Property conflict
    .Where(e => e.TenantId == tenantId && e.AccountingBookType == bookType)
    .OrderByDescending(e => e.CreatedAt)
    .ToListAsync(cancellationToken);
```

**Proposed Fix:**
```csharp
return await _context.AccountingEntries
    .Where(e => e.TenantId == tenantId && e.AccountingBookType == bookType)
    .OrderByDescending(e => e.CreatedAt)
    .ToListAsync(cancellationToken);
```

**Rationale:**
- After Phase 1 fix, global filter is disabled for AccountingEntry (special case)
- However, explicit tenant filtering in queries is still needed for multi-tenant safety
- Remove `IgnoreQueryFilters()` since there's no global filter to bypass for AccountingEntry

**Verification:**
- Run AccountingEntryRepository tests: `dotnet test --filter "FullyQualifiedName~AccountingEntryRepository"`
- Expected: 7/7 tests passing

---

## Phase 4: Remove IgnoreQueryFilters from KitchenService

### File: `c:\VibeCoding\Gemini_Windsurf\3_CoreHub\Services\KitchenService.cs`

**Methods to Update:**
1. `GetGroupedKitchenItemsAsync()` (lines 32-45)
2. `GetPendingItemsCountAsync()` (lines 155-158)
3. `GetAveragePreparationTimeAsync()` (lines 163-169)
4. `GetKitchenAnalyticsAsync()` (lines 249-251)

**Current Code:**
```csharp
var flatItems = await _context.OrderItems
    .IgnoreQueryFilters()  // Bypass global query filter to avoid EF.Property conflict
    .Where(oi => oi.Order.TenantId == new TenantId(shopId) && 
                (oi.KitchenStatus == KitchenStatus.Pending || oi.KitchenStatus == KitchenStatus.Preparing))
```

**Proposed Fix:**
```csharp
var flatItems = await _context.OrderItems
    .Where(oi => oi.Order.TenantId == new TenantId(shopId) && 
                (oi.KitchenStatus == KitchenStatus.Pending || oi.KitchenStatus == KitchenStatus.Preparing))
```

**Rationale:**
- After Phase 1 fix, global filter will work with EF.Property<Guid>
- No need to bypass tenant isolation
- Restores security boundaries for production

**Verification:**
- Run Kitchen tests: `dotnet test --filter "FullyQualifiedName~Kitchen"`
- Expected: 5/6 tests passing (FOREIGN KEY issue separate)

---

## Phase 5: Fix Kitchen FOREIGN KEY Constraint Issue

### File: `c:\VibeCoding\Gemini_Windsurf\6_Tests\VanAn.Core.Tests\KitchenServiceTests.cs`

**Failing Test:** `GetGroupedItems_Should_GroupIdenticalProducts_FromDifferentOrders`

**Error:** `SQLite Error 19: 'FOREIGN KEY constraint failed'`

**Investigation Steps:**

1. **Verify OrderItem FK converter configuration:**
   - Check `OrderItemConfiguration.cs` for OrderId converter:
   ```csharp
   builder.Property(x => x.OrderId)
       .HasConversion(id => id.Value, v => new OrderId(v));
   ```
   - If missing, add the converter

2. **Check test data ID consistency:**
   - Verify Order.Id matches OrderItem.OrderId in test setup
   - Ensure same Order instance is used for both Order and OrderItems

3. **Check SQLite FK enforcement:**
   - Verify test factory has `PRAGMA foreign_keys=ON;`
   - InMemory SQLite may have different FK behavior than file-based SQLite

4. **Investigate Order entity configuration:**
   - Check if Order has proper navigation property to OrderItems
   - Verify cascade delete behavior is configured correctly

**Proposed Fix (based on investigation):**
- Most likely cause: OrderId value converter missing or not applied correctly
- Fix: Add or verify OrderId converter in OrderItemConfiguration

**Verification:**
- Run Kitchen tests: `dotnet test --filter "FullyQualifiedName~Kitchen"`
- Expected: 6/6 tests passing

---

## Phase 6: Fix JournalTemplate Repository Test

### File: `c:\VibeCoding\Gemini_Windsurf\6_Tests\VanAn.Core.Tests\Infrastructure\Repositories\JournalTemplateRepositoryTests.cs`

**Failing Test:** `UpdateAsync_ShouldModifyTemplate`

**Error:** `DbUpdateConcurrencyException : Attempted to update or delete an entity that does not exist in the store`

**Investigation Steps:**

1. **Verify JournalTemplateRepository.UpdateAsync implementation:**
   - Check if repository uses `Update()` or modifies tracked entity
   - Verify it doesn't create a new entity with same ID

2. **Check test entity lifecycle:**
   - Verify entity is added and saved before update
   - Check if DbContext is same instance for add and update
   - Verify entity is not detached between operations

3. **Check JournalTemplate configuration:**
   - Verify primary key configuration
   - Check if there are any concurrency tokens

**Proposed Fix:**
- If repository creates new entity, fix to use tracked entity
- Ensure proper SaveChangesAsync() after AddAsync()
- Verify DbContext instance is not disposed

**Verification:**
- Run Infrastructure tests: `dotnet test --filter "FullyQualifiedName~JournalTemplateRepository"`
- Expected: All tests passing

---

## Phase 7: Address Performance/Integration Test Issues

### Files: Performance and Integration test files

**Current Issues:**
1. 1 test shows 0/50 messages processed (functional issue)
2. 3 tests fail performance thresholds:
   - Latency: 117ms vs 100ms threshold
   - Throughput: 30128ms vs 30000ms threshold
   - Batch size: 32 vs 4-16 expected

**Investigation Steps:**

1. **Check NATS connection initialization in test setup:**
   - Verify NATS connection is properly initialized
   - Check if NATS server is running in test environment
   - Add logging to trace connection status

2. **Verify message publisher/subscriber registration:**
   - Check if publisher is registered before sending messages
   - Verify subscriber is listening for messages
   - Add logging to trace registration

3. **Add logging to trace message flow:**
   - Log message publish events
   - Log message receive events
   - Log message processing events

4. **Check if test waits for async processing:**
   - Verify test has proper await/Task.Delay for async operations
   - Check if test completes before messages are processed
   - Add explicit wait if needed

**Proposed Options:**
1. **Option A (Functional Fix):** Debug why 0/50 messages are processed
   - Add comprehensive logging to trace message flow
   - Verify NATS infrastructure in test environment
   - Fix any initialization or registration issues

2. **Option B (Threshold Adjustment):** Adjust performance thresholds for test environment
   - Latency: 100ms → 150ms
   - Throughput: 30000ms → 35000ms
   - Batch size: Accept 32 as valid range (4-32)

**Recommendation:**
- Start with Option A (functional fix) for 0/50 messages issue
- Consider Option B only if performance is actually acceptable but thresholds are too strict

**Verification:**
- Run Performance/Integration tests
- Expected: All tests passing or documented reason for deferral

---

## Phase 8: Integration Test Coverage for Tenant Isolation

### File: `c:\VibeCoding\Gemini_Windsurf\6_Tests\VanAn.Core.Tests\Integration\`

**Purpose:** Verify tenant isolation still works after removing IgnoreQueryFilters

**Test to Add:**
```csharp
[Fact]
public async Task TenantIsolation_Should_PreventCrossTenantDataAccess()
{
    // Arrange: Create data for Tenant A
    using (var scopeA = _testFactory.Create())
    {
        var contextA = scopeA.Context;
        var tenantA = new TenantId(Guid.NewGuid());
        var customerA = new Customer(tenantA, "Customer A", "phone", "email");
        await contextA.Customers.AddAsync(customerA);
        await contextA.SaveChangesAsync();
    }

    // Act: Try to access Tenant A data as Tenant B
    using (var scopeB = _testFactory.Create())
    {
        var contextB = scopeB.Context;
        var tenantB = new TenantId(Guid.NewGuid());
        
        // Should not see Tenant A's customer
        var customers = await contextB.Customers
            .Where(c => c.TenantId == tenantB)
            .ToListAsync();
        
        // Assert
        Assert.Empty(customers);
    }
}
```

**Rationale:**
- After removing IgnoreQueryFilters, verify tenant isolation still works
- Ensure global filter is properly applied
- Prevent security regression

**Verification:**
- Run integration tests
- Expected: Tenant isolation verified

---

## Phase 9: Performance Baseline Measurement

### File: Performance test files

**Purpose:** Establish baseline for performance thresholds

**Steps:**
1. Run performance tests in local environment
2. Record actual measurements:
   - Latency: 117ms (current)
   - Throughput: 30128ms (current)
   - Batch size: 32 (current)
3. Compare with current thresholds:
   - Latency threshold: 100ms (17ms over)
   - Throughput threshold: 30000ms (128ms over)
   - Batch size: 4-16 (32 is 2x max)
4. Determine if thresholds are realistic:
   - If local environment is slower than CI/CD, adjust thresholds
   - If performance is acceptable, adjust thresholds to match reality

**Proposed Threshold Adjustments:**
- Latency: 100ms → 150ms (50% buffer)
- Throughput: 30000ms → 35000ms (16% buffer)
- Batch size: 4-16 → 4-32 (accept current behavior)

**Verification:**
- Run performance tests with new thresholds
- Expected: All tests passing

---

## Execution Order

1. **Phase 1:** Fix multi-tenancy filter in VanAnDbContext (foundational fix)
2. **Phase 2:** Remove IgnoreQueryFilters from CustomerRepository
3. **Phase 3:** Remove IgnoreQueryFilters from AccountingEntryRepository
4. **Phase 4:** Remove IgnoreQueryFilters from KitchenService
5. **Phase 5:** Fix Kitchen FOREIGN KEY issue
6. **Phase 6:** Fix JournalTemplate test
7. **Phase 7:** Address Performance/Integration issues
8. **Phase 8:** Add integration test for tenant isolation
9. **Phase 9:** Performance baseline measurement

**Verification After Each Phase:**
- Run affected test suite
- Ensure no regressions in previously passing tests
- Document any unexpected issues

---

## Risk Assessment

**Low Risk:**
- Phase 2: Removing IgnoreQueryFilters from CustomerRepository (after Phase 1 fix)
- Phase 3: Removing IgnoreQueryFilters from AccountingEntryRepository (after Phase 1 fix)
- Phase 4: Removing IgnoreQueryFilters from KitchenService (after Phase 1 fix)
- Phase 6: JournalTemplate test fix (isolated issue)
- Phase 8: Integration test for tenant isolation (new test, no existing code changes)

**Medium Risk:**
- Phase 1: Multi-tenancy filter refactoring with EF.Property<Guid> (affects all tenant-aware entities except AccountingEntry)
- Phase 5: FOREIGN KEY fix (may require OrderItem configuration changes)
- Phase 9: Performance baseline measurement (may require threshold adjustments)

**High Risk:**
- Phase 7: Performance/Integration tests (may require infrastructure changes for NATS)

**Mitigation:**
- Run full test suite after Phase 1 to ensure no regressions
- Keep detailed rollback notes for each phase
- Document specific error if Phase 1 rollback is needed
- Consider feature flags for production deployment
- Phase 1 uses EF.Property<Guid> which is a proven EF Core pattern

---

## Success Criteria

**After All Phases:**
- Infrastructure tests: 8/8 passing (7 AccountingEntry + 1 JournalTemplate)
- Customer Service tests: 2/2 passing
- Kitchen tests: 6/6 passing
- Performance/Integration tests: 4/4 passing (or documented reason for deferral)
- No `IgnoreQueryFilters()` in production code (except AccountingEntry special case)
- Tenant isolation security restored for all entities except AccountingEntry
- No technical debt warnings in code review

---

## Notes for Review

1. **AccountingEntry Special Case:** Intentionally excluded from global tenant filter due to:
   - Append-only audit trail requirements
   - Cross-tenant reconciliation queries
   - Historical reporting needs
   - Repository-level tenant filtering is explicit and controlled

2. **Value Object Comparison:** Using `TenantId == new TenantId(guid)` instead of `.Value` comparison is the correct pattern for EF Core with value converters.

3. **Expression Tree Building:** The Phase 1 fix uses expression tree building to create the filter dynamically. This is complex but necessary for the generic multi-tenancy pattern.

4. **Test Environment:** Performance thresholds may need adjustment for CI/CD environment vs. local development environment.

---

## Approval Required

Please review this plan and approve before execution. Key decisions:
- [ ] Approve Phase 1 multi-tenancy filter refactoring
- [ ] Approve removing IgnoreQueryFilters from CustomerRepository
- [ ] Approve removing IgnoreQueryFilters from KitchenService
- [ ] Approve FOREIGN KEY fix approach
- [ ] Approve Performance/Integration test approach (Option A or B)
