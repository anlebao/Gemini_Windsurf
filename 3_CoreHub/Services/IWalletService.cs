using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Services
{
    /// <summary>
    /// IWalletService — wallet operations for Community Commerce (v1.4).
    /// Sprint 0: Base atomic CreateTransactionAsync (HR-SCALE-3 SELECT FOR UPDATE).
    /// Sprint 5 extends with ConfirmCodAsync/ConfirmAdvanceAsync/ConfirmAdvanceReceivedAsync/ReverseTransactionAsync.
    /// Sprint 4 CoolingPeriodJob uses CreateTransactionAsync to pay commissions after 24h cooling.
    /// </summary>
    public interface IWalletService
    {
        /// <summary>
        /// v1.4: Base atomic method — creates WalletTransaction with correct BalanceAfter.
        /// Uses SELECT FOR UPDATE pattern (HR-SCALE-3) to prevent race conditions on concurrent creates.
        /// Dùng bởi Sprint 4 CoolingPeriodJob + Sprint 5 full WalletService.
        /// </summary>
        Task<WalletTransaction> CreateTransactionAsync(
            Guid ownerId,
            WalletTransactionType type,
            decimal amount,
            string description,
            Guid? relatedOrderId = null,
            Guid? relatedTransactionId = null);

        /// <summary>
        /// Get current balance for an owner (last transaction's BalanceAfter, or 0 if no transactions).
        /// </summary>
        Task<decimal> GetBalanceAsync(Guid ownerId);

        /// <summary>
        /// Sprint 5: Get wallet summary — balance + transaction history sorted by CreatedAt desc.
        /// </summary>
        Task<WalletSummaryDto> GetWalletAsync(Guid ownerId);

        /// <summary>
        /// Sprint 5: Shipper confirms COD collection for an order.
        /// Creates WalletTransaction(CODCollection, +amount) for shipper + WalletTransaction(Settlement, -amount) for shop.
        /// Sets Order.CodCollectedAt via Order.MarkCodCollected(). Idempotency: throws if already collected.
        /// TC-06: MarkCodCollected also marks the order Paid (method COD) and triggers
        /// accounting (OrderPaymentConfirmed outbox → ShopERP replica + local bookset entries).
        /// </summary>
        Task<WalletTransaction> ConfirmCodAsync(Guid shipperId, Guid orderId, decimal amount);

        /// <summary>
        /// Sprint 5: Shipper confirms advance payment to shop (shipper paid cash to shop before pickup).
        /// Creates WalletTransaction(AdvancePayment, -amount) for shipper. Pending shop confirmation.
        /// Shop confirms via ConfirmAdvanceReceivedAsync.
        /// </summary>
        Task<WalletTransaction> ConfirmAdvanceAsync(Guid shipperId, Guid orderId, decimal amount);

        /// <summary>
        /// Sprint 5: Shop confirms they received advance payment from shipper.
        /// Creates WalletTransaction(Settlement, +amount) for shop owner, linked to original AdvancePayment via RelatedTransactionId.
        /// </summary>
        Task<WalletTransaction> ConfirmAdvanceReceivedAsync(Guid shopOwnerId, Guid advanceTransactionId);

        /// <summary>
        /// Sprint 5: List pending advance payments for a shop owner (AdvancePayment txs without matching Settlement).
        /// </summary>
        Task<List<PendingAdvanceDto>> GetPendingAdvancesAsync(Guid shopOwnerId);

        /// <summary>
        /// Sprint 5: Reverse a wallet transaction by creating a Reversal entry (Amount = -original.Amount).
        /// Original transaction is NOT modified (immutable). Reversal links via RelatedTransactionId.
        /// </summary>
        Task<WalletTransaction> ReverseTransactionAsync(Guid ownerId, Guid originalTransactionId);

        /// <summary>
        /// Sprint 7 Q5: Confirm external payment (non-COD Reseller — VietQR/card).
        /// Reseller only — rejects Marketplace orders.
        /// Creates split: ExternalPayment + Settlement + DeliveryFee + PlatformFee + CommunityFund
        /// (commission leg removed TC-03 — paid once via CoolingPeriodJob).
        /// TC-06: marks the order Paid (method EXTERNAL) + triggers accounting like COD.
        /// </summary>
        Task<WalletTransaction> ConfirmExternalPaymentAsync(Guid orderId, decimal amount, string paymentRef);

        /// <summary>
        /// Sprint 7 Q3: Spend from community fund (SysAdmin disbursement).
        /// Creates CommunityFundSpend tx (-amount on CommunityFundWallet).
        /// Audit record created by CommunityFundService.SpendAsync.
        /// </summary>
        Task<WalletTransaction> SpendCommunityFundAsync(decimal amount, string reason, Guid approvedBy);

        /// <summary>
        /// Settlement Batch-3 (TC-08): Shipper nộp tiền COD đã thu hộ — theo đơn (Q1a).
        /// Marketplace: Remittance(-codAmount, shipper) + Settlement(+codAmount, shop).
        /// Reseller: Remittance(-codAmount, shipper) + Settlement(+codAmount, PlatformWallet) (Q1b).
        /// Amount is derived server-side from the order — never client-supplied.
        /// Idempotent: throws if a Remittance already exists for the order.
        /// </summary>
        Task<WalletTransaction> RemitCodAsync(Guid shipperId, Guid orderId);

        /// <summary>
        /// Settlement Batch-3 (TC-08): COD the shipper collected but has not yet remitted
        /// (and not reversed) — "tiền đang giữ hộ".
        /// </summary>
        Task<List<PendingRemittanceDto>> GetPendingRemittancesAsync(Guid shipperId);

        /// <summary>
        /// Settlement Batch-3 (TC-09): Owner requests a payout. Validates min amount (500k),
        /// available balance (ledger balance − COD held − pending requests), and rejects if a
        /// Pending/Approved request already exists.
        /// </summary>
        Task<VanAn.Shared.Domain.Aggregates.WalletAggregate.WithdrawalRequest> RequestWithdrawalAsync(Guid ownerId, decimal amount);

        /// <summary>TC-09: owner's own withdrawal request history.</summary>
        Task<List<WithdrawalRequestDto>> GetWithdrawalsAsync(Guid ownerId);

        /// <summary>TC-09: owner cancels their own Pending request.</summary>
        Task CancelWithdrawalAsync(Guid ownerId, Guid requestId);

        /// <summary>TC-09 admin: paginated withdrawal request list (all owners, optional status filter).</summary>
        Task<WithdrawalRequestListResult> GetWithdrawalRequestsAsync(VanAn.Shared.Domain.Aggregates.WalletAggregate.WithdrawalStatus? status, int page, int pageSize);

        /// <summary>TC-09 admin: Pending → Approved.</summary>
        Task ApproveWithdrawalAsync(Guid requestId, Guid adminId);

        /// <summary>TC-09 admin: Pending/Approved → Rejected (reason required).</summary>
        Task RejectWithdrawalAsync(Guid requestId, Guid adminId, string reason);

        /// <summary>
        /// TC-09 admin: Approved → Paid after the manual bank transfer. Creates the
        /// WalletTransaction(Withdrawal, -amount) exactly once in the same transaction.
        /// </summary>
        Task<VanAn.Shared.Domain.Aggregates.WalletAggregate.WithdrawalRequest> MarkWithdrawalPaidAsync(Guid requestId, Guid adminId, string bankReference);
    }

    /// <summary>Wallet summary DTO — balance + transaction history.</summary>
    public class WalletSummaryDto
    {
        public decimal Balance { get; set; }
        /// <summary>TC-08: COD collected but not yet remitted ("đang giữ hộ") — 0 for non-shippers.</summary>
        public decimal CodHeld { get; set; }
        /// <summary>TC-09: Balance − CodHeld − Pending/Approved withdrawal requests.</summary>
        public decimal AvailableBalance { get; set; }
        public List<WalletTransactionDto> Transactions { get; set; } = new();
    }

    /// <summary>Wallet transaction DTO for API responses.</summary>
    public class WalletTransactionDto
    {
        public Guid Id { get; set; }
        public string Type { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public decimal BalanceAfter { get; set; }
        public string Description { get; set; } = string.Empty;
        public Guid? RelatedOrderId { get; set; }
        public Guid? RelatedTransactionId { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    /// <summary>Pending advance payment DTO for shop owner confirmation queue.</summary>
    public class PendingAdvanceDto
    {
        public Guid TransactionId { get; set; }
        public Guid ShipperId { get; set; }
        public Guid OrderId { get; set; }
        public decimal Amount { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    /// <summary>TC-08: COD collected by shipper, not yet remitted.</summary>
    public class PendingRemittanceDto
    {
        public Guid OrderId { get; set; }
        public decimal Amount { get; set; }
        public DateTime CollectedAt { get; set; }
    }

    /// <summary>TC-09: withdrawal request DTO for API responses.</summary>
    public class WithdrawalRequestDto
    {
        public Guid Id { get; set; }
        public Guid OwnerId { get; set; }
        public decimal Amount { get; set; }
        public string Status { get; set; } = string.Empty;
        public string? BankReference { get; set; }
        public string? RejectReason { get; set; }
        public DateTime RequestedAt { get; set; }
        public DateTime? ProcessedAt { get; set; }
        public Guid? WalletTransactionId { get; set; }
    }

    /// <summary>TC-09 admin: paginated withdrawal request list.</summary>
    public class WithdrawalRequestListResult
    {
        public int Total { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
        public List<WithdrawalRequestDto> Items { get; set; } = new();
    }
}
