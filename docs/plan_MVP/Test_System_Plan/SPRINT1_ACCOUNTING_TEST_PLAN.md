# Sprint 1 — Accounting Module Test Plan (TDD Approach)

**Project:** VanAn Ecosystem — Frontend Accounting Module  
**Sprint:** Sprint 1 (Phase 2.6)  
**Version:** 1.0  
**Created:** 2026-05-20  

> **How to use:** Each phase section contains the problem, files to touch, exact changes required, and a validation command. After implementation, add `✅ Completed: YYYY-MM-DD` to the phase header.

---

## Namespace Strategy (MANDATORY — Read Before Any Phase)

```
Source of Truth (NEVER duplicate):
  AccountingEntry    → VanAn.Shared.Domain          (1_Shared/Domain/AccountingEntry.cs)
  Money              → VanAn.Shared.Domain
  TenantId           → VanAn.Shared.Domain
  AccountingPeriod   → VanAn.Shared.Domain

Test project namespaces (never leak into src):
  VanAn.Core.Tests.Accounting           → namespace VanAn.Core.Tests.Accounting.*
  VanAn.ShopERP.Tests.Components.Accounting → namespace VanAn.ShopERP.Tests.Components.Accounting.*
  VanAn.Integration.Tests.Accounting    → namespace VanAn.Integration.Tests.Accounting.*
```

---

## Test Pyramid Target

```
        /\
       /E2E\          ← 10% (Critical user flows only)
      /------\
     /Integration\    ← 30% (UI ↔ Service integration)
    /------------\
   /  Component   \   ← 40% (Blazor component behavior)
  /----------------\
 /     Unit Tests   \  ← 20% (Validation logic, formatting)
/--------------------\
```

---

## PHASE 1: Unit Tests — Validation & Logic Layer

**Problem:** Sprint 1 requires validation logic (duplicate detection, period validation, account code format, journal template matching) but no unit tests exist for accounting-specific logic.

**Files to Create:**
- `6_Tests/VanAn.Core.Tests/Accounting/AccountingValidationTests.cs`
- `6_Tests/VanAn.Core.Tests/Accounting/AccountCodeValidationTests.cs`
- `6_Tests/VanAn.Core.Tests/Accounting/JournalTemplateTests.cs`
- `6_Tests/VanAn.Core.Tests/Accounting/MoneyFormattingTests.cs`

**Files to Modify:**
- `6_Tests/VanAn.Core.Tests/VanAn.Core.Tests.csproj` — Add test dependencies if needed

---

### Phase 1.1: AccountingValidationTests.cs — Duplicate Detection & Period Validation

**File:** `6_Tests/VanAn.Core.Tests/Accounting/AccountingValidationTests.cs`

**Exact Changes Required:**

```csharp
using Xunit;
using VanAn.Shared.Domain;
using VanAn.CoreHub.Services;

namespace VanAn.Core.Tests.Accounting;

public class AccountingValidationTests
{
    [Fact]
    public async Task DetectDuplicateEntry_ShouldReturnTrue_WhenSameAmountDateAccountWithin5Minutes()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var existingEntry = new AccountingEntryDto
        {
            Amount = 1000000,
            Date = DateTime.Now.AddMinutes(-2),
            AccountCode = "511",
            Description = "Doanh thu bán hàng"
        };
        
        // Act
        var isDuplicate = await validator.IsDuplicateEntryAsync(tenantId, existingEntry);
        
        // Assert
        Assert.True(isDuplicate);
    }

    [Fact]
    public async Task DetectDuplicateEntry_ShouldReturnFalse_WhenSameEntryAfter5Minutes()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var existingEntry = new AccountingEntryDto
        {
            Amount = 1000000,
            Date = DateTime.Now.AddMinutes(-6),
            AccountCode = "511",
            Description = "Doanh thu bán hàng"
        };
        
        // Act
        var isDuplicate = await validator.IsDuplicateEntryAsync(tenantId, existingEntry);
        
        // Assert
        Assert.False(isDuplicate);
    }

    [Fact]
    public void ValidatePeriod_ShouldReturnError_WhenDateOutsideCurrentPeriod()
    {
        // Arrange
        var currentDate = new DateTime(2026, 5, 20);
        var entryDate = new DateTime(2026, 4, 15); // Previous month
        
        // Act
        var isValid = validator.IsValidDateForPeriod(entryDate, currentDate);
        
        // Assert
        Assert.False(isValid);
    }

    [Fact]
    public void ValidatePeriod_ShouldReturnTrue_WhenDateInCurrentPeriod()
    {
        // Arrange
        var currentDate = new DateTime(2026, 5, 20);
        var entryDate = new DateTime(2026, 5, 15);
        
        // Act
        var isValid = validator.IsValidDateForPeriod(entryDate, currentDate);
        
        // Assert
        Assert.True(isValid);
    }

    [Fact]
    public void ValidateBalanceConstraint_ShouldReturnWarning_WhenExpensesExceedRevenue()
    {
        // Arrange
        var totalRevenue = 10000000;
        var totalExpenses = 16000000; // > 1.5 * revenue
        
        // Act
        var hasWarning = validator.HasBalanceWarning(totalRevenue, totalExpenses);
        
        // Assert
        Assert.True(hasWarning);
    }
}
```

**Validation Command:**
```powershell
dotnet test 6_Tests/VanAn.Core.Tests/VanAn.Core.Tests.csproj --filter "FullyQualifiedName~AccountingValidationTests"
```

---

### Phase 1.2: AccountCodeValidationTests.cs — VN Chart of Accounts Validation

**File:** `6_Tests/VanAn.Core.Tests/Accounting/AccountCodeValidationTests.cs`

**Exact Changes Required:**

```csharp
using Xunit;
using VanAn.Shared.Domain;

namespace VanAn.Core.Tests.Accounting;

public class AccountCodeValidationTests
{
    [Theory]
    [InlineData("511")]  // Doanh thu bán hàng
    [InlineData("515")]  // Doanh thu dịch vụ
    [InlineData("711")]  // Giá vốn hàng bán
    [InlineData("621")]  // Chi phí vật liệu
    [InlineData("622")]  // Chi phí nhân công
    [InlineData("627")]  // Chi phí bán hàng
    [InlineData("641")]  // Chi phí quản lý
    [InlineData("111")]  // Tiền mặt
    [InlineData("112")]  // Tiền gửi ngân hàng
    public void ValidateAccountCode_ShouldReturnTrue_ForValidVNAccountCodes(string accountCode)
    {
        // Act
        var isValid = AccountCodeValidator.IsValidVNAccountCode(accountCode);
        
        // Assert
        Assert.True(isValid);
    }

    [Theory]
    [InlineData("999")]  // Invalid code
    [InlineData("51")]   // Too short
    [InlineData("5111")] // Too long
    [InlineData("ABC")]  // Non-numeric
    [InlineData("")]     // Empty
    public void ValidateAccountCode_ShouldReturnFalse_ForInvalidAccountCodes(string accountCode)
    {
        // Act
        var isValid = AccountCodeValidator.IsValidVNAccountCode(accountCode);
        
        // Assert
        Assert.False(isValid);
    }

    [Fact]
    public void GetAccountType_ShouldReturnRevenue_For5xxCodes()
    {
        // Arrange
        var accountCode = "511";
        
        // Act
        var accountType = AccountCodeValidator.GetAccountType(accountCode);
        
        // Assert
        Assert.Equal(AccountType.Revenue, accountType);
    }

    [Fact]
    public void GetAccountType_ShouldReturnExpense_For6xxCodes()
    {
        // Arrange
        var accountCode = "621";
        
        // Act
        var accountType = AccountCodeValidator.GetAccountType(accountCode);
        
        // Assert
        Assert.Equal(AccountType.Expense, accountType);
    }
}
```

**Validation Command:**
```powershell
dotnet test 6_Tests/VanAn.Core.Tests/VanAn.Core.Tests.csproj --filter "FullyQualifiedName~AccountCodeValidationTests"
```

---

### Phase 1.3: JournalTemplateTests.cs — Description Auto-Complete Matching

**File:** `6_Tests/VanAn.Core.Tests/Accounting/JournalTemplateTests.cs`

**Exact Changes Required:**

```csharp
using Xunit;
using VanAn.Shared.Domain;

namespace VanAn.Core.Tests.Accounting;

public class JournalTemplateTests
{
    [Fact]
    public void FindMatchingTemplate_ShouldReturnTemplate_WhenKeywordMatches()
    {
        // Arrange
        var templates = new List<JournalTemplate>
        {
            new JournalTemplate { Keyword = "bán hàng", Description = "Doanh thu bán hàng sản phẩm X" },
            new JournalTemplate { Keyword = "dịch vụ", Description = "Doanh thu cung cấp dịch vụ" },
            new JournalTemplate { Keyword = "vật liệu", Description = "Chi phí mua vật liệu sản xuất" }
        };
        
        var userInput = "bán hàng";
        
        // Act
        var matched = JournalTemplateService.FindMatchingTemplate(userInput, templates);
        
        // Assert
        Assert.NotNull(matched);
        Assert.Equal("Doanh thu bán hàng sản phẩm X", matched.Description);
    }

    [Fact]
    public void FindMatchingTemplate_ShouldReturnNull_WhenNoMatch()
    {
        // Arrange
        var templates = new List<JournalTemplate>
        {
            new JournalTemplate { Keyword = "bán hàng", Description = "Doanh thu bán hàng" }
        };
        
        var userInput = "khác";
        
        // Act
        var matched = JournalTemplateService.FindMatchingTemplate(userInput, templates);
        
        // Assert
        Assert.Null(matched);
    }

    [Fact]
    public void GetSuggestions_ShouldReturnMultipleMatches_WhenPartialKeyword()
    {
        // Arrange
        var templates = new List<JournalTemplate>
        {
            new JournalTemplate { Keyword = "bán hàng", Description = "Doanh thu bán hàng" },
            new JournalTemplate { Keyword = "bán lẻ", Description = "Doanh thu bán lẻ" },
            new JournalTemplate { Keyword = "bán buôn", Description = "Doanh thu bán buôn" }
        };
        
        var userInput = "bán";
        
        // Act
        var suggestions = JournalTemplateService.GetSuggestions(userInput, templates);
        
        // Assert
        Assert.Equal(3, suggestions.Count);
    }
}
```

