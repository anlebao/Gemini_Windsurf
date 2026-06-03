# Detailed Coding Plan + Namespace Strategy
**Date:** 2026-05-23
**Task:** Fix Business Logic Tests for ShopERP Accounting Components

## 1. Namespace Strategy

### 1.1 Current Namespace Structure

**Test Project Namespace:** `VanAn.ShopERP.Tests`

**Test File Namespaces:**
- `VanAn.ShopERP.Tests.Components` - Base test classes
- `VanAn.ShopERP.Tests.Components.Accounting` - Accounting component tests

**Production Namespaces Referenced:**
- `VanAn.UI.Platform.Components` - UI Platform components
- `VanAn.UI.Platform.Components.Composite` - Composite components
- `VanAn.UI.Platform.Models` - UI models
- `VanAn.UI.Platform.Core.Interfaces` - Core interfaces
- `VanAn.UI.Platform.Adapters` - Adapters
- `VanAn.Shared.DTOs` - Shared DTOs
- `VanAn.Shared.Domain` - Domain entities
- `VanAn.CoreHub.Services` - Core services
- `ShopERP.Components.Pages.Accounting` - ShopERP pages

### 1.2 Namespace Strategy

**Keep Existing Structure:**
- No namespace changes required
- Test files remain in current namespaces
- Using statements already correct in existing test files

**Add New Using Statements (as needed):**
```csharp
// For NavigationManager mock
using Microsoft.AspNetCore.Components.Web;

// For JSInterop
using Bunit.JSInterop;

// For test data helpers
using System.Security.Claims;
```

### 1.3 File Organization

**Test Files to Modify:**
```
6_Tests/VanAn.ShopERP.Tests/
├── Components/
│   ├── ComponentTestBase.cs (MODIFY)
│   └── Accounting/
│       ├── RevenueEntryTests.cs (REWRITE)
│       ├── ExpenseEntryTests.cs (REWRITE)
│       ├── AccountBalanceTests.cs (REWRITE)
│       ├── TransactionHistoryTests.cs (REWRITE)
│       └── TransactionDetailModalTests.cs (REWRITE)
```

**Production Files to Modify:**
```
UI.Platform/Components/Composite/
└── DynamicForm.razor (MODIFY - inheritance fix)
```

## 2. Detailed Coding Plan

### Phase 1: Fix DynamicForm Inheritance

**File:** `UI.Platform/Components/Composite/DynamicForm.razor`

**Change:**
```razor
// Line 7: Change from
@inherits BaseComponent

// To
@inherits VanAnComponentBase
```

**Also add using:**
```razor
// Line 2: Add
@using VanAn.UI.Platform.Components.Base
```

**Verification:**
- Build UI.Platform project
- Verify no compilation errors
- Run existing tests to ensure no breakage

### Phase 2: Improve ComponentTestBase

**File:** `6_Tests/VanAn.ShopERP.Tests/Components/ComponentTestBase.cs`

**Add using statements:**
```csharp
using Microsoft.AspNetCore.Components.Web;
using Bunit.JSInterop;
using System.Security.Claims;
```

**Add to constructor:**
```csharp
public ComponentTestBase()
{
    // Register UI Platform services
    Services.AddSingleton<IThemeProvider, ThemeProvider>();
    Services.AddSingleton<ICssAdapter, BootstrapAdapter>();
    
    // Register Authentication
    Services.AddSingleton<AuthenticationStateProvider, TestAuthenticationStateProvider>();
    
    // Register Logging
    Services.AddSingleton(typeof(ILogger<>), typeof(Logger<>));
    
    // Register Core Services (mock by default, tests can override)
    Services.AddSingleton<IAccountingService>(sp => new Mock<IAccountingService>().Object);

    // NEW: Register NavigationManager
    Services.AddSingleton<NavigationManager, TestNavigationManager>();
    
    // NEW: Configure JSInterop
    JSInterop.Mode = JSRuntimeMode.Loose;

    // Bunit automatically discovers components from referenced assemblies
    // UI.Platform is already referenced in the project, so components should be discoverable
}
```

