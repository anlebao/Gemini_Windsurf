using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using VanAn.CoreHub.Infrastructure;
using VanAn.CoreHub.Services;
using VanAn.Integration.Tests.Infrastructure;
using Xunit;
using Xunit.Abstractions;

namespace VanAn.Integration.Tests;

/// <summary>
/// Sprint 7 — Commerce Mode + Community Fund + Product Cost Price controller integration tests.
/// Validates DI registration + endpoint routing + 401 auth guard.
/// Uses GatewayWebApplicationFactory (SQLite in-memory, real DI container).
/// </summary>
public class CommerceModeControllerIntegrationTests : IClassFixture<GatewayWebApplicationFactory>, IDisposable
{
    private readonly GatewayWebApplicationFactory _factory;
    private readonly ITestOutputHelper _output;
    private readonly HttpClient _client;
    private readonly IServiceScope _scope;

    public CommerceModeControllerIntegrationTests(GatewayWebApplicationFactory factory, ITestOutputHelper output)
    {
        _factory = factory;
        _output = output;
        _client = factory.CreateClient();
        _scope = factory.Services.CreateScope();
    }

    public void Dispose()
    {
        _client.Dispose();
        _scope.Dispose();
    }

    // === CM1: CommerceModeService_RegisteredInDI ===
    [Fact(DisplayName = "CM1: CommerceModeService_RegisteredInDI")]
    public void CommerceModeService_RegisteredInDI()
    {
        var service = _scope.ServiceProvider.GetService<ICommerceModeService>();
        Assert.NotNull(service);
        Assert.IsType<CommerceModeService>(service);
    }

    // === CM2: CommunityFundService_RegisteredInDI ===
    [Fact(DisplayName = "CM2: CommunityFundService_RegisteredInDI")]
    public void CommunityFundService_RegisteredInDI()
    {
        var service = _scope.ServiceProvider.GetService<ICommunityFundService>();
        Assert.NotNull(service);
        Assert.IsType<CommunityFundService>(service);
    }

