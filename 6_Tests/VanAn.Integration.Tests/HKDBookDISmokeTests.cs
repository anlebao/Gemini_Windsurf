using Microsoft.Extensions.DependencyInjection;
using VanAn.CoreHub.Services;
using VanAn.CoreHub.Services.Cache;
using VanAn.CoreHub.Services.Data;
using VanAn.CoreHub.Services.Formula;
using VanAn.CoreHub.Services.PreAggregation;
using VanAn.CoreHub.Services.Template;
using VanAn.Integration.Tests.Infrastructure;
using Xunit;

namespace VanAn.Integration.Tests;

/// <summary>
/// DI smoke tests for Wave 7 — verify HKD Book calc engine services are resolvable
/// in the Gateway DI container.
///
/// THESE TESTS ARE BLOCKING — they catch the pre-existing latent DI bug where
/// Gateway Program.cs never registered IHKDBookService or any of the 5 calc engine
/// services. Without these tests, the bug only surfaces as a 500 on VPS at runtime
/// (AccountingEntriesController injects IHKDBookService but it was never registered).
///
/// Wave 7 fix: all 6 services now registered in Gateway Program.cs.
/// </summary>
[Trait("Category", "Startup")]
public class HKDBookDISmokeTests : IClassFixture<GatewayWebApplicationFactory>
{
    private readonly GatewayWebApplicationFactory _factory;

    public HKDBookDISmokeTests(GatewayWebApplicationFactory factory)
    {
        _factory = factory;
    }

    /// <summary>
    /// Verify all 5 calc engine services + IHKDBookService are resolvable in Gateway DI.
    /// This is the SC4 smoke test from the Wave 7 task card.
    /// </summary>
    [Fact(DisplayName = "W7-SC4: Gateway DI resolves 5 calc engine services + IHKDBookService")]
    public async Task Gateway_DI_Resolves_HKD_Book_Calc_Engine_Services()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var sp = scope.ServiceProvider;

        // 5 calc engine services (per task card SC4)
        Assert.NotNull(sp.GetRequiredService<IHKDBookGenerationService>());
        Assert.NotNull(sp.GetRequiredService<IFormulaEngine>());
        Assert.NotNull(sp.GetRequiredService<IDataProvider>());
        Assert.NotNull(sp.GetRequiredService<IPreAggregationService>());
        Assert.NotNull(sp.GetRequiredService<VanAn.CoreHub.Services.Template.TemplateFactory>());

        // IHKDBookService — the latent bug: AccountingEntriesController injected this
        // but Gateway never registered it. Wave 7 fixes this.
        Assert.NotNull(sp.GetRequiredService<IHKDBookService>());

        // Supporting services in the dependency chain
        Assert.NotNull(sp.GetRequiredService<IBookResultCache>());
    }

    /// <summary>
    /// Verify the full dependency chain can be resolved end-to-end:
    /// IHKDBookService → IHKDBookGenerationService → TemplateFactory →
    /// IFormulaEngine → IPreAggregationService → IDataProvider → IBookResultCache
    /// </summary>
    [Fact(DisplayName = "W7: Full HKD book dependency chain resolvable end-to-end")]
    public async Task Gateway_DI_Full_Dependency_Chain_Resolvable()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var sp = scope.ServiceProvider;

        // If any link in the chain is missing, GetRequiredService throws → test fails
        var hkdBookService = sp.GetRequiredService<IHKDBookService>();
        var generationService = sp.GetRequiredService<IHKDBookGenerationService>();
        var templateFactory = sp.GetRequiredService<VanAn.CoreHub.Services.Template.TemplateFactory>();
        var formulaEngine = sp.GetRequiredService<IFormulaEngine>();
        var preAggregation = sp.GetRequiredService<IPreAggregationService>();
        var dataProvider = sp.GetRequiredService<IDataProvider>();
        var cache = sp.GetRequiredService<IBookResultCache>();

        Assert.All(new object?[] { hkdBookService, generationService, templateFactory,
            formulaEngine, preAggregation, dataProvider, cache },
            x => Assert.NotNull(x));
    }
}
