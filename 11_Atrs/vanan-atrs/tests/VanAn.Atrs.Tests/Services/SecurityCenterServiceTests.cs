using Microsoft.EntityFrameworkCore;
using VanAn.Atrs.Domain;
using VanAn.Atrs.Infra;
using Xunit;

namespace VanAn.Atrs.Tests.Services;

/// <summary>
/// Security center summary (U06) — status/assurance/counts, không lộ hash/PII.
/// </summary>
public sealed class SecurityCenterServiceTests : IDisposable
{
    private readonly AtrsDbContext _db;
    private readonly SecurityCenterService _service;

    public SecurityCenterServiceTests()
    {
        var options = new DbContextOptionsBuilder<AtrsDbContext>()
            .UseInMemoryDatabase($"sec-{Guid.NewGuid()}")
            .Options;
        _db = new AtrsDbContext(options);
        _service = new SecurityCenterService(_db);
    }

    public void Dispose()
    {
        _db.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task GetSummary_NoProfile_DefaultsToPendingBasic()
    {
        var summary = await _service.GetSummaryAsync(Guid.NewGuid());
        Assert.Equal(TrustProfileStatus.PENDING_SETUP.ToString(), summary.Status);
        Assert.Equal(AssuranceState.BASIC.ToString(), summary.AssuranceState);
        Assert.Equal(0, summary.ActiveDeviceCount);
        Assert.Equal(0, summary.ActivePasskeyCount);
        Assert.False(summary.HasRecoveryCodes);
        Assert.Null(summary.LastSecurityActivityAt);
    }

    [Fact]
    public async Task GetSummary_CountsActiveDevicesCredentialsAndRecovery()
    {
        var subject = Guid.NewGuid();
        _db.AccountTrustProfiles.Add(new AccountTrustProfile(Guid.NewGuid(), subject));
        _db.UserDevices.Add(new UserDevice(Guid.NewGuid(), subject, "dev-1",
            RegistrationMethod.WEBAUTHN, DeviceType.Mobile, "Android 14", "Điện thoại"));
        _db.DeviceCredentials.Add(new DeviceCredential(
            Guid.NewGuid(), subject, "cred-1", "pubkey-1", 0, "timlathay.com", null, null, null, null, null));
        _db.RecoveryCodeSets.Add(new RecoveryCodeSet(Guid.NewGuid(), subject));
        await _db.SaveChangesAsync();

        var summary = await _service.GetSummaryAsync(subject);
        Assert.Equal(1, summary.ActiveDeviceCount);
        Assert.Equal(1, summary.ActivePasskeyCount);
        Assert.True(summary.HasRecoveryCodes);
    }
}