    // === CM3: GetSettings_NoToken_Returns401 ===
    [Fact(DisplayName = "CM3: GetSettings_NoToken_Returns401")]
    public async Task GetSettings_NoToken_Returns401()
    {
        var resp = await _client.GetAsync("/api/admin/commerce-mode");
        Assert.Equal(System.Net.HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    // === CM4: SetGlobalMode_NoToken_Returns401 ===
    [Fact(DisplayName = "CM4: SetGlobalMode_NoToken_Returns401")]
    public async Task SetGlobalMode_NoToken_Returns401()
    {
        var resp = await _client.PostAsync("/api/admin/commerce-mode/global",
            new StringContent("{\"mode\":\"Reseller\",\"platformFeeRate\":0.30,\"communityFundRate\":0.05,\"deliveryFee\":15000}", System.Text.Encoding.UTF8, "application/json"));
        Assert.Equal(System.Net.HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    // === CM5: SetTenantOverride_NoToken_Returns401 ===
    [Fact(DisplayName = "CM5: SetTenantOverride_NoToken_Returns401")]
    public async Task SetTenantOverride_NoToken_Returns401()
    {
        var resp = await _client.PostAsync($"/api/admin/commerce-mode/tenant/{Guid.NewGuid()}",
            new StringContent("{\"overrideMode\":\"Reseller\"}", System.Text.Encoding.UTF8, "application/json"));
        Assert.Equal(System.Net.HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    // === CM6: ResolveMode_NoToken_Returns401 ===
    [Fact(DisplayName = "CM6: ResolveMode_NoToken_Returns401")]
    public async Task ResolveMode_NoToken_Returns401()
    {
        var resp = await _client.GetAsync($"/api/admin/commerce-mode/resolve/{Guid.NewGuid()}");
        Assert.Equal(System.Net.HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    // === CM7: CommunityFundBalance_NoToken_Returns401 ===
    [Fact(DisplayName = "CM7: CommunityFundBalance_NoToken_Returns401")]
    public async Task CommunityFundBalance_NoToken_Returns401()
    {
        var resp = await _client.GetAsync("/api/admin/community-fund/balance");
        Assert.Equal(System.Net.HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    // === CM8: CommunityFundSpend_NoToken_Returns401 ===
    [Fact(DisplayName = "CM8: CommunityFundSpend_NoToken_Returns401")]
    public async Task CommunityFundSpend_NoToken_Returns401()
    {
        var resp = await _client.PostAsync("/api/admin/community-fund/spend",
            new StringContent("{\"amount\":50000,\"reason\":\"Test\",\"recipient\":\"Test\"}", System.Text.Encoding.UTF8, "application/json"));
        Assert.Equal(System.Net.HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    // === CM9: CommunityFundHistory_NoToken_Returns401 ===
    [Fact(DisplayName = "CM9: CommunityFundHistory_NoToken_Returns401")]
    public async Task CommunityFundHistory_NoToken_Returns401()
    {
        var resp = await _client.GetAsync("/api/admin/community-fund/history");
        Assert.Equal(System.Net.HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    // === CM10: ProductCostPrices_NoToken_Returns401 ===
    [Fact(DisplayName = "CM10: ProductCostPrices_NoToken_Returns401")]
    public async Task ProductCostPrices_NoToken_Returns401()
    {
        var resp = await _client.GetAsync("/api/admin/product-cost-prices");
        Assert.Equal(System.Net.HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    // === CM11: ProductCostPriceUpsert_NoToken_Returns401 ===
    [Fact(DisplayName = "CM11: ProductCostPriceUpsert_NoToken_Returns401")]
    public async Task ProductCostPriceUpsert_NoToken_Returns401()
    {
        var resp = await _client.PostAsync("/api/admin/product-cost-prices",
            new StringContent("{\"tenantId\":\"" + Guid.NewGuid() + "\",\"productId\":\"" + Guid.NewGuid() + "\",\"costPrice\":50000}", System.Text.Encoding.UTF8, "application/json"));
        Assert.Equal(System.Net.HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    // === CM12: ExternalPayment_NoToken_Returns401 ===
    [Fact(DisplayName = "CM12: ExternalPayment_NoToken_Returns401")]
    public async Task ExternalPayment_NoToken_Returns401()
    {
        var resp = await _client.PostAsync("/api/admin/commerce-mode/confirm-external-payment",
            new StringContent("{\"orderId\":\"" + Guid.NewGuid() + "\",\"amount\":100000,\"paymentRef\":\"VQR-123\"}", System.Text.Encoding.UTF8, "application/json"));
        Assert.Equal(System.Net.HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    // === CM13: SystemSettings_DbSet_Registered ===
    [Fact(DisplayName = "CM13: SystemSettings_DbSet_Registered")]
    public void SystemSettings_DbSet_Registered()
    {
        var dbContext = _scope.ServiceProvider.GetRequiredService<VanAnDbContext>();
        Assert.NotNull(dbContext.SystemSettings);
    }

    // === CM14: ProductCostPrices_DbSet_Registered ===
    [Fact(DisplayName = "CM14: ProductCostPrices_DbSet_Registered")]
    public void ProductCostPrices_DbSet_Registered()
    {
        var dbContext = _scope.ServiceProvider.GetRequiredService<VanAnDbContext>();
        Assert.NotNull(dbContext.ProductCostPrices);
    }

    // === CM15: CommunityFundSpendRecords_DbSet_Registered ===
    [Fact(DisplayName = "CM15: CommunityFundSpendRecords_DbSet_Registered")]
    public void CommunityFundSpendRecords_DbSet_Registered()
    {
        var dbContext = _scope.ServiceProvider.GetRequiredService<VanAnDbContext>();
        Assert.NotNull(dbContext.CommunityFundSpendRecords);
    }
}
