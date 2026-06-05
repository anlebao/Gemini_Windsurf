# Test System Upgrade — Detail Implementation Plan

**Project:** VanAn Ecosystem — 6_Tests & 6_Testing  
**Version:** 1.1  
**Created:** 2026-05-05  
**Last Updated:** 2026-05-05

> **How to use:** Each phase section contains the problem, files to touch, exact changes required, and a validation command. After implementation, add `✅ Completed: YYYY-MM-DD` to the phase header.

---

## Namespace Strategy (MANDATORY — Read Before Any Phase)

```
Source of Truth (NEVER duplicate):
  CartItem          → VanAn.Shared.Domain          (1_Shared/Domain/CartItem.cs)
  Product           → VanAn.Shared.Domain
  Order / OrderItem → VanAn.Shared.Domain
  Customer          → VanAn.Shared.Domain
  CartState         → VanAn.KhachLink.Services
  SyncConflictResolver → VanAn.KhachLink.Services
  OfflineOrderDto   → VanAn.KhachLink.Models

Test project namespaces (never leak into src):
  VanAn.Core.Tests            → namespace VanAn.Core.Tests.*
  VanAn.Unit.Tests            → namespace VanAn.Unit.Tests.*
  VanAn.Integration.Tests     → namespace VanAn.Integration.Tests.*
  VanAn.Architecture.Tests    → namespace VanAn.Architecture.Tests
  VanAn.OrderFlow.Tests       → namespace VanAn.OrderFlow.Tests

Standard using block for new test files touching CartItem/CartState:
  using VanAn.Shared.Domain;
  using VanAn.KhachLink.Services;   // only when testing services
  using VanAn.KhachLink.Models;     // only when testing DTOs
  using Xunit;
  using Moq;
  using FluentAssertions;
```

---

## Phase 1 — Fix VanAn.Core.Tests Build Error

**Problem:** `VanAn.Core.Tests.csproj` references only `ShopERP` but 5 test files import `VanAn.KhachLink.*` namespaces → compile failure.

**Affected test files (currently uncompilable):**
- `UIStateMachineTests.cs`
- `TimeBasedBugTests.cs`
- `RetryStrategyTests.cs`
- `FinancialSafetyTests.cs`
- `ProductionDataTests.cs`

### File to Modify

**`6_Tests/VanAn.Core.Tests/VanAn.Core.Tests.csproj`**

Add one line inside the `<ItemGroup>` containing ProjectReferences:

```xml
<ProjectReference Include="..\..\5_WebApps\KhachLink\VanAn.KhachLink.csproj" />
```

Result after edit — ItemGroup should be:
```xml
<ItemGroup>
  <ProjectReference Include="..\..\1_Shared\VanAn.Shared.csproj" />
  <ProjectReference Include="..\..\2_Gateway\VanAn.Gateway.csproj" />
  <ProjectReference Include="..\..\3_CoreHub\VanAn.CoreHub.csproj" />
  <ProjectReference Include="..\..\5_WebApps\KhachLink\VanAn.KhachLink.csproj" />
  <ProjectReference Include="..\..\5_WebApps\ShopERP\VanAn.ShopERP.csproj" />
</ItemGroup>
```

### Validation

```powershell
dotnet build 6_Tests\VanAn.Core.Tests\VanAn.Core.Tests.csproj
# Expected: Build succeeded. 0 Error(s)
```

---
<!-- ✅ Completed: 2026-05-05 -->

## Phase 2 — Fix VanAn.Unit.Tests Single Source of Truth Violation

**Problem:** `6_Tests/VanAn.Unit.Tests/Domain/Lead.cs` declares a local `Customer` class (line 73) and `Lead` class while `using VanAn.Shared.Domain;` is active. This creates namespace ambiguity and violates the SSoT principle — domain POCOs must not be redefined in test projects.

**Root cause:** These are test-only POCO models for the Lead Management module unit tests. They are NOT the same as `VanAn.Shared.Domain.Customer`. However, the naming causes dangerous shadowing.

### Files to Modify