**Verification:**
- Build test project
- Run existing tests to ensure they still pass
- Verify NavigationManager is available in tests

### Phase 3: Rewrite RevenueEntryTests

**File:** `6_Tests/VanAn.ShopERP.Tests/Components/Accounting/RevenueEntryTests.cs`

**Test 1: RevenueEntry_ShouldRenderDateField_WhenComponentMounted**
```csharp
[Fact]
public void RevenueEntry_ShouldRenderDateField_WhenComponentMounted()
{
    // Act
    var cut = RenderComponent<ShopERP.Components.Pages.Accounting.RevenueEntry>();

    // Assert
    var dateInput = cut.Find("input[type='date']");
    dateInput.Should().NotBeNull();
    
    var dateLabel = cut.Find("label[for='date']");
    dateLabel.TextContent.Should().Contain("Ngày");
}
```

**Test 2: RevenueEntry_ShouldRenderAmountField_WhenComponentMounted**
```csharp
[Fact]
public void RevenueEntry_ShouldRenderAmountField_WhenComponentMounted()
{
    // Act
    var cut = RenderComponent<ShopERP.Components.Pages.Accounting.RevenueEntry>();

    // Assert
    var amountInput = cut.Find("input[type='number'][step='0.01']");
    amountInput.Should().NotBeNull();
    
    var amountLabel = cut.Find("label[for='amount']");
    amountLabel.TextContent.Should().Contain("Số Tiền");
}
```

**Test 3: RevenueEntry_ShouldShowValidationError_WhenAmountIsZero**
```csharp
[Fact]
public void RevenueEntry_ShouldShowValidationError_WhenAmountIsZero()
{
    // Arrange
    var cut = RenderComponent<ShopERP.Components.Pages.Accounting.RevenueEntry>();
    var amountInput = cut.Find("input[type='number'][step='0.01']");
    var submitButton = cut.Find("button[type='submit']");

    // Act
    amountInput.Change("0");
    submitButton.Click();

    // Assert
    var errorAlert = cut.FindComponent<VanAAlert>();
    errorAlert.Instance.Type.Should().Be("error");
    errorAlert.Instance.Message.Should().Contain("phải lớn hơn 0");
}
```

**Test 4: RevenueEntry_ShouldShowValidationError_WhenDateIsMissing**
```csharp
[Fact]
public void RevenueEntry_ShouldShowValidationError_WhenDateIsMissing()
{
    // Arrange
    var cut = RenderComponent<ShopERP.Components.Pages.Accounting.RevenueEntry>();
    var dateInput = cut.Find("input[type='date']");
    var submitButton = cut.Find("button[type='submit']");

    // Act
    dateInput.Change("");
    submitButton.Click();

    // Assert
    var errorAlert = cut.FindComponent<VanAAlert>();
    errorAlert.Instance.Type.Should().Be("error");
    errorAlert.Instance.Message.Should().Contain("Ngày không hợp lệ");
}
```

**Test 5: RevenueEntry_ShouldShowValidationError_WhenAccountCodeInvalid**
```csharp
[Fact]
public void RevenueEntry_ShouldShowValidationError_WhenAccountCodeInvalid()
{
    // Arrange
    var cut = RenderComponent<ShopERP.Components.Pages.Accounting.RevenueEntry>();
    var accountSelect = cut.Find("select[id='account']");
    var submitButton = cut.Find("button[type='submit']");

    // Act
    accountSelect.Change("111"); // Invalid account code
    submitButton.Click();

    // Assert
    var errorAlert = cut.FindComponent<VanAAlert>();
    errorAlert.Instance.Type.Should().Be("error");
    errorAlert.Instance.Message.Should().Contain("Mã tài khoản không hợp lệ");
}
```

