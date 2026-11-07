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
/// Task AI-P10 — regression RV production: pending tab có ≥1 doc từng crash render
/// (500) vì @bind="_rejectReasons[d.Id]" đọc dictionary indexer chưa seed key.
/// </summary>
public sealed class TenantAiKnowledgeTests : ComponentTestBase
{
    private static readonly Guid Tenant = Guid.Parse("00000000-0000-0000-0000-000000000001");

    private Mock<ITenantAiClient> RegisterClient(bool flagOn)
    {
        var client = new Mock<ITenantAiClient>(MockBehavior.Strict);
        Services.AddSingleton(client.Object);
        var flags = new Mock<IShopFeatureSettingsService>();
        flags.Setup(f => f.IsEnabledAsync(
                Tenant, TenantAiKnowledgeSyncService.FlagName, It.IsAny<CancellationToken>()))
            .ReturnsAsync(flagOn);
        Services.AddSingleton(flags.Object);
        return client;
    }

    [Fact]
    public void PendingTab_WithPendingDoc_RendersWithoutException()
    {
        var client = RegisterClient(flagOn: true);
        var doc = new TenantAiDocItem(
            Guid.NewGuid(), "faq", "E2E FAQ", "public", "pending_review",
            null, null, null, DateTime.UtcNow, false, "nội dung test");
        client.Setup(c => c.GetDocumentsAsync(Tenant, "pending_review", 1, 100, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TenantAiDocList(1, 100, 1, [doc]));

        var cut = RenderComponent<TenantAiKnowledge>();

        cut.WaitForAssertion(() =>
            cut.FindAll("[data-testid='pending-doc']").Should().HaveCount(1), TimeSpan.FromSeconds(5));
        cut.Find("[data-testid='input-reject-reason']").Should().NotBeNull();
        cut.Find("[data-testid='btn-approve-doc']").Should().NotBeNull();
    }

    [Fact]
    public void FlagOff_RendersEmptyState_MakesZeroHttpCalls()
    {
        var client = RegisterClient(flagOn: false);

        var cut = RenderComponent<TenantAiKnowledge>();

        cut.Markup.Should().Contain("chưa bật");
        client.VerifyNoOtherCalls();
    }
}
