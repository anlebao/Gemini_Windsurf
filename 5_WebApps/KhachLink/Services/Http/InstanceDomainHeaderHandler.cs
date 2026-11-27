using Microsoft.AspNetCore.Components;

namespace VanAn.KhachLink.Services.Http;

/// <summary>
/// Instance Feature Gate (2026-11-27): attaches X-KhachLink-Domain (current browser hostname)
/// to every Gateway request so the server can resolve this portal's KhachLinkInstance NavFlags
/// and apply SysAdmin kill-switch overrides on tenant feature flags.
/// Hostname comes from NavigationManager (always correct — no dependency on instance-config
/// load order; direct API consumers without the header get no override, by design).
/// </summary>
public sealed class InstanceDomainHeaderHandler(NavigationManager navigation) : DelegatingHandler
{
    public const string HeaderName = "X-KhachLink-Domain";

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (!request.Headers.Contains(HeaderName))
        {
            string host = new Uri(navigation.Uri).Host;
            if (!string.IsNullOrWhiteSpace(host))
                request.Headers.TryAddWithoutValidation(HeaderName, host);
        }
        return base.SendAsync(request, cancellationToken);
    }
}