**1. `6_Tests/VanAn.Unit.Tests/Domain/Lead.cs`**

Rename classes:
- `public class Customer` → `public class LeadCustomer`
- `public class Lead` → `public class LeadModel`  
  *(Only if `VanAn.Shared.Domain.Lead` exists — check first)*

**2. `6_Tests/VanAn.Unit.Tests/TestBase.cs`**

Update all references:
- `protected Customer CreateTestCustomer()` → return type `LeadCustomer`
- All uses of `Customer` within `TestBase` → `LeadCustomer`

**3. Any test files in `VanAn.Unit.Tests/` that reference `Customer` or `Lead` as local type:**
- `TDDCustomerOnboardingServiceTests.cs` (if exists)
- `TDDLeadManagementServiceTests.cs` (if exists)
- `TDDLeadConversionServiceTests.cs` (if exists)

### Pre-check Before Implementing

```powershell
# Confirm whether Shared.Domain has a Lead entity
Select-String -Path "1_Shared\Domain.cs" -Pattern "public class Lead"
```

If `Lead` does NOT exist in `VanAn.Shared.Domain`, only rename `Customer` → `LeadCustomer`.

### Validation

```powershell
dotnet build 6_Tests\VanAn.Unit.Tests\VanAn.Unit.Tests.csproj
# Expected: Build succeeded. 0 Error(s)
```

> **⏸️ DEFERRED 2026-05-05:** 8 files / 96 refs require cascade rename. Risk too high for this iteration. Scheduled for next sprint.

---
<!-- ✅ Completed: YYYY-MM-DD -->

## Phase 3 — New Tests: CartItem Domain Coverage

**Problem:** Zero test coverage for `CartItem` after the domain refactoring that added `ProductId` and removed `Name` and `Price`.

**Goal:** Verify core `CartItem` invariants and computed properties.

### 3.1 — Add `CreateCartItem()` to BOTH TestEntityBuilders

**File A:** `6_Tests/VanAn.Core.Tests/TestInfrastructure/TestEntityBuilder.cs`

Add method (place after `CreateProduct()`):

```csharp
/// <summary>
/// Creates a test CartItem. CartItem is a record — all required properties must be set.
/// ProductId is the FK to the Product catalog (distinct from Id which is the cart line PK).
/// </summary>
public static CartItem CreateCartItem(
    Guid? productId = null,
    string productName = "Test Product",
    string description = "Test Description",
    int quantity = 1,
    decimal unitPrice = 25000m)
{
    return new CartItem
    {
        Id = Guid.NewGuid(),
        ProductId = productId ?? Guid.NewGuid(),
        ProductName = productName,
        Description = description,
        Quantity = quantity,
        UnitPrice = unitPrice
    };
}
```

**File B:** `6_Tests/VanAn.Integration.Tests/Infrastructure/TestEntityBuilder.cs`

Add the identical method as above.

### 3.2 — Create CartItemTests.cs

**New file:** `6_Tests/VanAn.Core.Tests/Domain/CartItemTests.cs`

