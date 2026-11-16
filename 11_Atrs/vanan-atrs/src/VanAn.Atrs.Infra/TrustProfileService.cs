using Microsoft.EntityFrameworkCore;
using VanAn.Atrs.Domain;

namespace VanAn.Atrs.Infra;

/// <summary>
/// TrustProfileService — resolve/lazy-create TrustSubject từ identity hiện hữu (ADR-001, D1).
/// FR-002: profile tạo ở PENDING_SETUP/BASIC khi user đầu tiên gọi API; không bulk.
/// </summary>
public interface ITrustProfileService
{
    /// <summary>Resolve TrustSubjectId của Customer; chưa có → lazy-create subject + link + profile.</summary>
    Task<Guid> GetOrCreateForCustomerAsync(Guid customerId, CancellationToken ct = default);
    Task<Guid?> TryResolveByLinkAsync(TrustIdentityType identityType, Guid identityId, CancellationToken ct = default);
}

public sealed class TrustProfileService(AtrsDbContext db) : ITrustProfileService
{
    public async Task<Guid?> TryResolveByLinkAsync(TrustIdentityType identityType, Guid identityId, CancellationToken ct = default)
    {
        var link = await db.TrustSubjectLinks
            .AsNoTracking()
            .FirstOrDefaultAsync(l => l.IdentityType == identityType && l.IdentityId == identityId, ct);
        return link?.TrustSubjectId;
    }

    public async Task<Guid> GetOrCreateForCustomerAsync(Guid customerId, CancellationToken ct = default)
    {
        var existing = await TryResolveByLinkAsync(TrustIdentityType.Customer, customerId, ct);
        if (existing.HasValue)
            return existing.Value;

        var subjectId = Guid.NewGuid();
        db.TrustSubjects.Add(new TrustSubject(subjectId, primaryContactHash: null));
        db.TrustSubjectLinks.Add(new TrustSubjectLink(Guid.NewGuid(), subjectId, TrustIdentityType.Customer, customerId, tenantId: null));
        db.AccountTrustProfiles.Add(new AccountTrustProfile(Guid.NewGuid(), subjectId));
        try
        {
            await db.SaveChangesAsync(ct);
            return subjectId;
        }
        catch (DbUpdateException)
        {
            // Race: unique (TrustSubjectId, IdentityType, IdentityId) — thằng khác tạo trước → đọc lại.
            var reRead = await TryResolveByLinkAsync(TrustIdentityType.Customer, customerId, ct);
            if (reRead.HasValue)
                return reRead.Value;
            throw;
        }
    }
}