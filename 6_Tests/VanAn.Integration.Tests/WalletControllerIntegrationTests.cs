using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using VanAn.CoreHub.Infrastructure;
using VanAn.CoreHub.Services;
using VanAn.Integration.Tests.Infrastructure;
using VanAn.Shared.Domain;
using VanAn.Shared.Domain.Common;
using Xunit;
using Xunit.Abstractions;
using static VanAn.Integration.Tests.Infrastructure.TestEntityBuilder;

namespace VanAn.Integration.Tests;

/// <summary>
/// CC-S5 (Sprint 5): Wallet controller integration tests.
/// Validates DI registration + endpoint routing + 401 auth guard for wallet endpoints.
/// Uses GatewayWebApplicationFactory (SQLite in-memory, real DI container).
/// </summary>
public class WalletControllerIntegrationTests : IClassFixture<GatewayWebApplicationFactory>, IDisposable
{
    private readonly GatewayWebApplicationFactory _factory;
    private readonly ITestOutputHelper _output;
    private readonly HttpClient _client;
    private readonly IServiceScope _scope;
    private readonly VanAnDbContext _dbContext;

    public WalletControllerIntegrationTests(GatewayWebApplicationFactory factory, ITestOutputHelper output)
    {
        _factory = factory;
        _output = output;
        _client = factory.CreateClient();
        _scope = factory.Services.CreateScope();
        _dbContext = _scope.ServiceProvider.GetRequiredService<VanAnDbContext>();
    }

    public void Dispose()
    {
        _client.Dispose();
        _scope.Dispose();
    }

    // === W1: WalletService_RegisteredInDI ===
    [Fact(DisplayName = "W1: WalletService_RegisteredInDI")]
    public void WalletService_RegisteredInDI()
    {
        var service = _scope.ServiceProvider.GetService<IWalletService>();
        Assert.NotNull(service);
        Assert.IsType<WalletService>(service);
    }

    // === W2: GetWallet_NoToken_Returns401 ===
    [Fact(DisplayName = "W2: GetWallet_NoToken_Returns401")]
    public async Task GetWallet_NoToken_Returns401()
    {
        var resp = await _client.GetAsync("/api/community/wallet");
        Assert.Equal(System.Net.HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    // === W3: ConfirmCod_NoToken_Returns401 ===
    [Fact(DisplayName = "W3: ConfirmCod_NoToken_Returns401")]
    public async Task ConfirmCod_NoToken_Returns401()
    {
        var resp = await _client.PostAsync("/api/community/wallet/confirm-cod",
            new StringContent("{\"orderId\":\"" + Guid.NewGuid() + "\",\"amount\":50000}", System.Text.Encoding.UTF8, "application/json"));
        Assert.Equal(System.Net.HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    // === W4: ConfirmAdvance_NoToken_Returns401 ===
    [Fact(DisplayName = "W4: ConfirmAdvance_NoToken_Returns401")]
    public async Task ConfirmAdvance_NoToken_Returns401()
    {
        var resp = await _client.PostAsync("/api/community/wallet/confirm-advance",
            new StringContent("{\"orderId\":\"" + Guid.NewGuid() + "\",\"amount\":30000}", System.Text.Encoding.UTF8, "application/json"));
        Assert.Equal(System.Net.HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    // === W5: GetPendingAdvances_NoToken_Returns401 ===
    [Fact(DisplayName = "W5: GetPendingAdvances_NoToken_Returns401")]
    public async Task GetPendingAdvances_NoToken_Returns401()
    {
        var resp = await _client.GetAsync("/api/community/wallet/pending-advances");
        Assert.Equal(System.Net.HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    // === W6: ConfirmAdvanceReceived_NoToken_Returns401 ===
    [Fact(DisplayName = "W6: ConfirmAdvanceReceived_NoToken_Returns401")]
    public async Task ConfirmAdvanceReceived_NoToken_Returns401()
    {
        var resp = await _client.PostAsync("/api/community/wallet/confirm-advance-received",
            new StringContent("{\"advanceTransactionId\":\"" + Guid.NewGuid() + "\"}", System.Text.Encoding.UTF8, "application/json"));
        Assert.Equal(System.Net.HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    // === W7: ConfirmCod_InvalidToken_Returns401Or500 ===
    [Fact(DisplayName = "W7: ConfirmCod_InvalidToken_Returns401Or500")]
    public async Task ConfirmCod_InvalidToken_Returns401Or500()
    {
        _client.DefaultRequestHeaders.Add("X-Customer-Token", "invalid-token-12345");
        var resp = await _client.PostAsync("/api/community/wallet/confirm-cod",
            new StringContent("{\"orderId\":\"" + Guid.NewGuid() + "\",\"amount\":50000}", System.Text.Encoding.UTF8, "application/json"));
        // 401 if ShopERP reachable + rejects token; 500 if ShopERP unreachable (test env connection refused)
        Assert.True(resp.StatusCode == System.Net.HttpStatusCode.Unauthorized || resp.StatusCode == System.Net.HttpStatusCode.InternalServerError,
            $"Expected 401 or 500, got {resp.StatusCode}");
    }
}
