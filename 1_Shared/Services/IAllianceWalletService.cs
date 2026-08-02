using VanAn.Shared.Domain;

namespace VanAn.Shared.Services;

/// <summary>
/// Cross-tenant alliance wallet operations. PG-only (AllianceWallet + AllianceTransaction).
/// Every mutation appends an AllianceTransaction row (immutable audit log) and publishes
/// a NATS event so ShopERP can sync local LoyaltyRewards.PointBalance.
/// Spec: docs/specs/loyalty-alliance-spec.md v1.0.
/// </summary>
public interface IAllianceWalletService
{
    /// <summary>Look up a wallet by customer device id. Null if not found.</summary>
    Task<AllianceWallet?> GetWalletByDeviceIdAsync(Guid customerDeviceId);

    /// <summary>Return the wallet for the device, creating it if it does not exist.</summary>
    Task<AllianceWallet> GetOrCreateWalletAsync(Guid customerDeviceId, string? phoneNumber);

    /// <summary>
    /// Add (earn) points to the wallet at the given tenant.
    /// Enforces MaxWalletPoints cap. Returns (success, newBalance, error).
    /// </summary>
    Task<(bool Success, int NewBalance, string? Error)> AddPointsAsync(
        Guid customerDeviceId, Guid tenantId, int points, string reason, Guid? sourceOrderId = null);

    /// <summary>
    /// Deduct (redeem) points from the wallet at the given tenant.
    /// Enforces sufficient balance. Returns (success, newBalance, error).
    /// </summary>
    Task<(bool Success, int NewBalance, string? Error)> DeductPointsAsync(
        Guid customerDeviceId, Guid tenantId, int points, string reason, string? voucherCode = null);

    /// <summary>
    /// Refund points back to the wallet. Per Q4, refund is attributed to the tenant
    /// where the redeem occurred (passed as <paramref name="tenantId"/>).
    /// Returns (success, newBalance, error).
    /// </summary>
    Task<(bool Success, int NewBalance, string? Error)> RefundAsync(
        Guid customerDeviceId, Guid tenantId, int points, string reason, string voucherCode);

    /// <summary>Return recent transactions for a wallet, newest first.</summary>
    Task<IReadOnlyList<AllianceTransaction>> GetTransactionsAsync(Guid walletId, int limit = 20);

    /// <summary>Return recent transactions for a wallet filtered to a specific tenant.</summary>
    Task<IReadOnlyList<AllianceTransaction>> GetTransactionsByTenantAsync(Guid walletId, Guid tenantId, int limit = 20);
}
