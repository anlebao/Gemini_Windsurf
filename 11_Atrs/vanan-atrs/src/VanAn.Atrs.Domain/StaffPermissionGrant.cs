namespace VanAn.Atrs.Domain;

/// <summary>
/// AtrsPermissions — hằng số permission staff ATRS (D5, FR-014, UI §4.2 subset Wave 3).
/// Permission check backend-authoritative qua <see cref="StaffPermissionGrant"/>;
/// UI ẩn nút KHÔNG phải security.
/// </summary>
public static class AtrsPermissions
{
    // Recovery review (A15/A16)
    public const string RecoveryView = "recovery.view";
    public const string RecoveryReview = "recovery.review";
    public const string RecoveryApprove = "recovery.approve";

    // Dispute (A17/A18/A19)
    public const string DisputeView = "dispute.view";
    public const string DisputeInvestigate = "dispute.investigate";
    public const string DisputeProposeAdjustment = "dispute.propose_adjustment";
    public const string DisputeApproveAdjustment = "dispute.approve_adjustment";

    // Case ops
    public const string CaseAssign = "case.assign";

    // Audit (A20 — audit explorer)
    public const string AuditRead = "audit.read";

    // Policy (A21 — security policy)
    public const string PolicyManage = "policy.manage";
    public const string PolicyApprove = "policy.approve";

    /// <summary>Toàn bộ permission — dùng cho grant UI / validation.</summary>
    public static readonly IReadOnlyList<string> All =
    [
        RecoveryView, RecoveryReview, RecoveryApprove,
        DisputeView, DisputeInvestigate, DisputeProposeAdjustment, DisputeApproveAdjustment,
        CaseAssign,
        AuditRead,
        PolicyManage, PolicyApprove
    ];
}

/// <summary>
/// StaffPermissionGrant — cấp quyền staff ATRS (D5). Cross-tenant registry (PG atrs).
/// ScopeTenantId = tenant scope của grant; null = mọi tenant (phải kèm approver role
/// cho thao tác nhạy cảm — maker-checker vẫn enforce riêng).
/// </summary>
public sealed class StaffPermissionGrant
{
    public Guid Id { get; private set; }
    /// <summary>DemoUserId của staff (sub claim JWT) — KHÔNG phải TrustSubject.</summary>
    public Guid DemoUserId { get; private set; }
    public string Permission { get; private set; } = string.Empty;
    public Guid? ScopeTenantId { get; private set; }
    public string GrantedBy { get; private set; } = string.Empty;
    public DateTime GrantedAt { get; private set; }
    public DateTime? ExpiresAt { get; private set; }
    public bool IsRevoked { get; private set; }

    private StaffPermissionGrant() { }

    public StaffPermissionGrant(Guid id, Guid demoUserId, string permission, string grantedBy, Guid? scopeTenantId = null, DateTime? expiresAt = null)
    {
        if (id == Guid.Empty) throw new ArgumentException("Id cannot be empty.", nameof(id));
        if (demoUserId == Guid.Empty) throw new ArgumentException("DemoUserId cannot be empty.", nameof(demoUserId));
        if (string.IsNullOrWhiteSpace(permission)) throw new ArgumentException("Permission cannot be empty.", nameof(permission));
        if (!AtrsPermissions.All.Contains(permission))
            throw new ArgumentException($"Unknown ATRS permission: {permission}", nameof(permission));
        if (string.IsNullOrWhiteSpace(grantedBy)) throw new ArgumentException("GrantedBy cannot be empty.", nameof(grantedBy));

        Id = id;
        DemoUserId = demoUserId;
        Permission = permission;
        ScopeTenantId = scopeTenantId;
        GrantedBy = grantedBy.Trim();
        GrantedAt = DateTime.UtcNow;
        ExpiresAt = expiresAt;
    }

    public void Revoke()
    {
        IsRevoked = true;
    }

    public bool IsActiveAt(DateTime utcNow)
        => !IsRevoked && (ExpiresAt is null || ExpiresAt > utcNow);
}
