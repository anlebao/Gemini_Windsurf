# Reverse Impact Analysis + TDD Plan
**Date:** 2026-05-23
**Task:** Fix Business Logic Tests for ShopERP Accounting Components

## 1. Reverse Impact Analysis

### 1.1 Impact Scope

**Files to Modify:**
1. `UI.Platform/Components/Composite/DynamicForm.razor` - Fix inheritance issue
2. `6_Tests/VanAn.ShopERP.Tests/Components/ComponentTestBase.cs` - Add missing services
3. `6_Tests/VanAn.ShopERP.Tests/Components/Accounting/RevenueEntryTests.cs` - Rewrite with business logic
4. `6_Tests/VanAn.ShopERP.Tests/Components/Accounting/ExpenseEntryTests.cs` - Rewrite with business logic
5. `6_Tests/VanAn.ShopERP.Tests/Components/Accounting/AccountBalanceTests.cs` - Rewrite with business logic
6. `6_Tests/VanAn.ShopERP.Tests/Components/Accounting/TransactionHistoryTests.cs` - Rewrite with business logic
7. `6_Tests/VanAn.ShopERP.Tests/Components/Accounting/TransactionDetailModalTests.cs` - Rewrite with business logic

**Files to Read (for reference):**
- `5_WebApps/ShopERP/Components/Pages/Accounting/*.razor` - All accounting pages
- `UI.Platform/Components/*.razor` - UI Platform components
- `1_Shared/Domain/*.cs` - Domain entities
- `1_Shared/DTOs/*.cs` - DTOs

### 1.2 Dependency Impact

**Upstream Dependencies (must fix first):**
1. **DynamicForm.razor** - BaseComponent class missing
   - Impact: All forms using DynamicForm will fail to render
   - Fix: Change inheritance to VanAnComponentBase
   - Risk: LOW - simple inheritance change

2. **ComponentTestBase.cs** - Missing NavigationManager and JSInterop
   - Impact: All component tests that use navigation or JS will fail
   - Fix: Add NavigationManager mock and JSInterop configuration
   - Risk: LOW - standard Bunit patterns

**Downstream Dependencies (will be fixed after upstream):**
1. RevenueEntryTests - depends on ComponentTestBase and DynamicForm
2. ExpenseEntryTests - depends on ComponentTestBase and DynamicForm
3. AccountBalanceTests - depends on ComponentTestBase
4. TransactionHistoryTests - depends on ComponentTestBase
5. TransactionDetailModalTests - depends on ComponentTestBase

### 1.3 Risk Assessment

**High Risk:**
- None identified

**Medium Risk:**
- DynamicForm inheritance change might affect other components using it
  - Mitigation: Search for all usages of DynamicForm before changing
  - Rollback plan: Revert if tests fail unexpectedly

**Low Risk:**
- ComponentTestBase changes are additive (adding services, not removing)
- Test rewrites are isolated to test files only
- No production code changes except DynamicForm inheritance

### 1.4 Business Logic Impact

**Critical Business Logic to Test:**

**RevenueEntry:**
- Amount validation (must be > 0)
- Date validation (must be valid date)
- Account code validation (must be 5xx or 7xx for revenue)
- Duplicate detection (same amount + date + account within 5 minutes)
- Service call verification (CreateRevenueEntryAsync)
- Success alert display
- Error alert display

**ExpenseEntry:**
- Amount validation (must be > 0)
- Date validation (must be valid date)
- Account code validation (must be 6xx for expense)
- Vendor validation (required for material/maintenance categories)
- Duplicate detection (same amount + date + account within 5 minutes)
- Service call verification (CreateExpenseEntryAsync)
- Success alert display
- Error alert display

**AccountBalance:**
- Metrics card rendering with correct data
- Trend calculation (current vs previous period)
- Warning alert when expense > 150% revenue
- Data grid rendering with account balances
- Negative balance alert
- Refresh functionality

**TransactionHistory:**
- Search bar rendering
- Data grid rendering with transactions
- Search functionality (filter by description)
- Period filter (month/year)
- Account type filter (Revenue/Expense)
- Modal open on row click
- Modal close on cancel
- Transaction detail display in modal

