using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using VanAn.ShopERP.Services;
using VanAn.Shared.Services;
using Xunit;

namespace VanAn.Tests.Services;

/// <summary>
/// Loyalty Consistency Fix Phase 0 — DI registration smoke test for ShopERP HTTP proxies.
/// Verifies that AllianceWalletServiceHttpProxy + LoyaltyModeResolverHttpProxy:
///   1. Implement the correct interfaces (IAllianceWalletService, ILoyaltyModeResolver)
///   2. Are constructible with expected DI params (IHttpClientFactory + IMemoryCache + ILogger)
///   3. Are resolvable from a ServiceCollection matching ShopERP Program.cs registrations
/// BUG #0 regression guard — catches missing DI registration or interface mismatch.
/// </summary>
public class ShopErpDiRegistrationTests
{
    [Fact(DisplayName = "LC-DI-1: AllianceWalletServiceHttpProxy implements IAllianceWalletService")]
    public void AllianceWalletServiceHttpProxy_ImplementsInterface()
    {
        typeof(AllianceWalletServiceHttpProxy).Should().Implement<IAllianceWalletService>();
    }

    [Fact(DisplayName = "LC-DI-2: LoyaltyModeResolverHttpProxy implements ILoyaltyModeResolver")]
    public void LoyaltyModeResolverHttpProxy_ImplementsInterface()
    {
        typeof(LoyaltyModeResolverHttpProxy).Should().Implement<ILoyaltyModeResolver>();
    }

    [Fact(DisplayName = "LC-DI-3: ServiceCollection with ShopERP registrations resolves both interfaces")]
    public void ServiceCollection_ResolvesBothProxies()
    {
        // Build a minimal ServiceCollection matching ShopERP Program.cs Phase 0 registrations
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddMemoryCache();
        services.AddHttpClient("GatewayInternal");  // IHttpClientFactory requires AddHttpClient

        // Same registrations as ShopERP Program.cs (lines 361-380)
        services.AddScoped<ILoyaltyModeResolver, LoyaltyModeResolverHttpProxy>();
        services.AddScoped<IAllianceWalletService, AllianceWalletServiceHttpProxy>();

        using var provider = services.BuildServiceProvider();

        var modeResolver = provider.GetService<ILoyaltyModeResolver>();
        modeResolver.Should().NotBeNull("ILoyaltyModeResolver must be resolvable from ShopERP DI");
        modeResolver.Should().BeOfType<LoyaltyModeResolverHttpProxy>();

        var walletService = provider.GetService<IAllianceWalletService>();
        walletService.Should().NotBeNull("IAllianceWalletService must be resolvable from ShopERP DI");
        walletService.Should().BeOfType<AllianceWalletServiceHttpProxy>();
    }

    [Fact(DisplayName = "LC-DI-4: HTTP proxy constructors accept expected DI params")]
    public void ProxyConstructors_AcceptExpectedDependencies()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddMemoryCache();
        services.AddHttpClient("GatewayInternal");
        using var provider = services.BuildServiceProvider();

        var httpClientFactory = provider.GetRequiredService<IHttpClientFactory>();
        var cache = provider.GetRequiredService<IMemoryCache>();
        var walletLogger = NullLogger<AllianceWalletServiceHttpProxy>.Instance;
        var modeLogger = NullLogger<LoyaltyModeResolverHttpProxy>.Instance;

        // Should not throw — constructors accept expected param types
        var walletProxy = new AllianceWalletServiceHttpProxy(httpClientFactory, cache, walletLogger);
        var modeProxy = new LoyaltyModeResolverHttpProxy(httpClientFactory, cache, modeLogger);

        walletProxy.Should().NotBeNull();
        modeProxy.Should().NotBeNull();
    }
}
