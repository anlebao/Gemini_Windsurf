using VanAn.CoreHub.Services;
using VanAn.Shared.Domain;

namespace VanAn.ShopERP.Services.Accounting;

/// <summary>
/// Regime-lock (2026-10-30): kết quả resolve chuẩn kế toán hiệu lực của tenant
/// (Tenant.Type + Tenant.AccountingStandard override → AccountingStandardRules).
/// Allowed rỗng → BCTC doanh nghiệp không áp dụng (HKD chưa bầu chọn TT 58).
/// </summary>
public record AccountingStandardSelection(
    TenantType? TenantType,
    AccountingStandard? Effective,
    IReadOnlyList<AccountingStandard> Allowed,
    bool IsVoluntary,
    bool HasBctc);

public interface IAccountingStandardSelectionService
{
    Task<AccountingStandardSelection> GetAsync(TenantId tenantId, CancellationToken ct = default);
}

/// <summary>
/// Gộp 1 lần gọi: GetTenantTypeAsync (ưu tiên PG) + Tenant.AccountingStandard override
/// (SQLite mirror — đồng bộ qua NATS tenant.type.changed) → AccountingStandardRules.
/// Dùng bởi 5 trang BCTC + AccountingIndex + FinancialReports hub.
/// </summary>
public class AccountingStandardSelectionService(
    IVasFeatureFlagService featureFlagService,
    ITenantManagementService tenantService) : IAccountingStandardSelectionService
{
    public async Task<AccountingStandardSelection> GetAsync(TenantId tenantId, CancellationToken ct = default)
    {
        var type = await featureFlagService.GetTenantTypeAsync(tenantId, ct).ConfigureAwait(false);
        var tenant = await tenantService.GetTenantByIdAsync(tenantId, ct).ConfigureAwait(false);
        var ovr = tenant?.AccountingStandard;

        var allowed = AccountingStandardRules.AllowedStandards(type, ovr);
        var effective = AccountingStandardRules.ResolveEffective(type, ovr);
        var voluntary = effective.HasValue && AccountingStandardRules.IsVoluntary(type, effective.Value);

        return new AccountingStandardSelection(type, effective, allowed, voluntary, effective.HasValue);
    }
}
