namespace VanAn.Atrs.Domain;

/// <summary>
/// Map TrustSubject ↔ identity hiện hữu (Customer per-tenant / DemoUser /
/// DeviceCluster). Một subject có thể có nhiều link (một người nhiều tenant).
/// Unique (TrustSubjectId, IdentityType, IdentityId) enforced tại DB.
/// </summary>
public sealed class TrustSubjectLink
{
    public Guid Id { get; private set; }
    public Guid TrustSubjectId { get; private set; }
    public TrustIdentityType IdentityType { get; private set; }
    public Guid IdentityId { get; private set; }
    /// <summary>Tenant của identity; null khi account-level (DemoUser/DeviceCluster).</summary>
    public Guid? TenantId { get; private set; }
    public DateTime CreatedAt { get; private set; }

    private TrustSubjectLink() { }

    public TrustSubjectLink(Guid id, Guid trustSubjectId, TrustIdentityType identityType, Guid identityId, Guid? tenantId)
    {
        if (id == Guid.Empty)
            throw new ArgumentException("Id cannot be empty.", nameof(id));
        if (trustSubjectId == Guid.Empty)
            throw new ArgumentException("TrustSubjectId cannot be empty.", nameof(trustSubjectId));
        if (identityId == Guid.Empty)
            throw new ArgumentException("IdentityId cannot be empty.", nameof(identityId));
        Id = id;
        TrustSubjectId = trustSubjectId;
        IdentityType = identityType;
        IdentityId = identityId;
        TenantId = tenantId;
        CreatedAt = DateTime.UtcNow;
    }
}