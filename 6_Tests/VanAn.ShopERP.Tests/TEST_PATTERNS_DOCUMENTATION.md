# Test Patterns Documentation
**Date:** 2026-05-23
**Purpose:** Capture fix/implement patterns discovered during test implementation

## Pattern Categories

### 1. Component Test Base Patterns

#### Pattern: NavigationManager Mock
**Use Case:** Components that use NavigationManager.NavigateTo()
**Implementation:**
```csharp
// In ComponentTestBase constructor
Services.AddSingleton<NavigationManager, TestNavigationManager>();
```
**Notes:** TestNavigationManager is a Bunit-provided mock that tracks navigation calls

#### Pattern: JSInterop Configuration
**Use Case:** Components with JavaScript interop
**Implementation:**
```csharp
// In ComponentTestBase constructor
JSInterop.Mode = JSRuntimeMode.Loose;
```
**Notes:** Loose mode allows JS calls to pass without actual implementation

#### Pattern: Layout Component Stub
**Use Case:** Pages using layout components
**Implementation:**
```csharp
// Register layout as simple stub or use RenderFragment to skip layout
// Option 1: Stub component
Services.AddSingleton<LayoutType, MockLayout>();

// Option 2: Render without layout
var cut = RenderComponent<PageType>(parameters => 
    parameters.Add(p => p.Layout, (RenderFragment)null));
```

### 2. Service Mock Patterns

#### Pattern: AccountingService Mock Setup
**Use Case:** Testing accounting operations
**Implementation:**
```csharp
var mockService = new Mock<IAccountingService>();
mockService.Setup(s => s.CreateRevenueEntryAsync(
    It.IsAny<TenantId>(), 
    It.IsAny<AccountingPeriod>(),
    It.IsAny<decimal>(),
    It.IsAny<string>()))
    .ReturnsAsync(new AccountingEntryDto { Id = Guid.NewGuid() });
Services.AddSingleton(mockService.Object);
```

#### Pattern: Authentication State Mock
**Use Case:** Components requiring authenticated user with TenantId
**Implementation:**
```csharp
// Already in ComponentTestBase, but can be customized
var testUser = new ClaimsPrincipal(new ClaimsIdentity(new[]
{
    new Claim("TenantId", "test-tenant-guid")
}, "TestAuthentication"));
```

### 3. Component Render Patterns

#### Pattern: Render with Parameters
**Use Case:** Components requiring specific parameters
**Implementation:**
```csharp
var cut = RenderComponent<ComponentType>(parameters => 
    parameters
        .Add(p => p.SomeProperty, value)
        .Add(p => p.EventCallback, callback));
```

#### Pattern: Find and Interact with Elements
**Use Case:** Testing user interactions
**Implementation:**
```csharp
// Find element by CSS selector
var dateInput = cut.Find("input[type='date']");
dateInput.Change("2026-05-23");

// Find button and click
var submitButton = cut.Find("button[type='submit']");
submitButton.Click();

// Find by component type
var alertComponent = cut.FindComponent<VanAAlert>();
```

### 4. Validation Test Patterns

#### Pattern: Form Validation Test
**Use Case:** Testing form validation logic
**Implementation:**
```csharp
[Fact]
public void Component_ShouldShowValidationError_WhenInputIsInvalid()
{
    // Arrange
    var cut = RenderComponent<ComponentType>();
    var input = cut.Find("#amount-input");
    
    // Act - Set invalid value
    input.Change("0");
    input.Blur(); // Trigger validation
    
    // Assert - Verify error message
    var errorMessage = cut.Find(".validation-error");
    errorMessage.TextContent.Should().Contain("phải lớn hơn 0");
}
```

#### Pattern: Service Call Verification
**Use Case:** Verifying service methods are called correctly
**Implementation:**
```csharp
[Fact]
public async Task Component_ShouldCallService_WhenFormIsValid()
{
    // Arrange
    var mockService = new Mock<IAccountingService>();
    Services.AddSingleton(mockService.Object);
    var cut = RenderComponent<ComponentType>();
    
    // Act - Fill form and submit
    cut.Find("#date-input").Change("2026-05-23");
    cut.Find("#amount-input").Change("1000000");
    cut.Find("button[type='submit']").Click();
    
    // Assert - Verify service called
    mockService.Verify(s => s.CreateRevenueEntryAsync(
        It.IsAny<TenantId>(),
        It.IsAny<AccountingPeriod>(),
        It.Is<decimal>(a => a == 1000000),
        It.IsAny<string>()), 
        Times.Once);
}
```

### 5. UI Component Patterns

#### Pattern: Alert Component Verification
**Use Case:** Testing alert display
**Implementation:**
```csharp
[Fact]
public void Component_ShouldShowSuccessAlert_WhenOperationSucceeds()
{
    // Arrange
    var mockService = new Mock<IAccountingService>();
    mockService.Setup(s => s.MethodAsync()).ReturnsAsync(successResult);
    Services.AddSingleton(mockService.Object);
    var cut = RenderComponent<ComponentType>();
    
    // Act - Trigger operation
    cut.Find("button[type='submit']").Click();
    
    // Assert - Verify alert
    var alert = cut.FindComponent<VanAAlert>();
    alert.Instance.Variant.Should().Be("success"); // Note: uses "Variant" not "Type"
    alert.Instance.Message.Should().Contain("thành công");
}
```
**Notes:** VanAAlert uses `Variant` property (info, success, warning, error) instead of `Type`