**Validation Command:**
```powershell
dotnet test 6_Tests/VanAn.Core.Tests/VanAn.Core.Tests.csproj --filter "FullyQualifiedName~JournalTemplateTests"
```

---

### Phase 1.4: MoneyFormattingTests.cs — Currency Formatting

**File:** `6_Tests/VanAn.Core.Tests/Accounting/MoneyFormattingTests.cs`

**Exact Changes Required:**

```csharp
using Xunit;
using VanAn.Shared.Domain;

namespace VanAn.Core.Tests.Accounting;

public class MoneyFormattingTests
{
    [Theory]
    [InlineData(1000, "1.000 ₫")]
    [InlineData(10000, "10.000 ₫")]
    [InlineData(100000, "100.000 ₫")]
    [InlineData(1000000, "1.000.000 ₫")]
    [InlineData(10000000, "10.000.000 ₫")]
    public void FormatCurrency_ShouldReturnCorrectFormat_ForVietnameseDong(decimal amount)
    {
        // Act
        var formatted = MoneyFormatter.FormatVND(amount);
        
        // Assert
        Assert.Contains("₫", formatted);
    }

    [Fact]
    public void ParseCurrency_ShouldReturnDecimal_WhenInputHasSeparator()
    {
        // Arrange
        var input = "1.000.000 ₫";
        
        // Act
        var parsed = MoneyFormatter.ParseVND(input);
        
        // Assert
        Assert.Equal(1000000, parsed);
    }

    [Fact]
    public void FormatCurrency_ShouldHandleZero()
    {
        // Arrange
        var amount = 0;
        
        // Act
        var formatted = MoneyFormatter.FormatVND(amount);
        
        // Assert
        Assert.Equal("0 ₫", formatted);
    }
}
```

**Validation Command:**
```powershell
dotnet test 6_Tests/VanAn.Core.Tests/VanAn.Core.Tests.csproj --filter "FullyQualifiedName~MoneyFormattingTests"
```

---

### Phase 1.5: DynamicFormExtensionTests.cs — UI.Platform FormTypes Rendering

**File:** `6_Tests/VanAn.Core.Tests/Accounting/DynamicFormExtensionTests.cs`

**Exact Changes Required:**

```csharp
using Bunit;
using Xunit;
using VanAn.UI.Platform.Components.Composite;
using VanAn.UI.Platform.Models;

namespace VanAn.Core.Tests.Accounting;

public class DynamicFormExtensionTests : TestContext
{
    [Fact]
    public void DynamicForm_ShouldRenderDateInput_WhenFieldTypeIsDate()
    {
        // Arrange
        var fields = new List<FormField>
        {
            new FormField { Id = "date", Label = "Ngày", Type = FieldType.Date }
        };
        
        // Act
        var cut = RenderComponent<DynamicForm>(p => p.Add(f => f.Fields, fields));
        
        // Assert
        var input = cut.Find("input[type='date']");
        input.ShouldNotBeNull();
        input.GetAttribute("id").ShouldBe("date");
    }

    [Fact]
    public void DynamicForm_ShouldRenderCurrencyInput_WhenFieldTypeIsCurrency()
    {
        // Arrange
        var fields = new List<FormField>
        {
            new FormField { Id = "amount", Label = "Số tiền", Type = FieldType.Currency }
        };
        
        // Act
        var cut = RenderComponent<DynamicForm>(p => p.Add(f => f.Fields, fields));
        
        // Assert
        var input = cut.Find("input[type='number']");
        input.ShouldNotBeNull();
        input.GetAttribute("step").ShouldBe("0.01");
        input.GetAttribute("min").ShouldBe("0");
    }

    [Fact]
    public void DynamicForm_ShouldRenderTextArea_WhenFieldTypeIsTextArea()
    {
        // Arrange
        var fields = new List<FormField>
        {
            new FormField { Id = "description", Label = "Diễn giải", Type = FieldType.TextArea }
        };
        
        // Act
        var cut = RenderComponent<DynamicForm>(p => p.Add(f => f.Fields, fields));
        
        // Assert
        cut.Find("textarea#description").ShouldNotBeNull();
    }

    [Fact]
    public void DynamicForm_ShouldRenderAllAccountingFields_WithCorrectTypes()
    {
        // Arrange — Full RevenueEntry field set
        var fields = new List<FormField>
        {
            new FormField { Id = "date",        Label = "Ngày",          Type = FieldType.Date },
            new FormField { Id = "amount",      Label = "Số tiền",        Type = FieldType.Currency },
            new FormField { Id = "account",     Label = "Tài khoản",      Type = FieldType.Select },
            new FormField { Id = "description", Label = "Diễn giải",      Type = FieldType.TextArea },
            new FormField { Id = "reference",   Label = "Số chứng từ",    Type = FieldType.Text }
        };
        
        // Act
        var cut = RenderComponent<DynamicForm>(p => p.Add(f => f.Fields, fields));
        
        // Assert
        cut.Find("input[type='date']").ShouldNotBeNull();
        cut.Find("input[type='number']").ShouldNotBeNull();
        cut.Find("select#account").ShouldNotBeNull();
        cut.Find("textarea#description").ShouldNotBeNull();
        cut.Find("input[type='text']#reference").ShouldNotBeNull();
    }
}
```

**Validation Command:**
```powershell
dotnet test 6_Tests/VanAn.Core.Tests/VanAn.Core.Tests.csproj --filter "FullyQualifiedName~DynamicFormExtensionTests"
```

---

## PHASE 2: Component Tests — Blazor Components Isolated

**Problem:** Need to test Blazor component behavior (form validation, field rendering, submit flow) before implementing components in ShopERP.

**Files to Create:**
- `6_Tests/VanAn.ShopERP.Tests/VanAn.ShopERP.Tests.csproj` (new test project)
- `6_Tests/VanAn.ShopERP.Tests/Components/Accounting/RevenueEntryTests.cs`
- `6_Tests/VanAn.ShopERP.Tests/Components/Accounting/ExpenseEntryTests.cs`
- `6_Tests/VanAn.ShopERP.Tests/Components/Accounting/TransactionHistoryTests.cs`
- `6_Tests/VanAn.ShopERP.Tests/Components/Accounting/AccountBalanceTests.cs`
- `6_Tests/VanAn.ShopERP.Tests/Components/Accounting/AccountingIndexTests.cs`
- `6_Tests/VanAn.ShopERP.Tests/Components/Accounting/VanAnSearchBarTests.cs`
- `6_Tests/VanAn.ShopERP.Tests/Components/Accounting/TransactionDetailModalTests.cs`
- `6_Tests/VanAn.ShopERP.Tests/Components/Accounting/AccountingLayoutNavigationTests.cs`

**Files to Modify:**
- `6_Tests/VanAn.Tests.sln` — Add VanAn.ShopERP.Tests project

---

### Phase 2.1: Create ShopERP Test Project

**File:** `6_Tests/VanAn.ShopERP.Tests/VanAn.ShopERP.Tests.csproj`

**Exact Changes Required:**

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <IsPackable>false</IsPackable>
    <IsTestProject>true</IsTestProject>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="bunit" Version="1.20.0" />
    <PackageReference Include="bunit.xunit" Version="1.20.0" />
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.8.0" />
    <PackageReference Include="xunit" Version="2.6.2" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.5.4">
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
      <PrivateAssets>all</PrivateAssets>
    </PackageReference>
    <PackageReference Include="coverlet.collector" Version="6.0.0">
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
      <PrivateAssets>all</PrivateAssets>
    </PackageReference>
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\5_WebApps\ShopERP\VanAn.ShopERP.csproj" />
    <ProjectReference Include="..\..\UI.Platform\VanAn.UI.Platform.csproj" />
  </ItemGroup>

</Project>
```

**Validation Command:**
```powershell
dotnet build 6_Tests/VanAn.ShopERP.Tests/VanAn.ShopERP.Tests.csproj
```

---

### Phase 2.2: RevenueEntryTests.cs — Form Validation & Submit Flow

**File:** `6_Tests/VanAn.ShopERP.Tests/Components/Accounting/RevenueEntryTests.cs`

**Exact Changes Required:**

```csharp
using Bunit;
using Xunit;
using VanAn.ShopERP.Components.Pages.Accounting;
using VanAn.Shared.DTOs;

namespace VanAn.ShopERP.Tests.Components.Accounting;

public class RevenueEntryTests : TestContext
{
    [Fact]
    public void RevenueEntry_ShouldRenderDateField_WhenComponentMounted()
    {
        // Act
        var cut = RenderComponent<RevenueEntry>();
        
        // Assert
        cut.Find("#date").ShouldNotBeNull();
    }

