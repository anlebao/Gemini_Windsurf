# Test Maintenance Guide

This guide provides best practices for maintaining and evolving the VanAn Accounting System test suite.

## When to Add Tests

### Unit Tests
Add unit tests when:
- Implementing new domain logic or business rules
- Adding new service methods or repository methods
- Refactoring existing code (Retrofit TDD approach)
- Fixing bugs (add regression test before fixing)
- Adding validation rules or constraints

### Integration Tests
Add integration tests when:
- Testing database interactions and EF Core queries
- Verifying multi-tenancy isolation at database level
- Testing service orchestration across multiple repositories
- Validating transaction boundaries and rollback behavior
- Testing real database constraints and indexes

### E2E Tests
Add E2E tests when:
- Testing critical user workflows (e.g., accounting entry creation, period closing)
- Validating integration between multiple services (Gateway, ShopERP, KhachLink)
- Testing authentication and authorization flows
- Validating UI components and user interactions
- Testing real-world scenarios that span multiple layers

## Test Refactoring Guidelines

### When to Refactor Tests
- Tests become slow or flaky
- Test setup code is duplicated across multiple test classes
- Test assertions are complex or unclear
- Tests are brittle (break frequently with unrelated changes)
- Test data setup is overly complex

### Refactoring Principles
1. **Keep tests independent** - Each test should be able to run in isolation
2. **Use descriptive test names** - Test names should clearly describe what is being tested
3. **Arrange-Act-Assert pattern** - Structure tests in three clear sections
4. **Avoid test logic duplication** - Extract common setup/teardown to fixtures or helpers
5. **Use test data builders** - For complex test data, use builder pattern or factory methods

### Example: Good Test Structure
```csharp
[Fact]
public async Task CreateRevenueEntryAsync_ShouldCreateEntry_WhenValidInput()
{
    // Arrange
    var tenantId = new TenantId(Guid.NewGuid());
    var period = new AccountingPeriod(2025, 1);
    var accountingService = CreateAccountingService(tenantId);

    // Act
    var entry = await accountingService.CreateRevenueEntryAsync(
        tenantId, period, 1000000m, "Test revenue", "511", "REF-001");

    // Assert
    Assert.NotNull(entry);
    Assert.Equal(1000000m, entry.Amount);
    Assert.Equal("Test revenue", entry.Description);
}
```

## Test Data Management

### Test Data Best Practices
- Use `TestDataSeeder` for consistent test data creation
- Clean up test data after each test to avoid interference
- Use deterministic test data (fixed GUIDs, predictable values)
- Avoid random data in tests unless specifically testing randomness
- Use test data builders for complex object graphs

### Cleanup Strategy
```csharp
public class MyIntegrationTests : IClassFixture<TestDatabaseFixture>
{
    private readonly TestDatabaseFixture _fixture;

    public MyIntegrationTests(TestDatabaseFixture fixture)
    {
        _fixture = fixture;
        _fixture.CleanupDatabaseAsync().GetAwaiter().GetResult();
    }

    [Fact]
    public async Task Test_WithCleanup()
    {
        // Test implementation
        // Database is automatically cleaned up via fixture
    }
}
```

## Handling Test Failures

### Debugging Failed Tests
1. Run the specific test with detailed output: `dotnet test --filter "FullyQualifiedName~TestName" --logger "console;verbosity=detailed"`
2. Check test logs and database state (for integration tests)
3. Verify test data setup is correct
4. Check for environment-specific issues (CI vs local)
5. Review recent changes that might affect the test

### Common Test Failure Patterns
- **Flaky tests**: Tests that pass sometimes and fail other times
  - Cause: Timing issues, resource contention, external dependencies
  - Fix: Add proper waits, use deterministic ordering, mock external dependencies
  
- **Brittle tests**: Tests that break with unrelated changes
  - Cause: Over-specific assertions, tight coupling to implementation details
  - Fix: Focus on behavior rather than implementation, use more flexible assertions
  
- **Slow tests**: Tests that take too long to run
  - Cause: Database operations, network calls, complex setup
  - Fix: Use in-memory databases, mock external services, optimize test data

## Coverage Guidelines

### Target Coverage
- Overall code coverage: > 80%
- Domain layer: > 90% (critical business logic)
- Service layer: > 80%
- Repository layer: > 70%
- API layer: > 60%

### Coverage Quality
- Focus on covering critical paths and edge cases
- Avoid writing tests just to increase coverage numbers
- Prioritize covering complex business logic over simple getters/setters
- Use coverage reports to identify untested critical code, not as a target to hit

## Test Documentation

### Test Documentation Requirements
- Complex test scenarios should have XML comments explaining the business context
- Integration tests should document what database state they verify
- E2E tests should document the user workflow being tested
- Non-obvious test data choices should be explained

### Example Test Documentation
```csharp
/// <summary>
/// Tests that revenue entries can be created with valid input.
/// This verifies the core accounting entry creation flow including:
/// - Domain validation (amount, description, account code)
/// - Database persistence via repository
/// - Audit trail generation
/// - Multi-tenancy enforcement
/// </summary>
[Fact]
public async Task CreateRevenueEntryAsync_ShouldCreateEntry_WhenValidInput()
{
    // Test implementation
}
```

## Continuous Integration

### CI Test Execution
- All tests must pass in CI before merging
- Parallel execution is enabled to reduce CI execution time
- Coverage reports are generated and uploaded as artifacts
- Failed tests block PR merges when branch protection is enabled

### Managing CI Test Failures
1. Check if the failure is consistent or flaky
2. Reproduce the failure locally if possible
3. Check for environment-specific issues (CI vs local)
4. Fix the root cause, not just the symptom
5. Add regression tests if the failure revealed a bug

## Test Suite Evolution

### Adding New Test Projects
- Follow existing project structure and naming conventions
- Use appropriate test framework (xUnit for C# tests)
- Configure test projects to use shared test infrastructure
- Update CI/CD workflows to include new test projects

### Deprecating Old Tests
- Document why a test is being deprecated
- Remove deprecated tests after a grace period
- Ensure test coverage is maintained after removal
- Update documentation to reflect test suite changes

## Resources

- [xUnit Documentation](https://xunit.net/)
- [EF Core Testing](https://docs.microsoft.com/en-us/ef/core/testing/)
- [Playwright Testing](https://playwright.dev/)
- [GitHub Actions Documentation](https://docs.github.com/en/actions)