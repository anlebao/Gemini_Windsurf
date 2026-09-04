using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using VanAn.CoreHub.Infrastructure;
using VanAn.CoreHub.Services;
using VanAn.Shared.Domain;
using VanAn.Shared.Domain.Aggregates.TenantAggregate;
using Xunit;
using Tenant = VanAn.Shared.Domain.Aggregates.TenantAggregate.Tenant;

namespace VanAn.Core.Tests.Community;

/// <summary>
/// R2 (2026-09-04): CommunityAdminService tenant-scoped overload tests.
/// Verifies IDOR guard + tenant filtering for Owner (Reseller owner) role management.
/// </summary>
public class CommunityAdminServiceTenantScopedTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly VanAnDbContext _context;
    private readonly CommunityAdminService _service;

    private static readonly Guid TenantA = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private static readonly Guid TenantB = Guid.Parse("00000000-0000-0000-0000-000000000002");
    private static readonly Guid OwnerId = Guid.Parse("00000000-0000-0000-0000-000000000003");

    private readonly TenantId _tenantIdA = new(TenantA);
    private readonly TenantId _tenantIdB = new(TenantB);

    public CommunityAdminServiceTenantScopedTests()
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
        _service = new CommunityAdminService(_context, NullLogger<CommunityAdminService>.Instance);

        SeedTenant(_tenantIdA, "Tenant A");
        SeedTenant(_tenantIdB, "Tenant B");
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    private void SeedTenant(TenantId id, string name)
    {
        var tenant = Tenant.CreateCompany(id, name, TenantSettings.Empty());
        _context.Tenants.Add(tenant);
        _context.SaveChanges();
    }

    private Customer CreateCustomer(TenantId tenantId, string name, IdentityLevel level, int points)
    {
        var customer = new Customer(tenantId, name, "0901234567", "test@test.com");
        var idProp = typeof(Customer).GetProperty("IdentityLevel");
        idProp!.SetValue(customer, level);
        var pointsProp = typeof(Customer).GetProperty("LoyaltyPoints");
        pointsProp!.SetValue(customer, points);
        _context.Customers.Add(customer);
        _context.SaveChanges();
        return customer;
    }

    // === TS1: GetEligibleCustomersForTenantAsync returns only customers of calling tenant ===
    [Fact(DisplayName = "TS1: GetEligibleCustomersForTenantAsync filters by tenantId")]
    public async Task GetEligibleCustomersForTenant_FiltersByTenantId()
    {
        var customerA = CreateCustomer(_tenantIdA, "Customer A", IdentityLevel.Verified, 1500);
        var customerB = CreateCustomer(_tenantIdB, "Customer B", IdentityLevel.Verified, 1500);

        var resultA = await _service.GetEligibleCustomersForTenantAsync(TenantA, 1, 20);
        var resultB = await _service.GetEligibleCustomersForTenantAsync(TenantB, 1, 20);

        Assert.Single(resultA.Items);
        Assert.Equal(customerA.Id, resultA.Items[0].CustomerId);
        Assert.Single(resultB.Items);
        Assert.Equal(customerB.Id, resultB.Items[0].CustomerId);
    }

    // === TS2: GetEligibleCustomersForTenantAsync applies eligibility criteria ===
    [Fact(DisplayName = "TS2: GetEligibleCustomersForTenantAsync applies eligibility criteria")]
    public async Task GetEligibleCustomersForTenant_AppliesEligibility()
    {
        // Eligible: Verified + 1500 points
        CreateCustomer(_tenantIdA, "Eligible A", IdentityLevel.Verified, 1500);
        // Not eligible: low points
        CreateCustomer(_tenantIdA, "Low Points A", IdentityLevel.Verified, 500);
        // Not eligible: not verified
        CreateCustomer(_tenantIdA, "Not Verified A", IdentityLevel.Guest, 1500);

        var result = await _service.GetEligibleCustomersForTenantAsync(TenantA, 1, 20);

        Assert.Single(result.Items);
        Assert.Equal("Eligible A", result.Items[0].FullName);
    }

    // === TS3: ActivateRoleForTenantAsync IDOR guard — customer of different tenant rejected ===
    [Fact(DisplayName = "TS3: ActivateRoleForTenantAsync IDOR guard rejects cross-tenant")]
    public async Task ActivateRoleForTenant_IDOR_RejectsCrossTenant()
    {
        var customerB = CreateCustomer(_tenantIdB, "Customer B", IdentityLevel.Verified, 1500);

        // Tenant A's owner tries to activate role for Tenant B's customer
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            _service.ActivateRoleForTenantAsync(TenantA, customerB.Id, CommunityRoleType.Shipper, OwnerId));
    }

    // === TS4: ActivateRoleForTenantAsync succeeds for same-tenant customer ===
    [Fact(DisplayName = "TS4: ActivateRoleForTenantAsync succeeds for same-tenant")]
    public async Task ActivateRoleForTenant_SucceedsForSameTenant()
    {
        var customerA = CreateCustomer(_tenantIdA, "Customer A", IdentityLevel.Verified, 1500);

        var role = await _service.ActivateRoleForTenantAsync(TenantA, customerA.Id, CommunityRoleType.Shipper, OwnerId);

        Assert.Equal(customerA.Id, role.CustomerId);
        Assert.Equal(CommunityRoleType.Shipper, role.RoleType);
        Assert.True(role.IsActive);
        Assert.Equal(OwnerId, role.ActivatedBy);
        // CommunityRole.TenantId == customerA.TenantId == TenantA
        Assert.Equal(_tenantIdA, role.TenantId);
    }

    // === TS5: ActivateRoleForTenantAsync Salesman generates SalesmanCode ===
    [Fact(DisplayName = "TS5: ActivateRoleForTenantAsync Salesman generates SalesmanCode")]
    public async Task ActivateRoleForTenant_Salesman_GeneratesCode()
    {
        var customerA = CreateCustomer(_tenantIdA, "Customer A", IdentityLevel.Verified, 1500);

        var role = await _service.ActivateRoleForTenantAsync(TenantA, customerA.Id, CommunityRoleType.Salesman, OwnerId);

        Assert.Equal(CommunityRoleType.Salesman, role.RoleType);
        Assert.NotNull(role.SalesmanCode);
        Assert.Equal(6, role.SalesmanCode!.Length);
    }

    // === TS6: ActivateRoleForTenantAsync duplicate active role rejected ===
    [Fact(DisplayName = "TS6: ActivateRoleForTenantAsync duplicate active role rejected")]
    public async Task ActivateRoleForTenant_Duplicate_Rejected()
    {
        var customerA = CreateCustomer(_tenantIdA, "Customer A", IdentityLevel.Verified, 1500);

        await _service.ActivateRoleForTenantAsync(TenantA, customerA.Id, CommunityRoleType.Shipper, OwnerId);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.ActivateRoleForTenantAsync(TenantA, customerA.Id, CommunityRoleType.Shipper, OwnerId));
    }

    // === TS7: DeactivateRoleForTenantAsync IDOR guard ===
    [Fact(DisplayName = "TS7: DeactivateRoleForTenantAsync IDOR guard rejects cross-tenant")]
    public async Task DeactivateRoleForTenant_IDOR_RejectsCrossTenant()
    {
        var customerA = CreateCustomer(_tenantIdA, "Customer A", IdentityLevel.Verified, 1500);
        await _service.ActivateRoleForTenantAsync(TenantA, customerA.Id, CommunityRoleType.Shipper, OwnerId);

        // Tenant B owner tries to deactivate role that belongs to Tenant A
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            _service.DeactivateRoleForTenantAsync(TenantB, customerA.Id, CommunityRoleType.Shipper));
    }

    // === TS8: DeactivateRoleForTenantAsync succeeds for same-tenant ===
    [Fact(DisplayName = "TS8: DeactivateRoleForTenantAsync succeeds for same-tenant")]
    public async Task DeactivateRoleForTenant_SucceedsForSameTenant()
    {
        var customerA = CreateCustomer(_tenantIdA, "Customer A", IdentityLevel.Verified, 1500);
        await _service.ActivateRoleForTenantAsync(TenantA, customerA.Id, CommunityRoleType.Shipper, OwnerId);

        await _service.DeactivateRoleForTenantAsync(TenantA, customerA.Id, CommunityRoleType.Shipper);

        // Verify deactivated in DB
        var role = await _context.CommunityRoles
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(r => r.CustomerId == customerA.Id && r.RoleType == CommunityRoleType.Shipper);
        Assert.NotNull(role);
        Assert.False(role!.IsActive);
    }

    // === TS9: GetCustomerRolesForTenantAsync IDOR guard ===
    [Fact(DisplayName = "TS9: GetCustomerRolesForTenantAsync IDOR guard rejects cross-tenant")]
    public async Task GetCustomerRolesForTenant_IDOR_RejectsCrossTenant()
    {
        var customerA = CreateCustomer(_tenantIdA, "Customer A", IdentityLevel.Verified, 1500);

        // Tenant B owner tries to read Tenant A customer's roles
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            _service.GetCustomerRolesForTenantAsync(TenantB, customerA.Id));
    }

    // === TS10: GetCustomerRolesForTenantAsync succeeds for same-tenant ===
    [Fact(DisplayName = "TS10: GetCustomerRolesForTenantAsync succeeds for same-tenant")]
    public async Task GetCustomerRolesForTenant_SucceedsForSameTenant()
    {
        var customerA = CreateCustomer(_tenantIdA, "Customer A", IdentityLevel.Verified, 1500);
        await _service.ActivateRoleForTenantAsync(TenantA, customerA.Id, CommunityRoleType.Shipper, OwnerId);

        var roles = await _service.GetCustomerRolesForTenantAsync(TenantA, customerA.Id);

        Assert.Single(roles);
        Assert.Equal(CommunityRoleType.Shipper, roles[0].RoleType);
    }

    // === TS11: ActivateRoleForTenantAsync eligibility criteria enforced ===
    [Fact(DisplayName = "TS11: ActivateRoleForTenantAsync enforces eligibility criteria")]
    public async Task ActivateRoleForTenant_EnforcesEligibility()
    {
        // Low points — not eligible
        var lowPointsCustomer = CreateCustomer(_tenantIdA, "Low Points", IdentityLevel.Verified, 500);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.ActivateRoleForTenantAsync(TenantA, lowPointsCustomer.Id, CommunityRoleType.Shipper, OwnerId));
    }

    // === TS12: ActivateRoleForTenantAsync non-existent customer throws InvalidOperationException ===
    [Fact(DisplayName = "TS12: ActivateRoleForTenantAsync non-existent customer throws")]
    public async Task ActivateRoleForTenant_NonExistentCustomer_Throws()
    {
        var randomId = Guid.NewGuid();
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.ActivateRoleForTenantAsync(TenantA, randomId, CommunityRoleType.Shipper, OwnerId));
    }
}
