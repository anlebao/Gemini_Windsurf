using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Services;

/// <summary>
/// Loyalty Points Integrity (Batch 2, Phase 1) — PG ledger = single source of truth for
/// EVERY loyalty WRITE (award / spend / refund / reversal).
///
/// Architecture (approved plan `docs/plans/loyalty-integrity-detail-coding-plan.md`):
///   - Gateway hosts the real implementation (PG). OrderWorkflowService (in-proc) and the
///     ShopERP POS HTTP proxy route every write through this service.
///   - Centralized double-award guard BY ORDER (RC3): LoyaltyIssuanceRecord (PG) per order —
///     not the old per-DB history-JSON check.
///   - Tenant attribution (RC2.4): Silo rows are per (customer, tenant); Alliance REDEEM entries
///     carry SourceTenantId (set in Batch 5 FIFO attribution).
///   - "Luật Silo": a customer can only spend points at the tenant that awarded them.
///   - Budget caps run on every award path (defense-in-depth in the write services too).
///
/// Emergency rollback: feature flag "LoyaltyLedgerV2" (default ON) — when OFF every write is
/// skipped/rejected with a clear message (deploy the previous version to restore old behavior).
/// </summary>
public interface ILoyaltyPointLedgerService
{
    /// <summary>
    /// Award points (order-completion, mission, welcome, refund-return). Budget check → mode
    /// routing (Silo row / Alliance wallet) → issuance record (PG, per order) → mirror sync.
    /// </summary>
    Task<LedgerResult> AwardAsync(AwardRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Spend points (redemption). Silo: only the row of the REDEEMING tenant ("luật Silo").
    /// Alliance: wallet deduct (FIFO SourceTenantId attribution lands in Batch 5).
    /// </summary>
    Task<LedgerResult> SpendAsync(SpendRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Refund points (redemption cancel) — Silo row / Alliance wallet (returns to redeeming tenant).
    /// </summary>
    Task<LedgerResult> RefundAsync(RefundRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reverse every non-reversed LoyaltyIssuanceRecord of an order (mode-aware). Marks records
    /// reversed regardless of whether the points could be physically returned (already spent).
    /// Returns the total points actually reversed (caller decrements budget counters).
    /// </summary>
    Task<int> RevertOrderAsync(Guid orderId, TenantId tenantId, string reason, CancellationToken cancellationToken = default);

    /// <summary>
    /// Points actually awarded for an order (PG issuance record) — banner reads the REAL number
    /// (fix RC2.2, decision D4). Null when the order was never awarded.
    /// </summary>
    Task<int?> GetAwardedPointsAsync(Guid orderId, Guid tenantId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Effective balance for a customer at a tenant (Silo row at (customer, tenant) / Alliance wallet).
    /// </summary>
    Task<int> GetBalanceAsync(Guid customerId, Guid tenantId, CancellationToken cancellationToken = default);
}

// === Request / result DTOs ===

public enum LedgerOperationStatus
{
    /// <summary>Write happened (or idempotent replay returning the cached state).</summary>
    Success = 0,
    /// <summary>Intentional no-op (duplicate order guard, budget exhausted, flag off) — not an error.</summary>
    Skipped = 1,
    /// <summary>User-facing rejection (insufficient balance / Silo law violation).</summary>
    Rejected = 2,
    /// <summary>Infrastructure failure.</summary>
    Error = 3
}

public sealed record LedgerResult(bool Success, int NewBalance, string? Error, LedgerOperationStatus Status)
{
    public static LedgerResult Ok(int newBalance) => new(true, newBalance, null, LedgerOperationStatus.Success);
    public static LedgerResult Skipped(string reason) => new(true, 0, reason, LedgerOperationStatus.Skipped);
    public static LedgerResult Rejected(string error) => new(false, 0, error, LedgerOperationStatus.Rejected);
    public static LedgerResult Failure(string error) => new(false, 0, error, LedgerOperationStatus.Error);
}

public class AwardRequest
{
    public Guid CustomerId { get; set; }
    public Guid TenantId { get; set; }
    public int Points { get; set; }
    public string Reason { get; set; } = string.Empty;

    /// <summary>Order that triggered the award — the centralized double-award guard key (RC3).</summary>
    public Guid? SourceOrderId { get; set; }

    /// <summary>Device identity (Alliance wallet lookup — falls back to customer.DeviceId / CustomerId).</summary>
    public Guid? CustomerDeviceId { get; set; }

    /// <summary>Order total for the per-order rate budget cap. Null (non-order awards) skips it.</summary>
    public decimal? OrderAmount { get; set; }

    /// <summary>Retry-safe key (e.g. "earn:{orderId}"). Auto-generated when null.</summary>
    public string? IdempotencyKey { get; set; }
}

public class SpendRequest
{
    public Guid CustomerId { get; set; }
    public Guid TenantId { get; set; }
    public int Points { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string? VoucherCode { get; set; }
    public string? IdempotencyKey { get; set; }
}

public class RefundRequest
{
    public Guid CustomerId { get; set; }
    public Guid TenantId { get; set; }
    public int Points { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string? VoucherCode { get; set; }
    public string? IdempotencyKey { get; set; }
}