    [Fact]
    public void RevenueEntry_ShouldShowValidationError_WhenAmountIsZero()
    {
        // Arrange
        var cut = RenderComponent<RevenueEntry>();
        
        // Act
        cut.Find("#amount").Change("0");
        cut.Find("form").Submit();
        
        // Assert
        cut.Find("div.alert-error").TextContent.ShouldContain("Số tiền phải > 0");
    }

    [Fact]
    public void RevenueEntry_ShouldShowValidationError_WhenDateIsMissing()
    {
        // Arrange
        var cut = RenderComponent<RevenueEntry>();
        
        // Act
        cut.Find("form").Submit();
        
        // Assert
        cut.Find("div.alert-error").TextContent.ShouldContain("Ngày không được để trống");
    }

    [Fact]
    public void RevenueEntry_ShouldCallService_WhenFormIsValid()
    {
        // Arrange
        var mockService = new Mock<IAccountingEntryService>();
        Components.Add<IAccountingEntryService>(mockService.Object);
        
        var cut = RenderComponent<RevenueEntry>();
        
        // Act
        cut.Find("#date").Change("2026-05-20");
        cut.Find("#amount").Change("1000000");
        cut.Find("#account").Select("511");
        cut.Find("#description").Input("Doanh thu bán hàng");
        cut.Find("form").Submit();
        
        // Assert
        mockService.Verify(s => s.CreateRevenueEntryAsync(It.IsAny<Guid>(), It.IsAny<RevenueEntryDto>()), Times.Once);
    }

    [Fact]
    public void RevenueEntry_ShouldShowSuccessAlert_WhenEntryCreated()
    {
        // Arrange
        var mockService = new Mock<IAccountingEntryService>();
        mockService.Setup(s => s.CreateRevenueEntryAsync(It.IsAny<Guid>(), It.IsAny<RevenueEntryDto>()))
                   .ReturnsAsync(new AccountingEntryDto { Id = Guid.NewGuid() });
        Components.Add<IAccountingEntryService>(mockService.Object);
        
        var cut = RenderComponent<RevenueEntry>();
        
        // Act
        cut.Find("#date").Change("2026-05-20");
        cut.Find("#amount").Change("1000000");
        cut.Find("#account").Select("511");
        cut.Find("#description").Input("Doanh thu bán hàng");
        cut.Find("form").Submit();
        
        // Assert
        cut.Find("div.alert-success").TextContent.ShouldContain("Đã tạo thành công");
    }
}
```

**Validation Command:**
```powershell
dotnet test 6_Tests/VanAn.ShopERP.Tests/VanAn.ShopERP.Tests.csproj --filter "FullyQualifiedName~RevenueEntryTests"
```

---

### Phase 2.3: ExpenseEntryTests.cs — Vendor & Category Fields

**File:** `6_Tests/VanAn.ShopERP.Tests/Components/Accounting/ExpenseEntryTests.cs`

**Exact Changes Required:**

```csharp
using Bunit;
using Xunit;
using VanAn.ShopERP.Components.Pages.Accounting;
using VanAn.Shared.DTOs;

namespace VanAn.ShopERP.Tests.Components.Accounting;

public class ExpenseEntryTests : TestContext
{
    [Fact]
    public void ExpenseEntry_ShouldRenderVendorField_WhenComponentMounted()
    {
        // Act
        var cut = RenderComponent<ExpenseEntry>();
        
        // Assert
        cut.Find("#vendor").ShouldNotBeNull();
    }

    [Fact]
    public void ExpenseEntry_ShouldRenderCategoryDropdown_WhenComponentMounted()
    {
        // Act
        var cut = RenderComponent<ExpenseEntry>();
        
        // Assert
        cut.Find("#category").ShouldNotBeNull();
    }

    [Fact]
    public void ExpenseEntry_ShouldRequireVendor_WhenExpenseTypeIsPurchase()
    {
        // Arrange
        var cut = RenderComponent<ExpenseEntry>();
        
        // Act
        cut.Find("#category").Select("Mua vật liệu");
        cut.Find("form").Submit();
        
        // Assert
        cut.Find("div.alert-error").TextContent.ShouldContain("Nhà cung cấp không được để trống");
    }

    [Fact]
    public void ExpenseEntry_ShouldCallService_WithVendorInfo_WhenFormIsValid()
    {
        // Arrange
        var mockService = new Mock<IAccountingEntryService>();
        Components.Add<IAccountingEntryService>(mockService.Object);
        
        var cut = RenderComponent<ExpenseEntry>();
        
        // Act
        cut.Find("#date").Change("2026-05-20");
        cut.Find("#amount").Change("500000");
        cut.Find("#account").Select("621");
        cut.Find("#vendor").Input("Công ty ABC");
        cut.Find("#category").Select("Mua vật liệu");
        cut.Find("#description").Input("Mua vật liệu sản xuất");
        cut.Find("form").Submit();
        
        // Assert
        mockService.Verify(s => s.CreateExpenseEntryAsync(
            It.IsAny<Guid>(), 
            It.Is<ExpenseEntryDto>(e => e.Vendor == "Công ty ABC")), 
            Times.Once);
    }
}
```

**Validation Command:**
```powershell
dotnet test 6_Tests/VanAn.ShopERP.Tests/VanAn.ShopERP.Tests.csproj --filter "FullyQualifiedName~ExpenseEntryTests"
```

---

### Phase 2.4: TransactionHistoryTests.cs — Filter & Data Grid

**File:** `6_Tests/VanAn.ShopERP.Tests/Components/Accounting/TransactionHistoryTests.cs`

**Exact Changes Required:**

```csharp
using Bunit;
using Xunit;
using VanAn.ShopERP.Components.Pages.Accounting;
using VanAn.Shared.DTOs;

namespace VanAn.ShopERP.Tests.Components.Accounting;

public class TransactionHistoryTests : TestContext
{
    [Fact]
    public void TransactionHistory_ShouldRenderSearchBar_WhenComponentMounted()
    {
        // Act
        var cut = RenderComponent<TransactionHistory>();
        
        // Assert
        cut.Find(".search-bar").ShouldNotBeNull();
    }

    [Fact]
    public void TransactionHistory_ShouldRenderDataGrid_WhenComponentMounted()
    {
        // Act
        var cut = RenderComponent<TransactionHistory>();
        
        // Assert
        cut.Find(".data-grid").ShouldNotBeNull();
    }

    [Fact]
    public void TransactionHistory_ShouldFilterByDescription_WhenSearchIsEntered()
    {
        // Arrange
        var mockService = new Mock<IAccountingEntryService>();
        mockService.Setup(s => s.GetEntriesAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<DateTime?>(), It.IsAny<DateTime?>()))
                   .ReturnsAsync(new List<AccountingEntryDto>
                   {
                       new AccountingEntryDto { Description = "Doanh thu bán hàng" },
                       new AccountingEntryDto { Description = "Chi phí mua vật liệu" }
                   });
        Components.Add<IAccountingEntryService>(mockService.Object);
        
        var cut = RenderComponent<TransactionHistory>();
        
        // Act
        cut.Find("#search-input").Input("bán hàng");
        cut.Find("#search-button").Click();
        
        // Assert
        mockService.Verify(s => s.GetEntriesAsync(
            It.IsAny<Guid>(), 
            It.Is<string>(search => search.Contains("bán hàng")), 
            It.IsAny<DateTime?>(), 
            It.IsAny<DateTime?>()), 
            Times.Once);
    }

    [Fact]
    public void TransactionHistory_ShouldFilterByPeriod_WhenMonthIsSelected()
    {
        // Arrange
        var mockService = new Mock<IAccountingEntryService>();
        Components.Add<IAccountingEntryService>(mockService.Object);
        
        var cut = RenderComponent<TransactionHistory>();
        
        // Act
        cut.Find("#period-select").Select("2026-05");
        cut.Find("#filter-button").Click();
        
        // Assert
        mockService.Verify(s => s.GetEntriesAsync(
            It.IsAny<Guid>(), 
            It.IsAny<string>(), 
            It.Is<DateTime?>(start => start.HasValue && start.Value.Month == 5), 
            It.Is<DateTime?>(end => end.HasValue && end.Value.Month == 5),
            It.IsAny<decimal?>(),
            It.IsAny<decimal?>(),
            It.IsAny<string?>()), 
            Times.Once);
    }

    [Fact]
    public void TransactionHistory_ShouldFilterByAccountType_WhenTypeIsSelected()
    {
        // Arrange
        var mockService = new Mock<IAccountingEntryService>();
        Components.Add<IAccountingEntryService>(mockService.Object);
        
        var cut = RenderComponent<TransactionHistory>();
        
        // Act
        cut.Find("#account-type-select").Select("Revenue");
        cut.Find("#filter-button").Click();
        
        // Assert
        mockService.Verify(s => s.GetEntriesAsync(
            It.IsAny<Guid>(),
            It.IsAny<string>(),
            It.IsAny<DateTime?>(),
            It.IsAny<DateTime?>(),
            It.IsAny<decimal?>(),
            It.IsAny<decimal?>(),
            It.Is<string?>(t => t == "Revenue")),
            Times.Once);
    }

    [Fact]
    public void TransactionHistory_ShouldShowAllEntries_WhenAccountTypeIsAll()
    {
        // Arrange
        var mockService = new Mock<IAccountingEntryService>();
        Components.Add<IAccountingEntryService>(mockService.Object);
        
        var cut = RenderComponent<TransactionHistory>();
        
        // Act
        cut.Find("#account-type-select").Select(""); // "Tất cả loại"
        cut.Find("#filter-button").Click();
        
        // Assert
        mockService.Verify(s => s.GetEntriesAsync(
            It.IsAny<Guid>(),
            It.IsAny<string>(),
            It.IsAny<DateTime?>(),
            It.IsAny<DateTime?>(),
            It.IsAny<decimal?>(),
            It.IsAny<decimal?>(),
            It.Is<string?>(t => t == null || t == "")),
            Times.Once);
    }
}
```

**Validation Command:**
```powershell
dotnet test 6_Tests/VanAn.ShopERP.Tests/VanAn.ShopERP.Tests.csproj --filter "FullyQualifiedName~TransactionHistoryTests"
```

---

### Phase 2.5: AccountBalanceTests.cs — Metrics & Alerts

**File:** `6_Tests/VanAn.ShopERP.Tests/Components/Accounting/AccountBalanceTests.cs`

**Exact Changes Required:**

```csharp
using Bunit;
using Xunit;
using VanAn.ShopERP.Components.Pages.Accounting;