**Test 6: RevenueEntry_ShouldCallService_WhenFormIsValid**
```csharp
[Fact]
public async Task RevenueEntry_ShouldCallService_WhenFormIsValid()
{
    // Arrange
    var mockService = new Mock<IAccountingService>();
    mockService.Setup(s => s.CreateRevenueEntryAsync(
        It.IsAny<TenantId>(),
        It.IsAny<AccountingPeriod>(),
        It.IsAny<decimal>(),
        It.IsAny<string>()))
        .ReturnsAsync(new AccountingEntryDto { Id = Guid.NewGuid() });
    Services.AddSingleton(mockService.Object);

    var cut = RenderComponent<ShopERP.Components.Pages.Accounting.RevenueEntry>();
    
    var dateInput = cut.Find("input[type='date']");
    var amountInput = cut.Find("input[type='number'][step='0.01']");
    var accountSelect = cut.Find("select[id='account']");
    var descriptionInput = cut.Find("textarea[id='description']");
    var submitButton = cut.Find("button[type='submit']");

    // Act
    dateInput.Change("2026-05-23");
    amountInput.Change("1000000");
    accountSelect.Change("511");
    descriptionInput.Change("Test revenue");
    submitButton.Click();

    // Wait for async operation
    cut.WaitForAssertion(() =>
    {
        mockService.Verify(s => s.CreateRevenueEntryAsync(
            It.IsAny<TenantId>(),
            It.IsAny<AccountingPeriod>(),
            It.Is<decimal>(a => a == 1000000),
            It.Is<string>(d => d == "Test revenue")),
            Times.Once);
    });
}
```

**Test 7: RevenueEntry_ShouldShowSuccessAlert_WhenEntryCreated**
```csharp
[Fact]
public async Task RevenueEntry_ShouldShowSuccessAlert_WhenEntryCreated()
{
    // Arrange
    var mockService = new Mock<IAccountingService>();
    mockService.Setup(s => s.CreateRevenueEntryAsync(
        It.IsAny<TenantId>(),
        It.IsAny<AccountingPeriod>(),
        It.IsAny<decimal>(),
        It.IsAny<string>()))
        .ReturnsAsync(new AccountingEntryDto { Id = Guid.NewGuid() });
    Services.AddSingleton(mockService.Object);

    var cut = RenderComponent<ShopERP.Components.Pages.Accounting.RevenueEntry>();
    
    var dateInput = cut.Find("input[type='date']");
    var amountInput = cut.Find("input[type='number'][step='0.01']");
    var accountSelect = cut.Find("select[id='account']");
    var descriptionInput = cut.Find("textarea[id='description']");
    var submitButton = cut.Find("button[type='submit']");

    // Act
    dateInput.Change("2026-05-23");
    amountInput.Change("1000000");
    accountSelect.Change("511");
    descriptionInput.Change("Test revenue");
    submitButton.Click();

    // Wait for async operation
    cut.WaitForAssertion(() =>
    {
        var successAlert = cut.FindComponent<VanAAlert>();
        successAlert.Instance.Type.Should().Be("success");
        successAlert.Instance.Message.Should().Contain("thành công");
    });
}
```

**Test 8: RevenueEntry_ShouldNavigateBack_WhenBackButtonClicked**
```csharp
[Fact]
public void RevenueEntry_ShouldNavigateBack_WhenBackButtonClicked()
{
    // Arrange
    var cut = RenderComponent<ShopERP.Components.Pages.Accounting.RevenueEntry>();
    var backButton = cut.Find("button:contains('Quay Lại')");

    // Act
    backButton.Click();

    // Assert
    var navManager = Services.GetRequiredService<NavigationManager>();
    navManager.History.Should().Contain("/accounting");
}
```

### Phase 4: Rewrite ExpenseEntryTests

**Similar structure to RevenueEntryTests with expense-specific validations:**

**Key differences:**
- Test vendor field rendering
- Test category dropdown rendering
- Test vendor validation for material/maintenance categories
- Test account code validation (6xx for expense)
- Verify CreateExpenseEntryAsync service call

### Phase 5: Rewrite AccountBalanceTests

