using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Services
{
    /// <summary>
    /// IWalletService — wallet operations for Community Commerce (v1.4).
    /// Sprint 0: Base atomic CreateTransactionAsync (HR-SCALE-3 SELECT FOR UPDATE).
    /// Sprint 5 extends with ConfirmCodAsync/ConfirmAdvanceAsync/ReverseTransactionAsync/SettleAsync.
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
    }
}