namespace VanAn.ShopERP.Tests.Components.Accounting;

public class AccountBalanceTests : TestContext
{
    [Fact]
    public void AccountBalance_ShouldRenderMetricsCards_WhenComponentMounted()
    {
        // Act
        var cut = RenderComponent<AccountBalance>();
        
        // Assert
        cut.FindAll(".metrics-card").Count.ShouldBe(3);
    }

    [Fact]
    public void AccountBalance_ShouldShowWarningAlert_WhenExpensesExceedRevenue()
    {
        // Arrange
        var mockService = new Mock<IAccountingEntryService>();
        mockService.Setup(s => s.GetBalanceSummaryAsync(It.IsAny<Guid>()))
                   .ReturnsAsync(new BalanceSummary
                   {
                       TotalRevenue = 10000000,
                       TotalExpenses = 16000000
                   });
        Components.Add<IAccountingEntryService>(mockService.Object);
        
        var cut = RenderComponent<AccountBalance>();
        
        // Assert
        cut.Find("div.alert-warning").TextContent.ShouldContain("Chi phí vượt quá doanh thu");
    }

    [Fact]
    public void AccountBalance_ShouldNotShowWarningAlert_WhenExpensesAreNormal()
    {
        // Arrange
        var mockService = new Mock<IAccountingEntryService>();
        mockService.Setup(s => s.GetBalanceSummaryAsync(It.IsAny<Guid>()))
                   .ReturnsAsync(new BalanceSummary
                   {
                       TotalRevenue = 10000000,
                       TotalExpenses = 8000000
                   });
        Components.Add<IAccountingEntryService>(mockService.Object);
        
        var cut = RenderComponent<AccountBalance>();
        
        // Assert
        cut.FindAll("div.alert-warning").Count.ShouldBe(0);
    }

    [Fact]
    public void AccountBalance_ShouldRenderBalanceGrid_WhenComponentMounted()
    {
        // Act
        var cut = RenderComponent<AccountBalance>();
        
        // Assert
        cut.Find(".balance-grid").ShouldNotBeNull();
    }
}
```

**Validation Command:**
```powershell
dotnet test 6_Tests/VanAn.ShopERP.Tests/VanAn.ShopERP.Tests.csproj --filter "FullyQualifiedName~AccountBalanceTests"
```

---

### Phase 2.6: AccountingIndexTests.cs — Hub Page Layout

**File:** `6_Tests/VanAn.ShopERP.Tests/Components/Accounting/AccountingIndexTests.cs`

**Exact Changes Required:**

```csharp
using Bunit;
using Xunit;
using VanAn.ShopERP.Components.Pages.Accounting;

namespace VanAn.ShopERP.Tests.Components.Accounting;

public class AccountingIndexTests : TestContext
{
    [Fact]
    public void AccountingIndex_ShouldRenderFourMetricsCards_WhenComponentMounted()
    {
        // Act
        var cut = RenderComponent<AccountingIndex>();
        
        // Assert
        cut.FindAll(".metrics-card").Count.ShouldBe(4);
    }

    [Fact]
    public void AccountingIndex_ShouldRenderQuickActionButtons_WhenComponentMounted()
    {
        // Act
        var cut = RenderComponent<AccountingIndex>();
        
        // Assert
        cut.FindAll("button.quick-action").Count.ShouldBeGreaterThanOrEqualTo(2);
    }

    [Fact]
    public void AccountingIndex_ShouldNavigateToRevenueEntry_WhenRevenueButtonClicked()
    {
        // Arrange
        var cut = RenderComponent<AccountingIndex>();
        
        // Act
        cut.Find("button[data-action='revenue']").Click();
        
        // Assert
        cut.Instance.NavigationManager.Uri.ShouldContain("/accounting/revenue");
    }

    [Fact]
    public void AccountingIndex_ShouldNavigateToExpenseEntry_WhenExpenseButtonClicked()
    {
        // Arrange
        var cut = RenderComponent<AccountingIndex>();
        
        // Act
        cut.Find("button[data-action='expense']").Click();
        
        // Assert
        cut.Instance.NavigationManager.Uri.ShouldContain("/accounting/expenses");
    }
}
```

**Validation Command:**
```powershell
dotnet test 6_Tests/VanAn.ShopERP.Tests/VanAn.ShopERP.Tests.csproj --filter "FullyQualifiedName~AccountingIndexTests"
```

---

### Phase 2.7: VanAnSearchBarTests.cs — UI.Platform Search Component

**File:** `6_Tests/VanAn.ShopERP.Tests/Components/Accounting/VanAnSearchBarTests.cs`

**Exact Changes Required:**

```csharp
using Bunit;
using Xunit;
using VanAn.UI.Platform.Components.Composite;
using Microsoft.AspNetCore.Components;

namespace VanAn.ShopERP.Tests.Components.Accounting;

public class VanAnSearchBarTests : TestContext
{
    [Fact]
    public void VanAnSearchBar_ShouldRenderPlaceholder_WhenPlaceholderPropIsSet()
    {
        // Act
        var cut = RenderComponent<VanAnSearchBar>(p => p
            .Add(c => c.Placeholder, "Tìm theo diễn giải..."));
        
        // Assert
        cut.Find("input#search-input").GetAttribute("placeholder")
           .ShouldBe("Tìm theo diễn giải...");
    }

    [Fact]
    public void VanAnSearchBar_ShouldFireOnSearch_WhenSearchButtonClicked()
    {
        // Arrange
        string? capturedSearchTerm = null;
        var cut = RenderComponent<VanAnSearchBar>(p => p
            .Add(c => c.OnSearch, EventCallback.Factory.Create<string>(this, s => capturedSearchTerm = s)));
        
        // Act
        cut.Find("input#search-input").Input("bán hàng");
        cut.Find("button#search-button").Click();
        
        // Assert
        capturedSearchTerm.ShouldBe("bán hàng");
    }

    [Fact]
    public void VanAnSearchBar_ShouldFireOnFilter_WhenFilterButtonClicked()
    {
        // Arrange
        var filterClicked = false;
        var cut = RenderComponent<VanAnSearchBar>(p => p
            .Add(c => c.OnFilter, EventCallback.Factory.Create(this, () => filterClicked = true)));
        
        // Act
        cut.Find("button#filter-button").Click();
        
        // Assert
        filterClicked.ShouldBeTrue();
    }

    [Fact]
    public void VanAnSearchBar_ShouldShowAmountInputs_WhenShowAmountFilterIsTrue()
    {
        // Act
        var cut = RenderComponent<VanAnSearchBar>(p => p
            .Add(c => c.ShowAmountFilter, true));
        
        // Assert
        cut.Find("input#amount-min").ShouldNotBeNull();
        cut.Find("input#amount-max").ShouldNotBeNull();
    }

    [Fact]
    public void VanAnSearchBar_ShouldHideAmountInputs_WhenShowAmountFilterIsFalse()
    {
        // Act
        var cut = RenderComponent<VanAnSearchBar>(p => p
            .Add(c => c.ShowAmountFilter, false));
        
        // Assert
        cut.FindAll("input#amount-min").Count.ShouldBe(0);
        cut.FindAll("input#amount-max").Count.ShouldBe(0);
    }

    [Fact]
    public void VanAnSearchBar_ShouldPassAmountRange_WhenOnSearchFired()
    {
        // Arrange
        decimal? capturedMin = null;
        decimal? capturedMax = null;
        var cut = RenderComponent<VanAnSearchBar>(p => p
            .Add(c => c.ShowAmountFilter, true)
            .Add(c => c.AmountMinChanged, EventCallback.Factory.Create<decimal?>(this, v => capturedMin = v))
            .Add(c => c.AmountMaxChanged, EventCallback.Factory.Create<decimal?>(this, v => capturedMax = v)));
        
        // Act
        cut.Find("input#amount-min").Change("500000");
        cut.Find("input#amount-max").Change("2000000");
        cut.Find("button#search-button").Click();
        
        // Assert
        capturedMin.ShouldBe(500000);
        capturedMax.ShouldBe(2000000);
    }
}
```

**Validation Command:**
```powershell
dotnet test 6_Tests/VanAn.ShopERP.Tests/VanAn.ShopERP.Tests.csproj --filter "FullyQualifiedName~VanAnSearchBarTests"
```

---

### Phase 2.8: TransactionDetailModalTests.cs — Transaction Detail Modal

**File:** `6_Tests/VanAn.ShopERP.Tests/Components/Accounting/TransactionDetailModalTests.cs`

**Exact Changes Required:**

```csharp
using Bunit;
using Xunit;
using VanAn.ShopERP.Components.Pages.Accounting;
using VanAn.Shared.DTOs;

namespace VanAn.ShopERP.Tests.Components.Accounting;

