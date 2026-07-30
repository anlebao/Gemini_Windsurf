using VanAn.Shared.Domain.Common;

namespace VanAn.Shared.Domain.Aggregates.CommunityFundAggregate
{
    /// <summary>
    /// CommunityFundSpendRecord — audit trail for community fund disbursement — Sprint 7 (Q3).
    /// TenantId = Guid.Empty (system-wide — quỹ cộng đồng không thuộc tenant cụ thể).
    /// Created when SysAdmin rút tiền từ CommunityFund wallet qua POST /api/admin/community-fund/spend.
    /// Immutable — no update methods (audit trail integrity).
    /// </summary>
    public class CommunityFundSpendRecord : BaseEntity, IMustHaveTenant
    {
        public decimal Amount { get; protected set; }
        public string Reason { get; protected set; } = string.Empty;
        public string Recipient { get; protected set; } = string.Empty;
        public Guid ApprovedBy { get; protected set; }
        public DateTime SpentAt { get; protected set; }
        public Guid WalletTransactionId { get; protected set; }

        protected CommunityFundSpendRecord() { }

        public CommunityFundSpendRecord(
            TenantId tenantId,
            decimal amount,
            string reason,
            string recipient,
            Guid approvedBy,
            Guid walletTransactionId)
            : base(tenantId)
        {
            if (amount <= 0)
                throw new ArgumentOutOfRangeException(nameof(amount), "Amount must be positive");
            if (string.IsNullOrWhiteSpace(reason))
                throw new ArgumentException("Reason cannot be empty", nameof(reason));
            if (reason.Length > 500)
                throw new ArgumentOutOfRangeException(nameof(reason), "Reason max 500 chars");
            if (string.IsNullOrWhiteSpace(recipient))
                throw new ArgumentException("Recipient cannot be empty", nameof(recipient));
            if (recipient.Length > 200)
                throw new ArgumentOutOfRangeException(nameof(recipient), "Recipient max 200 chars");
            if (approvedBy == Guid.Empty)
                throw new ArgumentException("ApprovedBy cannot be empty", nameof(approvedBy));
            if (walletTransactionId == Guid.Empty)
                throw new ArgumentException("WalletTransactionId cannot be empty", nameof(walletTransactionId));

            Amount = amount;
            Reason = reason;
            Recipient = recipient;
            ApprovedBy = approvedBy;
            SpentAt = DateTime.UtcNow;
            WalletTransactionId = walletTransactionId;
        }
    }
}
