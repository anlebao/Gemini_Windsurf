using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Infrastructure.Entities;

/// <summary>
/// W3: EF persistence entity for the AccountCharts reference-data table.
/// NOT a domain aggregate — purely an Infrastructure persistence concern.
/// Maps to/from the immutable <see cref="AccountChartEntry"/> Domain record.
///
/// Standalone entity (does NOT inherit BaseEntity, does NOT implement IMustHaveTenant):
/// AccountCharts is global reference data shared across all tenants per standard.
/// This avoids the multi-tenancy query filter (see VanAnDbContext.ApplyMultiTenancyFilters —
/// only applies to IMustHaveTenant entities; precedent: ProcessedWebhookKey).
/// </summary>
public class AccountChartEntity
{
    public Guid Id { get; private set; } = Guid.NewGuid();

    /// <summary>Account code (e.g., "111", "511", "214").</summary>
    public string AccountCode { get; private set; } = string.Empty;

    /// <summary>Vietnamese display name (e.g., "Tiền mặt", "Doanh thu bán hàng").</summary>
    public string AccountName { get; private set; } = string.Empty;

    /// <summary>Classification: Asset / Liability / Equity / Revenue / Expense. Stored as int.</summary>
    public AccountType Type { get; private set; }

    /// <summary>Which accounting standard this account belongs to. Stored as int.</summary>
    public AccountingStandard Standard { get; private set; }

    /// <summary>
    /// Normal credit balance flag. TRUE for contra-asset accounts (214 Hao mòn TSCĐ, 229 Dự phòng)
    /// and all Liability/Equity/Revenue accounts. FALSE for 521 (contra-revenue, normal debit).
    /// W4 report services use this to flip debit/credit sign for normal-balance reporting.
    /// </summary>
    public bool IsNormalCredit { get; private set; }

    public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;

    private AccountChartEntity() { }

    public AccountChartEntity(string accountCode, string accountName, AccountType type, AccountingStandard standard, bool isNormalCredit)
    {
        if (string.IsNullOrWhiteSpace(accountCode))
            throw new ArgumentException("AccountCode is required.", nameof(accountCode));
        if (string.IsNullOrWhiteSpace(accountName))
            throw new ArgumentException("AccountName is required.", nameof(accountName));

        AccountCode = accountCode;
        AccountName = accountName;
        Type = type;
        Standard = standard;
        IsNormalCredit = isNormalCredit;
        CreatedAt = DateTime.UtcNow;
    }

    /// <summary>Map to immutable Domain record.</summary>
    public AccountChartEntry ToDomainRecord() => new(AccountCode, AccountName, Type, Standard, IsNormalCredit);

    /// <summary>Factory from Domain record (used by seeder).</summary>
    public static AccountChartEntity FromDomainRecord(AccountChartEntry entry) =>
        new(entry.AccountCode, entry.AccountName, entry.Type, entry.Standard, entry.IsNormalCredit);
}