**Test 1: AccountBalance_ShouldRenderMetricsCards_WhenComponentMounted**
```csharp
[Fact]
public async Task AccountBalance_ShouldRenderMetricsCards_WhenComponentMounted()
{
    // Arrange
    var mockService = new Mock<IAccountingService>();
    mockService.Setup(s => s.GetEntriesByTenantAndPeriodAsync(
        It.IsAny<TenantId>(),
        It.IsAny<AccountingPeriod>()))
        .ReturnsAsync(new List<AccountingEntryDto>
        {
            new() { EntryType = AccountingEntryType.Revenue, Amount = 10000000, AccountCode = "511" },
            new() { EntryType = AccountingEntryType.Expense, Amount = 5000000, AccountCode = "621" }
        });
    Services.AddSingleton(mockService.Object);

    // Act
    var cut = RenderComponent<ShopERP.Components.Pages.Accounting.AccountBalance>();

    // Wait for data load
    cut.WaitForAssertion(() =>
    {
        var metricsCards = cut.FindComponents<VanAMetricsCard>();
        metricsCards.Count.Should().Be(3);
    });
}
```

**Test 2: AccountBalance_ShouldShowWarningAlert_WhenExpensesExceedRevenue**
```csharp
[Fact]
public async Task AccountBalance_ShouldShowWarningAlert_WhenExpensesExceedRevenue()
{
    // Arrange
    var mockService = new Mock<IAccountingService>();
    mockService.Setup(s => s.GetEntriesByTenantAndPeriodAsync(
        It.IsAny<TenantId>(),
        It.IsAny<AccountingPeriod>()))
        .ReturnsAsync(new List<AccountingEntryDto>
        {
            new() { EntryType = AccountingEntryType.Revenue, Amount = 10000000, AccountCode = "511" },
            new() { EntryType = AccountingEntryType.Expense, Amount = 16000000, AccountCode = "621" } // > 150% of revenue
        });
    Services.AddSingleton(mockService.Object);

    // Act
    var cut = RenderComponent<ShopERP.Components.Pages.Accounting.AccountBalance>();

    // Wait for data load
    cut.WaitForAssertion(() =>
    {
        var warningAlert = cut.FindComponent<VanAAlert>();
        warningAlert.Instance.Type.Should().Be("warning");
        warningAlert.Instance.Message.Should().Contain("vượt 150%");
    });
}
```

**Test 3: AccountBalance_ShouldNotShowWarningAlert_WhenExpensesAreNormal**
```csharp
[Fact]
public async Task AccountBalance_ShouldNotShowWarningAlert_WhenExpensesAreNormal()
{
    // Arrange
    var mockService = new Mock<IAccountingService>();
    mockService.Setup(s => s.GetEntriesByTenantAndPeriodAsync(
        It.IsAny<TenantId>(),
        It.IsAny<AccountingPeriod>()))
        .ReturnsAsync(new List<AccountingEntryDto>
        {
            new() { EntryType = AccountingEntryType.Revenue, Amount = 10000000, AccountCode = "511" },
            new() { EntryType = AccountingEntryType.Expense, Amount = 5000000, AccountCode = "621" } // 50% of revenue
        });
    Services.AddSingleton(mockService.Object);

    // Act
    var cut = RenderComponent<ShopERP.Components.Pages.Accounting.AccountBalance>();

    // Wait for data load
    cut.WaitForAssertion(() =>
    {
        var alerts = cut.FindComponents<VanAAlert>();
        alerts.Should().BeEmpty();
    });
}
```

### Phase 6: Rewrite TransactionHistoryTests

**Test 1: TransactionHistory_ShouldRenderSearchBar_WhenComponentMounted**
```csharp
[Fact]
public async Task TransactionHistory_ShouldRenderSearchBar_WhenComponentMounted()
{
    // Arrange
    var mockService = new Mock<IAccountingService>();
    mockService.Setup(s => s.GetEntriesByTenantAndPeriodAsync(
        It.IsAny<TenantId>(),
        It.IsAny<AccountingPeriod>()))
        .ReturnsAsync(new List<AccountingEntryDto>());
    Services.AddSingleton(mockService.Object);

    // Act
    var cut = RenderComponent<ShopERP.Components.Pages.Accounting.TransactionHistory>();

    // Assert
    var searchBar = cut.FindComponent<VanAnSearchBar>();
    searchBar.Should().NotBeNull();
}
```

