using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Moq;
using VanAn.Shared.Domain.Common;
using VanAn.Shared.Services;
using VanAn.ShopERP.Controllers;
using VanAn.ShopERP.Services;
using Xunit;

namespace VanAn.ShopERP.Tests.Controllers;

/// <summary>task_ai_p6b — POST /api/tenant-ai/sync (manual "Đồng bộ AI" [G9]).</summary>
public sealed class TenantAiControllerTests
{
    private static readonly Guid Tenant = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");

    private readonly Mock<ITenantProvider> _tenant = new();
    private readonly Mock<IShopFeatureSettingsService> _flags = new();
    private readonly Mock<ITenantAiKnowledgeSync> _sync = new();

    private TenantAiController Sut(bool hasTenant = true, bool withSync = true)
    {
        _tenant.SetupGet(t => t.HasTenant).Returns(hasTenant);
        _tenant.SetupGet(t => t.TenantId).Returns(Tenant);
        return new TenantAiController(_tenant.Object, _flags.Object,
            withSync ? _sync.Object : null);
    }

    [Fact]
    public async Task Sync_FlagOn_CallsSyncNow_ReturnsCount()
    {
        _flags.Setup(f => f.IsEnabledAsync(Tenant, TenantAiKnowledgeSyncService.FlagName,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _sync.Setup(s => s.SyncNowAsync(Tenant, It.IsAny<CancellationToken>())).ReturnsAsync(42);

        var result = await Sut().SyncNow(default);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().BeEquivalentTo(new { synced = 42 });
        _sync.Verify(s => s.SyncNowAsync(Tenant, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Sync_FlagOff_Disabled_ZeroHttp()
    {
        _flags.Setup(f => f.IsEnabledAsync(Tenant, TenantAiKnowledgeSyncService.FlagName,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var result = await Sut().SyncNow(default);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().BeEquivalentTo(new { synced = 0, disabled = true });
        _sync.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Sync_NoTenant_BadRequest()
    {
        var result = await Sut(hasTenant: false).SyncNow(default);
        result.Should().BeOfType<BadRequestObjectResult>();
        _sync.VerifyNoOtherCalls();
    }
}
