using Xunit;
using Microsoft.EntityFrameworkCore;
using VanAn.CoreHub.Infrastructure;
using VanAn.Shared.Domain;
using VanAn.Shared.Domain.Aggregates.TenantAggregate;
using VanAn.Shared.Domain.Aggregates.UserAggregate;

namespace VanAn.Integration.Tests.Infrastructure;

/// <summary>
/// Sample test demonstrating the new test infrastructure
/// Uses TestDatabaseFixture and TestDataSeeder
/// </summary>
public class TestDatabaseFixtureTests : IClassFixture<TestDatabaseFixture>
{
    private readonly TestDatabaseFixture _fixture;

    public TestDatabaseFixtureTests(TestDatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task TestDatabaseFixture_CanInitializeDbContext()
    {
        // Arrange & Act
        var dbContext = _fixture.DbContext;

        // Assert
        Assert.NotNull(dbContext);
        Assert.True(await dbContext.Database.CanConnectAsync());
    }

    [Fact]
    public async Task TestDataSeeder_CanSeedTenant()
    {
        // Arrange
        var dbContext = _fixture.CreateFreshDbContext();
        var tenantId = new TenantId(Guid.NewGuid());
        _fixture.SetCurrentTenant(tenantId);

        // Act
        var tenant = await TestDataSeeder.SeedTenantAsync(dbContext, tenantId, "Test Tenant");

        // Assert
        Assert.NotNull(tenant);
        Assert.Equal(tenantId, tenant.Id);
        Assert.Equal("Test Tenant", tenant.Name);
        Assert.Equal(BusinessType.HouseholdBusiness, tenant.BusinessType);
        
        await dbContext.DisposeAsync();
    }

    [Fact]
    public async Task TestDataSeeder_CanSeedUser()
    {
        // Arrange
        var dbContext = _fixture.CreateFreshDbContext();
        var tenantId = new TenantId(Guid.NewGuid());
        _fixture.SetCurrentTenant(tenantId);
        var tenant = await TestDataSeeder.SeedTenantAsync(dbContext, tenantId, "Test Tenant");

        // Act
        var user = await TestDataSeeder.SeedUserAsync(dbContext, tenantId: tenantId, username: "testuser", displayName: "Test User");

        // Assert
        Assert.NotNull(user);
        Assert.Equal("testuser", user.Username);
        Assert.Equal("Test User", user.DisplayName);
        Assert.True(user.IsActive);
        
        await dbContext.DisposeAsync();
    }

    [Fact]
    public async Task TestDataSeeder_CanSeedAccountingEntries()
    {
        // Arrange
        var dbContext = _fixture.CreateFreshDbContext();
        var tenantId = new TenantId(Guid.NewGuid());
        _fixture.SetCurrentTenant(tenantId);
        await TestDataSeeder.SeedTenantAsync(dbContext, tenantId, "Test Tenant");

        // Act
        var entries = await TestDataSeeder.SeedAccountingEntriesAsync(dbContext, tenantId, count: 3);

        // Assert
        Assert.NotNull(entries);
        Assert.Equal(3, entries.Count);
        Assert.All(entries, entry => Assert.Equal(tenantId, entry.TenantId));
        
        await dbContext.DisposeAsync();
    }

    [Fact]
    public async Task TestDataSeeder_CanSeedCompleteTestData()
    {
        // Arrange
        var dbContext = _fixture.CreateFreshDbContext();

        // Act
        var (tenant, user, entries) = await TestDataSeeder.SeedCompleteTestDataAsync(dbContext);

        // Set the tenant provider to match the seeded tenant
        _fixture.SetCurrentTenant(tenant.Id);

        // Assert
        Assert.NotNull(tenant);
        Assert.NotNull(user);
        Assert.NotNull(entries);
        Assert.Equal(5, entries.Count);
        Assert.Equal(tenant.Id, user.TenantId);
        Assert.All(entries, entry => Assert.Equal(tenant.Id, entry.TenantId));

        await dbContext.DisposeAsync();
    }
}
