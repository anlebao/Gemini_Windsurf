using Microsoft.Extensions.DependencyInjection;
using VanAn.UI.Platform.Adapters;
using VanAn.UI.Platform.Core.Interfaces;

namespace VanAn.UI.Platform.Extensions;

/// <summary>
/// Realtime Platform P4 (F4): one-call registration for the realtime UI surface on any host.
///
/// The named "realtime" HttpClient deliberately has NO base address — <see cref="RealtimeHttpAdapter"/>
/// builds absolute URLs from <see cref="IRealtimeEndpointProvider"/> per call. That sidesteps the
/// WASM-vs-Server scoping problem (a base address can only be configured from configuration or the
/// host, and the endpoint derivation needs the scoped NavigationManager) and keeps the RCL host-agnostic.
/// </summary>
public static class RealtimeServiceCollectionExtensions
{
    public static IServiceCollection AddRealtimePlatform(this IServiceCollection services)
    {
        services.AddScoped<IRealtimeEndpointProvider, NavigationRealtimeEndpointProvider>();
        services.AddScoped<IRealtimeChatClient, RealtimeHttpAdapter>();
        services.AddScoped<ILiveLocationClient, RealtimeHttpAdapter>();
        services.AddScoped<IMapJsAdapter, LeafletMapAdapter>();
        services.AddHttpClient(RealtimeHttpAdapter.HttpClientName);
        return services;
    }
}
