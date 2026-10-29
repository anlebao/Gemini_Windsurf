namespace VanAn.Metering.Contracts;

/// <summary>P7 — GET /v1/wallets/{tenantId}.</summary>
public sealed record WalletDto(
    Guid TenantId,
    decimal Balance,
    decimal BalancePurchased,
    decimal GrantedTotal,
    decimal RemainingFraction,
    string? AlertLevel,          // "warn50" | "warn90" | "depleted" | null — % còn lại (D4)
    DateTime UpdatedAtUtc);

/// <summary>P7 — GET /v1/wallets/{tenantId}/check?serviceCode=&estimate= (D3).
/// deny = Enforcement==Deny && balance < required. Callers cache 30s [G20].</summary>
public sealed record WalletCheckResponse(
    Guid TenantId,
    string ServiceCode,
    string? Enforcement,          // null when the code is unpriced (treated MeterOnly — D11)
    decimal Balance,
    decimal Required,             // rate × estimate units
    bool Deny,
    string? AlertLevel);

/// <summary>P7 D10 — POST /v1/wallets/{tenantId}/grants (admin).</summary>
public sealed record WalletGrantRequest(decimal Credits, string? Reason = null);

public sealed record LedgerEntryDto(
    Guid Id,
    string Type,                  // Grant | Deduct | Adjust
    decimal Credits,              // signed
    decimal BalanceAfter,
    string Reason,
    string CreatedBy,
    DateTime CreatedAtUtc,
    Guid? UsageEventId,
    Guid? TopupRequestId);

public sealed record LedgerListResponse(IReadOnlyList<LedgerEntryDto> Items, int Page, int PageSize);

// ---- Top-up config ----

public sealed record TopupConfigDto(
    string BankCode,
    string BankName,
    string AccountNumber,
    string AccountName,
    string UpdatedBy,
    DateTime UpdatedAtUtc);

public sealed record TopupConfigPutRequest(
    string BankCode,
    string BankName,
    string AccountNumber,
    string AccountName);

// ---- Issue #197: master switch toàn hệ thống ----

public sealed record EnforcementConfigDto(bool Enabled, string UpdatedBy, DateTime UpdatedAtUtc);

public sealed record EnforcementConfigPutRequest(bool Enabled);

// ---- Top-up requests ----

/// <summary>POST /v1/topups — tenant-side; caller key + Idempotency-Key dedupe.</summary>
public sealed record TopupCreateRequest(Guid TenantId, decimal? AmountVnd = null);

public sealed record TopupDto(
    Guid Id,
    Guid TenantId,
    string ReferenceCode,
    decimal? AmountVnd,
    decimal? CreditsGranted,
    string Status,               // pending | confirmed | rejected | expired
    string? QrPayload,           // vietqr.io URL (D7); null until topup-config is set
    string? Note,
    string? ConfirmedBy,
    DateTime? ConfirmedAtUtc,
    DateTime CreatedAtUtc,
    DateTime ExpiresAtUtc);

/// <summary>POST /v1/topups/{id}/confirm (admin) — credits do admin quyết (D8).</summary>
public sealed record TopupConfirmRequest(decimal Credits, string? Note = null);

public sealed record TopupRejectRequest(string? Reason = null);

public sealed record TopupListResponse(IReadOnlyList<TopupDto> Items);
