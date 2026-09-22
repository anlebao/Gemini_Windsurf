using VanAn.Shared.Domain.Common;

namespace VanAn.Shared.Domain.Aggregates.WalletAggregate
{
    /// <summary>
    /// WithdrawalRequest — payout lifecycle for wallet owners — Settlement Batch-3 (TC-09, Q4).
    /// Flow: Pending → Approved/Rejected (SystemAdmin) → Paid (manual bank ref, admin nhập tay).
    /// Owner can Cancel while still Pending.
    /// A WalletTransaction(Withdrawal, -amount) is created exactly once when Paid.
    /// </summary>
    public class WithdrawalRequest : BaseEntity, IMustHaveTenant
    {
        public Guid OwnerId { get; protected set; }
        public decimal Amount { get; protected set; }
        public WithdrawalStatus Status { get; protected set; }
        public string? BankReference { get; protected set; }
        public string? RejectReason { get; protected set; }
        public Guid? ProcessedBy { get; protected set; }
        public DateTime? ProcessedAt { get; protected set; }
        public Guid? WalletTransactionId { get; protected set; }
        public DateTime RequestedAt { get; protected set; }

        protected WithdrawalRequest() { }

        public WithdrawalRequest(TenantId tenantId, Guid ownerId, decimal amount)
            : base(tenantId)
        {
            if (ownerId == Guid.Empty)
                throw new ArgumentException("OwnerId cannot be empty", nameof(ownerId));
            if (amount <= 0)
                throw new ArgumentOutOfRangeException(nameof(amount), "Amount must be positive");

            OwnerId = ownerId;
            Amount = amount;
            Status = WithdrawalStatus.Pending;
            RequestedAt = DateTime.UtcNow;
        }

        public void Approve(Guid adminId)
        {
            if (Status != WithdrawalStatus.Pending)
                throw new InvalidOperationException($"Withdrawal request {Id} is {Status} — only Pending requests can be approved.");
            if (adminId == Guid.Empty)
                throw new ArgumentException("AdminId cannot be empty", nameof(adminId));

            Status = WithdrawalStatus.Approved;
            ProcessedBy = adminId;
            ProcessedAt = DateTime.UtcNow;
            UpdateAudit();
        }

        public void Reject(Guid adminId, string reason)
        {
            if (Status != WithdrawalStatus.Pending && Status != WithdrawalStatus.Approved)
                throw new InvalidOperationException($"Withdrawal request {Id} is {Status} — cannot reject.");
            if (adminId == Guid.Empty)
                throw new ArgumentException("AdminId cannot be empty", nameof(adminId));
            if (string.IsNullOrWhiteSpace(reason))
                throw new ArgumentException("Reject reason cannot be empty", nameof(reason));
            if (reason.Length > 500)
                throw new ArgumentOutOfRangeException(nameof(reason), "Reject reason max 500 chars");

            Status = WithdrawalStatus.Rejected;
            RejectReason = reason;
            ProcessedBy = adminId;
            ProcessedAt = DateTime.UtcNow;
            UpdateAudit();
        }

        /// <summary>Mark paid after the admin completes the manual bank transfer.</summary>
        public void MarkPaid(Guid adminId, string bankReference, Guid walletTransactionId)
        {
            if (Status != WithdrawalStatus.Approved)
                throw new InvalidOperationException($"Withdrawal request {Id} is {Status} — only Approved requests can be paid.");
            if (adminId == Guid.Empty)
                throw new ArgumentException("AdminId cannot be empty", nameof(adminId));
            if (string.IsNullOrWhiteSpace(bankReference))
                throw new ArgumentException("Bank reference cannot be empty", nameof(bankReference));
            if (bankReference.Length > 200)
                throw new ArgumentOutOfRangeException(nameof(bankReference), "Bank reference max 200 chars");
            if (walletTransactionId == Guid.Empty)
                throw new ArgumentException("WalletTransactionId cannot be empty", nameof(walletTransactionId));

            Status = WithdrawalStatus.Paid;
            BankReference = bankReference;
            WalletTransactionId = walletTransactionId;
            ProcessedBy = adminId;
            ProcessedAt = DateTime.UtcNow;
            UpdateAudit();
        }

        /// <summary>Owner cancels their own request while still Pending.</summary>
        public void Cancel()
        {
            if (Status != WithdrawalStatus.Pending)
                throw new InvalidOperationException($"Withdrawal request {Id} is {Status} — only Pending requests can be cancelled.");

            Status = WithdrawalStatus.Cancelled;
            ProcessedAt = DateTime.UtcNow;
            UpdateAudit();
        }
    }

    public enum WithdrawalStatus
    {
        Pending = 1,
        Approved = 2,
        Rejected = 3,
        Paid = 4,
        Cancelled = 5
    }
}
