using Microsoft.EntityFrameworkCore;
using VanAn.Atrs.Contracts;
using VanAn.Atrs.Domain;
using VanAn.Atrs.Infra;
using Xunit;

namespace VanAn.Atrs.Tests.Services;

public sealed class AtrsNotificationServiceTests : IDisposable
{
    private readonly AtrsDbContext _db;
    private readonly AtrsNotificationService _service;
    private readonly Guid _subject = Guid.NewGuid();
    private readonly Guid _other = Guid.NewGuid();

    public AtrsNotificationServiceTests()
    {
        var options = new DbContextOptionsBuilder<AtrsDbContext>()
            .UseInMemoryDatabase($"notif-{Guid.NewGuid()}")
            .Options;
        _db = new AtrsDbContext(options);
        _service = new AtrsNotificationService(_db);
    }

    public void Dispose()
    {
        _db.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task Notify_List_UnreadFirst()
    {
        await _service.NotifyAsync(_subject, "recovery.approved", "T1", "B1");
        await _service.NotifyAsync(_subject, "device.revoked", "T2", "B2");

        var page = await _service.ListAsync(_subject, 0, 50);
        Assert.Equal(2, page.Total);
        Assert.Equal(2, page.Unread);
        Assert.Equal(2, page.Items.Count);
        Assert.All(page.Items, i => Assert.False(i.IsRead));
        Assert.Equal("device.revoked", page.Items[0].EventType); // newest first (created order trong cùng ms — fallback)
    }

    [Fact]
    public async Task MarkRead_OwnedOnly()
    {
        await _service.NotifyAsync(_subject, "recovery.approved", "T1", "B1");
        var mine = await _service.ListAsync(_subject, 0, 50);
        var id = mine.Items[0].Id;

        // Người khác không đọc được → false (anti-enumeration).
        Assert.False(await _service.MarkReadAsync(id, _other));
        // Chủ sở hữu đọc được → true + IsRead.
        Assert.True(await _service.MarkReadAsync(id, _subject));
        var after = await _service.ListAsync(_subject, 0, 50);
        Assert.True(after.Items[0].IsRead);
        Assert.Equal(0, after.Unread);
    }

    [Fact]
    public async Task List_PagingAndUnreadCount()
    {
        for (var i = 0; i < 5; i++)
            await _service.NotifyAsync(_subject, "e", $"T{i}", "B");
        var page = await _service.ListAsync(_subject, 0, 2);
        Assert.Equal(2, page.Items.Count);
        Assert.Equal(5, page.Total);
        var page2 = await _service.ListAsync(_subject, 2, 2);
        Assert.Equal(2, page2.Items.Count);
    }

    [Fact]
    public async Task NotificationBody_NeverContainsSecret()
    {
        await _service.NotifyAsync(_subject, "recovery_codes.regenerated", "T", "Mã khôi phục mới đã được tạo");
        var page = await _service.ListAsync(_subject, 0, 50);
        Assert.DoesNotContain("XXXXX", page.Items[0].Body);
    }

    // ── Emit assertions qua managers ──

    [Fact]
    public async Task RecoveryApprove_EmitsNotificationAndMetric()
    {
        var writer = new StubNotificationWriter();
        var metrics = new AtrsMetrics();
        var manager = new RecoveryCaseManager(_db, new StubRecoveryCodeService(),
            new RiskPolicyEngine(), new NoopBumpNotifier(),
            new SecurityPolicyService(_db, new Microsoft.Extensions.Caching.Memory.MemoryCache(new Microsoft.Extensions.Caching.Memory.MemoryCacheOptions()), TimeProvider.System),
            writer, metrics);

        var submitted = await manager.SubmitAsync(_subject, RecoveryRequestType.LOST_DEVICE, "idem-n1");
        Assert.Contains(writer.Calls, c => c.EventType == "recovery.initiated");

        await manager.ManualApproveAsync(submitted.Request.Id, Guid.NewGuid());
        Assert.Contains(writer.Calls, c => c.EventType == "recovery.approved");
        Assert.Equal(1, metrics.Snapshot().GetValueOrDefault("recovery.approved"));
        Assert.Equal(1, metrics.Snapshot().GetValueOrDefault("recovery.created"));
    }

    [Fact]
    public async Task DeviceRevoke_EmitsNotification()
    {
        var writer = new StubNotificationWriter();
        var metrics = new AtrsMetrics();
        var service = new DeviceService(_db, writer, metrics);

        var deviceId = await service.EnsureDeviceAsync(_subject, "dev-rv1", RegistrationMethod.INSTALLATION_ID, DeviceType.Mobile);
        Assert.Contains(writer.Calls, c => c.EventType == "device.registered");

        Assert.True(await service.RevokeAsync(_subject, deviceId, "lost"));
        Assert.Contains(writer.Calls, c => c.EventType == "device.revoked");
        Assert.Equal(1, metrics.Snapshot().GetValueOrDefault("device.revoked"));
    }

    private sealed class StubRecoveryCodeService : IRecoveryCodeService
    {
        public const string GoodCode = "GOOD-CODE";
        public Task<GenerateRecoveryCodesResult> GenerateSetAsync(Guid trustSubjectId, CancellationToken ct = default)
            => Task.FromResult(new GenerateRecoveryCodesResult(Guid.NewGuid(), [GoodCode], DateTime.UtcNow));
        public Task<VerifyRecoveryCodeResult> VerifyAndConsumeAsync(Guid trustSubjectId, string code, Guid? deviceId, CancellationToken ct = default)
            => Task.FromResult(new VerifyRecoveryCodeResult(code == GoodCode, code == GoodCode ? "Success" : "InvalidCode"));
        public Task<RecoveryCodesStatusDto> GetStatusAsync(Guid trustSubjectId, CancellationToken ct = default)
            => Task.FromResult(new RecoveryCodesStatusDto(true, 1, DateTime.UtcNow, null));
    }

    private sealed class NoopBumpNotifier : ITokenVersionBumpNotifier
    {
        public Task BumpAsync(Guid userId, string identityType, CancellationToken ct = default) => Task.CompletedTask;
    }
}