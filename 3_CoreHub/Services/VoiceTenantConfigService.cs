using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using VanAn.CoreHub.Infrastructure;
using VanAn.Shared.Domain;
using VanAn.Shared.Domain.Aggregates.TenantAggregate;
using VanAn.Shared.Services;

namespace VanAn.CoreHub.Services;

/// <summary>
/// VOICE SERVICE C3 (2026-11-11, quyết định user — Gateway PG, SRS-compliant):
/// SystemAdmin CRUD cấu hình voice per-tenant (kill-switch + open calling, SRS v1.1 §14.4).
/// 1 row / tenant (unique TenantId); missing row → defaults (VoiceFeatureEnabled=true,
/// VoiceOpenCalling=false). Voice service đọc PG read-only (không viết vào đây).
/// Multi-tenancy: admin ops cross-tenant (pattern OrderFlowAdminService) — query tường minh.
/// </summary>
public class VoiceTenantConfigService(
    IVanAnDbContext context,
    ILogger<VoiceTenantConfigService> logger) : IVoiceTenantConfigService
{
    private readonly IVanAnDbContext _context = context;
    private readonly ILogger<VoiceTenantConfigService> _logger = logger;

    public async Task<List<VoiceTenantConfigDto>> GetAllAsync(CancellationToken ct = default)
    {
        var tenants = await _context.Tenants.AsNoTracking()
            .Where(t => !t.IsDeleted && t.Status == TenantStatus.Active)
            .Select(t => new { t.Id, t.Name })
            .OrderBy(t => t.Name)
            .ToListAsync(ct);

        var configs = await _context.VoiceTenantConfigs.AsNoTracking()
            .ToDictionaryAsync(c => c.TenantId.Value, ct);

        return tenants.Select(t => configs.TryGetValue(t.Id, out var cfg)
            ? new VoiceTenantConfigDto
            {
                TenantId = t.Id,
                TenantName = t.Name,
                VoiceFeatureEnabled = cfg.VoiceFeatureEnabled,
                VoiceOpenCalling = cfg.VoiceOpenCalling,
                IsDefault = false,
                LastChangedAt = cfg.LastChangedAt,
                LastChangedBy = cfg.LastChangedBy
            }
            : new VoiceTenantConfigDto
            {
                TenantId = t.Id,
                TenantName = t.Name,
                IsDefault = true
            }).ToList();
    }

    public async Task<VoiceTenantConfigDto> UpsertAsync(
        Guid tenantId, UpdateVoiceTenantConfigRequest request, string actor, CancellationToken ct = default)
    {
        if (request is null)
            throw new ArgumentNullException(nameof(request));

        var tenant = await _context.Tenants.AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == new TenantId(tenantId) && !t.IsDeleted, ct);
        if (tenant is null)
            throw new KeyNotFoundException($"Tenant {tenantId} not found.");

        var existing = await _context.VoiceTenantConfigs
            .FirstOrDefaultAsync(c => c.TenantId == new TenantId(tenantId), ct);

        if (existing is null)
        {
            _context.VoiceTenantConfigs.Add(new VoiceTenantConfig(
                tenantId, request.VoiceFeatureEnabled, request.VoiceOpenCalling, actor));
        }
        else
        {
            existing.Update(request.VoiceFeatureEnabled, request.VoiceOpenCalling, actor);
        }

        await _context.SaveChangesAsync(ct);
        _logger.LogInformation("VoiceTenantConfig upserted for tenant {TenantId} (feature={Feature}, open={Open}) by {Actor}",
            tenantId, request.VoiceFeatureEnabled, request.VoiceOpenCalling, actor);

        return new VoiceTenantConfigDto
        {
            TenantId = tenantId,
            TenantName = tenant.Name,
            VoiceFeatureEnabled = request.VoiceFeatureEnabled,
            VoiceOpenCalling = request.VoiceOpenCalling,
            IsDefault = false,
            LastChangedAt = DateTime.UtcNow,
            LastChangedBy = actor
        };
    }
}