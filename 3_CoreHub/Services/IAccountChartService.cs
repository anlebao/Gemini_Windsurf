using VanAn.Shared.Domain;
using DomainAccountType = VanAn.Shared.Domain.AccountType;

namespace VanAn.CoreHub.Services;

/// <summary>
/// W3: Account chart lookup service for VAS enterprise reports (TT 99/2025 + TT 133/2016 + TT 58/2026).
/// Returns immutable <see cref="AccountChartEntry"/> records (defined in Domain since W2).
/// Reference data — NOT tenant-scoped (shared chart of accounts per standard).
/// </summary>
public interface IAccountChartService
{
    /// <summary>Get account display name by code + standard. Returns fallback "Tài khoản {code}" if not found.</summary>
    Task<string> GetAccountNameAsync(string accountCode, AccountingStandard standard, CancellationToken ct = default);

    /// <summary>Get all accounts of a given type for a standard (e.g., all Asset accounts in TT 133).</summary>
    Task<List<AccountChartEntry>> GetAccountsByTypeAsync(DomainAccountType type, AccountingStandard standard, CancellationToken ct = default);

    /// <summary>Get the <see cref="DomainAccountType"/> classification for an account code under a standard.</summary>
    Task<DomainAccountType> GetAccountTypeAsync(string accountCode, AccountingStandard standard, CancellationToken ct = default);

    /// <summary>Get the full <see cref="AccountChartEntry"/> (includes IsNormalCredit flag for contra accounts).</summary>
    Task<AccountChartEntry?> GetAccountAsync(string accountCode, AccountingStandard standard, CancellationToken ct = default);

    /// <summary>Get all accounts for a standard (used by seeder verification + W4 report services).</summary>
    Task<List<AccountChartEntry>> GetAllAccountsAsync(AccountingStandard standard, CancellationToken ct = default);
}
