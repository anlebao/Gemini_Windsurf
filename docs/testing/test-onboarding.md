# Test Onboarding Guide

This guide helps new developers understand how to work with the VanAn Accounting System test suite.

## Quick Start

### Prerequisites
- .NET 8.0 SDK installed
- Node.js 20.x installed (for E2E tests)
- Git clone of the repository
- Your favorite IDE (Visual Studio, VS Code, or JetBrains Rider)

### Running Your First Test

1. **Open the solution**
   ```bash
   cd c:/VibeCoding/Gemini_Windsurf
   dotnet restore VanAn.sln
   ```

2. **Run all unit tests**
   ```bash
   dotnet test 6_Tests/VanAn.Core.Tests/VanAn.Core.Tests.csproj
   ```

3. **Run a specific test**
   ```bash
   dotnet test --filter "FullyQualifiedName~CreateRevenueEntryAsync_ShouldCreateRevenueEntry_WhenValidInput"
   ```

## Test Suite Overview

### Test Projects

1. **VanAn.Core.Tests** - Unit tests
   - Tests domain logic, business rules, and services
   - Fast execution (no external dependencies)
   - Uses mocks for external dependencies

2. **VanAn.Integration.Tests** - Integration tests
   - Tests database interactions and service orchestration
   - Uses real SQLite in-memory database
   - Verifies multi-tenancy and data persistence

3. **Vanan.Architecture.Tests** - Architecture tests
   - Enforces architectural rules and constraints
   - Validates layer dependencies and naming conventions
   - Ensures Clean Architecture compliance

4. **VanAn.E2E.Tests** - End-to-end tests (in 6_Testing directory)
   - Tests complete user workflows
   - Uses Playwright for browser automation
   - Requires running services (Gateway, ShopERP, KhachLink)

### Test Organization

```
6_Tests/
├── VanAn.Core.Tests/
│   └── Accounting/
│       ├── AccountingEntryServiceTests.cs
│       ├── BusinessRulesTests.cs
│       └── AccountingEdgeCaseTests.cs
├── VanAn.Integration.Tests/
│   ├── Accounting/
│   │   ├── JournalEntryIntegrationTests.cs
│   │   ├── HKDBookIntegrationTests.cs
│   │   └── ReversalIntegrationTests.cs
│   └── Infrastructure/
│       ├── TestDatabaseFixture.cs
│       ├── TestDataSeeder.cs
│       └── TestDbContextFactory.cs
└── VanAn.Architecture.Tests/
    └── ArchitectureValidationTests.cs
```

## Writing Your First Test

### Unit Test Example

```csharp
using Xunit;
using VanAn.Shared.Domain;
using VanAn.CoreHub.Services;

namespace VanAn.Core.Tests.Accounting;

public class MyFirstAccountingTest
{
    [Fact]
    public void MyTest_ShouldDoSomething_WhenConditionIsMet()
    {
        // Arrange
        var expected = 100;
        var actual = 50 + 50;

        // Act
        // No action needed for this simple example

        // Assert
        Assert.Equal(expected, actual);
    }
}
```

### Integration Test Example

```csharp
using Xunit;
using VanAn.Integration.Tests.Infrastructure;
using VanAn.Shared.Domain;

namespace VanAn.Integration.Tests.Accounting;

public class MyFirstIntegrationTest : IClassFixture<TestDatabaseFixture>
{
    private readonly TestDatabaseFixture _fixture;

    public MyFirstIntegrationTest(TestDatabaseFixture fixture)
    {
        _fixture = fixture;
        _fixture.CleanupDatabaseAsync().GetAwaiter().GetResult();
    }

    [Fact]
    public async Task MyTest_ShouldVerifyDatabaseBehavior()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        await TestDataSeeder.SeedTenantAsync(_fixture.DbContext, new TenantId(tenantId), "Test Tenant");

        // Act
        var tenants = await _fixture.DbContext.Tenants.ToListAsync();

        // Assert
        Assert.Single(tenants);
        Assert.Equal("Test Tenant", tenants.First().Name);
    }
}
```

## Common Test Patterns

### Testing Service Methods