```csharp
using VanAn.Shared.Domain;
using VanAn.Core.Tests.TestInfrastructure;
using Xunit;
using FluentAssertions;

namespace VanAn.Core.Tests.Domain;

[Trait("Category", "Unit")]
[Trait("Entity", "CartItem")]
public class CartItemTests
{
    [Fact(DisplayName = "TotalPrice equals Quantity times UnitPrice")]
    public void TotalPrice_IsQuantityTimesUnitPrice()
    {
        var item = TestEntityBuilder.CreateCartItem(quantity: 3, unitPrice: 25000m);
        item.TotalPrice.Should().Be(75000m);
    }

    [Fact(DisplayName = "ProductId is distinct from the cart line Id")]
    public void ProductId_IsDistinctFromCartLineId()
    {
        var productId = Guid.NewGuid();
        var item = TestEntityBuilder.CreateCartItem(productId: productId);
        item.ProductId.Should().Be(productId);
        item.Id.Should().NotBe(productId);
    }

    [Fact(DisplayName = "With expression produces new instance with updated Quantity")]
    public void WithExpression_UpdatesQuantityImmutably()
    {
        var original = TestEntityBuilder.CreateCartItem(quantity: 1);
        var updated = original with { Quantity = 5 };

        updated.Quantity.Should().Be(5);
        original.Quantity.Should().Be(1);
        updated.Id.Should().Be(original.Id);
        updated.ProductId.Should().Be(original.ProductId);
    }

    [Fact(DisplayName = "TotalPrice is zero when Quantity is zero")]
    public void TotalPrice_IsZero_WhenQuantityIsZero()
    {
        var item = TestEntityBuilder.CreateCartItem(quantity: 0);
        item.TotalPrice.Should().Be(0m);
    }

    [Fact(DisplayName = "ProductName is never null or empty after construction")]
    public void ProductName_IsNeverNullOrEmpty()
    {
        var item = TestEntityBuilder.CreateCartItem(productName: "Cà phê đen");
        item.ProductName.Should().NotBeNullOrEmpty();
    }

    [Fact(DisplayName = "Two CartItems with same ProductId but different Id are allowed")]
    public void SameProductId_DifferentCartLine_AllowedByDesign()
    {
        var sharedProductId = Guid.NewGuid();
        var item1 = TestEntityBuilder.CreateCartItem(productId: sharedProductId);
        var item2 = TestEntityBuilder.CreateCartItem(productId: sharedProductId);

        item1.ProductId.Should().Be(item2.ProductId);
        item1.Id.Should().NotBe(item2.Id);
    }
}
```

### Validation

```powershell
dotnet test 6_Tests\VanAn.Core.Tests\VanAn.Core.Tests.csproj --filter "Entity=CartItem"
# Expected: 6 tests passed
```

---
<!-- ✅ Completed: 2026-05-05 -->

## Phase 4 — New Tests: CartState Service Coverage

**Problem:** `CartState.cs` was updated to dedup by `ProductId` instead of `Id`, but no test verifies this critical business logic.

**Location for new test:** `VanAn.Integration.Tests` (already references KhachLink).

**Note:** `CartState` is a pure in-memory service — no mock needed. Instantiate directly.

### Create CartStateTests.cs

**New file:** `6_Tests/VanAn.Integration.Tests/CartStateTests.cs`