public class TransactionDetailModalTests : TestContext
{
    [Fact]
    public void TransactionHistory_ShouldOpenModal_WhenRowIsClicked()
    {
        // Arrange
        var mockService = new Mock<IAccountingEntryService>();
        mockService.Setup(s => s.GetEntriesAsync(It.IsAny<Guid>(), null, null, null, null, null))
                   .ReturnsAsync(new List<AccountingEntryDto>
                   {
                       new AccountingEntryDto { Id = Guid.NewGuid(), Description = "Doanh thu bán hàng", Amount = 1000000 }
                   });
        Components.Add<IAccountingEntryService>(mockService.Object);
        
        var cut = RenderComponent<TransactionHistory>();
        
        // Act
        cut.Find("tr.data-row").Click();
        
        // Assert
        cut.Find(".modal").GetAttribute("class").ShouldContain("show");
    }

    [Fact]
    public void TransactionHistory_ShouldDisplayEntryData_WhenModalIsOpen()
    {
        // Arrange
        var entryId = Guid.NewGuid();
        var mockService = new Mock<IAccountingEntryService>();
        mockService.Setup(s => s.GetEntriesAsync(It.IsAny<Guid>(), null, null, null, null, null))
                   .ReturnsAsync(new List<AccountingEntryDto>
                   {
                       new AccountingEntryDto
                       {
                           Id = entryId,
                           Description = "Doanh thu bán hàng",
                           Amount = 1000000,
                           AccountCode = "511",
                           Date = new DateTime(2026, 5, 20)
                       }
                   });
        Components.Add<IAccountingEntryService>(mockService.Object);
        
        var cut = RenderComponent<TransactionHistory>();
        
        // Act
        cut.Find("tr.data-row").Click();
        
        // Assert
        var modal = cut.Find(".modal-body");
        modal.TextContent.ShouldContain("Doanh thu bán hàng");
        modal.TextContent.ShouldContain("511");
        modal.TextContent.ShouldContain("1.000.000");
    }

    [Fact]
    public void TransactionHistory_ShouldCloseModal_WhenCancelButtonClicked()
    {
        // Arrange
        var mockService = new Mock<IAccountingEntryService>();
        mockService.Setup(s => s.GetEntriesAsync(It.IsAny<Guid>(), null, null, null, null, null))
                   .ReturnsAsync(new List<AccountingEntryDto>
                   {
                       new AccountingEntryDto { Id = Guid.NewGuid(), Description = "Test" }
                   });
        Components.Add<IAccountingEntryService>(mockService.Object);
        
        var cut = RenderComponent<TransactionHistory>();
        cut.Find("tr.data-row").Click();
        cut.Find(".modal").GetAttribute("class").ShouldContain("show");
        
        // Act
        cut.Find("button.btn-cancel").Click();
        
        // Assert
        cut.FindAll(".modal.show").Count.ShouldBe(0);
    }
}
```

**Validation Command:**
```powershell
dotnet test 6_Tests/VanAn.ShopERP.Tests/VanAn.ShopERP.Tests.csproj --filter "FullyQualifiedName~TransactionDetailModalTests"
```

---

### Phase 2.9: AccountingLayoutNavigationTests.cs — Navigation Menu

**File:** `6_Tests/VanAn.ShopERP.Tests/Components/Accounting/AccountingLayoutNavigationTests.cs`

**Exact Changes Required:**

```csharp
using Bunit;
using Xunit;
using VanAn.ShopERP.Components.Pages.Accounting;
using VanAn.ShopERP.Components;

namespace VanAn.ShopERP.Tests.Components.Accounting;

public class AccountingLayoutNavigationTests : TestContext
{
    [Fact]
    public void AccountingLayout_ShouldRenderFiveMenuItems_WhenComponentMounted()
    {
        // Act
        var cut = RenderComponent<AccountingLayout>();
        
        // Assert
        cut.FindAll("nav a.menu-item").Count.ShouldBe(5);
    }

    [Fact]
    public void AccountingLayout_ShouldContainAllRequiredRoutes_WhenRendered()
    {
        // Act
        var cut = RenderComponent<AccountingLayout>();
        var links = cut.FindAll("nav a.menu-item")
                       .Select(a => a.GetAttribute("href"))
                       .ToList();
        
        // Assert
        links.ShouldContain("/accounting");
        links.ShouldContain("/accounting/revenue");
        links.ShouldContain("/accounting/expenses");
        links.ShouldContain("/accounting/history");
        links.ShouldContain("/accounting/balance");
    }

    [Fact]
    public void AccountingLayout_ShouldRenderMenuLabelsInVietnamese_WhenMounted()
    {
        // Act
        var cut = RenderComponent<AccountingLayout>();
        var navText = cut.Find("nav").TextContent;
        
        // Assert — verify Vietnamese labels
        navText.ShouldContain("Doanh thu");
        navText.ShouldContain("Chi phí");
        navText.ShouldContain("Lịch sử");
        navText.ShouldContain("Số dư");
    }

    [Fact]
    public void VanADashboard_ShouldContainAccountingMenuItem_AfterSidebarUpdate()
    {
        // Act
        var cut = RenderComponent<VanADashboard>();
        
        // Assert
        cut.Find("nav").TextContent.ShouldContain("Kế Toán");
    }

    [Fact]
    public void VanADashboard_AccountingMenuItem_ShouldLinkToAccountingRoute()
    {
        // Act
        var cut = RenderComponent<VanADashboard>();
        var accountingLink = cut.FindAll("nav a")
                                .FirstOrDefault(a => a.TextContent.Contains("Kế Toán"));
        
        // Assert
        accountingLink.ShouldNotBeNull();
        accountingLink!.GetAttribute("href").ShouldBe("/accounting");
    }
}
```

**Validation Command:**
```powershell
dotnet test 6_Tests/VanAn.ShopERP.Tests/VanAn.ShopERP.Tests.csproj --filter "FullyQualifiedName~AccountingLayoutNavigationTests"
```

---

## PHASE 3: Integration Tests — UI ↔ CoreHub Services

**Problem:** Need to test full flow from UI form through IAccountingEntryService to database persistence.

**Files to Create:**
- `6_Tests/VanAn.Integration.Tests/Accounting/AccountingEntryFlowTests.cs`
- `6_Tests/VanAn.Integration.Tests/Accounting/TransactionHistoryQueryTests.cs`
- `6_Tests/VanAn.Integration.Tests/Accounting/BalanceCalculationTests.cs`
- `6_Tests/VanAn.Integration.Tests/Accounting/MultiTenancyTests.cs`

**Files to Modify:**
- `6_Tests/VanAn.Integration.Tests/VanAn.Integration.Tests.csproj` — Add project references if needed

---

### Phase 3.1: AccountingEntryFlowTests.cs — Full Entry Persistence

**File:** `6_Tests/VanAn.Integration.Tests/Accounting/AccountingEntryFlowTests.cs`

**Exact Changes Required:**

```csharp
using Xunit;
using VanAn.CoreHub.Tests.TestInfrastructure;
using VanAn.Shared.DTOs;
using VanAn.CoreHub.Services;

namespace VanAn.Integration.Tests.Accounting;

public class AccountingEntryFlowTests : IntegrationTestBase
{
    public AccountingEntryFlowTests() : base() { }

    [Fact]
    public async Task RevenueEntry_ShouldPersistToDatabase_WithCorrectTenantId()
    {
        // Arrange
        await CreateContextAsync();
        var tenantId = Guid.NewGuid();
        var service = ContextScope.ServiceProvider.GetRequiredService<IAccountingEntryService>();
        
        var dto = new RevenueEntryDto
        {
            Date = DateTime.Today,
            Amount = 1000000,
            AccountCode = "511",
            Description = "Doanh thu bán hàng",
            Reference = "HĐ-001"
        };
        
        // Act
        var entry = await service.CreateRevenueEntryAsync(tenantId, dto);
        
        // Assert
        var saved = await Context.AccountingEntries.FirstOrDefaultAsync(e => e.Id == entry.Id);
        Assert.NotNull(saved);
        Assert.Equal(tenantId, saved.TenantId);
        Assert.Equal(1000000, saved.Amount);
        Assert.Equal("511", saved.AccountCode);
    }

    [Fact]
    public async Task ExpenseEntry_ShouldPersistToDatabase_WithVendorInfo()
    {
        // Arrange
        await CreateContextAsync();
        var tenantId = Guid.NewGuid();
        var service = ContextScope.ServiceProvider.GetRequiredService<IAccountingEntryService>();
        
        var dto = new ExpenseEntryDto
        {
            Date = DateTime.Today,
            Amount = 500000,
            AccountCode = "621",
            Description = "Mua vật liệu",
            Vendor = "Công ty ABC",
            Category = "Mua vật liệu"
        };
        
        // Act
        var entry = await service.CreateExpenseEntryAsync(tenantId, dto);
        
        // Assert
        var saved = await Context.AccountingEntries.FirstOrDefaultAsync(e => e.Id == entry.Id);
        Assert.NotNull(saved);
        Assert.Equal("Công ty ABC", saved.Vendor);
        Assert.Equal("Mua vật liệu", saved.Category);
    }

