namespace VanAn.Atrs.Contracts;

/// <summary>Kết quả generate recovery codes — plaintext codes chỉ trả về ĐÚNG 1 lần (FR-008).</summary>
public sealed record GenerateRecoveryCodesResult(Guid SetId, IReadOnlyList<string> Codes, DateTime GeneratedAt);

/// <summary>Kết quả verify recovery code — reason: Success | InvalidCode | Locked | AlreadyUsed.
/// StepUpToken chỉ set khi Success (amr=recovery-code) — cho regenerate codes / quản lý thiết bị.</summary>
public sealed record VerifyRecoveryCodeResult(bool Success, string Reason, string? StepUpToken = null);

/// <summary>Trạng thái recovery codes cho UI (U05/U06) — KHÔNG trả code.</summary>
public sealed record RecoveryCodesStatusDto(
    bool HasActiveSet,
    int RemainingCount,
    DateTime? GeneratedAt,
    DateTime? LastRegeneratedAt);