```csharp
using VanAn.Shared.Domain;
using VanAn.KhachLink.Services;
using VanAn.Integration.Tests.Infrastructure;
using Xunit;
using FluentAssertions;

namespace VanAn.Integration.Tests;

[Trait("Category", "Unit")]
[Trait("Service", "CartState")]
public class CartStateTests
{
    private readonly CartState _cartState;
    private readonly TenantId _tenantId;

    public CartStateTests()
    {
        _cartState = new CartState();
        _tenantId = TestEntityBuilder.CreateTenantId();
    }

    // --- Helper: create a Product using TestEntityBuilder ---
    private Product MakeProduct(string name = "Test Product", decimal price = 25000m)
        => TestEntityBuilder.CreateProduct(_tenantId, name, price);

    [Fact(DisplayName = "AddItem — new product is added to cart")]
    public void AddItem_NewProduct_AddsToCart()
    {
        var product = MakeProduct();
        _cartState.AddItem(product);
        _cartState.Items.Should().HaveCount(1);
        _cartState.Items[0].ProductId.Should().Be(product.Id);
    }

    [Fact(DisplayName = "AddItem — same product increments Quantity, does not duplicate")]
    public void AddItem_SameProduct_IncrementsQuantityNotDuplicates()
    {
        var product = MakeProduct();
        _cartState.AddItem(product);
        _cartState.AddItem(product);

        _cartState.Items.Should().HaveCount(1);
        _cartState.Items[0].Quantity.Should().Be(2);
    }

    [Fact(DisplayName = "AddItem — different products both appear in cart")]
    public void AddItem_DifferentProducts_AddsBoth()
    {
        _cartState.AddItem(MakeProduct("Product A", 10000m));
        _cartState.AddItem(MakeProduct("Product B", 20000m));
        _cartState.Items.Should().HaveCount(2);
    }

    [Fact(DisplayName = "RemoveItem — removes correct line by ProductId")]
    public void RemoveItem_ByProductId_RemovesCorrectLine()
    {
        var product = MakeProduct();
        _cartState.AddItem(product);
        _cartState.RemoveItem(product.Id);
        _cartState.Items.Should().BeEmpty();
    }

    [Fact(DisplayName = "UpdateQuantity — changes quantity by ProductId")]
    public void UpdateQuantity_ByProductId_ChangesQuantity()
    {
        var product = MakeProduct();
        _cartState.AddItem(product);
        _cartState.UpdateQuantity(product.Id, 5);
        _cartState.Items[0].Quantity.Should().Be(5);
    }

    [Fact(DisplayName = "UpdateQuantity — zero quantity removes the item")]
    public void UpdateQuantity_ZeroQuantity_RemovesItem()
    {
        var product = MakeProduct();
        _cartState.AddItem(product);
        _cartState.UpdateQuantity(product.Id, 0);
        _cartState.Items.Should().BeEmpty();
    }

    [Fact(DisplayName = "ClearCart — removes all items")]
    public void ClearCart_RemovesAllItems()
    {
        _cartState.AddItem(MakeProduct("A"));
        _cartState.AddItem(MakeProduct("B"));
        _cartState.ClearCart();
        _cartState.Items.Should().BeEmpty();
    }

    [Fact(DisplayName = "TotalAmount — sums TotalPrice of all items")]
    public void TotalAmount_SumsAllItemTotalPrices()
    {
        _cartState.AddItem(MakeProduct("A", 10000m));
        _cartState.AddItem(MakeProduct("B", 20000m));
        _cartState.TotalAmount.Should().Be(30000m);
    }
}
```

> **Pre-flight confirmed 2026-05-05:** `CartState()` has no constructor params. `AddItem(Product, int)` takes a `Product` object. Method is `Clear()` NOT `ClearCart()`. `FluentAssertions` added to `VanAn.Integration.Tests.csproj` as part of this phase.

### Validation

```powershell
dotnet test 6_Tests\VanAn.Integration.Tests\VanAn.Integration.Tests.csproj --filter "Service=CartState"
# Expected: 8 tests passed
```

---
<!-- ✅ Completed: 2026-05-05 -->

## Phase 5 — New Tests: SyncConflictResolver Coverage

**Problem:** `SyncConflictResolver.cs` was updated to use `serverItem.ProductId.ToString()` instead of `serverItem.Id.ToString()` for deduplication and DTO mapping. No regression test exists.

**Critical test:** Verify `ProductId` (not `Id`) is used as the dedup key and DTO mapping key.

### Create SyncConflictResolverTests.cs

**New file:** `6_Tests/VanAn.Integration.Tests/SyncConflictResolverTests.cs`

