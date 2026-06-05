# Test Fix Plan
## Generated: 2026-05-22
## Total Failed Tests: 83

---

## Summary
- **VanAn.Core.Tests**: 2 failed (Performance issues)
- **VanAn.ShopERP.Tests**: 37 failed (ThemeProvider registration + Navigation rendering)
- **VanAn.Load.Tests**: 6 failed (DI configuration)
- **VanAn.Integration.Tests**: 38 failed (DI configuration)

---

## 1. VanAn.Core.Tests (2 failed)

### 1.1 OutboxPerformanceTest_ShouldProcessOutboxMessages_Efficiently
- **File**: `6_Tests/VanAn.Core.Tests/Performance/SQLiteConcurrencyPerformanceTests.cs`
- **Error**: Expected at least 80% processed, got 0/50
- **Root Cause**: Outbox processing mechanism not working in test environment
- **Fix Steps**:
  1. Verify outbox configuration in test setup
  2. Check if outbox background service is properly registered
  3. Ensure outbox table exists in test database
  4. Add logging to diagnose why 0 messages are processed
  5. Consider mocking outbox processor or adjusting test expectations

### 1.2 LatencyTest_ShouldProcessOrders_WithAcceptableLatency
- **File**: `6_Tests/VanAn.Core.Tests/Performance/SQLiteConcurrencyPerformanceTests.cs`
- **Error**: Average latency 311.589ms exceeds 250ms
- **Root Cause**: Performance threshold too strict for current implementation
- **Fix Steps**:
  1. Analyze latency distribution (max was 10008.77ms - indicates outliers)
  2. Consider adjusting threshold to 500ms or implementing outlier removal
  3. Optimize SQLite operations if possible
  4. Check for blocking operations in order processing
  5. Consider using in-memory database for better performance

---

## 2. VanAn.ShopERP.Tests (37 failed)

### 2.1 Component Tests (34 tests)
- **Affected Tests**: All accounting component tests (ExpenseEntry, AccountBalance, TransactionHistory, etc.)
- **Error**: `Cannot provide a value for property 'ThemeProvider' on type... There is no registered service of type 'VanAn.UI.Platform.Services.IThemeProvider'`
- **Root Cause**: IThemeProvider service not registered in test DI container
- **Fix Steps**:
  1. Locate test setup file (likely in `VanAn.ShopERP.Tests/Infrastructure/` or test base class)
  2. Add mock or real IThemeProvider registration:
     ```csharp
     services.AddScoped<IThemeProvider, MockThemeProvider>();
     // or
     services.AddScoped<IThemeProvider, ThemeProvider>();
     ```
  3. Create mock implementation if needed:
     ```csharp
     public class MockThemeProvider : IThemeProvider
     {
         public string CurrentTheme => "light";
         public void SetTheme(string theme) { }
     }
     ```
  4. Verify all component tests use the updated test setup

### 2.2 Navigation Tests (3 tests)
- **Affected Tests**:
  - `AccountingLayout_ShouldRenderMenuLabelsInVietnamese_WhenMounted`
  - `AccountingLayout_ShouldRenderFiveMenuItems_WhenComponentMounted`
  - `AccountingLayout_ShouldContainAllRequiredRoutes_WhenRendered`
- **Errors**: 
  - Expected navText to contain "Doanh thu"
  - Expected 5 menu items but found 0
  - Expected links to contain "/accounting"
- **Root Cause**: Components not rendering properly due to missing ThemeProvider
- **Fix Steps**:
  1. Fix ThemeProvider registration first (see 2.1)
  2. Verify AccountingLayout component has proper dependencies injected
  3. Check if navigation menu data is properly initialized
  4. Review component rendering logic in AccountingLayout
  5. Add assertions to verify component state before checking navigation

---

## 3. VanAn.Load.Tests (6 failed)

