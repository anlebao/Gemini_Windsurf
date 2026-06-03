using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Services.Journal
{
    /// <summary>
    /// Domain Service for Journal Entry operations and validation
    /// Moved from Shared Domain to maintain Clean Architecture
    /// </summary>
    public class JournalEntryService
    {
        private readonly ILogger<JournalEntryService> _logger;

        public JournalEntryService(ILogger<JournalEntryService> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Validate Vietnamese account number format
        /// Implementation moved from Shared Domain
        /// </summary>
        public bool IsValidAccountNumber(string accountNumber)
        {
            if (string.IsNullOrWhiteSpace(accountNumber))
                return false;

            // Vietnamese account numbers are typically 3 digits for main accounts
            // Can have sub-accounts with additional digits
            return accountNumber.Length >= 3 && 
                   accountNumber.Length <= 10 && 
                   int.TryParse(accountNumber, out _);
        }

        /// <summary>
        /// Check if journal entry is balanced
        /// Implementation moved from Shared Domain
        /// </summary>
        public bool IsBalanced(JournalEntry journalEntry)
        {
            if (journalEntry == null)
                return false;

            decimal totalDebit = 0;
            decimal totalCredit = 0;
            
            foreach (var line in journalEntry.Lines)
            {
                totalDebit += line.DebitAmount;
                totalCredit += line.CreditAmount;
            }
            
            return Math.Abs(totalDebit - totalCredit) < 0.01m; // Allow for rounding differences
        }

        /// <summary>
        /// Get total debit amount
        /// Implementation moved from Shared Domain
        /// </summary>
        public decimal GetTotalDebit(JournalEntry journalEntry)
        {
            if (journalEntry == null)
                return 0;

            return journalEntry.Lines.Sum(line => line.DebitAmount);
        }

        /// <summary>
        /// Get total credit amount
        /// Implementation moved from Shared Domain
        /// </summary>
        public decimal GetTotalCredit(JournalEntry journalEntry)
        {
            if (journalEntry == null)
                return 0;

            return journalEntry.Lines.Sum(line => line.CreditAmount);
        }

        /// <summary>
        /// Check if journal entry has valid account numbers
        /// Implementation moved from Shared Domain
        /// </summary>
        public bool HasValidAccountNumbers(JournalEntry journalEntry)
        {
            if (journalEntry == null)
                return false;

            return journalEntry.Lines.All(line => IsValidAccountNumber(line.AccountNumber));
        }

        /// <summary>
        /// Validate journal entry line data
        /// </summary>
        public bool ValidateJournalEntryLine(string accountNumber, decimal debitAmount, decimal creditAmount)
        {
            if (string.IsNullOrWhiteSpace(accountNumber))
                return false;

            if (!IsValidAccountNumber(accountNumber))
                return false;

            if (debitAmount < 0 || creditAmount < 0)
                return false;

            if (debitAmount > 0 && creditAmount > 0)
                return false;

            return true;
        }

        /// <summary>
        /// Generate journal number
        /// Implementation moved from Shared Domain
        /// </summary>
        public string GenerateJournalNo()
        {
            return $"J{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..8].ToUpper()}";
        }

        /// <summary>
        /// Validate complete journal entry
        /// </summary>
        public ValidationResult ValidateJournalEntry(JournalEntry journalEntry)
        {
            if (journalEntry == null)
                return ValidationResult.Failure("Journal entry is null");

            var errors = new List<string>();

            // Check if entry has lines
            if (!journalEntry.Lines.Any())
                errors.Add("Journal entry must have at least one line");

            // Check if entry is balanced
            if (!IsBalanced(journalEntry))
                errors.Add("Journal entry is not balanced");

            // Check account numbers
            if (!HasValidAccountNumbers(journalEntry))
                errors.Add("Journal entry contains invalid account numbers");

            // Check for valid amounts
            foreach (var line in journalEntry.Lines)
            {
                if (line.DebitAmount < 0 || line.CreditAmount < 0)
                    errors.Add($"Line {line.AccountNumber} has negative amounts");

                if (line.DebitAmount > 0 && line.CreditAmount > 0)
                    errors.Add($"Line {line.AccountNumber} has both debit and credit amounts");
            }

            return errors.Any() ? ValidationResult.Failure(errors.ToArray()) : ValidationResult.Success();
        }

        /// <summary>
        /// Get journal entry summary
        /// </summary>
        public string GetJournalEntrySummary(JournalEntry journalEntry)
        {
            if (journalEntry == null)
                return "No journal entry";

            var totalDebit = GetTotalDebit(journalEntry);
            var totalCredit = GetTotalCredit(journalEntry);
            var lineCount = journalEntry.Lines.Count;

            return $"Journal {journalEntry.JournalNo}: {lineCount} lines, " +
                   $"Debit: {totalDebit:N2}, Credit: {totalCredit:N2}, " +
                   $"Balanced: {IsBalanced(journalEntry)}";
        }
    }
}