```csharp
using VanAn.Shared.Domain;
using VanAn.KhachLink.Services;
using VanAn.KhachLink.Models;
using VanAn.Integration.Tests.Infrastructure;
using Xunit;
using FluentAssertions;
using Moq;

namespace VanAn.Integration.Tests;

[Trait("Category", "Unit")]
[Trait("Service", "SyncConflictResolver")]
public class SyncConflictResolverTests
{
    private readonly SyncConflictResolver _resolver;
    private readonly TenantId _tenantId;

    public SyncConflictResolverTests()
    {
        _resolver = new SyncConflictResolver();
        _tenantId = TestEntityBuilder.CreateTenantId();
    }

    [Fact(DisplayName = "ToOfflineItemDto — maps ProductId from CartItem.ProductId, not CartItem.Id")]
    public void ToOfflineItemDto_MapsProductId_FromCartItemProductId_NotId()
    {
        var productId = Guid.NewGuid();
        var item = TestEntityBuilder.CreateCartItem(productId: productId);

        var dto = _resolver.ToOfflineItemDto(item);

        dto.ProductId.Should().Be(productId.ToString());
        dto.ProductId.Should().NotBe(item.Id.ToString());
    }

    [Fact(DisplayName = "Resolve — deduplicates server items by ProductId, not by CartItem.Id")]
    public void Resolve_DeduplicatesServerItems_ByProductId()
    {
        var sharedProductId = Guid.NewGuid();
        var serverItem1 = TestEntityBuilder.CreateCartItem(productId: sharedProductId);
        var serverItem2 = TestEntityBuilder.CreateCartItem(productId: sharedProductId);
        var serverItems = new List<CartItem> { serverItem1, serverItem2 };

        var result = _resolver.ResolveConflict(new List<CartItem>(), serverItems);

        result.Should().HaveCount(1);
        result[0].ProductId.Should().Be(sharedProductId);
    }

    [Fact(DisplayName = "Resolve — server items win when server has newer data")]
    public void Resolve_ServerItemsWin_WhenServerHasData()
    {
        var productId = Guid.NewGuid();
        var localItem  = TestEntityBuilder.CreateCartItem(productId: productId, quantity: 1);
        var serverItem = TestEntityBuilder.CreateCartItem(productId: productId, quantity: 3);

        var result = _resolver.ResolveConflict(
            new List<CartItem> { localItem },
            new List<CartItem> { serverItem });

        result.Should().HaveCount(1);
        result[0].Quantity.Should().Be(3);
    }

    [Fact(DisplayName = "Resolve — server extra items are added to merged result")]
    public void Resolve_ServerExtraItems_AddedToMerged()
    {
        var localItem  = TestEntityBuilder.CreateCartItem();
        var serverExtra = TestEntityBuilder.CreateCartItem();

        var result = _resolver.ResolveConflict(
            new List<CartItem> { localItem },
            new List<CartItem> { localItem, serverExtra });

        result.Should().HaveCount(2);
    }
}
```

> **Pre-flight confirmed 2026-05-05:** `ToOfflineItemDto()` does NOT exist as public method. `ResolveConflict()` does NOT exist. Actual public API: `ResolveCartConflictAsync(List<OfflineOrderItemDto>, List<CartItem>)`. Constructor requires `ILogger<SyncConflictResolver>` — use `Mock<ILogger<SyncConflictResolver>>().Object`. Test file was redesigned accordingly.

### Validation

```powershell
dotnet test 6_Tests\VanAn.Integration.Tests\VanAn.Integration.Tests.csproj --filter "Service=SyncConflictResolver"
# Expected: 4 tests passed
```

---
<!-- ✅ Completed: 2026-05-05 -->

## Phase 6 — Fix VanAn.OrderFlow.Tests Anti-Patterns

**Problem:** `OrderApiTests.cs` uses Pattern 1 (Direct Entity Creation with object initializer) which bypasses `protected` setters on domain entities. Tests also hard-code a live PostgreSQL connection string.

### 6.1 — Create TestEntityBuilder for OrderFlow

**New file:** `6_Tests/VanAn.OrderFlow.Tests/TestInfrastructure/TestEntityBuilder.cs`

```csharp
using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Tests.TestInfrastructure;

public static class TestEntityBuilder
{
    public static TenantId CreateTenantId(Guid? guid = null)
        => new TenantId(guid ?? Guid.Parse("12345678-1234-1234-1234-123456789abc"));

    public static Order CreateOrder(TenantId tenantId, Guid? customerId = null, decimal totalAmount = 100m)
        => new Order(tenantId, customerId ?? Guid.NewGuid(), totalAmount);

    public static OrderItem CreateOrderItem(TenantId tenantId, Guid orderId,
        Guid? productId = null, int quantity = 1, decimal unitPrice = 25000m)
        => new OrderItem(tenantId, orderId, productId ?? Guid.NewGuid(), quantity, unitPrice);
}
```

### 6.2 — Refactor OrderApiTests.cs

**File:** `6_Tests/VanAn.OrderFlow.Tests/OrderApiTests.cs`

