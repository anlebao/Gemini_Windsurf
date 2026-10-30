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

    // ===================== SRS v1.2 W2/B — TENANT CHART (G-H2, M4) =====================

    /// <summary>
    /// Resolve 1 account theo tenant chart: tenant row (Override/Custom, IsActive + đã
    /// EffectiveFrom) thắng global row; tenant row deactivate → fallback global (Override)
    /// hoặc null (Custom đã tắt).
    /// </summary>
    Task<AccountChartEntry?> GetTenantAccountAsync(TenantId tenantId, string accountCode, AccountingStandard standard, CancellationToken ct = default);

    /// <summary>Full tenant chart = global standard chart + tenant overrides + tenant custom accounts (active).</summary>
    Task<List<AccountChartEntry>> GetTenantAccountsAsync(TenantId tenantId, AccountingStandard standard, CancellationToken ct = default);

    /// <summary>
    /// Thêm/cập nhật tenant account. Validation (FR-COA-005/006):
    /// - Source=Custom → ParentCode bắt buộc + tồn tại trong global chart (cùng standard)
    ///   hoặc tenant chart active; Type bắt buộc (không kế thừa).
    /// - Source=Override → AccountCode phải tồn tại trong global chart (cùng standard).
    /// Audit log mọi thay đổi.
    /// </summary>
    Task<VanAn.CoreHub.Infrastructure.Entities.TenantAccountChartEntity> UpsertTenantAccountAsync(
        TenantId tenantId, string accountCode, string accountName, string source,
        AccountingStandard standard, VanAn.Shared.Domain.AccountType? type = null,
        bool? isNormalCredit = null, string? parentCode = null, DateTime? effectiveFrom = null,
        CancellationToken ct = default);

    /// <summary>Deactivate tenant account (FR-COA-005: KHÔNG xóa — deactivate kể cả TK đã phát sinh; dừng dùng mới).</summary>
    Task DeactivateTenantAccountAsync(TenantId tenantId, string accountCode, CancellationToken ct = default);

    /// <summary>FR-COA-005: xóa TK luôn bị chặn — chỉ deactivate. Method tồn tại để reject rõ ràng.</summary>
    Task DeleteTenantAccountAsync(TenantId tenantId, string accountCode, CancellationToken ct = default);
}
