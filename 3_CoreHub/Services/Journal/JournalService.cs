using System;
using System.Threading.Tasks;
using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Services.Journal
{
    /// <summary>
    /// Journal Service Implementation
    /// Handles journal entry operations with Vietnamese accounting standards
    /// </summary>
    public sealed class JournalService : IJournalService
    {
        public Task<JournalEntry> CreateJournalEntryAsync(TenantId tenantId, DateTime date, string description, string? referenceType = null, Guid? referenceId = null)
        {
            var entry = new JournalEntry(tenantId, date, description, referenceType, referenceId);
            return Task.FromResult(entry);
        }

        public Task<JournalEntry> CreateReversalEntryAsync(JournalEntry originalEntry, string? reason = null)
        {
            if (originalEntry == null)
                throw new ArgumentNullException(nameof(originalEntry));

            // Vietnamese accounting: Reversal must be done within same accounting period
            var reversalDate = DateTime.UtcNow;
            if (reversalDate.Month != originalEntry.CreatedAt.Month || reversalDate.Year != originalEntry.CreatedAt.Year)
            {
                throw new ValidationException("Reversal must be done within same accounting period according to Vietnamese accounting standards");
            }

            var reversalDescription = string.IsNullOrEmpty(reason) 
                ? $"Reversal of {originalEntry.Id}" 
                : $"Reversal of {originalEntry.Id} - {reason}";

            var reversalEntry = new JournalEntry(
                originalEntry.TenantId,
                reversalDate,
                reversalDescription,
                originalEntry.ReferenceType,
                originalEntry.ReferenceId
            );

            // Add reversal lines (opposite of original)
            foreach (var line in originalEntry.Lines)
            {
                reversalEntry.AddLine(
                    line.AccountNumber,
                    line.CreditAmount, // Reverse: Credit becomes Debit
                    line.DebitAmount,  // Reverse: Debit becomes Credit
                    $"Reversal of: {line.Description}"
                );
            }

            return Task.FromResult(reversalEntry);
        }

        public Task<JournalEntry> AddLineAsync(JournalEntry entry, string accountNumber, decimal debitAmount, decimal creditAmount, string? description = null)
        {
            if (entry == null)
                throw new ArgumentNullException(nameof(entry));

            // Vietnamese account number validation
            if (string.IsNullOrEmpty(accountNumber) || accountNumber.Length < 3)
                throw new ValidationException("Account number must be at least 3 digits");

            if (!accountNumber.All(char.IsDigit))
                throw new ValidationException("Account number must contain only digits");

            // Vietnamese accounting rule: Both debit and credit cannot be positive
            if (debitAmount > 0 && creditAmount > 0)
                throw new ValidationException("Both debit and credit amounts cannot be positive");

            // Vietnamese accounting rule: Amounts cannot be negative
            if (debitAmount < 0 || creditAmount < 0)
                throw new ValidationException("Amounts cannot be negative");

            entry.AddLine(accountNumber, debitAmount, creditAmount, description);
            return Task.FromResult(entry);
        }

        public Task<JournalEntry> ValidateAndSaveAsync(JournalEntry entry)
        {
            if (entry == null)
                throw new ArgumentNullException(nameof(entry));

            // Vietnamese accounting validation
            if (entry.Lines.Count == 0)
                throw new ValidationException("Journal entry must have at least one line");

            var totalDebit = entry.Lines.Sum(l => l.DebitAmount);
            var totalCredit = entry.Lines.Sum(l => l.CreditAmount);

            if (totalDebit != totalCredit)
                throw new ValidationException($"Total debit ({totalDebit}) must equal total credit ({totalCredit})");

            return Task.FromResult(entry);
        }
    }
}