**Test 2: TransactionHistory_ShouldRenderDataGrid_WhenComponentMounted**
```csharp
[Fact]
public async Task TransactionHistory_ShouldRenderDataGrid_WhenComponentMounted()
{
    // Arrange
    var mockService = new Mock<IAccountingService>();
    mockService.Setup(s => s.GetEntriesByTenantAndPeriodAsync(
        It.IsAny<TenantId>(),
        It.IsAny<AccountingPeriod>()))
        .ReturnsAsync(new List<AccountingEntryDto>
        {
            new() { Id = Guid.NewGuid(), Description = "Test", Amount = 1000000 }
        });
    Services.AddSingleton(mockService.Object);

    // Act
    var cut = RenderComponent<ShopERP.Components.Pages.Accounting.TransactionHistory>();

    // Wait for data load
    cut.WaitForAssertion(() =>
    {
        var dataGrid = cut.FindComponent<VanAnDataGrid<AccountingEntryDto>>();
        dataGrid.Should().NotBeNull();
    });
}
```

### Phase 7: Rewrite TransactionDetailModalTests

**Test 1: TransactionHistory_ShouldOpenModal_WhenRowIsClicked**
```csharp
[Fact]
public async Task TransactionHistory_ShouldOpenModal_WhenRowIsClicked()
{
    // Arrange
    var mockService = new Mock<IAccountingService>();
    var testEntry = new AccountingEntryDto
    {
        Id = Guid.NewGuid(),
        Description = "Test transaction",
        Amount = 1000000
    };
    mockService.Setup(s => s.GetEntriesByTenantAndPeriodAsync(
        It.IsAny<TenantId>(),
        It.IsAny<AccountingPeriod>()))
        .ReturnsAsync(new List<AccountingEntryDto> { testEntry });
    Services.AddSingleton(mockService.Object);

    var cut = RenderComponent<ShopERP.Components.Pages.Accounting.TransactionHistory>();

    // Wait for data load
    cut.WaitForAssertion(() =>
    {
        var detailButton = cut.Find("button:contains('Chi Tiết')");
        detailButton.Click();
    });

    // Assert
    var modal = cut.FindComponent<VanAModal>();
    modal.Should().NotBeNull();
}
```

## 3. Implementation Order

### Sequential Execution:

1. **Fix DynamicForm** (5 minutes)
   - Change inheritance
   - Build UI.Platform
   - Verify no errors

2. **Improve ComponentTestBase** (10 minutes)
   - Add NavigationManager
   - Add JSInterop
   - Build test project
   - Run existing tests

3. **Rewrite RevenueEntryTests** (45 minutes)
   - Write all 8 tests
   - Run tests
   - Fix any issues
   - Document patterns

4. **Rewrite ExpenseEntryTests** (30 minutes)
   - Write tests
   - Run tests
   - Fix any issues

5. **Rewrite AccountBalanceTests** (30 minutes)
   - Write tests
   - Run tests
   - Fix any issues

6. **Rewrite TransactionHistoryTests** (30 minutes)
   - Write tests
   - Run tests
   - Fix any issues

7. **Rewrite TransactionDetailModalTests** (20 minutes)
   - Write tests
   - Run tests
   - Fix any issues

8. **Final Validation** (15 minutes)
   - Run all tests
   - Verify all pass
   - Update documentation

## 4. Risk Mitigation

### 4.1 DynamicForm Inheritance Change
**Risk:** Other components might depend on BaseComponent
**Mitigation:** Search for all BaseComponent usages before changing
**Rollback:** Revert if tests fail

### 4.2 ComponentTestBase Changes
**Risk:** New services might break existing tests
**Mitigation:** Run all existing tests after changes
**Rollback:** Remove new services if tests fail

### 4.3 Test Rewrites
**Risk:** New tests might be flaky or fail intermittently
**Mitigation:** Run tests multiple times to ensure determinism
**Rollback:** Revert to old tests if new ones are unstable

## 5. Success Metrics

- All 5 test files rewritten
- Each test file has 5-8 meaningful tests
- All tests pass consistently
- No tests only check render (all verify business logic)
- Total test execution time < 30 seconds
- Documentation updated with new patterns discovered
