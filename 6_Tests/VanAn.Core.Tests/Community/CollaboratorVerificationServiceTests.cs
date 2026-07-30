using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using VanAn.CoreHub.Infrastructure;
using VanAn.CoreHub.Services;
using VanAn.Shared.Domain;
using VanAn.Shared.Domain.Aggregates.SystemSettingAggregate;
using VanAn.Shared.Domain.Aggregates.TenantAggregate;
using VanAn.Shared.Domain.Common;
using Tenant = VanAn.Shared.Domain.Aggregates.TenantAggregate.Tenant;
using Xunit;

namespace VanAn.Core.Tests.Community;

/// <summary>
/// CC-S6-T5: CollaboratorVerificationService unit tests.
/// Tests: settings CRUD, toggle gating, deposit balance check, OTP send+cache, OTP verify,
/// retry limit, deposit, IsVerificationRequired logic.
/// Uses SQLite in-memory + Mock ISmsService + real WalletService + real IMemoryCache.
/// </summary>
public class CollaboratorVerificationServiceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly VanAnDbContext _context;
    private readonly WalletService _walletService;
    private readonly Mock<ISmsService> _smsServiceMock;
    private readonly IMemoryCache _cache;
    private readonly CollaboratorVerificationService _service;
    private static readonly Guid TenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private static readonly Guid CustomerId = Guid.NewGuid();

    public CollaboratorVerificationServiceTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<VanAnDbContext>()
            .UseSqlite(_connection)
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        _context = new VanAnDbContext(options);
        _context.Database.EnsureCreated();

        var tenantProvider = new StubTenantProvider(TenantId);
        _walletService = new WalletService(_context, tenantProvider, NullLogger<WalletService>.Instance);
        _smsServiceMock = new Mock<ISmsService>();
        _cache = new MemoryCache(new MemoryCacheOptions());

        _service = new CollaboratorVerificationService(
            _context,
            _smsServiceMock.Object,
            _walletService,
            _cache,
            NullLogger<CollaboratorVerificationService>.Instance);
    }

    public void Dispose()
    {
        _cache.Dispose();
        _context.Dispose();
        _connection.Dispose();
    }

    private async Task SeedTenantAsync()
    {
        var tenant = Tenant.CreateCompany(new TenantId(TenantId), "Shop A",
            TenantSettings.Empty().WithCoordinates(10.8, 106.7));
        _context.Tenants.Add(tenant);
        await _context.SaveChangesAsync();
    }

    private async Task SeedCommunityRoleAsync()
    {
        var role = new CommunityRole(new TenantId(TenantId), CustomerId, CommunityRoleType.Salesman, Guid.NewGuid());
        _context.CommunityRoles.Add(role);
        await _context.SaveChangesAsync();
    }

    private async Task EnableToggleAsync()
    {
        await _service.SetSettingsAsync(true, 200m, 10000m, Guid.NewGuid());
    }

    private async Task DepositAsync(decimal amount)
    {
        await _service.DepositAsync(CustomerId, amount);
    }

    // === Settings tests ===

    [Fact]
    public async Task GetSettings_Defaults_WhenNoSettingRow()
    {
        var settings = await _service.GetSettingsAsync();
        Assert.False(settings.Enabled);
        Assert.Equal(200m, settings.FeePerVerification);
        Assert.Equal(10000m, settings.MinDeposit);
    }

    [Fact]
    public async Task SetSettings_PersistsAllThreeKeys()
    {
        await _service.SetSettingsAsync(true, 500m, 20000m, Guid.NewGuid());

        var settings = await _service.GetSettingsAsync();
        Assert.True(settings.Enabled);
        Assert.Equal(500m, settings.FeePerVerification);
        Assert.Equal(20000m, settings.MinDeposit);
    }

    [Fact]
    public async Task SetSettings_ThrowsWhenFeeNegative()
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.SetSettingsAsync(true, -1m, 10000m, Guid.NewGuid()));
    }

    [Fact]
    public async Task SetSettings_ThrowsWhenMinDepositNegative()
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.SetSettingsAsync(true, 200m, -1m, Guid.NewGuid()));
    }

    // === InitVerification tests ===

    [Fact]
    public async Task InitVerification_ThrowsWhenToggleOff()
    {
        // Toggle is OFF by default
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.InitVerificationAsync(CustomerId, "0912345678"));
    }

    [Fact]
    public async Task InitVerification_ThrowsWhenInsufficientBalance()
    {
        await SeedTenantAsync();
        await EnableToggleAsync();
        // No deposit → balance = 0 < 200

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.InitVerificationAsync(CustomerId, "0912345678"));
        Assert.Contains("Số dư ví không đủ", ex.Message);
    }

    [Fact]
    public async Task InitVerification_SendsSms_AndDeductsFee_AndCachesOtp()
    {
        await SeedTenantAsync();
        await EnableToggleAsync();
        await DepositAsync(50000m); // plenty of balance

        _smsServiceMock.Setup(s => s.SendSmsAsync(It.IsAny<string>(), It.IsAny<string>(), default))
            .ReturnsAsync(true);

        var result = await _service.InitVerificationAsync(CustomerId, "0912345678");

        Assert.Contains("Ma OTP", result.Message);
        Assert.Equal(200m, result.FeeDeducted);
        Assert.Equal(49800m, result.BalanceAfter);

        _smsServiceMock.Verify(s => s.SendSmsAsync("0912345678", It.IsAny<string>(), default), Times.Once);

        // OTP should be cached
        Assert.True(_cache.TryGetValue("Otp_" + CustomerId, out _));
    }

    [Fact]
    public async Task InitVerification_ThrowsWhenSmsSendFails()
    {
        await SeedTenantAsync();
        await EnableToggleAsync();
        await DepositAsync(50000m);

        _smsServiceMock.Setup(s => s.SendSmsAsync(It.IsAny<string>(), It.IsAny<string>(), default))
            .ReturnsAsync(false);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.InitVerificationAsync(CustomerId, "0912345678"));
    }

    [Fact]
    public async Task InitVerification_ThrowsWhenEmptyPhoneNumber()
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.InitVerificationAsync(CustomerId, ""));
    }

    // === Retry limit tests ===

    [Fact]
    public async Task InitVerification_ThrowsAfterMaxRetriesPerDay()
    {
        await SeedTenantAsync();
        await EnableToggleAsync();
        await DepositAsync(100000m); // enough for 3+ OTPs

        _smsServiceMock.Setup(s => s.SendSmsAsync(It.IsAny<string>(), It.IsAny<string>(), default))
            .ReturnsAsync(true);

        // Send 3 OTPs (max allowed)
        for (int i = 0; i < 3; i++)
        {
            await _service.InitVerificationAsync(CustomerId, "0912345678");
        }

        // 4th attempt should fail
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.InitVerificationAsync(CustomerId, "0912345678"));
        Assert.Contains("tối đa", ex.Message);
    }

    // === VerifyOtp tests ===

    [Fact]
    public async Task VerifyOtp_ThrowsWhenOtpNotFound()
    {
        // No OTP in cache (not initiated)
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.VerifyOtpAsync(CustomerId, "123456"));
    }

    [Fact]
    public async Task InitAndVerifyOtp_Success_MarksPhoneVerified()
    {
        await SeedTenantAsync();
        await SeedCommunityRoleAsync();
        await EnableToggleAsync();
        await DepositAsync(50000m);

        _smsServiceMock.Setup(s => s.SendSmsAsync(It.IsAny<string>(), It.IsAny<string>(), default))
            .ReturnsAsync(true);

        // Init
        await _service.InitVerificationAsync(CustomerId, "0912345678");

        // Extract OTP from cache
        var otp = _cache.Get<string>("Otp_" + CustomerId);
        Assert.NotNull(otp);

        // Verify
        await _service.VerifyOtpAsync(CustomerId, otp!);

        // Check role was updated
        var role = await _context.CommunityRoles
            .IgnoreQueryFilters()
            .FirstAsync(r => r.CustomerId == CustomerId);
        Assert.True(role.IsPhoneVerified);
        Assert.NotNull(role.PhoneVerifiedAt);

        // OTP should be removed from cache
        Assert.False(_cache.TryGetValue("Otp_" + CustomerId, out _));
    }

    [Fact]
    public async Task VerifyOtp_ThrowsWhenOtpMismatch()
    {
        await SeedTenantAsync();
        await SeedCommunityRoleAsync();
        await EnableToggleAsync();
        await DepositAsync(50000m);

        _smsServiceMock.Setup(s => s.SendSmsAsync(It.IsAny<string>(), It.IsAny<string>(), default))
            .ReturnsAsync(true);

        await _service.InitVerificationAsync(CustomerId, "0912345678");

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.VerifyOtpAsync(CustomerId, "000000"));
    }

    [Fact]
    public async Task VerifyOtp_ThrowsWhenNoActiveRole()
    {
        await SeedTenantAsync();
        await EnableToggleAsync();
        await DepositAsync(50000m);

        _smsServiceMock.Setup(s => s.SendSmsAsync(It.IsAny<string>(), It.IsAny<string>(), default))
            .ReturnsAsync(true);

        await _service.InitVerificationAsync(CustomerId, "0912345678");
        var otp = _cache.Get<string>("Otp_" + CustomerId)!;

        // No CommunityRole seeded
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.VerifyOtpAsync(CustomerId, otp));
    }

    [Fact]
    public async Task VerifyOtp_ThrowsWhenEmptyCode()
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.VerifyOtpAsync(CustomerId, ""));
    }

    // === Deposit tests ===

    [Fact]
    public async Task Deposit_PositiveAmount_CreatesTransaction()
    {
        await SeedTenantAsync();
        await _service.DepositAsync(CustomerId, 50000m);

        var balance = await _walletService.GetBalanceAsync(CustomerId);
        Assert.Equal(50000m, balance);
    }

    [Fact]
    public async Task Deposit_ThrowsWhenZeroOrNegative()
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.DepositAsync(CustomerId, 0m));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.DepositAsync(CustomerId, -100m));
    }

    // === IsVerificationRequired tests ===

    [Fact]
    public async Task IsVerificationRequired_FalseWhenToggleOff()
    {
        await SeedTenantAsync();
        await SeedCommunityRoleAsync();

        // Toggle OFF by default
        var required = await _service.IsVerificationRequiredAsync(CustomerId);
        Assert.False(required);
    }

    [Fact]
    public async Task IsVerificationRequired_FalseWhenNotCollaborator()
    {
        await SeedTenantAsync();
        await EnableToggleAsync();

        // No CommunityRole
        var required = await _service.IsVerificationRequiredAsync(CustomerId);
        Assert.False(required);
    }

    [Fact]
    public async Task IsVerificationRequired_TrueWhenToggleOn_AndNotVerified()
    {
        await SeedTenantAsync();
        await SeedCommunityRoleAsync();
        await EnableToggleAsync();

        var required = await _service.IsVerificationRequiredAsync(CustomerId);
        Assert.True(required);
    }

    [Fact]
    public async Task IsVerificationRequired_FalseWhenAlreadyVerified()
    {
        await SeedTenantAsync();
        await SeedCommunityRoleAsync();
        await EnableToggleAsync();

        // Manually mark as verified
        var role = await _context.CommunityRoles
            .IgnoreQueryFilters()
            .FirstAsync(r => r.CustomerId == CustomerId);
        role.MarkPhoneVerified();
        await _context.SaveChangesAsync();

        var required = await _service.IsVerificationRequiredAsync(CustomerId);
        Assert.False(required);
    }

    private sealed class StubTenantProvider : ITenantProvider
    {
        public StubTenantProvider(Guid tenantId) => TenantId = tenantId;
        public Guid TenantId { get; }
        public string? CurrentUser => "test";
        public bool HasTenant => true;
        public void SetTenant(Guid tenantId) { }
        public void ClearTenant() { }
    }
}
