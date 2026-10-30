using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Infrastructure.Entities;

/// <summary>
/// SRS v1.2 W2/B (G-H2, M4 — approved 2026-10-31): COA per-tenant override layer trên
/// global <see cref="AccountChartEntity"/>. Persistence entity (KHÔNG domain aggregate —
/// precedent AccountChartEntity/EntryPostingStatusEntity).
///
/// - Source "Override": cùng AccountCode với global chart — ghi đè tên/IsActive (FR-COA-005:
///   không delete TK đã phát sinh — chỉ deactivate).
/// - Source "Custom": TK chi tiết mới của tenant — ParentCode BẮT BUỘC tồn tại trong
///   global chart (cùng Standard) hoặc tenant chart (service validate).
/// TenantId filter tường minh ở query (không IMustHaveTenant — giống EntryPostingStatusEntity).
/// </summary>
public class TenantAccountChartEntity
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid TenantId { get; private set; }
    public string AccountCode { get; private set; } = string.Empty;
    public string AccountName { get; private set; } = string.Empty;

    /// <summary>Null trên Override → kế thừa Type từ global row.</summary>
    public AccountType? Type { get; private set; }

    /// <summary>Null trên Override → kế thừa từ global row.</summary>
    public bool? IsNormalCredit { get; private set; }

    /// <summary>TK cha — bắt buộc với Source=Custom.</summary>
    public string? ParentCode { get; private set; }

    public bool IsActive { get; private set; } = true;
    public DateTime? EffectiveFrom { get; private set; }

    /// <summary>"Override" | "Custom".</summary>
    public string Source { get; private set; } = TenantAccountChartSources.Custom;

    public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; private set; } = DateTime.UtcNow;

    private TenantAccountChartEntity() { }

    public TenantAccountChartEntity(
        Guid tenantId,
        string accountCode,
        string accountName,
        string source,
        AccountType? type = null,
        bool? isNormalCredit = null,
        string? parentCode = null,
        DateTime? effectiveFrom = null)
    {
        if (string.IsNullOrWhiteSpace(accountCode))
            throw new ArgumentException("AccountCode is required.", nameof(accountCode));
        if (string.IsNullOrWhiteSpace(accountName))
            throw new ArgumentException("AccountName is required.", nameof(accountName));

        TenantId = tenantId;
        AccountCode = accountCode;
        AccountName = accountName;
        Source = source;
        Type = type;
        IsNormalCredit = isNormalCredit;
        ParentCode = parentCode;
        EffectiveFrom = effectiveFrom;
    }

    public void Update(string accountName, AccountType? type, bool? isNormalCredit, string? parentCode, DateTime? effectiveFrom)
    {
        if (string.IsNullOrWhiteSpace(accountName))
            throw new ArgumentException("AccountName is required.", nameof(accountName));
        AccountName = accountName;
        Type = type;
        IsNormalCredit = isNormalCredit;
        ParentCode = parentCode;
        EffectiveFrom = effectiveFrom;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Deactivate()
    {
        IsActive = false;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Reactivate()
    {
        IsActive = true;
        UpdatedAt = DateTime.UtcNow;
    }
}

public static class TenantAccountChartSources
{
    public const string Override = "Override";
    public const string Custom = "Custom";
}
