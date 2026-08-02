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

    /// <summary>
    /// Loyalty Alliance Phase 4: Silo→Alliance migration — consolidate per-tenant SQLite
    /// LoyaltyRewards balances into cross-tenant PG AllianceWallets.
    /// Caller provides customer balances from ShopERP SQLite (AllianceWalletService is PG-only
    /// and cannot query per-tenant SQLite). For each customer: creates/credits AllianceWallet +
    /// appends AllianceTransaction(ADJUST). Idempotent: skips customers with existing ADJUST
    /// migration transaction for this tenant.
    /// </summary>
    Task<MigrationResult> ConsolidateWalletsAsync(
        Guid tenantId,
        IReadOnlyList<CustomerBalanceInput> customerBalances,
        string changedBy);

    /// <summary>
    /// Loyalty Alliance Phase 4: Alliance→Silo migration — split cross-tenant PG AllianceWallet
    /// balances back to per-tenant SQLite LoyaltyRewards. Calculates net EARN per-tenant from
    /// AllianceTransaction log, distributes TotalPointBalance proportionally. Edge case: tenants
    /// with net EARN ≤ 0 get no allocation. Freezes wallet after split. Returns allocations so
    /// caller can update ShopERP SQLite LoyaltyRewards.PointBalance.
    /// </summary>
    Task<MigrationResult> SplitWalletsAsync(Guid tenantId, string changedBy);
}

/// <summary>Input for Silo→Alliance consolidation: customer device + balance from SQLite.</summary>
public record CustomerBalanceInput(Guid CustomerDeviceId, int PointBalance, string? PhoneNumber);

/// <summary>Result of a mode switch migration (consolidate or split).</summary>
public class MigrationResult
{
    public int CustomersProcessed { get; set; }
    public int TotalPointsTransferred { get; set; }
    public List<WalletAllocation> Allocations { get; set; } = new();
    public string? Error { get; set; }
    public bool Success => Error is null;
}

/// <summary>Per-tenant allocation from Alliance→Silo split.</summary>
public record WalletAllocation(Guid CustomerDeviceId, Guid TenantId, int Points);
