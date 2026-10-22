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
    /// Loyalty Consistency Fix Phase 0: idempotencyKey enables retry-safe HTTP proxy calls.
    /// Loyalty Points Integrity (BUG-1 fix): <paramref name="customerId"/> is the PG customer PK
    /// (Guid.Empty when unknown — e.g. raw internal API calls). It is carried into the PG→SQLite
    /// sync event so LoyaltySyncSubscriber can bootstrap the local mirror stub with the SAME
    /// customer identity as PG (device-based stub would mismatch POS customers).
    /// </summary>
    Task<(bool Success, int NewBalance, string? Error)> AddPointsAsync(
        Guid customerDeviceId, Guid tenantId, int points, string reason, Guid? sourceOrderId = null,
        string? idempotencyKey = null, Guid customerId = default);

    /// <summary>
    /// Deduct (redeem) points from the wallet at the given tenant.
    /// Enforces sufficient balance. Returns (success, newBalance, error).
    /// Loyalty Consistency Fix Phase 0: idempotencyKey enables retry-safe HTTP proxy calls.
    /// <paramref name="customerId"/>: see AddPointsAsync (BUG-1 fix — propagated to the sync event).
    /// </summary>
    Task<(bool Success, int NewBalance, string? Error)> DeductPointsAsync(
        Guid customerDeviceId, Guid tenantId, int points, string reason, string? voucherCode = null,
        string? idempotencyKey = null, Guid customerId = default);

    /// <summary>
    /// Refund points back to the wallet. Per Q4, refund is attributed to the tenant
    /// where the redeem occurred (passed as <paramref name="tenantId"/>).
    /// Returns (success, newBalance, error).
    /// Loyalty Consistency Fix Phase 0: idempotencyKey enables retry-safe HTTP proxy calls.
    /// <paramref name="customerId"/>: see AddPointsAsync (BUG-1 fix — propagated to the sync event).
    /// </summary>
    Task<(bool Success, int NewBalance, string? Error)> RefundAsync(
        Guid customerDeviceId, Guid tenantId, int points, string reason, string voucherCode,
        string? idempotencyKey = null, Guid customerId = default);

    /// <summary>Return recent transactions for a wallet, newest first.</summary>
    Task<IReadOnlyList<AllianceTransaction>> GetTransactionsAsync(Guid walletId, int limit = 20);

    /// <summary>Return recent transactions for a wallet filtered to a specific tenant.</summary>
    Task<IReadOnlyList<AllianceTransaction>> GetTransactionsByTenantAsync(Guid walletId, Guid tenantId, int limit = 20);

    /// <summary>
    /// Loyalty Points Integrity (Batch 5, T5.1): attribution-correct per-tenant net balance for a wallet.
    /// net = Σ EARN/ADJUST (by TransactionTenantId) − Σ |REDEEM| (by SourceTenantId — the tenant that
    /// OWNS the consumed points; legacy rows without SourceTenantId fall back to TransactionTenantId).
    /// Sum over tenants == AllianceWallet.TotalPointBalance. Used for the customer wallet breakdown
    /// (GET /api/loyalty/wallet) so cross-tenant redemptions charge the correct tenant.
    /// </summary>
    Task<IReadOnlyList<WalletTenantBalance>> GetTenantBalancesAsync(Guid walletId);

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

    /// <summary>
    /// P1.3 (audit §6 Q6): manual balance adjustment by SystemAdmin — writes ONE
    /// <see cref="AllianceTransactionType.ADJUST"/> row so earn/redeem settlement math is not
    /// distorted. <paramref name="points"/> is SIGNED (positive credits, negative debits).
    /// Negative adjusts require balance ≥ |points|. Positive adjusts respect MaxWalletPoints
    /// cap but BYPASS tenant budget counters (admin op, same as Consolidate/Split ADJUST rows).
    /// Idempotent via <paramref name="idempotencyKey"/>.
    /// </summary>
    Task<(bool Success, int NewBalance, string? Error)> AdjustPointsAsync(
        Guid customerDeviceId, Guid tenantId, int points, string reason,
        string? idempotencyKey = null, Guid customerId = default);

    /// <summary>
    /// P1.1 (audit §6 Q3, settle-then-burn): when a member tenant leaves the alliance, burn every
    /// customer's points still attributed to that tenant (per ComputeNetEarnByTenantAsync).
    /// Guard: the tenant MUST no longer be an alliance member. Idempotent per wallet
    /// (key "exit:{tenantId}:{walletId}"). Returns MigrationResult (CustomersProcessed = wallets
    /// burned, TotalPointsTransferred = points burned). Trigger AFTER off-system money settlement.
    /// </summary>
    Task<MigrationResult> ExitMemberSettlementAsync(Guid tenantId, string settlementNote, string changedBy);

    /// <summary>
    /// P1.2 (audit §6 Q4): read-only cross-tenant settlement report — per tenant:
    /// EarnCredit (Σ EARN at tenant), RedeemCost (Σ |REDEEM| at tenant), AdjustNet (Σ ADJUST at
    /// tenant), NetSettlement = EarnCredit − RedeemCost + AdjustNet. Optional UTC date range.
    /// Settlement money moves OFF-SYSTEM between HTX and members — this is a report only.
    /// </summary>
    Task<IReadOnlyList<AllianceSettlementRow>> GetSettlementReportAsync(DateTime? fromUtc, DateTime? toUtc);
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

/// <summary>
/// Loyalty Points Integrity (Batch 5, T5.1): net point balance attributed to a tenant in an
/// Alliance wallet. See IAllianceWalletService.GetTenantBalancesAsync.
/// </summary>
public record WalletTenantBalance(Guid TenantId, int NetPoints);

/// <summary>
/// P1.2 (audit §6 Q4): one row of the cross-tenant settlement report.
/// NetSettlement = EarnCredit − RedeemCost + AdjustNet; positive means the member issued more
/// credit than it absorbed in redemptions (HTX owes the member), negative means the member
/// absorbed more redemptions than it issued (member owes HTX).
/// </summary>
public record AllianceSettlementRow(
    Guid TenantId,
    string TenantName,
    int EarnCredit,
    int RedeemCost,
    int AdjustNet,
    int NetSettlement);
