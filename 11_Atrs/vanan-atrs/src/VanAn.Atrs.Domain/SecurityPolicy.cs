namespace VanAn.Atrs.Domain;

/// <summary>Trạng thái SecurityPolicy (A21) — versioned, approval flow.</summary>
public enum SecurityPolicyStatus
{
    /// <summary>Đang có hiệu lực — version hiện tại.</summary>
    EFFECTIVE = 0,
    /// <summary>Đề xuất thay đổi — chờ approver (maker-checker).</summary>
    PROPOSED = 1,
    /// <summary>Bị từ chối (giữ lại để audit).</summary>
    REJECTED = 2,
    /// <summary>Version cũ đã bị thay thế (history).</summary>
    SUPERSEDED = 3
}

/// <summary>Nhóm policy (A21 §2.3) — hiển thị + quyền truy cập.</summary>
public static class SecurityPolicyGroups
{
    public const string Passkey = "passkey";
    public const string Recovery = "recovery";
    public const string Dispute = "dispute";
    public const string MakerChecker = "maker_checker";
    public const string Sla = "sla";
    public const string Risk = "risk"; // fraud thresholds — KHÔNG lộ cho unauthorized
}

/// <summary>
/// SecurityPolicy (A21, FR-014) — policy versioned, approval flow.
/// Mỗi lần thay đổi = row mới PROPOSED (Version+1) → approve → EFFECTIVE
/// (version cũ → SUPERSEDED) | reject → REJECTED. Bảng này tự là audit trail
/// (ProposedBy/ApprovedBy/ProposedAt/EffectiveAt — KHÔNG cần AccountSecurityEvent
/// vì policy là system-level, không có TrustSubject).
/// JSON value — từng policy quyết định schema; service parse + fallback an toàn.
/// </summary>
public sealed class SecurityPolicy
{
    public Guid Id { get; private set; }
    /// <summary>Key duy nhất, vd "recovery.sla_hours".</summary>
    public string PolicyKey { get; private set; } = string.Empty;
    public string PolicyGroup { get; private set; } = string.Empty;
    public string JsonValue { get; private set; } = string.Empty;
    public int Version { get; private set; }
    public SecurityPolicyStatus Status { get; private set; }
    public Guid ProposedBy { get; private set; }
    public Guid? ApprovedBy { get; private set; }
    public DateTime ProposedAt { get; private set; }
    public DateTime? EffectiveAt { get; private set; }
    public DateTime? RejectedAt { get; private set; }
    /// <summary>Human-readable mô tả thay đổi (audit + UI).</summary>
    public string ChangeNote { get; private set; } = string.Empty;

    private SecurityPolicy() { }

    public SecurityPolicy(Guid id, string policyKey, string policyGroup, string jsonValue, int version, Guid proposedBy, string changeNote)
    {
        if (id == Guid.Empty) throw new ArgumentException("Id cannot be empty.", nameof(id));
        if (string.IsNullOrWhiteSpace(policyKey)) throw new ArgumentException("PolicyKey cannot be empty.", nameof(policyKey));
        if (string.IsNullOrWhiteSpace(policyGroup)) throw new ArgumentException("PolicyGroup cannot be empty.", nameof(policyGroup));
        if (string.IsNullOrWhiteSpace(jsonValue)) throw new ArgumentException("JsonValue cannot be empty.", nameof(jsonValue));
        if (version < 1) throw new ArgumentException("Version must be >= 1.", nameof(version));
        if (proposedBy == Guid.Empty) throw new ArgumentException("ProposedBy cannot be empty.", nameof(proposedBy));

        Id = id;
        PolicyKey = policyKey;
        PolicyGroup = policyGroup;
        JsonValue = jsonValue;
        Version = version;
        Status = version == 1 ? SecurityPolicyStatus.EFFECTIVE : SecurityPolicyStatus.PROPOSED;
        ProposedBy = proposedBy;
        ProposedAt = DateTime.UtcNow;
        ChangeNote = (changeNote ?? string.Empty).Trim();
    }

    public void Approve(Guid approverId)
    {
        if (Status != SecurityPolicyStatus.PROPOSED)
            throw new InvalidOperationException($"Cannot approve policy in status {Status}.");
        Status = SecurityPolicyStatus.EFFECTIVE;
        ApprovedBy = approverId;
        EffectiveAt = DateTime.UtcNow;
    }

    public void Reject(Guid approverId)
    {
        if (Status != SecurityPolicyStatus.PROPOSED)
            throw new InvalidOperationException($"Cannot reject policy in status {Status}.");
        Status = SecurityPolicyStatus.REJECTED;
        ApprovedBy = approverId;
        RejectedAt = DateTime.UtcNow;
    }

    public void Supersede()
    {
        if (Status != SecurityPolicyStatus.EFFECTIVE)
            throw new InvalidOperationException($"Cannot supersede policy in status {Status}.");
        Status = SecurityPolicyStatus.SUPERSEDED;
    }
}

/// <summary>Key policy được hệ thống đọc (wired) — v1.</summary>
public static class SecurityPolicyKeys
{
    /// <summary>SLA recovery (giờ) — RecoveryCaseManager đọc effective, fallback 24.</summary>
    public const string RecoverySlaHours = "recovery.sla_hours";
    /// <summary>Maker-checker bắt buộc cho dispute decision (bool) — DisputeCaseManager đọc, fallback true.</summary>
    public const string DisputeMakerCheckerRequired = "dispute.maker_checker_required";
}