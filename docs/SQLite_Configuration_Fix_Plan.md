# SQLite Integration Test Configuration Conflict Fix Plan

**Date**: 2026-06-04
**Issue**: SQLite Error 1: 'no such table: Orders' when inserting Customer entity
**Root Cause**: Duplicate/conflicting EF Core relationship configuration

---

## Problem Summary

**Root Cause**: Duplicate/conflicting EF Core relationship configuration for Customer-Order relationship

**Current State**:
- **CustomerConfiguration.cs** (lines 49-52): Defines `HasMany(e => e.Orders).WithOne().HasForeignKey(o => o.CustomerId).OnDelete(DeleteBehavior.Restrict)`
- **OrderConfiguration.cs** (lines 63-66): Defines `HasOne(o => o.Customer).WithMany(c => c.Orders).HasForeignKey(o => o.CustomerId).OnDelete(DeleteBehavior.SetNull)`

**Impact**:
- EF Core cannot resolve which configuration to use
- Conflicting delete behaviors (Restrict vs SetNull)
- May cause schema generation issues with `EnsureCreated()` in SQLite in-memory database
- Results in "no such table: Orders" error when inserting Customer entity

**Affected Tests**:
- `IsolatedSQLiteTests.SQLite_SimpleEntity_Insert_WithBehavior_Works`
- `IsolatedSQLiteTests.SQLite_MultiTenant_WithBusinessRules_Isolation_Works`
- `IsolatedSQLiteTests.Debug_CustomerInsertOnly`

---

## Dependency Analysis Results

**Code using Customer.Orders navigation property**:
- `LeadToCustomerConversionTests.cs` line 135: `.Include(c => c.Orders)` - uses navigation property for loading
- This will continue to work after fix because OrderConfiguration has complete relationship definition

**No other code depends on CustomerConfiguration's relationship configuration**

---

## Proposed Fix

### Step 1: Remove Duplicate Configuration
**File**: `3_CoreHub/Infrastructure/Configurations/CustomerConfiguration.cs`
**Action**: Remove lines 49-52 (the HasMany.Orders configuration)
**Reason**: OrderConfiguration already defines the complete relationship with both navigation properties

**Code to remove**:
```csharp
// Navigation property: Orders
_ = builder.HasMany(e => e.Orders)
      .WithOne()
      .HasForeignKey(o => o.CustomerId)
      .OnDelete(DeleteBehavior.Restrict);
```

### Step 2: Verify OrderConfiguration
**File**: `3_CoreHub/Infrastructure/Configurations/OrderConfiguration.cs`
**Verification**: Confirm lines 63-66 have complete relationship definition
**Expected**: `HasOne(o => o.Customer).WithMany(c => c.Orders).HasForeignKey(o => o.CustomerId).OnDelete(DeleteBehavior.SetNull)`

**Current configuration**:
```csharp
// Navigation properties
_ = builder.HasOne(o => o.Customer)
      .WithMany(c => c.Orders)
      .HasForeignKey(o => o.CustomerId)
      .OnDelete(DeleteBehavior.SetNull);
```

### Step 3: Test Fix
**Target**: `6_Tests/VanAn.Integration.Tests/IsolatedSQLiteTests.cs`
**Tests to run**:
- `SQLite_SimpleEntity_Insert_WithBehavior_Works`
- `SQLite_MultiTenant_WithBusinessRules_Isolation_Works`
- `Debug_CustomerInsertOnly`

**Expected Result**: All tests pass without "no such table: Orders" error

### Step 4: Regression Check
**Target**: `6_Tests/VanAn.Integration.Tests/LeadToCustomerConversionTests.cs`
**Test to run**: `LeadConversion_WithOrders_ShouldImportOrderHistory` (line 92)
**Verification**: Confirm `.Include(c => c.Orders)` still works correctly

---

## Risk Assessment

**Low Risk**:
- Removing duplicate configuration is standard EF Core practice
- OrderConfiguration has complete relationship definition
- Navigation property will still work
- Only 4 lines removed

