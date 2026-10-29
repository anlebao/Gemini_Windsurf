using Microsoft.EntityFrameworkCore;
using VanAn.Metering.Contracts;
using VanAn.Metering.Domain;

namespace VanAn.Metering.Infra;

/// <summary>
/// Issue #197 — master switch "phí nền tảng" toàn hệ thống (SystemAdmin).
/// Singleton row; OFF → check deny=false + ingest không deduct + không alert.
/// Mọi mutation admin ghi AuditLog trong cùng SaveChanges (metering.md §1).
/// </summary>
public sealed class EnforcementConfigService(MeteringDbContext db)
{
    /// <summary>Row chưa tồn tại → enabled (default ON — backward compat).</summary>
    public async Task<bool> IsEnabledAsync(CancellationToken ct = default)
    {
        var cfg = await db.EnforcementConfigs.AsNoTracking().FirstOrDefaultAsync(ct);
        return cfg?.Enabled ?? true;
    }

    /// <summary>GET — trả DTO đầy đủ (default enabled=true, updatedBy rỗng khi chưa từng đổi).</summary>
    public async Task<EnforcementConfigDto> GetAsync(CancellationToken ct = default)
    {
        var cfg = await db.EnforcementConfigs.AsNoTracking().FirstOrDefaultAsync(ct);
        return cfg == null ? new EnforcementConfigDto(true, string.Empty, DateTime.MinValue) : ToDto(cfg);
    }

    public async Task<EnforcementConfigDto> SetAsync(bool enabled, string actor, DateTime nowUtc, CancellationToken ct = default)
    {
        var cfg = await db.EnforcementConfigs.FirstOrDefaultAsync(ct);
        var oldJson = cfg == null ? null : $"{{\"enabled\":{cfg.Enabled.ToString().ToLowerInvariant()}}}";
        if (cfg == null)
        {
            cfg = EnforcementConfig.Create(enabled, actor, nowUtc);
            db.EnforcementConfigs.Add(cfg);
        }
        else
        {
            cfg.Set(enabled, actor, nowUtc);
        }
        db.AuditLogs.Add(AuditLog.Create(actor, enabled ? "enable" : "disable",
            nameof(EnforcementConfig), cfg.Id, oldJson,
            $"{{\"enabled\":{enabled.ToString().ToLowerInvariant()}}}", nowUtc));
        await db.SaveChangesAsync(ct);
        return ToDto(cfg);
    }

    private static EnforcementConfigDto ToDto(EnforcementConfig cfg)
        => new(cfg.Enabled, cfg.UpdatedBy, cfg.UpdatedAtUtc);
}