    [Fact]
    public async Task GetEntries_ShouldReturnOnlyTenantEntries_WhenMultipleTenantsExist()
    {
        // Arrange
        await CreateContextAsync();
        var tenant1 = Guid.NewGuid();
        var tenant2 = Guid.NewGuid();
        var service = ContextScope.ServiceProvider.GetRequiredService<IAccountingEntryService>();
        
        await service.CreateRevenueEntryAsync(tenant1, new RevenueEntryDto { Amount = 1000, AccountCode = "511", Date = DateTime.Today });
        await service.CreateRevenueEntryAsync(tenant2, new RevenueEntryDto { Amount = 2000, AccountCode = "511", Date = DateTime.Today });
        
        // Act
        var entries1 = await service.GetEntriesAsync(tenant1, null, null, null);
        var entries2 = await service.GetEntriesAsync(tenant2, null, null, null);
        
        // Assert
        Assert.Single(entries1);
        Assert.Single(entries2);
        Assert.NotEqual(entries1[0].Id, entries2[0].Id);
    }
}
```

**Validation Command:**
```powershell
dotnet test 6_Tests/VanAn.Integration.Tests/VanAn.Integration.Tests.csproj --filter "FullyQualifiedName~AccountingEntryFlowTests"
```

---

### Phase 3.2: TransactionHistoryQueryTests.cs — Filter Logic

**File:** `6_Tests/VanAn.Integration.Tests/Accounting/TransactionHistoryQueryTests.cs`

**Exact Changes Required:**

```csharp
using Xunit;
using VanAn.CoreHub.Tests.TestInfrastructure;
using VanAn.Shared.DTOs;
using VanAn.CoreHub.Services;

namespace VanAn.Integration.Tests.Accounting;

public class TransactionHistoryQueryTests : IntegrationTestBase
{
    public TransactionHistoryQueryTests() : base() { }

    [Fact]
    public async Task GetEntries_ShouldFilterByDescription_WhenSearchTermProvided()
    {
        // Arrange
        await CreateContextAsync();
        var tenantId = Guid.NewGuid();
        var service = ContextScope.ServiceProvider.GetRequiredService<IAccountingEntryService>();
        
        await service.CreateRevenueEntryAsync(tenantId, new RevenueEntryDto { Amount = 1000, AccountCode = "511", Description = "Doanh thu bán hàng", Date = DateTime.Today });
        await service.CreateRevenueEntryAsync(tenantId, new RevenueEntryDto { Amount = 2000, AccountCode = "515", Description = "Doanh thu dịch vụ", Date = DateTime.Today });
        
        // Act
        var entries = await service.GetEntriesAsync(tenantId, "bán hàng", null, null);
        
        // Assert
        Assert.Single(entries);
        Assert.Contains("bán hàng", entries[0].Description);
    }

    [Fact]
    public async Task GetEntries_ShouldFilterByPeriod_WhenDateRangeProvided()
    {
        // Arrange
        await CreateContextAsync();
        var tenantId = Guid.NewGuid();
        var service = ContextScope.ServiceProvider.GetRequiredService<IAccountingEntryService>();
        
        await service.CreateRevenueEntryAsync(tenantId, new RevenueEntryDto { Amount = 1000, AccountCode = "511", Date = new DateTime(2026, 5, 15) });
        await service.CreateRevenueEntryAsync(tenantId, new RevenueEntryDto { Amount = 2000, AccountCode = "511", Date = new DateTime(2026, 4, 15) });
        
        // Act
        var entries = await service.GetEntriesAsync(tenantId, null, new DateTime(2026, 5, 1), new DateTime(2026, 5, 31));
        
        // Assert
        Assert.Single(entries);
        Assert.Equal(5, entries[0].Date.Month);
    }

    [Fact]
    public async Task GetEntries_ShouldReturnEmpty_WhenNoMatchingEntries()
    {
        // Arrange
        await CreateContextAsync();
        var tenantId = Guid.NewGuid();
        var service = ContextScope.ServiceProvider.GetRequiredService<IAccountingEntryService>();
        
        // Act
        var entries = await service.GetEntriesAsync(tenantId, "nonexistent", null, null);
        
        // Assert
        Assert.Empty(entries);
    }
}
```

**Validation Command:**
```powershell
dotnet test 6_Tests/VanAn.Integration.Tests/VanAn.Integration.Tests.csproj --filter "FullyQualifiedName~TransactionHistoryQueryTests"
```

---

### Phase 3.3: BalanceCalculationTests.cs — Aggregation Logic

**File:** `6_Tests/VanAn.Integration.Tests/Accounting/BalanceCalculationTests.cs`

**Exact Changes Required:**

```csharp
using Xunit;
using VanAn.CoreHub.Tests.TestInfrastructure;
using VanAn.Shared.DTOs;
using VanAn.CoreHub.Services;

namespace VanAn.Integration.Tests.Accounting;

public class BalanceCalculationTests : IntegrationTestBase
{
    public BalanceCalculationTests() : base() { }

    [Fact]
    public async Task GetBalanceSummary_ShouldCalculateTotalRevenue_WhenMultipleRevenueEntriesExist()
    {
        // Arrange
        await CreateContextAsync();
        var tenantId = Guid.NewGuid();
        var service = ContextScope.ServiceProvider.GetRequiredService<IAccountingEntryService>();
        
        await service.CreateRevenueEntryAsync(tenantId, new RevenueEntryDto { Amount = 1000000, AccountCode = "511", Date = DateTime.Today });
        await service.CreateRevenueEntryAsync(tenantId, new RevenueEntryDto { Amount = 2000000, AccountCode = "515", Date = DateTime.Today });
        
        // Act
        var summary = await service.GetBalanceSummaryAsync(tenantId);
        
        // Assert
        Assert.Equal(3000000, summary.TotalRevenue);
    }

    [Fact]
    public async Task GetBalanceSummary_ShouldCalculateTotalExpenses_WhenMultipleExpenseEntriesExist()
    {
        // Arrange
        await CreateContextAsync();
        var tenantId = Guid.NewGuid();
        var service = ContextScope.ServiceProvider.GetRequiredService<IAccountingEntryService>();
        
        await service.CreateExpenseEntryAsync(tenantId, new ExpenseEntryDto { Amount = 500000, AccountCode = "621", Date = DateTime.Today });
        await service.CreateExpenseEntryAsync(tenantId, new ExpenseEntryDto { Amount = 300000, AccountCode = "622", Date = DateTime.Today });
        
        // Act
        var summary = await service.GetBalanceSummaryAsync(tenantId);
        
        // Assert
        Assert.Equal(800000, summary.TotalExpenses);
    }

    [Fact]
    public async Task GetBalanceSummary_ShouldCalculateNetProfit_WhenRevenueAndExpensesExist()
    {
        // Arrange
        await CreateContextAsync();
        var tenantId = Guid.NewGuid();
        var service = ContextScope.ServiceProvider.GetRequiredService<IAccountingEntryService>();
        
        await service.CreateRevenueEntryAsync(tenantId, new RevenueEntryDto { Amount = 1000000, AccountCode = "511", Date = DateTime.Today });
        await service.CreateExpenseEntryAsync(tenantId, new ExpenseEntryDto { Amount = 300000, AccountCode = "621", Date = DateTime.Today });
        
        // Act
        var summary = await service.GetBalanceSummaryAsync(tenantId);
        
        // Assert
        Assert.Equal(700000, summary.NetProfit);
    }
}
```

**Validation Command:**
```powershell
dotnet test 6_Tests/VanAn.Integration.Tests/VanAn.Integration.Tests.csproj --filter "FullyQualifiedName~BalanceCalculationTests"
```

---

### Phase 3.4: MultiTenancyTests.cs — Tenant Isolation

**File:** `6_Tests/VanAn.Integration.Tests/Accounting/MultiTenancyTests.cs`

**Exact Changes Required:**

```csharp
using Xunit;
using VanAn.CoreHub.Tests.TestInfrastructure;
using VanAn.Shared.DTOs;
using VanAn.CoreHub.Services;

namespace VanAn.Integration.Tests.Accounting;

public class MultiTenancyTests : IntegrationTestBase
{
    public MultiTenancyTests() : base() { }

    [Fact]
    public async Task CreateRevenueEntry_ShouldNotLeakToOtherTenants()
    {
        // Arrange
        await CreateContextAsync();
        var tenant1 = Guid.NewGuid();
        var tenant2 = Guid.NewGuid();
        var service = ContextScope.ServiceProvider.GetRequiredService<IAccountingEntryService>();
        
        // Act
        await service.CreateRevenueEntryAsync(tenant1, new RevenueEntryDto { Amount = 1000, AccountCode = "511", Date = DateTime.Today });
        
        // Assert
        var entries1 = await service.GetEntriesAsync(tenant1, null, null, null);
        var entries2 = await service.GetEntriesAsync(tenant2, null, null, null);
        
        Assert.Single(entries1);
        Assert.Empty(entries2);
    }

    [Fact]
    public async Task GetBalanceSummary_ShouldNotAggregateAcrossTenants()
    {
        // Arrange
        await CreateContextAsync();
        var tenant1 = Guid.NewGuid();
        var tenant2 = Guid.NewGuid();
        var service = ContextScope.ServiceProvider.GetRequiredService<IAccountingEntryService>();
        
        await service.CreateRevenueEntryAsync(tenant1, new RevenueEntryDto { Amount = 1000000, AccountCode = "511", Date = DateTime.Today });
        await service.CreateRevenueEntryAsync(tenant2, new RevenueEntryDto { Amount = 2000000, AccountCode = "511", Date = DateTime.Today });
        
        // Act
        var summary1 = await service.GetBalanceSummaryAsync(tenant1);
        var summary2 = await service.GetBalanceSummaryAsync(tenant2);
        
        // Assert
        Assert.Equal(1000000, summary1.TotalRevenue);
        Assert.Equal(2000000, summary2.TotalRevenue);
    }
}
```

**Validation Command:**
```powershell
dotnet test 6_Tests/VanAn.Integration.Tests/VanAn.Integration.Tests.csproj --filter "FullyQualifiedName~MultiTenancyTests"
```

---

### Phase 3.5: AccountingUIServiceTests.cs — Adapter & Period Comparison

**File:** `6_Tests/VanAn.Integration.Tests/Accounting/AccountingUIServiceTests.cs`

**Exact Changes Required:**

```csharp
using Xunit;
using Moq;
using VanAn.ShopERP.Services.Accounting;
using VanAn.CoreHub.Services;
using VanAn.Shared.DTOs;

