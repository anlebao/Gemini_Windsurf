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
    }

    /// <summary>Wallet summary DTO — balance + transaction history.</summary>
    public class WalletSummaryDto
    {
        public decimal Balance { get; set; }
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
}
