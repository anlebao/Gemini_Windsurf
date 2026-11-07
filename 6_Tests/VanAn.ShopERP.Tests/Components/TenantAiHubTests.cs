using Bunit;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using VanAn.Shared.Services;
using VanAn.ShopERP.Components.Pages.Admin.TenantAi;
using VanAn.ShopERP.Services;
using Xunit;

namespace VanAn.ShopERP.Tests.Components;

/// <summary>
/// Task AI-P10 — TenantAiHub flag gating: flag OFF → empty state + ZERO HTTP calls
/// (pattern FlagOff_MakesZeroHttpCalls); flag ON → hub loads status/profile/usage once.
/// </summary>
public sealed class TenantAiHubTests : ComponentTestBase
{
    private static readonly Guid Tenant = Guid.Parse("00000000-0000-0000-0000-000000000001");

    private Mock<ITenantAiClient> RegisterClient()
    {
        var client = new Mock<ITenantAiClient>(MockBehavior.Strict);
        Services.AddSingleton(client.Object);
        return client;
    }

    private void SetFlag(bool on)
    {
        var flags = new Mock<IShopFeatureSettingsService>();
        flags.Setup(f => f.IsEnabledAsync(
                Tenant, TenantAiKnowledgeSyncService.FlagName, It.IsAny<CancellationToken>()))
            .ReturnsAsync(on);
        Services.AddSingleton(flags.Object);
    }

    [Fact]
    public void FlagOff_RendersEmptyState_MakesZeroHttpCalls()
    {
        var client = RegisterClient();
        SetFlag(on: false);

        var cut = RenderComponent<TenantAiHub>();

        cut.Find("[data-testid='tenant-ai-disabled']").Should().NotBeNull();
        client.VerifyNoOtherCalls();
    }

    [Fact]
    public void FlagOn_LoadsStatusProfileUsage_Once()
    {
        var client = RegisterClient();
        SetFlag(on: true);
        client.Setup(c => c.GetKnowledgeStatusAsync(Tenant, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TenantAiStatus(1, 3, 0, 4, 4, 0, 22, "v1",
                DateTime.UtcNow, false, 24));
        client.Setup(c => c.GetProfileAsync(Tenant, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TenantAiProfileResponse(
                null,
                new TenantAiProfileVersion(1, "published", "Bé", "{}", "{}",
                    DateTime.UtcNow, "ops"),
                []));
        client.Setup(c => c.GetUsageAsync(Tenant, 30, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TenantAiUsageResponse(30, [],
                new TenantAiUsageDay("total", 7, 1, 0, 500, 200)));

        var cut = RenderComponent<TenantAiHub>();

        cut.WaitForAssertion(() =>
            cut.Find("[data-testid='card-ai-products']").Should().NotBeNull(), TimeSpan.FromSeconds(5));
        client.Verify(c => c.GetKnowledgeStatusAsync(Tenant, It.IsAny<CancellationToken>()), Times.Once);
        client.Verify(c => c.GetProfileAsync(Tenant, It.IsAny<CancellationToken>()), Times.Once);
        client.Verify(c => c.GetUsageAsync(Tenant, 30, It.IsAny<CancellationToken>()), Times.Once);
        client.VerifyNoOtherCalls();
        cut.Markup.Should().Contain("Bé");
    }
}
