using VanAn.KhachLink.Models;
using VanAn.KhachLink.Services.Http;

namespace VanAn.KhachLink.Services;

/// <summary>
/// W2-2/W2-5 (2026-11-14): Scoped holder for the current KhachLinkInstanceConfig.
/// MainLayout fetches once via KhachLinkInstanceHttpService (localStorage-cached);
/// pages read Profile/OwnerTenantId/NavFlags without re-fetching.
/// Also exposes effective tenant context for feature gating (W2-5 FeatureContextService).
/// </summary>
public sealed class InstanceContextService(KhachLinkInstanceHttpService instanceHttp)
{
    private KhachLinkInstanceConfig? _config;
    private bool _loaded;

    /// <summary>Resolved instance config (null = feature flag OFF / domain unknown / fetch failed → FullCommerce fallback semantics).</summary>
    public KhachLinkInstanceConfig? Config => _config;

    /// <summary>True when this instance is the cooperative portal (Profile = Hoptacxa).</summary>
    public bool IsHoptacxa => _config?.Profile == VanAn.Shared.Domain.Aggregates.KhachLinkAggregate.KhachLinkProfile.Hoptacxa;

    /// <summary>OwnerTenantId của instance (tenant scope) — null cho platform-level portal.</summary>
    public Guid? OwnerTenantId => _config?.OwnerTenantId;

    /// <summary>Load config once per circuit/session (idempotent).</summary>
    public async Task<KhachLinkInstanceConfig?> GetConfigAsync()
    {
        if (!_loaded)
        {
            _config = await instanceHttp.GetByCurrentDomainAsync();
            _loaded = true;
        }
        return _config;
    }
}
