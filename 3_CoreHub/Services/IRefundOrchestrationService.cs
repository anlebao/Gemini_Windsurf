using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Services;

/// <summary>
/// VALCN v2.0 Phase 4 — Refund Orchestration (UC-06, INV-002).
/// Coordinates full 4-step reversal when an order is cancelled or refunded:
///   2a. Payment refund / accrual liability entry (Cash = Accounting — TT 152/2025 compliance)
///   2b. Accounting reversal (AccountingEntry.CreateReversal, preserve CorrelationId)
///   2c. Loyalty reversal (LoyaltyIssuanceRecord.MarkReversed + budget decrement)
///   2d. Referral commission reversal (WalletService.ReverseTransactionAsync)
/// Idempotent via natural idempotency: checks if reversal entries already exist for the CorrelationId.
/// Full refund/cancel only — no partial refund (fix M2).
/// Feature-flagged via ValcnV2_RefundReversal (default OFF — existing silent-cancel behavior).
/// </summary>
public interface IRefundOrchestrationService
{
    /// <summary>
    /// Orchestrate FULL reversal (UC-06 — 4 steps) when an order is cancelled or refunded.
    /// </summary>
    /// <param name="orderId">The order being cancelled/refunded.</param>
    /// <param name="tenantId">Tenant that owns the order.</param>
    /// <param name="reason">Human-readable reason for the reversal (audit trail).</param>
    /// <param name="ct">Cancellation token.</param>
    Task OrchestrateReversalAsync(Guid orderId, TenantId tenantId, string reason, CancellationToken ct = default);
}