## 2. TDD Plan

### 2.1 Test-Driven Development Approach

**Phase 1: Foundation (ComponentTestBase)**
1. Add NavigationManager mock
2. Add JSInterop configuration
3. Verify existing tests still pass
4. Document new patterns discovered

**Phase 2: Fix DynamicForm**
1. Change inheritance from BaseComponent to VanAnComponentBase
2. Verify DynamicForm compiles
3. Test DynamicForm renders in isolation
4. Document pattern for component inheritance

**Phase 3: RevenueEntryTests (Priority: HIGH)**
For each test:
1. Write failing test (should fail with current implementation)
2. Verify test fails for right reason
3. Implement test logic (no production code changes)
4. Verify test passes
5. Refactor if needed

**Test List for RevenueEntry:**
1. `RevenueEntry_ShouldRenderDateField_WhenComponentMounted`
   - Verify input[type="date"] exists
   - Verify label is "Ngày"
2. `RevenueEntry_ShouldRenderAmountField_WhenComponentMounted`
   - Verify input[type="number"] with step="0.01" exists
   - Verify label is "Số Tiền (VNĐ)"
3. `RevenueEntry_ShouldShowValidationError_WhenAmountIsZero`
   - Set amount = 0
   - Submit form
   - Verify error alert with "Số tiền phải lớn hơn 0"
4. `RevenueEntry_ShouldShowValidationError_WhenDateIsMissing`
   - Clear date field
   - Submit form
   - Verify error alert with "Ngày không hợp lệ"
5. `RevenueEntry_ShouldShowValidationError_WhenAccountCodeInvalid`
   - Select invalid account code (e.g., "111")
   - Submit form
   - Verify error alert with account code validation message
6. `RevenueEntry_ShouldCallService_WhenFormIsValid`
   - Fill all fields with valid data
   - Submit form
   - Verify CreateRevenueEntryAsync called once with correct parameters
7. `RevenueEntry_ShouldShowSuccessAlert_WhenEntryCreated`
   - Mock service returns success
   - Submit valid form
   - Verify success alert with "Đã lưu doanh thu thành công!"
8. `RevenueEntry_ShouldNavigateBack_WhenBackButtonClicked`
   - Click back button
   - Verify NavigationManager.NavigateTo("/accounting") called

**Phase 4: ExpenseEntryTests (Priority: MEDIUM)**
Similar to RevenueEntry but with expense-specific validations:
1. Vendor field rendering
2. Category dropdown rendering
3. Vendor validation for material/maintenance categories
4. Account code validation (6xx for expense)
5. Service call verification (CreateExpenseEntryAsync)

**Phase 5: AccountBalanceTests (Priority: MEDIUM)**
1. Metrics cards render with correct data
2. Trend calculation accuracy
3. Warning alert when expense > 150% revenue
4. No warning when expense is normal
5. Data grid renders with account balances
6. Negative balance alert
7. Refresh button calls LoadBalanceData

**Phase 6: TransactionHistoryTests (Priority: LOW)**
1. Search bar renders
2. Data grid renders with transactions
3. Search filters by description
4. Period filter (month/year)
5. Account type filter
6. Modal opens on row click
7. Modal displays transaction details
8. Modal closes on cancel

**Phase 7: TransactionDetailModalTests (Priority: LOW)**
1. Modal renders when showDetailModal is true
2. Modal displays correct transaction data
3. Modal closes when OnClose triggered

### 2.2 Test Data Strategy

**Common Test Data:**
```csharp
var testTenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");
var testDate = new DateTime(2026, 5, 23);
var testAmount = 1000000m;
var testDescription = "Test transaction";
```

**Revenue Test Data:**
```csharp
var validRevenueAccount = "511"; // Doanh thu bán hàng
var invalidRevenueAccount = "111"; // Invalid
```

