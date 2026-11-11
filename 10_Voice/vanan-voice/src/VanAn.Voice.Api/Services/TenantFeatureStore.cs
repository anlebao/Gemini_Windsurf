using System.Collections.Concurrent;
using Npgsql;

namespace VanAn.Voice.Api.Services;

/// <summary>
/// Per-tenant voice configuration (SRS v1.1 §14.4): admin kill-switch + open-calling flag.
/// C3 (2026-11-11, quyết định user — Gateway PG, SRS-compliant): đọc read-only từ bảng
/// VoiceTenantConfigs (Gateway PG, nguồn SystemAdmin qua /api/admin/voice-config), cache 30s,
/// fail-open → default (feature ENABLED — metering gate still governs cost, open-calling OFF).
/// Khi chưa cấu hình Voice:GatewayDb (local dev) → default, không lỗi.
/// </summary>
public sealed record VoiceTenantConfig(bool VoiceFeatureEnabled = true, bool VoiceOpenCalling = false);

public sealed class TenantFeatureStore
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(30);

    private readonly string? _connectionString;
    private readonly ILogger<TenantFeatureStore> _logger;
    private readonly ConcurrentDictionary<Guid, (VoiceTenantConfig Config, DateTime ExpiresUtc)> _cache = new();

    public TenantFeatureStore(IConfiguration configuration, ILogger<TenantFeatureStore> logger)
    {
        _connectionString = configuration["Voice:GatewayDb"];
        _logger = logger;
    }

    public Task<VoiceTenantConfig> GetAsync(Guid tenantId, CancellationToken ct = default)
        => GetAsync(tenantId, DateTime.UtcNow, ct);

    /// <summary>PG-backed read with 30s cache; missing row / PG down → default (fail-open).</summary>
    public async Task<VoiceTenantConfig> GetAsync(Guid tenantId, DateTime nowUtc, CancellationToken ct = default)
    {
        if (_cache.TryGetValue(tenantId, out var cached) && cached.ExpiresUtc > nowUtc)
            return cached.Config;

        var config = string.IsNullOrWhiteSpace(_connectionString)
            ? new VoiceTenantConfig()
            : await ReadFromPgAsync(tenantId, ct);

        _cache[tenantId] = (config, nowUtc + CacheTtl);
        return config;
    }

    /// <summary>Dev/E2E override (chỉ khi Voice:AllowInsecureAdminApi=true) — ghi đè cache local.</summary>
    public VoiceTenantConfig Set(Guid tenantId, VoiceTenantConfig config)
    {
        _cache[tenantId] = (config, DateTime.UtcNow + CacheTtl);
        return config;
    }

    private async Task<VoiceTenantConfig> ReadFromPgAsync(Guid tenantId, CancellationToken ct)
    {
        try
        {
            await using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync(ct);
            await using var cmd = new NpgsqlCommand(
                "SELECT \"VoiceFeatureEnabled\", \"VoiceOpenCalling\" FROM \"VoiceTenantConfigs\" WHERE \"TenantId\" = @tenantId LIMIT 1", conn);
            cmd.Parameters.AddWithValue("tenantId", tenantId.ToString());
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            if (!await reader.ReadAsync(ct))
                return new VoiceTenantConfig(); // no row → defaults (feature enabled, open-calling off)

            return new VoiceTenantConfig(
                reader.GetBoolean(0),
                reader.GetBoolean(1));
        }
        catch (Exception ex)
        {
            // Fail-open (mirror metering G9): config read failure must never break calls.
            _logger.LogWarning(ex, "TenantFeatureStore PG read failed for tenant {TenantId} — using defaults.", tenantId);
            return new VoiceTenantConfig();
        }
    }
}