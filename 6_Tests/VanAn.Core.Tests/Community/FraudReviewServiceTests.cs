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
/// CC-S6 (Sprint 6 v1.2): FraudReviewService unit tests — 6 test cases (T9-T14).
/// Get pending (sort), confirm (reject + reversal + 3-strike), dismiss (whitelist), my-flags.
/// </summary>
public class FraudReviewServiceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly VanAnDbContext _context;
    private readonly FraudReviewService _service;
    private readonly WalletService _walletService;
    private readonly StubTenantProvider _tenantProvider;
    private static readonly Guid TenantGuid = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private static readonly Guid AdminId = Guid.Parse("00000000-0000-0000-0000-000000000002");
    private static readonly Guid SalesmanId = Guid.Parse("00000000-0000-0000-0000-000000000003");
    private static readonly Guid ProductId = Guid.Parse("00000000-0000-0000-0000-000000000004");
    private readonly TenantId _tenantId = new(TenantGuid);

    public FraudReviewServiceTests()
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
        _tenantProvider = new StubTenantProvider(TenantGuid);
        _walletService = new WalletService(_context, _tenantProvider, NullLogger<WalletService>.Instance);
        _service = new FraudReviewService(_context, _walletService, NullLogger<FraudReviewService>.Instance);

        SeedTenant();
        SeedCustomer();
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

    private void SeedCustomer()
    {
        var customer = new Customer(_tenantId, "Flagged Customer", "0901234567", "cust@test.com");
        typeof(Customer).GetProperty("IdentityLevel")!.SetValue(customer, IdentityLevel.Verified);
        typeof(Customer).GetProperty("LoyaltyPoints")!.SetValue(customer, 1500);
        _context.Customers.Add(customer);
        _context.SaveChanges();
    }

    private Customer GetCustomer() => _context.Customers.IgnoreQueryFilters().First();

    private FraudFlag CreateFlag(int riskScore, Guid? customerId = null, FraudEntityType entityType = FraudEntityType.SalesReferral)
    {
        var flag = new FraudFlag(
            _tenantId,
            entityType,
            Guid.NewGuid(),
            customerId ?? GetCustomer().Id,
            FraudFlagType.HighRiskScore,
            riskScore,
            "{\"sameIp\":true}",
            "High risk score detected");
        _context.FraudFlags.Add(flag);
        _context.SaveChanges();
        return flag;
    }

    private SalesReferral CreateSalesReferral(CommissionStatus status = CommissionStatus.Pending)
    {
        var referral = new SalesReferral(_tenantId, SalesmanId, "ABC123", ProductId, "TR-001");
        referral.AttachToOrder(Guid.NewGuid(), GetCustomer().Id, 100000, 0.05m);
        referral.SetRiskScore(70, "{}");

        // Override status if needed
        if (status == CommissionStatus.Paid)
        {
            typeof(SalesReferral).GetProperty("CommissionStatus")!.SetValue(referral, CommissionStatus.Paid);
        }

        _context.SalesReferrals.Add(referral);
        _context.SaveChanges();
        return referral;
    }

    private DeviceRegistration CreateDevice()
    {
        var device = new DeviceRegistration(
            _tenantId,
            GetCustomer().Id,
            "fake-token-64chars-aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
            "fake-fingerprint-hash-64chars-aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
            "{}",
            "TestAgent",
            "Win32",
            "127.0.0.1");
        _context.DeviceRegistrations.Add(device);
        _context.SaveChanges();
        return device;
    }

    // === T9: FraudReview_GetPending_SortsByRiskScoreDesc ===
    [Fact(DisplayName = "T9: FraudReview_GetPending_SortsByRiskScoreDesc")]
    public async Task GetPending_SortsByRiskScoreDesc()
    {
        CreateFlag(45);
        CreateFlag(85);
        CreateFlag(60);

        var result = await _service.GetFlagsAsync("Pending", 1, 20);

        Assert.Equal(3, result.Items.Count);
        Assert.Equal(85, result.Items[0].RiskScore);
        Assert.Equal(60, result.Items[1].RiskScore);
        Assert.Equal(45, result.Items[2].RiskScore);
    }

    // === T10: FraudReview_Confirm_SetsStatusAndRejectsEntity ===
    [Fact(DisplayName = "T10: FraudReview_Confirm_SetsStatusAndRejectsEntity")]
    public async Task Confirm_SetsStatusAndRejectsEntity()
    {
        var referral = CreateSalesReferral();
        var flag = new FraudFlag(_tenantId, FraudEntityType.SalesReferral, referral.Id,
            GetCustomer().Id, FraudFlagType.HighRiskScore, 75, "{}", "High risk");
        _context.FraudFlags.Add(flag);
        _context.SaveChanges();

        var result = await _service.ConfirmAsync(flag.Id, AdminId);

        Assert.Equal("Confirmed", result.Status);
        Assert.Contains(result.SideEffects, s => s.Contains("Rejected"));

        var flagFromDb = await _context.FraudFlags.IgnoreQueryFilters().FirstAsync(f => f.Id == flag.Id);
        Assert.Equal(FraudFlagStatus.Confirmed, flagFromDb.Status);

        var referralFromDb = await _context.SalesReferrals.IgnoreQueryFilters().FirstAsync(r => r.Id == referral.Id);
        Assert.Equal(CommissionStatus.Rejected, referralFromDb.CommissionStatus);
    }

    // === T11: FraudReview_Confirm_CreatesWalletReversalIfPaid ===
    [Fact(DisplayName = "T11: FraudReview_Confirm_CreatesWalletReversalIfPaid")]
    public async Task Confirm_CreatesWalletReversalIfPaid()
    {
        var referral = CreateSalesReferral(CommissionStatus.Paid);
        // Create a commission wallet transaction for this referral
        var commissionTx = await _walletService.CreateTransactionAsync(
            SalesmanId, WalletTransactionType.Commission, 5000,
            "Commission for order", referral.OrderId);
        // Link the transaction to the referral
        typeof(SalesReferral).GetProperty("CommissionStatus")!.SetValue(referral, CommissionStatus.Paid);

        var flag = new FraudFlag(_tenantId, FraudEntityType.SalesReferral, referral.Id,
            GetCustomer().Id, FraudFlagType.HighRiskScore, 75, "{}", "High risk");
        _context.FraudFlags.Add(flag);
        _context.SaveChanges();

        var result = await _service.ConfirmAsync(flag.Id, AdminId);

        Assert.Contains(result.SideEffects, s => s.Contains("WalletReversal"));

        // Verify reversal transaction created
        var reversalTx = await _context.WalletTransactions
            .IgnoreQueryFilters()
            .Where(w => w.Type == WalletTransactionType.Reversal && w.RelatedTransactionId == commissionTx.Id)
            .FirstOrDefaultAsync();
        Assert.NotNull(reversalTx);
    }

    // === T12: FraudReview_Confirm_ThreeStrikes_AutoBans ===
    [Fact(DisplayName = "T12: FraudReview_Confirm_ThreeStrikes_AutoBans")]
    public async Task Confirm_ThreeStrikes_AutoBans()
    {
        var customer = GetCustomer();
        // Create 2 already-confirmed flags
        var flag1 = CreateFlag(70, customer.Id);
        flag1.Confirm(AdminId, "First strike");
        var flag2 = CreateFlag(75, customer.Id);
        flag2.Confirm(AdminId, "Second strike");
        _context.SaveChanges();

        // Create 3rd pending flag
        var flag3 = CreateFlag(80, customer.Id);

        var result = await _service.ConfirmAsync(flag3.Id, AdminId);

        Assert.True(result.CustomerBanned);
        Assert.Contains(result.SideEffects, s => s.Contains("Banned"));

        var customerFromDb = await _context.Customers.IgnoreQueryFilters().FirstAsync(c => c.Id == customer.Id);
        Assert.False(customerFromDb.IsActive);
    }

    // === T13: FraudReview_Dismiss_WhitelistsDevice_NoStrike ===
    [Fact(DisplayName = "T13: FraudReview_Dismiss_WhitelistsDevice_NoStrike")]
    public async Task Dismiss_WhitelistsDevice_NoStrike()
    {
        var device = CreateDevice();
        var flag = new FraudFlag(_tenantId, FraudEntityType.DeviceRegistration, device.Id,
            GetCustomer().Id, FraudFlagType.HighRiskScore, 65, "{}", "Suspicious device");
        _context.FraudFlags.Add(flag);
        _context.SaveChanges();

        var result = await _service.DismissAsync(flag.Id, AdminId);

        Assert.Equal("Dismissed", result.Status);
        Assert.Contains(result.SideEffects, s => s.Contains("IsVerified=true"));

        var deviceFromDb = await _context.DeviceRegistrations.IgnoreQueryFilters().FirstAsync(d => d.Id == device.Id);
        Assert.True(deviceFromDb.IsVerified);

        // Customer should NOT be banned (dismiss = no strike)
        var customer = await _context.Customers.IgnoreQueryFilters().FirstAsync(c => c.Id == GetCustomer().Id);
        Assert.True(customer.IsActive);
    }

    // === T14: FraudReview_GetMyFlags_ReturnsOwnOnly ===
    [Fact(DisplayName = "T14: FraudReview_GetMyFlags_ReturnsOwnOnly")]
    public async Task GetMyFlags_ReturnsOwnOnly()
    {
        var customer = GetCustomer();
        // Create another customer
        var otherCustomer = new Customer(_tenantId, "Other Customer", "0987654321", "other@test.com");
        typeof(Customer).GetProperty("IdentityLevel")!.SetValue(otherCustomer, IdentityLevel.Verified);
        _context.Customers.Add(otherCustomer);
        _context.SaveChanges();

        // Create flags for both customers
        CreateFlag(50, customer.Id);
        CreateFlag(70, otherCustomer.Id);
        CreateFlag(80, customer.Id);

        var myFlags = await _service.GetMyFlagsAsync(customer.Id);

        Assert.Equal(2, myFlags.Count);
        Assert.All(myFlags, f => Assert.Equal(customer.Id, f.CustomerId));
        Assert.DoesNotContain(myFlags, f => f.CustomerId == otherCustomer.Id);
    }

    private sealed class StubTenantProvider : ITenantProvider
    {
        public StubTenantProvider(Guid tenantId) => TenantId = tenantId;
        public Guid TenantId { get; }
        public string? CurrentUser => "test";
        public bool HasTenant => true;
        public void SetTenant(Guid tenantId) { }
    }
}
