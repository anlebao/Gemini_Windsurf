using Microsoft.EntityFrameworkCore;
using VanAn.Atrs.Contracts;
using VanAn.Atrs.Domain;

namespace VanAn.Atrs.Infra;

/// <summary>
/// Security center summary (U06) — GET /api/v1/account-trust/me.
/// Map AssuranceState → "Chưa thiết lập/Cơ bản/Đã tăng cường" (UI SRS §9.1, bảng mapping ATRS-1).
/// KHÔNG lộ technical IDs/hash; chỉ state an toàn cho UI.
/// </summary>
public interface ISecurityCenterService
{
    Task<TrustProfileSummaryDto> GetSummaryAsync(Guid trustSubjectId, CancellationToken ct = default);
}

public sealed class SecurityCenterService(AtrsDbContext db) : ISecurityCenterService
{
    public async Task<TrustProfileSummaryDto> GetSummaryAsync(Guid trustSubjectId, CancellationToken ct = default)
    {
        var profile = await db.AccountTrustProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.TrustSubjectId == trustSubjectId, ct);

        var activeDevices = await db.UserDevices
            .AsNoTracking()
            .CountAsync(d => d.TrustSubjectId == trustSubjectId && d.Status == DeviceStatus.ACTIVE, ct);
        var activeCredentials = await db.DeviceCredentials
            .AsNoTracking()
            .CountAsync(c => c.TrustSubjectId == trustSubjectId && c.Status == CredentialStatus.ACTIVE, ct);
        var hasRecoveryCodes = await db.RecoveryCodeSets
            .AsNoTracking()
            .AnyAsync(s => s.TrustSubjectId == trustSubjectId && s.Status == RecoveryCodeSetStatus.ACTIVE, ct);
        var lastActivity = await db.AccountSecurityEvents
            .AsNoTracking()
            .Where(e => e.TrustSubjectId == trustSubjectId)
            .OrderByDescending(e => e.OccurredAt)
            .Select(e => (DateTime?)e.OccurredAt)
            .FirstOrDefaultAsync(ct);

        return new TrustProfileSummaryDto(
            trustSubjectId,
            profile?.Status.ToString() ?? TrustProfileStatus.PENDING_SETUP.ToString(),
            profile?.AssuranceState.ToString() ?? AssuranceState.BASIC.ToString(),
            activeDevices,
            activeCredentials,
            hasRecoveryCodes,
            lastActivity);
    }
}