### 3.1 All Load Tests
- **File**: `6_Tests/VanAn.Load.Tests/SimpleLoadTests.cs`
- **Error**: Multiple service resolution failures:
  - `Unable to resolve service for type 'VanAn.CoreHub.Repositories.IOrderRepository'`
  - `Unable to resolve service for type 'VanAn.CoreHub.Infrastructure.IVanAnDbContext'`
  - `Unable to resolve service for type 'VanAn.CoreHub.Repositories.ISocialCampaignRepository'`
  - `Unable to resolve service for type 'VanAn.CoreHub.Repositories.ILoyaltyRewardsRepository'`
- **Root Cause**: Gateway application DI configuration missing repository registrations
- **Fix Steps**:
  1. Check `2_Gateway/Program.cs` for service registration
  2. Add missing repository registrations:
     ```csharp
     services.AddScoped<IOrderRepository, OrderRepository>();
     services.AddScoped<ISocialCampaignRepository, SocialCampaignRepository>();
     services.AddScoped<ILoyaltyRewardsRepository, LoyaltyRewardsRepository>();
     services.AddScoped<IVanAnDbContext, VanAnDbContext>();
     ```
  3. Ensure VanAnDbContext is properly configured with connection string
  4. Verify all repository implementations exist in CoreHub
  5. Consider using test-specific WebApplicationFactory with proper configuration

---

## 4. VanAn.Integration.Tests (38 failed)

### 4.1 All Integration Tests
- **File**: `6_Tests/VanAn.Integration.Tests/Infrastructure/HttpIntegrationTestBase.cs`
- **Error**: `Unable to resolve service for type 'VanAn.CoreHub.Infrastructure.IVanAnDbContext'`
- **Root Cause**: Integration test setup not registering IVanAnDbContext and related services
- **Fix Steps**:
  1. Review `HttpIntegrationTestBase.cs` constructor and ConfigureWebHost method
  2. Add service registrations in test setup:
     ```csharp
     builder.ConfigureServices(services =>
     {
         services.AddScoped<IVanAnDbContext, VanAnDbContext>();
         services.AddScoped<ICustomerRepository, CustomerRepository>();
         services.AddScoped<IOrderRepository, OrderRepository>();
         // Add other missing repositories
     });
     ```
  3. Configure test database connection (use in-memory SQLite for tests)
  4. Ensure database migrations are run before tests
  5. Create test data seeding if needed

---

## Priority Order

1. **HIGH - VanAn.ShopERP.Tests ThemeProvider**: Blocks 34 tests, simple fix
2. **HIGH - VanAn.Integration.Tests DI**: Blocks 38 tests, affects integration testing
3. **HIGH - VanAn.Load.Tests DI**: Blocks 6 tests, affects load testing
4. **MEDIUM - VanAn.Core.Tests Performance**: 2 tests, may require investigation
5. **LOW - VanAn.ShopERP.Tests Navigation**: Dependent on ThemeProvider fix

---

## Dependencies

- ShopERP Navigation tests depend on ThemeProvider fix
- Load tests and Integration tests may share similar DI configuration patterns
- Performance tests may need environment optimization

---

## Estimated Time

- ThemeProvider registration: 30 minutes
- Integration Tests DI fix: 1 hour
- Load Tests DI fix: 30 minutes
- Performance tests investigation: 1-2 hours
- Navigation tests fix (after ThemeProvider): 30 minutes

**Total Estimated Time**: 3.5 - 4.5 hours

---

## Verification Steps

After each fix:
1. Run specific test project: `dotnet test 6_Tests/[ProjectName]/[ProjectName].csproj`
2. Verify failed count decreases
3. Check for new errors introduced
4. Run full test suite: `dotnet test 6_Tests/VanAn.Tests.sln`
5. Verify coverage remains acceptable

---

## Notes

- All DI-related failures suggest a systematic issue with service registration in test environments
- Consider creating a shared test utilities project with common DI setup
- ThemeProvider issue indicates missing UI.Platform service registration in test setup
- Performance test failures may indicate need for test-specific configuration optimizations