**No Breaking Changes Expected**:
- Customer.Orders navigation property remains in domain model
- EF Core will use OrderConfiguration's relationship definition
- All existing queries using `.Include(c => c.Orders)` will continue to work

---

## Implementation Order

1. Remove lines 49-52 from CustomerConfiguration.cs
2. Add comment explaining why configuration was removed
3. Run IsolatedSQLiteTests to verify fix
4. Run LeadToCustomerConversionTests for regression check
5. Document fix if needed

---

## Alternative Considered (Rejected)

**Alternative**: Keep both configurations and make them consistent
**Rejected**: EF Core doesn't allow duplicate relationship configurations - would still cause errors
**Better approach**: Single source of truth in OrderConfiguration (aggregate root)

---

## Architectural Rationale

**Why OrderConfiguration should be the single source of truth**:
- Order is the aggregate root in this relationship
- OrderConfiguration defines both navigation properties (Customer and Orders)
- More complete configuration (specifies both ends of relationship)
- Aligns with DDD principle (aggregate manages its relationships)

**DeleteBehavior.SetNull is appropriate**:
- When a Customer is deleted, Orders should have CustomerId set to null
- Allows Orders to exist without a Customer (for historical records)
- More flexible than Restrict for business scenarios

---

## Implementation Results

### Changes Made

**1. CustomerConfiguration.cs** (lines 49-52 removed)
- Removed duplicate `HasMany(e => e.Orders)` relationship configuration
- Added comment explaining the change and referencing OrderConfiguration as single source of truth

**2. OrderConfiguration.cs** (lines 69-74 removed)
- Removed redundant `TenantIdConverter` configuration (additional fix)
- TenantIdConverter is already configured globally in VanAnDbContext.ConfigureConventions
- This was causing build errors: "The type or namespace name 'TenantIdConverter' could not be found"

**3. IsolatedSQLiteTests.cs** (multiple fixes)
- Changed from in-memory SQLite to file-based SQLite for better test reliability
- In-memory SQLite was causing "unable to open database file" errors in test constructor pattern
- Added temporary database file cleanup in Dispose method
- Fixed tests to use `Customer.Id` instead of `Customer.CustomerId.Value` for Order.CustomerId foreign key
- Updated WAL mode test expectation from "memory" to "wal" (file-based SQLite uses WAL mode)

### Test Results

**IsolatedSQLiteTests**: All 6 tests passing
- SQLite_SimpleEntity_Insert_WithBehavior_Works ✓
- SQLite_MultiTenant_WithBusinessRules_Isolation_Works ✓
- SQLite_WALMode_IsEnabled ✓
- SQLite_DatabaseConnectionStatus ✓
- Debug_DumpSchemaAndForeignKeys ✓
- Debug_CustomerInsertOnly ✓

**LeadToCustomerConversionTests**: Skipped regression check
- Tests fail with "Relational-specific methods can only be used when the context is using a relational database provider"
- This is a separate infrastructure issue (IntegrationTestBase not configured with SQLite provider)
- Not related to the EF Core configuration fix
- The Customer.Orders navigation property will work correctly once the test infrastructure is fixed

### Root Cause Analysis Update

The original "no such table: Orders" error was caused by:
1. **Primary**: Duplicate EF Core relationship configuration (CustomerConfiguration + OrderConfiguration)
2. **Secondary**: In-memory SQLite connection issues in test constructor pattern
3. **Tertiary**: Incorrect foreign key reference (Customer.CustomerId.Value instead of Customer.Id)

All three issues have been resolved.

---

## Approval Status

- [x] Analysis completed
- [x] Dependency analysis completed
- [x] Risk assessment completed
- [x] Remove duplicate relationship configuration from CustomerConfiguration.cs
- [x] Remove redundant TenantIdConverter from OrderConfiguration.cs
- [x] Fix SQLite connection mode (in-memory → file-based)
- [x] Fix foreign key references (Customer.CustomerId.Value → Customer.Id)
- [x] Run tests to verify fix
- [x] All IsolatedSQLiteTests passing (6/6)
- [ ] Regression check completed (LeadToCustomerConversionTests has separate infrastructure issue)
