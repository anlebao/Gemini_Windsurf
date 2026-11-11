namespace VanAn.Shared.Services;

/// <summary>VOICE SERVICE C3 — one tenant's effective voice config row (defaults when no row stored).</summary>
public sealed class VoiceTenantConfigDto
{
    public Guid TenantId { get; set; }
    public string? TenantName { get; set; }
    public bool VoiceFeatureEnabled { get; set; } = true;
    public bool VoiceOpenCalling { get; set; }
    /// <summary>True khi chưa có row trong VoiceTenantConfigs — voice service dùng defaults.</summary>
    public bool IsDefault { get; set; } = true;
    public DateTime? LastChangedAt { get; set; }
    public string? LastChangedBy { get; set; }
}

/// <summary>PUT /api/admin/voice-config/{tenantId} body (camelCase serialization).</summary>
public sealed class UpdateVoiceTenantConfigRequest
{
    public bool VoiceFeatureEnabled { get; set; } = true;
    public bool VoiceOpenCalling { get; set; }
}

/// <summary>
/// VOICE SERVICE C3 (2026-11-11, quyết định user — Gateway PG, SRS-compliant):
/// SystemAdmin CRUD cấu hình voice per-tenant (kill-switch VoiceFeatureEnabled +
/// VoiceOpenCalling, SRS v1.1 §14.4). Gateway-only — voice service đọc PG read-only.
/// </summary>
public interface IVoiceTenantConfigService
{
    /// <summary>GET — mọi tenant (kèm tên) + config hiệu lực (defaults khi chưa có row).</summary>
    Task<List<VoiceTenantConfigDto>> GetAllAsync(CancellationToken ct = default);

    /// <summary>PUT — upsert 1 row / tenant; validate tenant tồn tại (404 khi không).</summary>
    Task<VoiceTenantConfigDto> UpsertAsync(Guid tenantId, UpdateVoiceTenantConfigRequest request, string actor, CancellationToken ct = default);
}