#### Pattern: Modal Component Testing
**Use Case:** Testing modal open/close
**Implementation:**
```csharp
[Fact]
public void Component_ShouldOpenModal_WhenButtonClicked()
{
    // Arrange
    var cut = RenderComponent<ComponentType>();
    
    // Act - Click button to open modal
    cut.Find(".open-modal-button").Click();
    
    // Assert - Verify modal is visible
    var modal = cut.FindComponent<VanAModal>();
    modal.Instance.Should().NotBeNull();
}
```

### 6. Data Grid Patterns

#### Pattern: Data Grid Render Verification
**Use Case:** Testing data grid with items
**Implementation:**
```csharp
[Fact]
public void Component_ShouldRenderDataGrid_WhenDataExists()
{
    // Arrange
    var mockService = new Mock<IAccountingService>();
    mockService.Setup(s => s.GetEntriesAsync(...))
        .ReturnsAsync(new List<AccountingEntryDto> { /* test data */ });
    Services.AddSingleton(mockService.Object);
    
    // Act
    var cut = RenderComponent<ComponentType>();
    
    // Assert - Verify data grid
    var dataGrid = cut.FindComponent<VanAnDataGrid<AccountingEntryDto>>();
    dataGrid.Instance.Items.Count.Should().BeGreaterThan(0);
}
```

#### Pattern: Filter Testing
**Use Case:** Testing search/filter functionality
**Implementation:**
```csharp
[Fact]
public void Component_ShouldFilterData_WhenSearchIsEntered()
{
    // Arrange
    var cut = RenderComponent<ComponentType>();
    var searchInput = cut.Find(".search-input");
    
    // Act - Enter search term
    searchInput.Change("test");
    
    // Assert - Verify filter applied
    mockService.Verify(s => s.GetEntriesAsync(
        It.IsAny<Guid>(),
        It.Is<string>(search => search.Contains("test")),
        ...), Times.Once);
}
```

### 7. Async Operation Patterns

#### Pattern: Async Form Submission
**Use Case:** Testing async form operations
**Implementation:**
```csharp
[Fact]
public async Task Component_ShouldHandleAsyncSubmit_WhenFormIsValid()
{
    // Arrange
    var mockService = new Mock<IAccountingService>();
    mockService.Setup(s => s.CreateRevenueEntryAsync(...))
        .ReturnsAsync(new AccountingEntryDto { Id = Guid.NewGuid() });
    Services.AddSingleton(mockService.Object);
    var cut = RenderComponent<ComponentType>();
    
    // Act - Submit form
    await cut.Find("form").SubmitAsync(new FormEventArgs());
    
    // Assert - Wait for async operation
    cut.WaitForAssertion(() => 
    {
        mockService.Verify(s => s.CreateRevenueEntryAsync(...), Times.Once);
    });
}
```

### 8. Component Discovery Patterns

#### Pattern: Component Type Resolution
**Use Case:** When Bunit cannot discover components
**Implementation:**
```csharp
// In ComponentTestBase or test setup
TestContext.JSInterop.Mode = JSRuntimeMode.Loose;
TestContext.Services.Add(new ServiceDescriptor(
    typeof(ComponentType), 
    typeof(ComponentType), 
    ServiceLifetime.Transient));

// Or use FindComponents with explicit type
var components = cut.FindComponents<ComponentType>();
```

### 9. Error Handling Patterns

#### Pattern: Error Boundary Testing
**Use Case:** Testing error display
**Implementation:**
```csharp
[Fact]
public void Component_ShouldShowError_WhenServiceFails()
{
    // Arrange
    var mockService = new Mock<IAccountingService>();
    mockService.Setup(s => s.MethodAsync())
        .ThrowsAsync(new Exception("Service error"));
    Services.AddSingleton(mockService.Object);
    var cut = RenderComponent<ComponentType>();
    
    // Act - Trigger operation
    cut.Find("button").Click();
    
    // Assert - Verify error message
    var errorAlert = cut.FindComponent<VanAAlert>();
    errorAlert.Instance.Type.Should().Be("error");
}
```

### 10. Integration Test Patterns

#### Pattern: Full User Flow Test
**Use Case:** Testing complete user journey
**Implementation:**
```csharp
[Fact]
public async Task UserFlow_ShouldComplete_WhenAllStepsValid()
{
    // Arrange
    var cut = RenderComponent<ComponentType>();
    
    // Act - Step 1: Fill form
    cut.Find("#date").Change("2026-05-23");
    cut.Find("#amount").Change("1000000");
    
    // Act - Step 2: Submit
    cut.Find("button[type='submit']").Click();
    
    // Act - Step 3: Verify success
    cut.WaitForState(() => cut.Instance.IsSuccess);
    
    // Assert - Verify final state
    cut.Find(".success-message").Should().NotBeNull();
}
```

## Patterns Discovered During Implementation

*This section will be updated as new patterns are discovered during the implementation phase*

### [Pattern Name]
**Date Discovered:** YYYY-MM-DD
**Context:** Description of when this pattern was needed
**Implementation:** Code example
**Notes:** Any special considerations or gotchas

---

## Anti-Patterns to Avoid

### 1. Over-Mocking
**Problem:** Mocking too many services makes tests brittle
**Solution:** Only mock services that have side effects or external dependencies

### 2. Testing Implementation Details
**Problem:** Tests break when internal implementation changes
**Solution:** Test behavior, not implementation (user interactions, visible outputs)

### 3. Ignoring Async Operations
**Problem:** Not waiting for async operations to complete
**Solution:** Use `await` and `WaitForAssertion` for async operations

### 4. Shallow Assertions
**Problem:** Only checking that component renders
**Solution:** Verify actual business logic (validation, service calls, UI state changes)