```csharp
[Fact]
public async Task ServiceMethod_ShouldReturnExpectedResult_WhenValidInput()
{
    // Arrange
    var repositoryMock = new Mock<IAccountingEntryRepository>();
    var service = new AccountingEntryService(repositoryMock.Object, ...);
    
    repositoryMock.Setup(r => r.AddAsync(It.IsAny<AccountingEntry>(), It.IsAny<CancellationToken>()))
        .Returns(Task.CompletedTask);

    // Act
    var result = await service.CreateRevenueEntryAsync(...);

    // Assert
    Assert.NotNull(result);
    repositoryMock.Verify(r => r.AddAsync(It.IsAny<AccountingEntry>(), It.IsAny<CancellationToken>()), Times.Once);
}
```

### Testing Database Operations

```csharp
[Fact]
public async Task Repository_ShouldPersistEntity_WhenValidInput()
{
    // Arrange
    var entry = AccountingEntry.CreateRevenue(...);
    
    // Act
    await _repository.AddAsync(entry, CancellationToken.None);
    await _fixture.DbContext.SaveChangesAsync();

    // Assert
    var savedEntry = await _fixture.DbContext.AccountingEntries.FindAsync(entry.Id);
    Assert.NotNull(savedEntry);
    Assert.Equal(entry.Amount, savedEntry.Amount);
}
```

### Testing Multi-Tenancy

```csharp
[Fact]
public async Task Service_ShouldEnforceTenantIsolation_WhenMultipleTenantsExist()
{
    // Arrange
    var tenant1 = new TenantId(Guid.NewGuid());
    var tenant2 = new TenantId(Guid.NewGuid());
    
    await TestDataSeeder.SeedTenantAsync(_fixture.DbContext, tenant1, "Tenant 1");
    await TestDataSeeder.SeedTenantAsync(_fixture.DbContext, tenant2, "Tenant 2");

    // Act
    var tenant1Entries = await _repository.GetByTenantAsync(tenant1, CancellationToken.None);
    var tenant2Entries = await _repository.GetByTenantAsync(tenant2, CancellationToken.None);

    // Assert
    Assert.DoesNotContain(tenant1Entries, e => e.TenantId == tenant2);
    Assert.DoesNotContain(tenant2Entries, e => e.TenantId == tenant1);
}
```

## Test Infrastructure

### TestDatabaseFixture

The `TestDatabaseFixture` provides:
- SQLite in-memory database for each test class
- Automatic cleanup between tests
- Dependency injection container for service resolution
- Tenant context management

### TestDataSeeder

The `TestDataSeeder` utility provides:
- `SeedTenantAsync` - Create test tenants
- `SeedUserAsync` - Create test users
- `SeedAccountingEntriesAsync` - Create test accounting entries
- `CleanupTenantDataAsync` - Clean up data for specific tenant
- `CleanupAllTestDataAsync` - Clean up all test data

## Troubleshooting

### Common Issues

**Issue**: Tests fail with "Database connection error"
- **Solution**: Ensure TestDatabaseFixture is properly initialized and cleanup is called

**Issue**: Tests pass locally but fail in CI
- **Solution**: Check for environment-specific differences, ensure all dependencies are committed

**Issue**: Tests are slow
- **Solution**: Use parallel execution flag `--parallel`, check for unnecessary database operations

**Issue**: Flaky tests (sometimes pass, sometimes fail)
- **Solution**: Check for timing issues, add proper waits, ensure test data is deterministic

### Getting Help

1. Check the test documentation in `6_Tests/README.md`
2. Review existing test implementations for patterns
3. Consult the test maintenance guide in `docs/testing/test-maintenance.md`
4. Ask team members for help with complex scenarios

## Best Practices

1. **Write tests before fixing bugs** (Retrofit TDD)
2. **Keep tests simple and focused** - One test should verify one thing
3. **Use descriptive test names** - Test names should read like documentation
4. **Clean up test data** - Don't leave test data that might interfere with other tests
5. **Run tests frequently** - Run tests after making changes to catch issues early
6. **Keep tests fast** - Slow tests discourage developers from running them
7. **Avoid test logic duplication** - Extract common setup to fixtures or helpers

## Next Steps

1. Explore existing test files to understand patterns
2. Try writing a simple unit test for a method you're working on
3. Run the full test suite to ensure everything passes
4. Read the test maintenance guide for more advanced topics
5. Set up your IDE to run tests efficiently (keyboard shortcuts, test explorer)

## Resources

- [6_Tests/README.md](../../6_Tests/README.md) - Test infrastructure and running instructions
- [test-maintenance.md](test-maintenance.md) - Test maintenance and refactoring guidelines
- [xUnit Documentation](https://xunit.net/) - Testing framework documentation
- [Moq Documentation](https://github.com/moq/moq4) - Mocking framework documentation