**Expense Test Data:**
```csharp
var validExpenseAccount = "621"; // Chi phí nguyên vật liệu
var invalidExpenseAccount = "111"; // Invalid
var materialCategory = "materials";
var maintenanceCategory = "maintenance";
var otherCategory = "other";
```

**AccountBalance Test Data:**
```csharp
var currentPeriodEntries = new List<AccountingEntryDto>
{
    new() { EntryType = AccountingEntryType.Revenue, Amount = 10000000, AccountCode = "511" },
    new() { EntryType = AccountingEntryType.Expense, Amount = 5000000, AccountCode = "621" }
};
var previousPeriodEntries = new List<AccountingEntryDto>
{
    new() { EntryType = AccountingEntryType.Revenue, Amount = 8000000, AccountCode = "511" },
    new() { EntryType = AccountingEntryType.Expense, Amount = 4000000, AccountCode = "621" }
};
```

### 2.3 Mock Strategy

**IAccountingService Mock Setup:**
```csharp
var mockService = new Mock<IAccountingService>();
mockService.Setup(s => s.CreateRevenueEntryAsync(
    It.IsAny<TenantId>(),
    It.IsAny<AccountingPeriod>(),
    It.IsAny<decimal>(),
    It.IsAny<string>()))
    .ReturnsAsync(new AccountingEntryDto { Id = Guid.NewGuid() });

mockService.Setup(s => s.GetEntriesByTenantAndPeriodAsync(
    It.IsAny<TenantId>(),
    It.IsAny<AccountingPeriod>()))
    .ReturnsAsync(new List<AccountingEntryDto> { /* test data */ });
```

**AuthenticationState Mock Setup:**
```csharp
var testUser = new ClaimsPrincipal(new ClaimsIdentity(new[]
{
    new Claim("TenantId", testTenantId.ToString())
}, "TestAuthentication"));
```

### 2.4 Assertion Strategy

**Element Existence:**
```csharp
var element = cut.Find("input[type='date']");
element.Should().NotBeNull();
```

**Text Content:**
```csharp
var label = cut.Find("label[for='date']");
label.TextContent.Should().Be("Ngày");
```

**Component Instance:**
```csharp
var alert = cut.FindComponent<VanAAlert>();
alert.Instance.Type.Should().Be("success");
alert.Instance.Message.Should().Contain("thành công");
```

**Service Verification:**
```csharp
mockService.Verify(s => s.CreateRevenueEntryAsync(
    It.Is<TenantId>(t => t.Value == testTenantId),
    It.IsAny<AccountingPeriod>(),
    It.Is<decimal>(a => a == testAmount),
    It.Is<string>(d => d == testDescription)),
    Times.Once);
```

**Navigation Verification:**
```csharp
var navManager = Services.GetRequiredService<NavigationManager>();
navManager.History.Should().Contain("/accounting");
```

## 3. Success Criteria

### 3.1 Test Coverage
- All 5 test files rewritten with business logic verification
- Each test file has 5-8 meaningful tests
- Tests cover: render, validation, service calls, UI state, user interactions

### 3.2 Test Quality
- No tests only check `cut.Markup.Should().NotBeNullOrEmpty()`
- All tests verify specific business logic
- Tests use proper mocks and assertions
- Tests are maintainable and readable

### 3.3 Test Execution
- All tests pass after implementation
- Tests run in under 30 seconds total
- No flaky tests
- Tests are deterministic

### 3.4 Documentation
- Component dependency analysis documented
- Test patterns documented
- Implementation plan documented
- Any new patterns discovered during implementation added to patterns doc

## 4. Timeline Estimate

- **Phase 1 (ComponentTestBase):** 30 minutes
- **Phase 2 (DynamicForm fix):** 15 minutes
- **Phase 3 (RevenueEntryTests):** 1 hour
- **Phase 4 (ExpenseEntryTests):** 45 minutes
- **Phase 5 (AccountBalanceTests):** 45 minutes
- **Phase 6 (TransactionHistoryTests):** 1 hour
- **Phase 7 (TransactionDetailModalTests):** 30 minutes
- **Documentation updates:** 30 minutes

**Total Estimated Time:** 4.5 hours
