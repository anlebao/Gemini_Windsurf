# VanAn Integration Tests

This directory contains integration tests for the VanAn Accounting System.

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
- No Docker Desktop required (uses SQLite in-memory mode with Cache=Shared for connection pooling)
- Connection pooling is configured via connection string: `Data Source=:memory:;Cache=Shared;Mode=Memory`

### Run all integration tests
```bash
dotnet test 6_Tests/VanAn.Integration.Tests/VanAn.Integration.Tests.csproj
```

### Run specific test class
```bash
dotnet test --filter "FullyQualifiedName~MyIntegrationTests"
```

### Run with detailed output
```bash
dotnet test --logger "console;verbosity=detailed"
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
