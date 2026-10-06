using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Services.Booking;

/// <summary>
/// ICommissionService — ledger hợp nhất BOOKING|ORDER (D3, SRS §17).
/// Qualification MVP single rule (§17.1): COMPLETED + payment qualified + attribution valid.
/// Ledger entry IMMUTABLE sau finalized (§17.5): rule/tax snapshot không mutate; điều chỉnh = reversal entry mới.
/// Payout → WalletTransaction (Commission + Reversal — reuse).
/// </summary>
public interface ICommissionService
{
    /// <summary>
    /// Finalize commission cho booking COMPLETED (§17.1). Không qualified → null (KHÔNG tạo ledger entry).
    /// Idempotent: booking đã có ledger entry → trả entry hiện có (không double-create).
    /// </summary>
    Task<CommissionLedgerEntry?> FinalizeCommissionForBookingAsync(TenantId tenantId, Guid bookingId, CancellationToken ct = default);

    /// <summary>
    /// Reversal sau refund/cancel (§26.6, AC-Q03-Q05): tạo reversal entry mới (RelatedEntryId = original),
    /// original → Reversed. Nếu original đã PAID → WalletTransaction(Reversal, -net) hoàn lại. Idempotent.
    /// </summary>
    Task<CommissionLedgerEntry> ReverseCommissionAsync(TenantId tenantId, Guid ledgerEntryId, string? reason = null, CancellationToken ct = default);

    /// <summary>Payout EARNED → PAID: WalletTransaction(Commission, +net) + link WalletTransactionId.</summary>
    Task<CommissionLedgerEntry> PayCommissionAsync(TenantId tenantId, Guid ledgerEntryId, CancellationToken ct = default);

    /// <summary>Ledger read (P5 UI + salesman view §18.3 — filter salesmanId theo scope).</summary>
    Task<IReadOnlyList<CommissionLedgerEntry>> GetLedgerAsync(TenantId tenantId, Guid? salesmanId = null, Guid? bookingId = null, CancellationToken ct = default);
}
