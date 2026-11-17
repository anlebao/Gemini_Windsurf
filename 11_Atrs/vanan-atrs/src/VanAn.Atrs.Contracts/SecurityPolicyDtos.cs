namespace VanAn.Atrs.Contracts;

/// <summary>SecurityPolicy DTO (A21) — version + status + proposer/approver + value.</summary>
public sealed record SecurityPolicyDto(
    Guid Id,
    string PolicyKey,
    string PolicyGroup,
    string JsonValue,
    int Version,
    string Status,                 // SecurityPolicyStatus
    Guid ProposedBy,
    Guid? ApprovedBy,
    DateTime ProposedAt,
    DateTime? EffectiveAt,
    DateTime? RejectedAt,
    string? ChangeNote);

/// <summary>Propose thay đổi policy (A21) — version tự tăng + maker-checker.</summary>
public sealed record ProposePolicyChangeRequest(string JsonValue, string? ChangeNote);