namespace VanAn.Metering.Contracts;

/// <summary>P10 — one credit lot inside WalletDto.Lots.</summary>
public sealed record CreditLotDto(
    Guid Id,
    decimal CreditsGranted,
    decimal CreditsRemaining,
    DateTime? ExpiresAtUtc,
    Guid? TopupRequestId);

/// <summary>P7 — GET /v1/wallets/{tenantId}.</summary>
public sealed record WalletDto(
    Guid TenantId,
    decimal Balance,
    decimal BalancePurchased,
    decimal GrantedTotal,
    decimal RemainingFraction,
    string? AlertLevel,          // "warn50" | "warn90" | "depleted" | null — % còn lại (D4)
    DateTime UpdatedAtUtc,
    IReadOnlyList<CreditLotDto>? Lots = null); // P10 — spendable lots (FEFO view)

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
    DateTime UpdatedAtUtc,
    decimal VndPerCredit, // P8 — VND per 1 credit; 0 = auto-confirm OFF
    bool VaPollEnabled = true,       // P12 — VA transaction polling fallback
    int VaPollIntervalMinutes = 2);  // P12 — poll interval (sysadmin-set)

public sealed record TopupConfigPutRequest(
    string BankCode,
    string BankName,
    string AccountNumber,
    string AccountName,
    decimal? VndPerCredit = null,   // null = keep current value
    bool? VaPollEnabled = null,      // P12 — null = keep current value
    int? VaPollIntervalMinutes = null); // P12 — null = keep current value

// ---- Issue #197: master switch toàn hệ thống ----

public sealed record EnforcementConfigDto(bool Enabled, string UpdatedBy, DateTime UpdatedAtUtc);

public sealed record EnforcementConfigPutRequest(bool Enabled);

// ---- Top-up requests ----

/// <summary>POST /v1/topups — tenant-side; caller key + Idempotency-Key dedupe.
/// P11 — PackageCode selects a sysadmin-defined package (price/credits/validity
/// come from the catalog; amountVnd is ignored when set).</summary>
public sealed record TopupCreateRequest(Guid TenantId, decimal? AmountVnd = null, string? PackageCode = null);

/// <summary>P11 — GET /v1/packages — active package catalog for tenants.</summary>
public sealed record PackageDto(
    string Code, string Name, decimal PriceVnd, decimal Credits, int ValidityDays, int SortOrder);

/// <summary>P11 — admin upsert body (sysadmin only).</summary>
public sealed record PackageUpsertRequest(
    string Name, decimal PriceVnd, decimal Credits, int ValidityDays,
    int SortOrder = 0, bool IsActive = true);

public sealed record TopupDto(
    Guid Id,
    Guid TenantId,
    string ReferenceCode,
    decimal? AmountVnd,
    decimal? CreditsGranted,
    string Status,               // pending | confirmed | rejected | expired
    string? QrPayload,           // vietqr.io URL (D7); null until topup-config is set
    string? PackageCode,         // P11 — set when created from a package
    string? Note,
    string? ConfirmedBy,
    DateTime? ConfirmedAtUtc,
    DateTime CreatedAtUtc,
    DateTime ExpiresAtUtc);

/// <summary>POST /v1/topups/{id}/confirm (admin) — credits do admin quyết (D8).</summary>
public sealed record TopupConfirmRequest(decimal Credits, string? Note = null);

public sealed record TopupRejectRequest(string? Reason = null);

/// <summary>P8 — POST /v1/topups/{id}/pay → Tingee checkout URL (fresh link, expire ≤30 min).</summary>
public sealed record TopupPayResponse(
    string CheckoutUrl,
    string OrderId,
    decimal? AmountVnd,
    int ExpiresInMinute);

/// <summary>P9 — POST /v1/topups/{id}/qr → Tingee dynamic QR payload (EMV VietQR string).</summary>
public sealed record TopupQrResponse(
    string QrPayload,
    string BillId,
    string? QrAccount,
    int ExpiresInMinute);

public sealed record TopupListResponse(IReadOnlyList<TopupDto> Items);