namespace VanAn.Integration.Tests.Accounting;

public class AccountingUIServiceTests
{
    private readonly Mock<IAccountingEntryService> _mockCoreService;
    private readonly AccountingUIService _uiService;

    public AccountingUIServiceTests()
    {
        _mockCoreService = new Mock<IAccountingEntryService>();
        _uiService = new AccountingUIService(_mockCoreService.Object);
    }

    [Fact]
    public async Task CreateRevenueEntry_ShouldMapDtoCorrectly_ToServiceCall()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var formData = new Dictionary<string, object>
        {
            ["date"]        = "2026-05-20",
            ["amount"]      = 1000000m,
            ["account"]     = "511",
            ["description"] = "Doanh thu bán hàng",
            ["reference"]   = "HĐ-001"
        };
        
        _mockCoreService
            .Setup(s => s.CreateRevenueEntryAsync(tenantId, It.IsAny<RevenueEntryDto>()))
            .ReturnsAsync(new AccountingEntryDto { Id = Guid.NewGuid() });
        
        // Act
        await _uiService.SubmitRevenueFormAsync(tenantId, formData);
        
        // Assert
        _mockCoreService.Verify(s => s.CreateRevenueEntryAsync(
            tenantId,
            It.Is<RevenueEntryDto>(dto =>
                dto.Amount == 1000000m &&
                dto.AccountCode == "511" &&
                dto.Description == "Doanh thu bán hàng" &&
                dto.Reference == "HĐ-001")),
            Times.Once);
    }

    [Fact]
    public async Task GetPeriodComparison_ShouldCalculateDelta_BetweenCurrentAndPreviousMonth()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var currentPeriod  = new DateTime(2026, 5, 1);
        var previousPeriod = new DateTime(2026, 4, 1);
        
        _mockCoreService
            .Setup(s => s.GetBalanceSummaryAsync(tenantId, It.Is<DateTime>(d => d.Month == 5)))
            .ReturnsAsync(new BalanceSummary { TotalRevenue = 11500000 });
        
        _mockCoreService
            .Setup(s => s.GetBalanceSummaryAsync(tenantId, It.Is<DateTime>(d => d.Month == 4)))
            .ReturnsAsync(new BalanceSummary { TotalRevenue = 10000000 });
        
        // Act
        var comparison = await _uiService.GetPeriodComparisonAsync(tenantId, currentPeriod);
        
        // Assert
        Assert.Equal(15.0m, comparison.RevenueDeltaPercent);  // +15%
        Assert.Equal("+15.0%", comparison.RevenueLabel);
    }

    [Fact]
    public async Task GetPeriodComparison_ShouldReturnNegativeDelta_WhenRevenueDecreased()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        
        _mockCoreService
            .Setup(s => s.GetBalanceSummaryAsync(tenantId, It.Is<DateTime>(d => d.Month == 5)))
            .ReturnsAsync(new BalanceSummary { TotalRevenue = 8000000 });
        
        _mockCoreService
            .Setup(s => s.GetBalanceSummaryAsync(tenantId, It.Is<DateTime>(d => d.Month == 4)))
            .ReturnsAsync(new BalanceSummary { TotalRevenue = 10000000 });
        
        // Act
        var comparison = await _uiService.GetPeriodComparisonAsync(tenantId, new DateTime(2026, 5, 1));
        
        // Assert
        Assert.Equal(-20.0m, comparison.RevenueDeltaPercent);  // -20%
        Assert.Equal("-20.0%", comparison.RevenueLabel);
    }

    [Fact]
    public async Task GetPeriodComparison_ShouldHandleZeroPreviousRevenue_WithoutException()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        
        _mockCoreService
            .Setup(s => s.GetBalanceSummaryAsync(tenantId, It.Is<DateTime>(d => d.Month == 5)))
            .ReturnsAsync(new BalanceSummary { TotalRevenue = 5000000 });
        
        _mockCoreService
            .Setup(s => s.GetBalanceSummaryAsync(tenantId, It.Is<DateTime>(d => d.Month == 4)))
            .ReturnsAsync(new BalanceSummary { TotalRevenue = 0 });
        
        // Act
        var comparison = await _uiService.GetPeriodComparisonAsync(tenantId, new DateTime(2026, 5, 1));
        
        // Assert — no division by zero, returns 0%
        Assert.Equal(0m, comparison.RevenueDeltaPercent);
    }
}
```

**Validation Command:**
```powershell
dotnet test 6_Tests/VanAn.Integration.Tests/VanAn.Integration.Tests.csproj --filter "FullyQualifiedName~AccountingUIServiceTests"
```

---

## PHASE 4: E2E Tests — Full User Journey

**Problem:** Need to test critical user flows from login through accounting entry to verification in history.

**Files to Create:**
- `6_Testing/e2e-tests/accounting-entry-flow.spec.ts`
- `6_Testing/e2e-tests/expense-entry-flow.spec.ts`
- `6_Testing/e2e-tests/balance-dashboard-flow.spec.ts`
- `6_Testing/e2e-tests/export-excel-flow.spec.ts`

**Files to Modify:**
- `6_Testing/e2e-tests/package.json` — Add Playwright if not already present

---

### Phase 4.1: accounting-entry-flow.spec.ts — Revenue Entry Full Flow

**File:** `6_Testing/e2e-tests/accounting-entry-flow.spec.ts`

**Exact Changes Required:**

```typescript
import { test, expect } from '@playwright/test';

test.describe('Accounting Entry Flow', () => {
  test.beforeEach(async ({ page }) => {
    // Login before each test
    await page.goto('/login');
    await page.fill('#email', 'accounting@vanan.com');
    await page.fill('#password', 'test123');
    await page.click('button[type="submit"]');
    await page.waitForURL('/dashboard');
  });

  test('should create revenue entry and appear in history', async ({ page }) => {
    // Navigate to revenue entry
    await page.goto('/accounting/revenue');
    
    // Fill form
    await page.fill('#date', '2026-05-20');
    await page.fill('#amount', '1000000');
    await page.selectOption('#account', '511');
    await page.fill('#description', 'Doanh thu bán hàng');
    await page.fill('#reference', 'HĐ-001');
    
    // Submit
    await page.click('button[type="submit"]');
    
    // Verify success message
    await expect(page.locator('div.alert-success')).toBeVisible();
    await expect(page.locator('div.alert-success')).toContainText('Đã tạo thành công');
    
    // Navigate to history
    await page.goto('/accounting/history');
    
    // Verify entry appears in table
    await expect(page.locator('table')).toContainText('1.000.000 ₫');
    await expect(page.locator('table')).toContainText('511');
    await expect(page.locator('table')).toContainText('Doanh thu bán hàng');
  });

  test('should show validation error when amount is zero', async ({ page }) => {
    await page.goto('/accounting/revenue');
    
    await page.fill('#date', '2026-05-20');
    await page.fill('#amount', '0');
    await page.selectOption('#account', '511');
    await page.click('button[type="submit"]');
    
    await expect(page.locator('div.alert-error')).toBeVisible();
    await expect(page.locator('div.alert-error')).toContainText('Số tiền phải > 0');
  });

  test('should show validation error when date is missing', async ({ page }) => {
    await page.goto('/accounting/revenue');
    
    await page.fill('#amount', '1000000');
    await page.selectOption('#account', '511');
    await page.click('button[type="submit"]');
    
    await expect(page.locator('div.alert-error')).toBeVisible();
    await expect(page.locator('div.alert-error')).toContainText('Ngày không được để trống');
  });

  test('should detect duplicate entry within 5 minutes', async ({ page }) => {
    await page.goto('/accounting/revenue');
    
    // First entry
    await page.fill('#date', '2026-05-20');
    await page.fill('#amount', '1000000');
    await page.selectOption('#account', '511');
    await page.fill('#description', 'Doanh thu bán hàng');
    await page.click('button[type="submit"]');
    await expect(page.locator('div.alert-success')).toBeVisible();
    
    // Second duplicate entry
    await page.goto('/accounting/revenue');
    await page.fill('#date', '2026-05-20');
    await page.fill('#amount', '1000000');
    await page.selectOption('#account', '511');
    await page.fill('#description', 'Doanh thu bán hàng');
    await page.click('button[type="submit"]');
    
    // Verify duplicate warning
    await expect(page.locator('div.alert-warning')).toBeVisible();
    await expect(page.locator('div.alert-warning')).toContainText('Giao dịch trùng lặp');
  });
});
```

**Validation Command:**
```powershell
cd 6_Testing/e2e-tests
npx playwright test accounting-entry-flow.spec.ts
```

---

### Phase 4.2: expense-entry-flow.spec.ts — Expense Entry Full Flow

**File:** `6_Testing/e2e-tests/expense-entry-flow.spec.ts`

**Exact Changes Required:**

```typescript
import { test, expect } from '@playwright/test';

