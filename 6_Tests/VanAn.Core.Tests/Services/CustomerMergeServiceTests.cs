using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using VanAn.CoreHub.Infrastructure;
using VanAn.CoreHub.Infrastructure.Repositories;
using VanAn.CoreHub.Services;
using VanAn.Shared.Domain;
using VanAn.Shared.Domain.Aggregates.TenantAggregate;
using Xunit;
using Tenant = VanAn.Shared.Domain.Aggregates.TenantAggregate.Tenant;

namespace VanAn.Core.Tests.Services;

/// <summary>
/// CustomerMergeService tests — TD-CUSTSYNC-001 guest-stub merge.
/// Regression guard for the shared-device bug: merge must only touch IdentityLevel.Guest
/// stubs — real login accounts (Social/Verified) stamped with the same DeviceId must never
/// be soft-deleted or have their loyalty points drained by another account's login.
/// </summary>
public class CustomerMergeServiceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly VanAnDbContext _context;
    private readonly CustomerMergeService _service;
    private static readonly Guid TenantGuid = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private readonly TenantId _tenantId = new(TenantGuid);

    public CustomerMergeServiceTests()
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

        _service = new CustomerMergeService(
            new CustomerRepository(_context),
            new FakeLoyaltyRewardsService(_context),
            _context,
            NullLogger<CustomerMergeService>.Instance);

        SeedTenant();
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    private void SeedTenant()
    {
        _context.Tenants.Add(Tenant.CreateCompany(_tenantId, "Test Tenant", TenantSettings.Empty()));
        _context.SaveChanges();
    }

    private Customer AddRealCustomer(string name, string? email, IdentityLevel level, Guid? deviceId)
    {
        var c = new Customer(_tenantId, name, "0901234567", email);
        c.UpdateCustomerDetails(name, "0901234567", email, "Bronze", deviceId, true);
        c.UpgradeIdentityLevel(level);
        _context.Customers.Add(c);
        _context.SaveChanges();
        return c;
    }

    private Customer AddGuestStub(Guid deviceId, int points)
    {
        var stub = new Customer(_tenantId, "Khách lẻ", "N/A");
        stub.UpdateCustomerDetails("Khách lẻ", "N/A", null, "Bronze", deviceId, true);
        stub.MarkAsGuestStub();
        _context.Customers.Add(stub);
        _context.SaveChanges();

        if (points > 0)
        {
            var rewards = new LoyaltyRewards(_tenantId, stub.Id);
            rewards.AddPoints(points, "guest earn");
            _context.LoyaltyRewards.Add(rewards);
            _context.SaveChanges();
        }
        return stub;
    }

    [Fact(DisplayName = "Guest stub is merged: points transferred, stub soft-deleted, DeviceId linked to login customer")]
    public async Task Merge_GuestStub_TransfersPointsAndSoftDeletes()
    {
        var deviceId = Guid.NewGuid();
        var login = AddRealCustomer("Real User", "real@test.com", IdentityLevel.Social, null);
        var stub = AddGuestStub(deviceId, 50);

        var result = await _service.MergeDeviceStubsIntoLoginAsync(login.Id, deviceId);

        Assert.Equal(1, result.StubsMerged);
        Assert.Equal(50, result.PointsTransferred);

        var stubAfter = await _context.Customers.IgnoreQueryFilters().FirstAsync(c => c.Id == stub.Id);
        Assert.True(stubAfter.IsDeleted);

        var loginAfter = await _context.Customers.IgnoreQueryFilters().FirstAsync(c => c.Id == login.Id);
        Assert.False(loginAfter.IsDeleted);
        Assert.Equal(deviceId, loginAfter.DeviceId);

        var loginRewards = await _context.LoyaltyRewards.IgnoreQueryFilters().FirstAsync(r => r.CustomerId == login.Id);
        Assert.Equal(50, loginRewards.PointBalance);
    }

    [Fact(DisplayName = "Regression: real Social account sharing DeviceId is NOT deleted and NOT drained by another login")]
    public async Task Merge_RealAccountSameDevice_NotTouched()
    {
        var deviceId = Guid.NewGuid();
        // User A previously logged in on this device → merge stamped DeviceId onto their REAL account.
        var userA = AddRealCustomer("User A", "a@test.com", IdentityLevel.Social, deviceId);
        var rewardsA = new LoyaltyRewards(_tenantId, userA.Id);
        rewardsA.AddPoints(500, "A earn");
        _context.LoyaltyRewards.Add(rewardsA);
        // User B logs in on the same shared device.
        var userB = AddRealCustomer("User B", "b@test.com", IdentityLevel.Social, null);
        await _context.SaveChangesAsync();

        var result = await _service.MergeDeviceStubsIntoLoginAsync(userB.Id, deviceId);

        Assert.Equal(0, result.StubsMerged);
        Assert.Equal(0, result.PointsTransferred);

        var userAAfter = await _context.Customers.IgnoreQueryFilters().FirstAsync(c => c.Id == userA.Id);
        Assert.False(userAAfter.IsDeleted);
        Assert.Equal(deviceId, userAAfter.DeviceId);
        Assert.Equal(500, (await _context.LoyaltyRewards.IgnoreQueryFilters().FirstAsync(r => r.CustomerId == userA.Id)).PointBalance);
    }

    [Fact(DisplayName = "Verified account sharing DeviceId is untouched")]
    public async Task Merge_VerifiedAccountSameDevice_NotTouched()
    {
        var deviceId = Guid.NewGuid();
        var verified = AddRealCustomer("OTP User", null, IdentityLevel.Verified, deviceId);
        var login = AddRealCustomer("New Login", "new@test.com", IdentityLevel.Social, null);

        var result = await _service.MergeDeviceStubsIntoLoginAsync(login.Id, deviceId);

        Assert.Equal(0, result.StubsMerged);
        var verifiedAfter = await _context.Customers.IgnoreQueryFilters().FirstAsync(c => c.Id == verified.Id);
        Assert.False(verifiedAfter.IsDeleted);
    }

    [Fact(DisplayName = "No matching stubs → no-op, still links DeviceId to login customer")]
    public async Task Merge_NoStubs_LinksDeviceIdOnly()
    {
        var deviceId = Guid.NewGuid();
        var login = AddRealCustomer("Real User", "real@test.com", IdentityLevel.Social, null);

        var result = await _service.MergeDeviceStubsIntoLoginAsync(login.Id, deviceId);

        Assert.Equal(0, result.StubsMerged);
        Assert.Equal(0, result.PointsTransferred);
        var loginAfter = await _context.Customers.IgnoreQueryFilters().FirstAsync(c => c.Id == login.Id);
        Assert.Equal(deviceId, loginAfter.DeviceId);
    }

    [Fact(DisplayName = "MarkAsGuestStub never downgrades a real (Verified+) account")]
    public void MarkAsGuestStub_DoesNotDowngradeVerified()
    {
        var c = new Customer(_tenantId, "User", "0901");
        c.UpgradeIdentityLevel(IdentityLevel.Verified);
        c.MarkAsGuestStub();
        Assert.Equal(IdentityLevel.Verified, c.IdentityLevel);
    }

    /// <summary>Minimal ILoyaltyRewardsService backed by the same DbContext.</summary>
    private sealed class FakeLoyaltyRewardsService(VanAnDbContext db) : ILoyaltyRewardsService
    {
        public async Task<LoyaltyRewards> GetOrCreateCustomerRewardsAsync(Guid customerId, TenantId tenantId)
        {
            var r = await db.LoyaltyRewards.IgnoreQueryFilters()
                .FirstOrDefaultAsync(x => x.CustomerId == customerId && x.TenantId == tenantId);
            if (r == null)
            {
                r = new LoyaltyRewards(tenantId, customerId);
                db.LoyaltyRewards.Add(r);
                await db.SaveChangesAsync();
            }
            return r;
        }

        public async Task<LoyaltyRewards?> GetCustomerRewardsAsync(Guid customerId)
            => await db.LoyaltyRewards.IgnoreQueryFilters().FirstOrDefaultAsync(x => x.CustomerId == customerId);

        public Task<bool> AddPointsAsync(Guid customerId, Guid tenantId, int points, string reason) => Task.FromResult(true);
        public Task<bool> SubtractPointsAsync(Guid customerId, Guid tenantId, int points, string reason) => Task.FromResult(true);
        public Task<List<LoyaltyRewards>> GetAllRewardsAsync() => Task.FromResult(new List<LoyaltyRewards>());
        public Task<bool> UpdateHistoryAsync(Guid customerId, string historyEntry) => Task.FromResult(true);
        public Task<bool> ActivateCustomerAsync(Guid customerId) => Task.FromResult(true);
    }
}
