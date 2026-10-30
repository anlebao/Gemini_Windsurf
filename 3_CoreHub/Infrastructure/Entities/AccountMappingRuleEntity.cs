namespace VanAn.CoreHub.Infrastructure.Entities;

/// <summary>
/// SRS v1.2 W2/B (G-H1, M5 — approved 2026-10-31): mapping sự kiện kế toán → cặp TK Nợ/Có
/// bằng DATA (thay hardcode). Persistence entity — KHÔNG domain aggregate.
///
/// Precedence (AccountMapResolver): tenant rule (TenantId set) &gt; regime rule
/// (TenantId null + Regime match) &gt; builtin hardcode fallback.
///
/// DebitAccount/CreditAccount hỗ trợ placeholder:
///   "{src}"  = account hint từ nghiệp vụ (entry.AccountCode — vd 511/512/632/611).
///   "{cash}" = TK tiền (entry.CashAccountCode, default "111" — Phase D mở 112x).
/// Regime values: "TT152" (HKD) · "TT58" (DNSN) · "TT133" · "TT99" · "TT71" (HTX).
/// </summary>
public class AccountMappingRuleEntity
{
    public Guid Id { get; private set; } = Guid.NewGuid();

    /// <summary>NULL = rule global theo Regime; set = tenant override.</summary>
    public Guid? TenantId { get; private set; }

    /// <summary>Scope regime khi TenantId null — null = áp mọi regime.</summary>
    public string? Regime { get; private set; }

    /// <summary>Event code: manual-receipt · manual-expense · deposit-offset · order-revenue
    /// · order-vat · order-cogs · receivable · receivable-payment · payable · payable-payment
    /// · transfer · closing-* (dự phòng Phase C-F).</summary>
    public string EventCode { get; private set; } = string.Empty;

    public string? DebitAccount { get; private set; }
    public string? CreditAccount { get; private set; }

    /// <summary>Semantic số tiền: amount/net/vat/cogs (nền cho B2 rule engine — hiện "amount").</summary>
    public string? AmountKind { get; private set; }

    public DateTime? EffectiveFrom { get; private set; }
    public int Version { get; private set; } = 1;
    public bool IsActive { get; private set; } = true;

    public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; private set; } = DateTime.UtcNow;

    private AccountMappingRuleEntity() { }

    public AccountMappingRuleEntity(
        string eventCode,
        string? debitAccount,
        string? creditAccount,
        Guid? tenantId = null,
        string? regime = null,
        string? amountKind = "amount",
        int version = 1,
        DateTime? effectiveFrom = null)
    {
        if (string.IsNullOrWhiteSpace(eventCode))
            throw new ArgumentException("EventCode is required.", nameof(eventCode));

        EventCode = eventCode;
        DebitAccount = debitAccount;
        CreditAccount = creditAccount;
        TenantId = tenantId;
        Regime = regime;
        AmountKind = amountKind;
        Version = version;
        EffectiveFrom = effectiveFrom;
    }

    public void Update(string? debitAccount, string? creditAccount, string? amountKind, DateTime? effectiveFrom)
    {
        DebitAccount = debitAccount;
        CreditAccount = creditAccount;
        AmountKind = amountKind;
        EffectiveFrom = effectiveFrom;
        Version++;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Deactivate()
    {
        IsActive = false;
        UpdatedAt = DateTime.UtcNow;
    }
}
