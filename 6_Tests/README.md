# VanAn Accounting Tests

This directory contains all tests for the VanAn Accounting System, including unit tests, integration tests, and E2E tests.

## Test Structure

- **VanAn.Core.Tests** - Unit tests for domain logic, business rules, and services
- **VanAn.Integration.Tests** - Integration tests with real SQLite database
- **VanAn.Architecture.Tests** - Architecture compliance tests
- **VanAn.E2E.Tests** - End-to-end tests with Playwright (in 6_Testing directory)

## Test Infrastructure

### TestDatabaseFixture

The `TestDatabaseFixture` class provides a SQLite in-memory database for integration tests. It implements `IAsyncLifetime` for proper resource cleanup.

**Note:** Testcontainers.Sqlite package does not exist on NuGet. We use SQLite in-memory mode with connection pooling as an alternative, which provides similar benefits for integration testing.

**Usage:**
```csharp
public class MyIntegrationTests : IClassFixture<TestDatabaseFixture>
{
    private readonly TestDatabaseFixture _fixture;

    public MyIntegrationTests(TestDatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Test_Something()
    {
        var dbContext = _fixture.DbContext;
        // Use dbContext for testing
    }
}
```

### TestDataSeeder

The `TestDataSeeder` utility provides methods to seed test data:

- `SeedTenantAsync` - Creates a test tenant
- `SeedUserAsync` - Creates a test user
- `SeedAccountingEntriesAsync` - Creates test accounting entries
- `SeedCompleteTestDataAsync` - Seeds complete test data (tenant, user, entries)
- `CleanupTenantDataAsync` - Cleans up data for a specific tenant
- `CleanupAllTestDataAsync` - Cleans up all test data

**Usage:**
```csharp
[Fact]
public async Task Test_WithSeededData()
{
    var (tenant, user, entries) = await TestDataSeeder.SeedCompleteTestDataAsync(_fixture.DbContext);
    
    // Test with seeded data
    
    await TestDataSeeder.CleanupTenantDataAsync(_fixture.DbContext, tenant.Id);
}
```

### TestDbContextFactory

The `TestDbContextFactory` provides factory methods for creating test DbContext instances:

- `Create(string connectionString)` - Creates DbContext with PostgreSQL connection
- `CreateInMemory(string databaseName)` - Creates DbContext with in-memory database
- `CreateSqliteInMemory()` - Creates DbContext with SQLite in-memory database (real relational behavior)

## Running Tests

### Prerequisites
- .NET 8.0 SDK
- Node.js 20.x (for E2E tests)
- No Docker Desktop required for integration tests (uses SQLite in-memory mode with Cache=Shared for connection pooling)
- Connection pooling is configured via connection string: `Data Source=:memory:;Cache=Shared;Mode=Memory`

### Run All Accounting Tests
```bash
# Run all unit tests
dotnet test 6_Tests/VanAn.Core.Tests/VanAn.Core.Tests.csproj

# Run all integration tests
dotnet test 6_Tests/VanAn.Integration.Tests/VanAn.Integration.Tests.csproj

# Run architecture tests
dotnet test 6_Tests/VanAn.Architecture.Tests/VanAn.Architecture.Tests.csproj

# Run all tests in solution
dotnet test VanAn.sln
```

### Run Accounting-Specific Tests
```bash
# Run accounting unit tests
dotnet test 6_Tests/VanAn.Core.Tests/ --filter "FullyQualifiedName~Accounting"

# Run accounting integration tests
dotnet test 6_Tests/VanAn.Integration.Tests/ --filter "FullyQualifiedName~Accounting"

# Run with parallel execution (faster)
dotnet test 6_Tests/VanAn.Integration.Tests/ --filter "FullyQualifiedName~Accounting" --parallel
```

### Run Specific Test Classes
```bash
# Run specific integration test class
dotnet test --filter "FullyQualifiedName~JournalEntryIntegrationTests"

# Run specific test method
dotnet test --filter "FullyQualifiedName~CreateRevenueEntryAsync_ShouldCreateRevenueEntry_WhenValidInput"
```

### Run with Detailed Output
```bash
dotnet test --logger "console;verbosity=detailed"

# Run with coverage collection
dotnet test --collect:"XPlat Code Coverage"
```

### Run E2E Tests
```bash
cd 6_Testing
npm install
npx playwright install --with-deps chromium
npx playwright test
```

## Test Configuration

Test configuration is stored in `appsettings.test.json`:

- Connection strings for test databases
- Testcontainers configuration
- Test data seeding settings

## Test Data Management

### Default Test IDs
- Default TenantId: `12345678-1234-1234-1234-123456789abc`
- Default UserId: `87654321-4321-4321-4321-cba987654321`

### Cleanup Strategy
Tests should clean up their own data using the `TestDataSeeder` cleanup methods. The SQLite in-memory database is automatically disposed when the test fixture is disposed.

## Architecture Notes

- Tests use SQLite in-memory database for fast execution with real relational behavior
- Domain entities use factory methods (e.g., `AccountingEntry.CreateRevenue`)
- Rich Domain aggregates are used (e.g., `TenantAggregate`, `UserAggregate`)
- Multi-tenancy is enforced via `ITenantProvider` and query filters

## CI/CD Integration

### GitHub Actions Workflows

The project uses GitHub Actions for automated testing:

1. **test-accounting.yml** - Dedicated workflow for accounting tests (Wave 5)
   - Runs accounting unit tests with coverage collection
   - Runs accounting integration tests with coverage collection
   - Uploads test results and coverage reports as artifacts
   - Configured for parallel execution to optimize CI performance
   - Triggers on push/PR to branches with accounting-related changes

2. **ci.yml** - Main CI pipeline
   - Runs all unit tests and architecture tests
   - Integration tests are currently disabled to save CI minutes
   - Runs on every push and pull request

3. **full-test-suite.yml** - Comprehensive test suite
   - Can run integration tests, E2E tests, and load tests
   - Triggered manually or on weekly schedule
   - Includes service startup for E2E tests

### Coverage Reporting

- Coverage reports are collected using `--collect:"XPlat Code Coverage"`
- Reports are uploaded as GitHub Actions artifacts
- Combined coverage reports are available in the `coverage-report-combined` artifact

### Test Parallelization

- Unit tests run in parallel by default
- Integration tests run in parallel when using `--parallel` flag
- Playwright E2E tests use 4 workers in CI (optimized in Wave 5)
- GitHub Actions workflow is configured for parallel job execution

### Branch Protection

Failed tests will block PR merges when branch protection rules are configured to require the accounting tests workflow to pass.
