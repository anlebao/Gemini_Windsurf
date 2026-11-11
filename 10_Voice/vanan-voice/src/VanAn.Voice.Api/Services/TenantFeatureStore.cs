using System.Collections.Concurrent;

namespace VanAn.Voice.Api.Services;

/// <summary>
/// Per-tenant voice configuration (SRS v1.1 §14.4): admin kill-switch + open-calling flag.
/// P1 keeps this in memory (admin HTTP endpoints) — persistence via admin UI lands in P4.
/// Defaults: feature ENABLED (metering gate still governs cost), open-calling OFF.
/// </summary>
public sealed record VoiceTenantConfig(bool VoiceFeatureEnabled = true, bool VoiceOpenCalling = false);

public sealed class TenantFeatureStore
{
    private readonly ConcurrentDictionary<Guid, VoiceTenantConfig> _configs = new();

    public VoiceTenantConfig Get(Guid tenantId)
        => _configs.GetOrAdd(tenantId, _ => new VoiceTenantConfig());

    public VoiceTenantConfig Set(Guid tenantId, VoiceTenantConfig config)
        => _configs[tenantId] = config;
}