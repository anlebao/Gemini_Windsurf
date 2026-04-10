# Van An E2E Testing Framework

## 🚨 IMPORTANT: APPLICATION MUST BE RUNNING

Before executing any E2E tests, ensure the following applications are running locally:

### Required Running Applications
- **KhachLink Mobile App**: `http://localhost:5002`
- **ShopERP Desktop App**: `http://localhost:5003`

### How to Start Applications
```bash
# Terminal 1 - Start KhachLink
cd 5_WebApps/KhachLink
dotnet run

# Terminal 2 - Start ShopERP  
cd 5_WebApps/ShopERP
dotnet run
```

## E2E Test Infrastructure Status

### ✅ COMPLETED COMPONENTS
- **Playwright Setup**: ✅ Chromium browser automation configured
- **Cross-Device Testing**: ✅ Mobile (390x844), Desktop (1920x1080), Tablet (768x1024)
- **Navigation Methods**: ✅ KhachLink, ShopERP, Admin routing
- **Base Test Class**: ✅ E2ETestBase with helper methods
- **Screenshot Functionality**: ✅ Debugging and visual regression support

### 📋 READY FOR TEST DEVELOPMENT
The E2E infrastructure is now ready for real test scenario development. All dummy/template tests have been removed.

### 🎯 NEXT STEPS
1. Start the required applications locally
2. Develop real E2E test scenarios for:
   - KhachLink mobile workflows
   - ShopERP desktop workflows  
   - Omnichannel synchronization
   - Production deployment

### 📁 PROJECT STRUCTURE
```
6_Tests/VanAn.E2E.Tests/
├── E2ETestBase.cs          # Base test class with Playwright setup
├── VanAn.E2E.Tests.csproj  # Project configuration
└── README.md               # This file
```

## Configuration Details

### Browser Configuration
- **Browser**: Chromium (headless for CI/CD)
- **Mobile Viewport**: 390x844 (iPhone 12)
- **Desktop Viewport**: 1920x1080
- **Tablet Viewport**: 768x1024

### Application URLs
- **KhachLink**: `http://localhost:5002` (mobile-first)
- **ShopERP**: `http://localhost:5003` (desktop)
- **Admin**: `http://localhost:5003/admin` (via ShopERP)

## Development Guidelines

### Test Naming Convention
- Use descriptive test names with `DisplayName` attribute
- Follow pattern: `E2E: [App] - [Feature] - [Scenario]`
- Example: `E2E: KhachLink - Product Catalog - Browse Products`

### Test Structure
```csharp
[Fact(DisplayName = "E2E: [App] - [Feature] - [Scenario]")]
public async Task [AppName]_[Feature]_[Scenario]_ShouldWorkCorrectly()
{
    // Arrange - Setup test conditions
    await NavigateTo[AppName]Async();
    
    // Act - Perform user actions
    await ClickElementAsync("[selector]");
    
    // Assert - Verify expected outcomes
    Assert.True(await ElementExistsAsync("[selector]"));
    
    await TakeScreenshotAsync("test_name");
}
```

### Important Notes
- **No Dummy Tests**: All tests must validate real application functionality
- **Real URLs Only**: Never use example.com or placeholder URLs
- **Applications Required**: Tests will fail if apps aren't running
- **Screenshots**: Take screenshots at key points for debugging
