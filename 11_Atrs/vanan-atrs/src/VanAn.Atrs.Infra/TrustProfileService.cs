using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using VanAn.Atrs.Domain;

namespace VanAn.Atrs.Infra;

/// <summary>
/// TrustProfileService — resolve/lazy-create TrustSubject từ identity hiện hữu (ADR-001, D1).
/// FR-002: profile tạo ở PENDING_SETUP/BASIC khi user đầu tiên gọi API; không bulk.
/// ATRS-16: guest device (buyer không email/SĐT) → DeviceCluster link (L0/L1 device identity).
/// </summary>
public interface ITrustProfileService
{
    /// <summary>Resolve TrustSubjectId của Customer; chưa có → lazy-create subject + link + profile.</summary>
    Task<Guid> GetOrCreateForCustomerAsync(Guid customerId, CancellationToken ct = default);
    /// <summary>Resolve TrustSubjectId của guest device (DeviceCluster); chưa có → lazy-create.</summary>
    Task<Guid> GetOrCreateForDeviceAsync(string deviceId, Guid? tenantId = null, CancellationToken ct = default);
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

    /// <summary>
    /// ATRS-16: guest device → DeviceCluster. identityId = stable Guid từ SHA-256(deviceId)
    /// (deterministic — cùng device luôn về cùng subject; không cần migration).
    /// Device id CHỈ là risk signal, không phải nhân thân pháp lý (FR-004/005).
    /// </summary>
    public async Task<Guid> GetOrCreateForDeviceAsync(string deviceId, Guid? tenantId = null, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(deviceId) || deviceId.Length is < 8 or > 128)
            throw new ArgumentException("deviceId phải 8–128 ký tự.", nameof(deviceId));

        var identityId = StableGuidFrom(deviceId);
        var existing = await TryResolveByLinkAsync(TrustIdentityType.DeviceCluster, identityId, ct);
        if (existing.HasValue)
            return existing.Value;

        var subjectId = Guid.NewGuid();
        db.TrustSubjects.Add(new TrustSubject(subjectId, primaryContactHash: null));
        db.TrustSubjectLinks.Add(new TrustSubjectLink(Guid.NewGuid(), subjectId, TrustIdentityType.DeviceCluster, identityId, tenantId));
        db.AccountTrustProfiles.Add(new AccountTrustProfile(Guid.NewGuid(), subjectId));
        try
        {
            await db.SaveChangesAsync(ct);
            return subjectId;
        }
        catch (DbUpdateException)
        {
            var reRead = await TryResolveByLinkAsync(TrustIdentityType.DeviceCluster, identityId, ct);
            if (reRead.HasValue)
                return reRead.Value;
            throw;
        }
    }

    /// <summary>SHA-256(deviceId) → 16 bytes → Guid (deterministic, cross-instance ổn định).</summary>
    public static Guid StableGuidFrom(string value)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return new Guid(hash.AsSpan(0, 16));
    }
}