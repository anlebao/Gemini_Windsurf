using Microsoft.EntityFrameworkCore;
using VanAn.Atrs.Domain;
using VanAn.Atrs.Infra;
using Xunit;

namespace VanAn.Atrs.Tests.Services;

/// <summary>
/// Device registry (SRS §7.2, FR-003) — list/rename/revoke/mark-lost/ensure + credential link.
/// Revoke device → toàn bộ credential của device bị REVOKED (U08).
/// </summary>
public sealed class DeviceServiceTests : IDisposable
{
    private readonly AtrsDbContext _db;
    private readonly DeviceService _service;

    public DeviceServiceTests()
    {
        var options = new DbContextOptionsBuilder<AtrsDbContext>()
            .UseInMemoryDatabase($"devices-{Guid.NewGuid()}")
            .Options;
        _db = new AtrsDbContext(options);
        _service = new DeviceService(_db);
    }

    public void Dispose()
    {
        _db.Dispose();
        GC.SuppressFinalize(this);
    }

    private async Task<Guid> SeedDeviceAsync(Guid subject, string deviceId = "device-a")
        => await _service.EnsureDeviceAsync(subject, deviceId, RegistrationMethod.WEBAUTHN, DeviceType.Mobile, "Android 14");

    [Fact]
    public async Task EnsureDevice_CreatesOnce_ReusesOnSecondCall()
    {
        var subject = Guid.NewGuid();
        var first = await SeedDeviceAsync(subject);
        var second = await SeedDeviceAsync(subject);

        Assert.Equal(first, second);
        Assert.Single(await _db.UserDevices.Where(d => d.TrustSubjectId == subject).ToListAsync());
    }

    [Fact]
    public async Task EnsureDevice_TouchesLastSeen()
    {
        var subject = Guid.NewGuid();
        var id = await SeedDeviceAsync(subject);
        var device = await _db.UserDevices.SingleAsync(d => d.Id == id);
        var firstSeen = device.FirstSeenAt;

        await Task.Delay(20);
        await _service.EnsureDeviceAsync(subject, "device-a", RegistrationMethod.WEBAUTHN, DeviceType.Mobile, "Android 14");

        var after = await _db.UserDevices.SingleAsync(d => d.Id == id);
        Assert.Equal(firstSeen, after.FirstSeenAt);
        Assert.True(after.LastSeenAt >= firstSeen);
    }

    [Fact]
    public async Task Rename_UpdatesFriendlyName()
    {
        var subject = Guid.NewGuid();
        var id = await SeedDeviceAsync(subject);

        var dto = await _service.RenameAsync(subject, id, "Điện thoại chính");

        Assert.NotNull(dto);
        Assert.Equal("Điện thoại chính", dto!.FriendlyName);
    }

    [Fact]
    public async Task Rename_DeviceOfOtherSubject_ReturnsNull()
    {
        var id = await SeedDeviceAsync(Guid.NewGuid());
        var dto = await _service.RenameAsync(Guid.NewGuid(), id, "X");
        Assert.Null(dto);
    }

    [Fact]
    public async Task Revoke_DeviceAndCredentials_AllRevoked()
    {
        var subject = Guid.NewGuid();
        var deviceId = await SeedDeviceAsync(subject);
        _db.DeviceCredentials.Add(new DeviceCredential(
            Guid.NewGuid(), subject, "cred-1", "pubkey-1", 0, "timlathay.com", deviceId, null, null, null, null));
        _db.DeviceCredentials.Add(new DeviceCredential(
            Guid.NewGuid(), subject, "cred-2", "pubkey-2", 0, "timlathay.com", deviceId, null, null, null, null));
        await _db.SaveChangesAsync();

        var ok = await _service.RevokeAsync(subject, deviceId, "USER_REQUESTED");

        Assert.True(ok);
        var device = await _db.UserDevices.SingleAsync(d => d.Id == deviceId);
        Assert.Equal(DeviceStatus.REVOKED, device.Status);
        Assert.NotNull(device.RevokedAt);
        Assert.Equal("USER_REQUESTED", device.RevokedReason);
        var creds = await _db.DeviceCredentials.Where(c => c.DeviceRecordId == deviceId).ToListAsync();
        Assert.All(creds, c => Assert.Equal(CredentialStatus.REVOKED, c.Status));
    }

    [Fact]
    public async Task Revoke_DeviceOfOtherSubject_ReturnsFalse()
    {
        var id = await SeedDeviceAsync(Guid.NewGuid());
        Assert.False(await _service.RevokeAsync(Guid.NewGuid(), id, null));
    }

    [Fact]
    public async Task MarkLost_SetsLostStatus()
    {
        var subject = Guid.NewGuid();
        var id = await SeedDeviceAsync(subject);
        Assert.True(await _service.MarkLostAsync(subject, id));
        var device = await _db.UserDevices.SingleAsync(d => d.Id == id);
        Assert.Equal(DeviceStatus.LOST, device.Status);
    }

    [Fact]
    public async Task List_ReturnsDevicesWithCredentialCount()
    {
        var subject = Guid.NewGuid();
        var deviceId = await SeedDeviceAsync(subject);
        var device = await _db.UserDevices.SingleAsync(d => d.Id == deviceId);
        device.Activate();
        _db.DeviceCredentials.Add(new DeviceCredential(
            Guid.NewGuid(), subject, "cred-1", "pubkey-1", 0, "timlathay.com", deviceId, null, null, null, null));
        await _db.SaveChangesAsync();

        var list = await _service.ListAsync(subject);
        var dto = Assert.Single(list);
        Assert.Equal(1, dto.ActiveCredentialCount);
        Assert.Equal("ACTIVE", dto.Status);
    }

    [Fact]
    public async Task List_DoesNotThrow_OnOrphanCredentialWithoutDevice()
    {
        // RV finding 2026-11-16: credential với DeviceRecordId=null (passkey không gắn
        // device) làm GroupBy key null → ToDictionaryAsync ArgumentNullException → 500.
        var subject = Guid.NewGuid();
        var deviceId = await SeedDeviceAsync(subject);
        var device = await _db.UserDevices.SingleAsync(d => d.Id == deviceId);
        device.Activate();
        _db.DeviceCredentials.Add(new DeviceCredential(
            Guid.NewGuid(), subject, "cred-orphan", "pubkey-x", 0, "timlathay.com", null, null, null, null, null));
        await _db.SaveChangesAsync();

        var list = await _service.ListAsync(subject);
        var dto = Assert.Single(list);
        Assert.Equal(0, dto.ActiveCredentialCount);
    }

    [Fact]
    public async Task Get_Detail_ReturnsCredentials()
    {
        var subject = Guid.NewGuid();
        var deviceId = await SeedDeviceAsync(subject);
        _db.DeviceCredentials.Add(new DeviceCredential(
            Guid.NewGuid(), subject, "cred-1", "pubkey-1", 0, "timlathay.com", deviceId, null, null, null, null));
        await _db.SaveChangesAsync();

        var detail = await _service.GetAsync(subject, deviceId);
        Assert.NotNull(detail);
        var cred = Assert.Single(detail!.Credentials);
        Assert.Equal("cred-1", cred.CredentialId);
        // KHÔNG lộ public key qua DTO (FR-004) — DTO chỉ có metadata an toàn
        var dtoProps = typeof(VanAn.Atrs.Contracts.DeviceCredentialSummaryDto).GetProperties().Select(p => p.Name);
        Assert.DoesNotContain("PublicKey", dtoProps);
        Assert.DoesNotContain("PrivateKey", dtoProps);
    }
}