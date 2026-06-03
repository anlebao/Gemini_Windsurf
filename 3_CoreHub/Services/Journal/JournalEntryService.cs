using Microsoft.Extensions.Logging;
using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Services.Journal
{
    /// <summary>
    /// Domain Service for Journal Entry operations and validation
    /// Moved from Shared Domain to maintain Clean Architecture
    /// </summary>
    public class JournalEntryService(ILogger<JournalEntryService> logger)
    {
        private readonly ILogger<JournalEntryService> _logger = logger;

        /// <summary>
        /// Validate Vietnamese account number format
        /// Implementation moved from Shared Domain
        /// </summary>
        public static bool IsValidAccountNumber(string accountNumber)
        {
            if (string.IsNullOrWhiteSpace(accountNumber))
            {
                return false;
            }

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
        public static bool IsBalanced(JournalEntry journalEntry)
        {
            if (journalEntry == null)
            {
                return false;
            }

            decimal totalDebit = 0;
            decimal totalCredit = 0;

            foreach (JournalEntryLine line in journalEntry.Lines)
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
        public static decimal GetTotalDebit(JournalEntry journalEntry)
        {
            return journalEntry == null ? 0 : journalEntry.Lines.Sum(line => line.DebitAmount);
        }

        /// <summary>
        /// Get total credit amount
        /// Implementation moved from Shared Domain
        /// </summary>
        public static decimal GetTotalCredit(JournalEntry journalEntry)
        {
            return journalEntry == null ? 0 : journalEntry.Lines.Sum(line => line.CreditAmount);
        }

        /// <summary>
        /// Check if journal entry has valid account numbers
        /// Implementation moved from Shared Domain
        /// </summary>
        public static bool HasValidAccountNumbers(JournalEntry journalEntry)
        {
            return journalEntry != null && journalEntry.Lines.All(line => IsValidAccountNumber(line.AccountNumber));
        }

        /// <summary>
        /// Validate journal entry line data
        /// </summary>
        public static bool ValidateJournalEntryLine(string accountNumber, decimal debitAmount, decimal creditAmount)
        {
            if (string.IsNullOrWhiteSpace(accountNumber))
            {
                return false;
            }

            if (!IsValidAccountNumber(accountNumber))
            {
                return false;
            }

            return debitAmount < 0 || creditAmount < 0 ? false : debitAmount <= 0 || creditAmount <= 0;
        }

        /// <summary>
        /// Generate journal number
        /// Implementation moved from Shared Domain
        /// </summary>
        public static string GenerateJournalNo()
        {
            return $"J{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..8].ToUpper(System.Globalization.CultureInfo.CurrentCulture)}";
        }

        /// <summary>
        /// Validate complete journal entry
        /// </summary>
        public ValidationResult ValidateJournalEntry(JournalEntry journalEntry)
        {
            if (journalEntry == null)
            {
                return ValidationResult.Failure("Journal entry is null");
            }

            List<string> errors =
            [
                // Check if entry has lines
                .. journalEntry.Lines.Count == 0 ? ["Journal entry must have at least one line"] : [],

                // Check if entry is balanced
                .. !IsBalanced(journalEntry) ? ["Journal entry is not balanced"] : [],

                // Check account numbers
                .. !HasValidAccountNumbers(journalEntry) ? ["Journal entry contains invalid account numbers"] : [],
            ];

            // Check for valid amounts
            foreach (JournalEntryLine line in journalEntry.Lines)
            {
                if (line.DebitAmount < 0 || line.CreditAmount < 0)
                {
                    errors.Add($"Line {line.AccountNumber} has negative amounts");
                }

                if (line.DebitAmount > 0 && line.CreditAmount > 0)
                {
                    errors.Add($"Line {line.AccountNumber} has both debit and credit amounts");
                }
            }

            return errors.Count != 0 ? ValidationResult.Failure([.. errors]) : ValidationResult.Success();
        }

        /// <summary>
        /// Get journal entry summary
        /// </summary>
        public static string GetJournalEntrySummary(JournalEntry journalEntry)
        {
            if (journalEntry == null)
            {
                return "No journal entry";
            }

            decimal totalDebit = GetTotalDebit(journalEntry);
            decimal totalCredit = GetTotalCredit(journalEntry);
            int lineCount = journalEntry.Lines.Count;

            return $"Journal {journalEntry.JournalNo}: {lineCount} lines, " +
                   $"Debit: {totalDebit:N2}, Credit: {totalCredit:N2}, " +
                   $"Balanced: {IsBalanced(journalEntry)}";
        }
    }
}