test.describe('Expense Entry Flow', () => {
  test.beforeEach(async ({ page }) => {
    await page.goto('/login');
    await page.fill('#email', 'accounting@vanan.com');
    await page.fill('#password', 'test123');
    await page.click('button[type="submit"]');
    await page.waitForURL('/dashboard');
  });

  test('should create expense entry with vendor info', async ({ page }) => {
    await page.goto('/accounting/expenses');
    
    await page.fill('#date', '2026-05-20');
    await page.fill('#amount', '500000');
    await page.selectOption('#account', '621');
    await page.fill('#vendor', 'Công ty ABC');
    await page.selectOption('#category', 'Mua vật liệu');
    await page.fill('#description', 'Mua vật liệu sản xuất');
    await page.click('button[type="submit"]');
    
    await expect(page.locator('div.alert-success')).toBeVisible();
    
    await page.goto('/accounting/history');
    await expect(page.locator('table')).toContainText('500.000 ₫');
    await expect(page.locator('table')).toContainText('Công ty ABC');
  });

  test('should require vendor when category is purchase', async ({ page }) => {
    await page.goto('/accounting/expenses');
    
    await page.fill('#date', '2026-05-20');
    await page.fill('#amount', '500000');
    await page.selectOption('#account', '621');
    await page.selectOption('#category', 'Mua vật liệu');
    await page.click('button[type="submit"]');
    
    await expect(page.locator('div.alert-error')).toBeVisible();
    await expect(page.locator('div.alert-error')).toContainText('Nhà cung cấp không được để trống');
  });
});
```

**Validation Command:**
```powershell
cd 6_Testing/e2e-tests
npx playwright test expense-entry-flow.spec.ts
```

---

### Phase 4.3: balance-dashboard-flow.spec.ts — Balance Verification

**File:** `6_Testing/e2e-tests/balance-dashboard-flow.spec.ts`

**Exact Changes Required:**

```typescript
import { test, expect } from '@playwright/test';

test.describe('Balance Dashboard Flow', () => {
  test.beforeEach(async ({ page }) => {
    await page.goto('/login');
    await page.fill('#email', 'accounting@vanan.com');
    await page.fill('#password', 'test123');
    await page.click('button[type="submit"]');
    await page.waitForURL('/dashboard');
  });

  test('should display correct balance metrics', async ({ page }) => {
    // Pre-create some entries via API or manual setup
    // Then navigate to balance page
    await page.goto('/accounting/balance');
    
    // Verify metrics cards are displayed
    await expect(page.locator('.metrics-card')).toHaveCount(3);
    
    // Verify labels
    await expect(page.locator('text=Tổng doanh thu')).toBeVisible();
    await expect(page.locator('text=Tổng chi phí')).toBeVisible();
    await expect(page.locator('text=Lợi nhuận ròng')).toBeVisible();
  });

  test('should show warning when expenses exceed threshold', async ({ page }) => {
    // Setup: Create high expense entries
    await page.goto('/accounting/balance');
    
    // Verify warning alert is shown
    await expect(page.locator('div.alert-warning')).toBeVisible();
    await expect(page.locator('div.alert-warning')).toContainText('Chi phí vượt quá doanh thu');
  });

  test('should display balance grid with account details', async ({ page }) => {
    await page.goto('/accounting/balance');
    
    await expect(page.locator('.balance-grid')).toBeVisible();
    await expect(page.locator('table')).toContainText('Tài khoản');
    await expect(page.locator('table')).toContainText('Số dư');
  });
});
```

**Validation Command:**
```powershell
cd 6_Testing/e2e-tests
npx playwright test balance-dashboard-flow.spec.ts
```

---

### Phase 4.4: export-excel-flow.spec.ts — Export Functionality

**File:** `6_Testing/e2e-tests/export-excel-flow.spec.ts`

**Exact Changes Required:**

```typescript
import { test, expect } from '@playwright/test';

test.describe('Export Excel Flow', () => {
  test.beforeEach(async ({ page }) => {
    await page.goto('/login');
    await page.fill('#email', 'accounting@vanan.com');
    await page.fill('#password', 'test123');
    await page.click('button[type="submit"]');
    await page.waitForURL('/dashboard');
  });

  test('should export transaction history to Excel', async ({ page }) => {
    await page.goto('/accounting/history');
    
    // Apply filter
    await page.selectOption('#period-select', '2026-05');
    await page.click('#filter-button');
    
    // Click export button
    const downloadPromise = page.waitForEvent('download');
    await page.click('button[data-action="export"]');
    const download = await downloadPromise;
    
    // Verify file is downloaded
    expect(download.suggestedFilename()).toMatch(/\.xlsx$/);
    
    // Save file for verification
    await download.saveAs('./temp-export.xlsx');
  });

  test('should export only filtered data', async ({ page }) => {
    await page.goto('/accounting/history');
    
    // Search for specific description
    await page.fill('#search-input', 'bán hàng');
    await page.click('#search-button');
    
    // Export
    const downloadPromise = page.waitForEvent('download');
    await page.click('button[data-action="export"]');
    const download = await downloadPromise;
    
    expect(download.suggestedFilename()).toMatch(/\.xlsx$/);
  });
});
```

**Validation Command:**
```powershell
cd 6_Testing/e2e-tests
npx playwright test export-excel-flow.spec.ts
```

---

## PHASE 5: Dependencies & Configuration

**Problem:** Need to add required NuGet packages and NPM dependencies for test frameworks.

### Phase 5.1: Add NuGet Packages to ShopERP Tests

**File:** `6_Tests/VanAn.ShopERP.Tests/VanAn.ShopERP.Tests.csproj`

**Exact Changes Required:**
Already created in Phase 2.1. No additional changes needed.

**Validation Command:**
```powershell
dotnet restore 6_Tests/VanAn.ShopERP.Tests/VanAn.ShopERP.Tests.csproj
```

---

### Phase 5.2: Add ShopERP Tests to Solution

**File:** `6_Tests/VanAn.Tests.sln`

**Exact Changes Required:**
Add project reference to solution file.

**Validation Command:**
```powershell
dotnet sln 6_Tests/VanAn.Tests.sln add 6_Tests/VanAn.ShopERP.Tests/VanAn.ShopERP.Tests.csproj
```

---

### Phase 5.3: Verify Playwright Installation

**File:** `6_Testing/e2e-tests/package.json`

**Exact Changes Required:**
Ensure Playwright is listed in devDependencies.

**Validation Command:**
```powershell
cd 6_Testing/e2e-tests
npm install
npx playwright install
```

---

## PHASE 6: Final Validation

**Problem:** Verify all tests pass before starting implementation.

### Phase 6.1: Run All Unit Tests

**Validation Command:**
```powershell
dotnet test 6_Tests/VanAn.Core.Tests/VanAn.Core.Tests.csproj --filter "FullyQualifiedName~Accounting"
```

**Expected Result:** All tests should FAIL initially (TDD red phase).

---

### Phase 6.2: Run All Component Tests

**Validation Command:**
```powershell
dotnet test 6_Tests/VanAn.ShopERP.Tests/VanAn.ShopERP.Tests.csproj
```

**Expected Result:** All tests should FAIL initially (TDD red phase).

---

### Phase 6.3: Run All Integration Tests

**Validation Command:**
```powershell
dotnet test 6_Tests/VanAn.Integration.Tests/VanAn.Integration.Tests.csproj --filter "FullyQualifiedName~Accounting"
```

**Expected Result:** All tests should FAIL initially (TDD red phase).

---

### Phase 6.4: Run All E2E Tests

**Validation Command:**
```powershell
cd 6_Testing/e2e-tests
npx playwright test
```

**Expected Result:** All tests should FAIL initially (TDD red phase).

---

## TDD Implementation Sequence

### Week 1: Unit Tests + Component Tests
1. **Day 1:** Phase 1.1-1.4 (Unit Tests) — Write failing tests
2. **Day 2:** Phase 2.1-2.2 (Create test project, RevenueEntry tests) — Write failing tests
3. **Day 3:** Phase 2.3-2.4 (ExpenseEntry, TransactionHistory tests) — Write failing tests
4. **Day 4:** Phase 2.5-2.6 (AccountBalance, AccountingIndex tests) — Write failing tests
5. **Day 5:** Implement validation logic to make unit tests pass (Green phase)

### Week 2: Integration Tests + E2E Tests
1. **Day 1:** Phase 3.1-3.2 (AccountingEntryFlow, TransactionHistoryQuery) — Write failing tests
2. **Day 2:** Phase 3.3-3.4 (BalanceCalculation, MultiTenancy) — Write failing tests
3. **Day 3:** Phase 4.1-4.2 (Accounting entry E2E, Expense entry E2E) — Write failing tests
4. **Day 4:** Phase 4.3-4.4 (Balance dashboard, Export Excel) — Write failing tests
5. **Day 5:** Implement services and UI to make all tests pass (Green phase)

---

## Test Coverage Targets

| Test Layer | Target Coverage | Priority |
|------------|-----------------|----------|
| Unit Tests (validation logic) | 90%+ | HIGH |
| Component Tests (Blazor) | 80%+ | HIGH |
| Integration Tests (service calls) | 70%+ | MEDIUM |
| E2E Tests (critical flows) | 100% (critical paths) | MEDIUM |

---

## Notes

- **TDD Discipline:** Always write the test BEFORE implementing the feature
- **Red-Green-Refactor:** Ensure test fails (red), implement to pass (green), then refactor
- **Test Independence:** Each test should be able to run independently
- **Test Data:** Use TestDataBuilder for consistent test data
- **Mock Services:** Use Mock<> for external dependencies in component tests
- **Real Services:** Use real services with test database in integration tests
- **Browser Automation:** Use Playwright for E2E tests with real browser
