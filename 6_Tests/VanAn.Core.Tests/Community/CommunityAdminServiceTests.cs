using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using VanAn.CoreHub.Infrastructure;
using VanAn.CoreHub.Services;
using VanAn.Shared.Domain;
using VanAn.Shared.Domain.Aggregates.TenantAggregate;
using VanAn.Shared.Domain.Common;
using Xunit;
using Tenant = VanAn.Shared.Domain.Aggregates.TenantAggregate.Tenant;

using Microsoft.Extensions.DependencyInjection;
namespace VanAn.Core.Tests.Community;

/// <summary>
/// CC-S6 (Sprint 6): CommunityAdminService unit tests — 8 test cases (T1-T8).
/// Eligible list, activate/deactivate roles, get customer roles.
/// </summary>
public class CommunityAdminServiceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly VanAnDbContext _context;
    private readonly CommunityAdminService _service;
    private static readonly Guid TenantGuid = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private static readonly Guid AdminId = Guid.Parse("00000000-0000-0000-0000-000000000002");
    private readonly TenantId _tenantId = new(TenantGuid);

    public CommunityAdminServiceTests()
    {
        _connection = new SqliteConnection($"DataSource=test_{Guid.NewGuid()};Mode=Memory;Cache=Shared");
        _connection.Open();

        var efServiceProvider = new ServiceCollection().AddEntityFrameworkSqlite().BuildServiceProvider();

        var options = new DbContextOptionsBuilder<VanAnDbContext>()
            .UseInternalServiceProvider(efServiceProvider).UseSqlite(_connection)
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        _context = new VanAnDbContext(options);
        _context.Database.EnsureCreated();
        _service = new CommunityAdminService(_context, new StubShopFeatureSettingsService(), NullLogger<CommunityAdminService>.Instance);

        SeedTenant();
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    private void SeedTenant()
    {
        var tenant = Tenant.CreateCompany(_tenantId, "Test Tenant",
            TenantSettings.Empty());
        _context.Tenants.Add(tenant);
        _context.SaveChanges();
    }

    private Customer CreateCustomer(string name, IdentityLevel level, int points, bool isActive = true)
    {
        var customer = new Customer(_tenantId, name, "0901234567", "test@test.com")
        {
            // Use reflection to set properties that don't have public setters
        };
        // Set IdentityLevel + LoyaltyPoints + IsActive via reflection
        var idProp = typeof(Customer).GetProperty("IdentityLevel");
        idProp!.SetValue(customer, level);

        var pointsProp = typeof(Customer).GetProperty("LoyaltyPoints");
        pointsProp!.SetValue(customer, points);

        if (!isActive)
        {
            customer.UpdateCustomerDetails(customer.FullName, customer.PhoneNumber, customer.Email,
                customer.CustomerTier, customer.DeviceId, false);
        }

        _context.Customers.Add(customer);
        _context.SaveChanges();
        return customer;
    }

    // === T1: GetEligible_FiltersByVerifiedAndPoints ===
    [Fact(DisplayName = "T1: GetEligible_FiltersByVerifiedAndPoints")]
    public async Task GetEligible_FiltersByVerifiedAndPoints()
    {
        // Eligible: Verified + 1500 points
        var eligible = CreateCustomer("Eligible User", IdentityLevel.Verified, 1500);
        // Not eligible: Social + 1500 points (IdentityLevel too low)
        var lowIdentity = CreateCustomer("Low Identity", IdentityLevel.Social, 1500);
        // Not eligible: Verified + 500 points (points too low)
        var lowPoints = CreateCustomer("Low Points", IdentityLevel.Verified, 500);
        // Eligible: DeviceVerified + 2000 points (v1.2)
        var deviceVerified = CreateCustomer("Device Verified", IdentityLevel.DeviceVerified, 2000);

        var result = await _service.GetEligibleCustomersAsync(1, 20);

        Assert.Equal(2, result.Total);
        Assert.Contains(result.Items, i => i.CustomerId == eligible.Id);
        Assert.Contains(result.Items, i => i.CustomerId == deviceVerified.Id);
        Assert.DoesNotContain(result.Items, i => i.CustomerId == lowIdentity.Id);
        Assert.DoesNotContain(result.Items, i => i.CustomerId == lowPoints.Id);
    }

    // === T2: GetEligible_Paginates ===
    [Fact(DisplayName = "T2: GetEligible_Paginates")]
    public async Task GetEligible_Paginates()
    {
        for (int i = 0; i < 25; i++)
            CreateCustomer($"User {i}", IdentityLevel.Verified, 1000 + i);

        var page1 = await _service.GetEligibleCustomersAsync(1, 10);
        var page2 = await _service.GetEligibleCustomersAsync(2, 10);

        Assert.Equal(25, page1.Total);
        Assert.Equal(10, page1.Items.Count);
        Assert.Equal(10, page2.Items.Count);
        // No overlap
        Assert.Empty(page1.Items.Select(i => i.CustomerId).Intersect(page2.Items.Select(i => i.CustomerId)));
    }

    // === T3: ActivateRole_CreatesRole ===
    [Fact(DisplayName = "T3: ActivateRole_CreatesRole")]
    public async Task ActivateRole_CreatesRole()
    {
        var customer = CreateCustomer("New Shipper", IdentityLevel.Verified, 1200);

        var role = await _service.ActivateRoleAsync(customer.Id, CommunityRoleType.Shipper, AdminId);

        Assert.NotNull(role);
        Assert.Equal(customer.Id, role.CustomerId);
        Assert.Equal(CommunityRoleType.Shipper, role.RoleType);
        Assert.True(role.IsActive);
        Assert.Equal(AdminId, role.ActivatedBy);

        // Verify persisted
        var fromDb = await _context.CommunityRoles
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(r => r.Id == role.Id);
        Assert.NotNull(fromDb);
        Assert.True(fromDb.IsActive);
    }

    // === T4: ActivateRole_AlreadyActive_Throws ===
    [Fact(DisplayName = "T4: ActivateRole_AlreadyActive_Throws")]
    public async Task ActivateRole_AlreadyActive_Throws()
    {
        var customer = CreateCustomer("Existing Shipper", IdentityLevel.Verified, 1200);
        await _service.ActivateRoleAsync(customer.Id, CommunityRoleType.Shipper, AdminId);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.ActivateRoleAsync(customer.Id, CommunityRoleType.Shipper, AdminId));
    }

    // === T5: ActivateRole_NotEligible_Throws ===
    [Fact(DisplayName = "T5: ActivateRole_NotEligible_Throws")]
    public async Task ActivateRole_NotEligible_Throws()
    {
        var lowPoints = CreateCustomer("Low Points", IdentityLevel.Verified, 500);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.ActivateRoleAsync(lowPoints.Id, CommunityRoleType.Shipper, AdminId));
    }

    // === T6: DeactivateRole_SetsInactive ===
    [Fact(DisplayName = "T6: DeactivateRole_SetsInactive")]
    public async Task DeactivateRole_SetsInactive()
    {
        var customer = CreateCustomer("To Deactivate", IdentityLevel.Verified, 1200);
        var role = await _service.ActivateRoleAsync(customer.Id, CommunityRoleType.Shipper, AdminId);

        await _service.DeactivateRoleAsync(customer.Id, CommunityRoleType.Shipper);

        var fromDb = await _context.CommunityRoles
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(r => r.Id == role.Id);
        Assert.NotNull(fromDb);
        Assert.False(fromDb.IsActive);
        Assert.NotNull(fromDb.DeactivatedAt);
    }

    // === T7: DeactivateRole_NotFound_Throws ===
    [Fact(DisplayName = "T7: DeactivateRole_NotFound_Throws")]
    public async Task DeactivateRole_NotFound_Throws()
    {
        var customer = CreateCustomer("No Role", IdentityLevel.Verified, 1200);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.DeactivateRoleAsync(customer.Id, CommunityRoleType.Shipper));
    }

    // === T8: GetCustomerRoles_ReturnsAll ===
    [Fact(DisplayName = "T8: GetCustomerRoles_ReturnsAll")]
    public async Task GetCustomerRoles_ReturnsAll()
    {
        var customer = CreateCustomer("Multi Role", IdentityLevel.Verified, 1200);

        // Activate Shipper, then deactivate, then activate Salesman
        var shipperRole = await _service.ActivateRoleAsync(customer.Id, CommunityRoleType.Shipper, AdminId);
        await _service.DeactivateRoleAsync(customer.Id, CommunityRoleType.Shipper);
        var salesmanRole = await _service.ActivateRoleAsync(customer.Id, CommunityRoleType.Salesman, AdminId);

        var roles = await _service.GetCustomerRolesAsync(customer.Id);

        Assert.Equal(2, roles.Count);
        Assert.Contains(roles, r => r.Id == shipperRole.Id && !r.IsActive);
        Assert.Contains(roles, r => r.Id == salesmanRole.Id && r.IsActive);
    }
}
