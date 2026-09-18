using Microsoft.AspNetCore.Components;
using VanAn.UI.Platform.Core.Interfaces;

namespace VanAn.UI.Platform.Adapters;

/// <summary>
/// Realtime Platform P4 (F4): default <see cref="IRealtimeEndpointProvider"/> — derives the Gateway
/// base URL from the current app origin (see <see cref="RealtimeEndpointUrls"/>). Works on every
/// host (WASM, Blazor Server, SSR) with zero configuration.
/// </summary>
public class NavigationRealtimeEndpointProvider(NavigationManager navigation) : IRealtimeEndpointProvider
{
    private readonly NavigationManager _navigation = navigation;

    public string GetGatewayBaseUrl()
        => RealtimeEndpointUrls.DeriveGatewayBaseUrl(_navigation.BaseUri);

    public string GetMessagingHubUrl()
        => $"{GetGatewayBaseUrl().TrimEnd('/')}/hubs/messaging";

    public string GetTrackingHubUrl()
        => $"{GetGatewayBaseUrl().TrimEnd('/')}/hubs/tracking";
}
