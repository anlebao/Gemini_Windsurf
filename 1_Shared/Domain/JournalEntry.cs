using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using VanAn.Shared.Domain.Common;

namespace VanAn.Shared.Domain
{
    /// <summary>
    /// Immutable Journal Entry aggregate root for Hybrid Architecture
    /// Source of Truth: VanAn.Shared.Domain
    /// </summary>
    public sealed class JournalEntry : BaseEntity, IMustHaveTenant
    {
        public TenantId TenantId { get; } = null!;
        public JournalEntryId JournalEntryId { get; } = null!;
        public string JournalNo { get; } = null!;
        public DateTime EntryDate { get; }
        public AccountingPeriod Period { get; } = null!;
        public string Description { get; } = null!;
        public string? ReferenceType { get; }
        public Guid? ReferenceId { get; }
        public bool IsReversal { get; }
        public JournalEntryId? ReversedJournalId { get; }

        private readonly List<JournalEntryLine> _lines = new();
        public IReadOnlyCollection<JournalEntryLine> Lines => _lines.AsReadOnly();

        // EF Core constructor
#pragma warning disable CS8618
        private JournalEntry() { }
#pragma warning restore CS8618

        // ✅ FIXED: Add Create static method for TemplateFactory compatibility
        public static JournalEntry Create(
            TenantId tenantId,
            string accountNumber,
            decimal amount,
            DateTime entryDate,
            string description,
            AccountingPeriod period)
        {
            var entry = new JournalEntry(tenantId, entryDate, description);
            // Add line with debit/credit amounts
            entry.AddLine(accountNumber, amount, 0, description);
            return entry;
        }

        public JournalEntry(
            TenantId tenantId,
            DateTime entryDate,
            string description,
            string? referenceType = null,
            Guid? referenceId = null,
            bool isReversal = false,
            JournalEntryId? reversedJournalId = null)
            : base(tenantId)
        {
            TenantId = tenantId;
            JournalEntryId = new JournalEntryId(Guid.NewGuid());
            EntryDate = entryDate;
            Period = AccountingPeriod.FromDateTime(entryDate);
            Description = description ?? throw new ArgumentNullException(nameof(description));
            ReferenceType = referenceType;
            ReferenceId = referenceId;
            IsReversal = isReversal;
            ReversedJournalId = reversedJournalId;
            JournalNo = GenerateJournalNo();
        }

        public void AddLine(string accountNumber, decimal debitAmount, decimal creditAmount, string? description = null)
        {
            if (string.IsNullOrWhiteSpace(accountNumber))
                throw new ArgumentException("Account number is required", nameof(accountNumber));

            if (!System.Text.RegularExpressions.Regex.IsMatch(accountNumber, @"^\d{1,10}$"))
                throw new ArgumentException($"Invalid Vietnamese account number format: {accountNumber}", nameof(accountNumber));

            if (debitAmount < 0 || creditAmount < 0)
                throw new ArgumentException("Amounts cannot be negative");
            
            if (debitAmount > 0 && creditAmount > 0)
                throw new ArgumentException("Cannot have both debit and credit amounts greater than zero");

            _lines.Add(new JournalEntryLine(this, accountNumber, debitAmount, creditAmount, description));
        }

        public JournalEntry CreateReversal(string reason)
        {
            if (IsReversal)
                throw new InvalidOperationException("Cannot create reversal of a reversal entry");

            var reversalEntry = new JournalEntry(
                TenantId,
                DateTime.UtcNow,
                $"Reversal: {Description}",
                ReferenceType,
                ReferenceId,
                isReversal: true,
                reversedJournalId: JournalEntryId
            );

            // Create reversal lines (swap debit/credit)
            foreach (var line in _lines)
            {
                reversalEntry.AddLine(line.AccountNumber, line.CreditAmount, line.DebitAmount, 
                    $"Reversal: {line.Description}");
            }

            return reversalEntry;
        }

        /// <summary>
        /// Asynchronously create a reversal entry with validation
        /// Enhanced domain method for future extensibility
        /// </summary>
        public Task<JournalEntry> CreateReversalAsync(string reason)
        {
            // For now, delegate to synchronous method
            // In future, this could include async validation, database checks, etc.
            return Task.FromResult(CreateReversal(reason));
        }

        private static string GenerateJournalNo() => $"J{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..8].ToUpper()}";
    }

    public sealed class JournalEntryLine
    {
        public Guid JournalEntryId { get; private set; }
        public string AccountNumber { get; private set; } = string.Empty;
        public decimal DebitAmount { get; private set; }
        public decimal CreditAmount { get; private set; }
        public string? Description { get; private set; }

        // EF Core constructor for materialization
        protected JournalEntryLine() { }

        public JournalEntryLine(JournalEntry journal, string accountNumber, decimal debitAmount, decimal creditAmount, string? description)
        {
            if (debitAmount < 0 || creditAmount < 0)
                throw new ArgumentException("Amounts cannot be negative");

            if (debitAmount > 0 && creditAmount > 0)
                throw new ArgumentException("Cannot have both debit and credit amounts greater than zero");

            JournalEntryId = journal.JournalEntryId.Value;
            AccountNumber = accountNumber;
            DebitAmount = debitAmount;
            CreditAmount = creditAmount;
            Description = description;
        }
    }

    public sealed record JournalEntryId(Guid Value)
    {
        public static JournalEntryId Empty => new(Guid.Empty);
        
        public static JournalEntryId New() => new(Guid.NewGuid());
    }
}
