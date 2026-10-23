namespace VanAn.ShopERP.Services;

/// <summary>P7 — GET /v1/wallets/{tenantId}.</summary>
public sealed class MeteringWalletDto
{
    public Guid TenantId { get; set; }
    public decimal Balance { get; set; }
    public decimal BalancePurchased { get; set; }
    public decimal GrantedTotal { get; set; }
    public decimal RemainingFraction { get; set; }
    public string? AlertLevel { get; set; }   // "warn50" | "warn90" | "depleted" | null
    public DateTime UpdatedAtUtc { get; set; }
}

public sealed class MeteringLedgerEntryDto
{
    public Guid Id { get; set; }
    public string Type { get; set; } = string.Empty;      // Grant | Deduct | Adjust
    public decimal Credits { get; set; }                // signed
    public decimal BalanceAfter { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
    public Guid? UsageEventId { get; set; }
    public Guid? TopupRequestId { get; set; }
}

public sealed class MeteringLedgerPage
{
    public List<MeteringLedgerEntryDto> Items { get; set; } = new();
    public int Page { get; set; }
    public int PageSize { get; set; }
}

public sealed class MeteringTopupDto
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string ReferenceCode { get; set; } = string.Empty;
    public decimal? AmountVnd { get; set; }
    public decimal? CreditsGranted { get; set; }
    public string Status { get; set; } = string.Empty;    // pending | confirmed | rejected | expired
    public string? QrPayload { get; set; }              // vietqr.io image URL
    public string? Note { get; set; }
    public string? ConfirmedBy { get; set; }
    public DateTime? ConfirmedAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
}

public sealed class MeteringTopupList
{
    public List<MeteringTopupDto> Items { get; set; } = new();
}

public sealed class MeteringTopupConfigDto
{
    public string BankCode { get; set; } = string.Empty;
    public string BankName { get; set; } = string.Empty;
    public string AccountNumber { get; set; } = string.Empty;
    public string AccountName { get; set; } = string.Empty;
    public string UpdatedBy { get; set; } = string.Empty;
    public DateTime UpdatedAtUtc { get; set; }
}