Changes:
1. Add `[Fact(Skip = "Requires live PostgreSQL — run manually")]` attribute to all 3 test methods  
   *(Temporary: until CI environment has a test DB)*
2. Replace `new Order { ... }` with `TestEntityBuilder.CreateOrder(...)`
3. Replace `new OrderItem { ... }` with `TestEntityBuilder.CreateOrderItem(...)`
4. Remove `order.Items.Add(orderItem)` if the constructor/factory already sets items,  
   or use `order.AddItem(orderItem)` if that business method exists

### Validation

```powershell
dotnet build 6_Tests\VanAn.OrderFlow.Tests\VanAn.OrderFlow.Tests.csproj
# Expected: Build succeeded. 0 Error(s)
# Note: tests themselves are skipped (Skip attribute), which is intentional
```

---
<!-- ✅ Completed: 2026-05-05 -->

## Phase 7 — Architecture Regression Rules for CartItem

**Problem:** `ArchitectureRulesTests.cs` has no rule to prevent future developers from re-adding `Name` or `Price` to `CartItem`, or removing `ProductId`.

### File to Modify

**`6_Tests/VanAn.Architecture.Tests/ArchitectureRulesTests.cs`**

Add two new test methods at the end of the class (before the closing `}`):

```csharp
[Fact(DisplayName = "Rule F: CartItem must have ProductId property")]
public void CartItem_MustHave_ProductId()
{
    var cartItemFile = "../../../../../1_Shared/Domain/CartItem.cs";

    if (File.Exists(cartItemFile))
    {
        var content = File.ReadAllText(cartItemFile);
        Assert.Contains("ProductId", content);
    }
    else
    {
        Assert.Fail($"CartItem domain file not found: {cartItemFile}");
    }
}

[Fact(DisplayName = "Rule G: CartItem must NOT have redundant Name or Price properties")]
public void CartItem_MustNotHave_RedundantNameOrPrice()
{
    var cartItemFile = "../../../../../1_Shared/Domain/CartItem.cs";

    if (File.Exists(cartItemFile))
    {
        var content = File.ReadAllText(cartItemFile);
        Assert.DoesNotContain("required string Name", content);
        Assert.DoesNotContain("required decimal Price", content);
    }
    else
    {
        Assert.Fail($"CartItem domain file not found: {cartItemFile}");
    }
}
```

> **Relative path check:** The existing rules in `ArchitectureRulesTests.cs` use `"../../../../../1_Shared/..."`. Confirm `CartItem.cs` path is `1_Shared/Domain/CartItem.cs` before implementing.

### Validation

```powershell
dotnet test 6_Tests\VanAn.Architecture.Tests\VanAn.Architecture.Tests.csproj --filter "DisplayName~Rule F|DisplayName~Rule G"
# Expected: 2 tests passed
```

---
<!-- ✅ Completed: 2026-05-05 -->

## Phase 8 — Full Build + Test Verification

**Run after all phases 1–7 are complete.**

```powershell
# Full solution build
dotnet build VanAn.Tests.sln

# Run all non-DB-dependent tests
dotnet test VanAn.Tests.sln --filter "Category!=RequiresDB"

# Expected output:
#   Build succeeded. 0 Error(s) 0 Warning(s)
#   Test run summary: All tests passed
```

If any test fails, identify root cause before fixing — do NOT apply case-by-case patches.  
Follow the Pattern-Based Fix Protocol from `.windsurfrules`.

---
<!-- ✅ Completed: YYYY-MM-DD -->

---

## Update Protocol

After each phase is implemented and verified:

1. In this file: Add `✅ Completed: YYYY-MM-DD` on the comment line below the phase header
2. In `TEST_MASTER_PLAN.md`: Change `[ ]` → `[x]` in the Phase Status table
3. Update `Last Updated` date at the top of both files
4. Commit message format: `test: Phase N complete — [one-line description]`

Example:
```
test: Phase 1 complete — Added KhachLink reference to VanAn.Core.Tests.csproj
test: Phase 3 complete — Added CartItem domain test coverage (6 tests)
```
