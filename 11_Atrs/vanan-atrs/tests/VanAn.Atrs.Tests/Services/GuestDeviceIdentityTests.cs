using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using VanAn.Atrs.Contracts;
using VanAn.Atrs.Domain;
using VanAn.Atrs.Infra;
using Xunit;

namespace VanAn.Atrs.Tests.Services;

public sealed class GuestDeviceIdentityTests : IDisposable
{
    private readonly AtrsDbContext _db;
    private readonly TrustProfileService _service;

    public GuestDeviceIdentityTests()
    {
        var options = new DbContextOptionsBuilder<AtrsDbContext>()
            .UseInMemoryDatabase($"guest-{Guid.NewGuid()}")
            .Options;
        _db = new AtrsDbContext(options);
        _service = new TrustProfileService(_db);
    }

    public void Dispose()
    {
        _db.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task SameDeviceId_ResolvesSameSubject()
    {
        var s1 = await _service.GetOrCreateForDeviceAsync("device-abc-12345", tenantId: null);
        var s2 = await _service.GetOrCreateForDeviceAsync("device-abc-12345", tenantId: null);
        Assert.Equal(s1, s2);
        var link = await _db.TrustSubjectLinks.SingleAsync(l => l.IdentityType == TrustIdentityType.DeviceCluster);
        Assert.Equal(TrustProfileService.StableGuidFrom("device-abc-12345"), link.IdentityId);
        Assert.Equal(1, await _db.TrustSubjects.CountAsync());
    }

    [Fact]
    public async Task DifferentDeviceId_DifferentSubject()
    {
        var s1 = await _service.GetOrCreateForDeviceAsync("device-aaa-11111");
        var s2 = await _service.GetOrCreateForDeviceAsync("device-bbb-22222");
        Assert.NotEqual(s1, s2);
        Assert.Equal(2, await _db.TrustSubjects.CountAsync());
    }

    [Fact]
    public async Task DeviceId_Customer_DoNotMerge()
    {
        var guest = await _service.GetOrCreateForDeviceAsync("device-ccc-33333");
        var customer = await _service.GetOrCreateForCustomerAsync(Guid.NewGuid());
        Assert.NotEqual(guest, customer);
    }

    [Fact]
    public async Task StableGuid_Deterministic()
    {
        Assert.Equal(TrustProfileService.StableGuidFrom("x"), TrustProfileService.StableGuidFrom("x"));
        Assert.NotEqual(TrustProfileService.StableGuidFrom("x"), TrustProfileService.StableGuidFrom("y"));
    }

    [Fact]
    public async Task InvalidDeviceId_Throws()
    {
        await Assert.ThrowsAsync<ArgumentException>(() => _service.GetOrCreateForDeviceAsync("short"));
        await Assert.ThrowsAsync<ArgumentException>(() => _service.GetOrCreateForDeviceAsync(""));
    }

    [Fact]
    public async Task RecoveryDailyCap_LimitsSubmissions()
    {
        var subjectId = Guid.NewGuid();
        var manager = new RecoveryCaseManager(
            _db,
            new StubRecoveryCodeService(),
            new RiskPolicyEngine(),
            new NoopBumpNotifier(),
            new SecurityPolicyService(_db, new MemoryCache(new MemoryCacheOptions()), TimeProvider.System),
            new StubNotificationWriter(),
            new AtrsMetrics(),
            Options.Create(new AtrsRecoveryLimits { MaxPerDay = 2 }));

        for (var i = 0; i < 2; i++)
        {
            var r = await manager.SubmitAsync(subjectId, RecoveryRequestType.LOST_DEVICE, $"idem-cap-{i}");
            Assert.False(r.Existing);
            // Clear active state (anti-duplicate) — reject để request kế không bị trả existing.
            await manager.ManualRejectAsync(r.Request.Id, Guid.NewGuid(), "rv-test-clear-active");
        }
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            manager.SubmitAsync(subjectId, RecoveryRequestType.LOST_DEVICE, "idem-cap-3"));
        Assert.Contains("rate-limited", ex.Message);
    }

    private sealed class StubRecoveryCodeService : IRecoveryCodeService
    {
        public Task<GenerateRecoveryCodesResult> GenerateSetAsync(Guid trustSubjectId, CancellationToken ct = default)
            => Task.FromResult(new GenerateRecoveryCodesResult(Guid.NewGuid(), ["GOOD-CODE"], DateTime.UtcNow));
        public Task<VerifyRecoveryCodeResult> VerifyAndConsumeAsync(Guid trustSubjectId, string code, Guid? deviceId, CancellationToken ct = default)
            => Task.FromResult(new VerifyRecoveryCodeResult(code == "GOOD-CODE", code == "GOOD-CODE" ? "Success" : "InvalidCode"));
        public Task<RecoveryCodesStatusDto> GetStatusAsync(Guid trustSubjectId, CancellationToken ct = default)
            => Task.FromResult(new RecoveryCodesStatusDto(true, 1, DateTime.UtcNow, null));
    }

    private sealed class NoopBumpNotifier : ITokenVersionBumpNotifier
    {
        public Task BumpAsync(Guid userId, string identityType, CancellationToken ct = default) => Task.CompletedTask;
    }
}