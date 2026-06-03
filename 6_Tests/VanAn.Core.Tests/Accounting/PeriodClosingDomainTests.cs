using VanAn.Shared.Domain;
using Xunit;

namespace VanAn.Core.Tests.Accounting
{
    /// <summary>
    /// TDD Phase 1 — Domain unit tests for Period Closing entities.
    /// Pure domain tests: no mocks, no DB, no DI.
    /// </summary>
    public class PeriodClosingDomainTests
    {
        // ── PeriodClosingStatus ──────────────────────────────────────────────

        [Fact]
        public void PeriodClosingStatus_AllValuesAreDefined()
        {
            PeriodClosingStatus[] values = Enum.GetValues<PeriodClosingStatus>();

            Assert.Contains(PeriodClosingStatus.Open, values);
            Assert.Contains(PeriodClosingStatus.Validating, values);
            Assert.Contains(PeriodClosingStatus.Closing, values);
            Assert.Contains(PeriodClosingStatus.Closed, values);
            Assert.Contains(PeriodClosingStatus.Reopening, values);
            Assert.Equal(5, values.Length);
        }

        // ── PeriodClosingCheckResult ─────────────────────────────────────────

        [Fact]
        public void PeriodClosingCheckResult_WithValidData_ReturnsCorrectValues()
        {
            List<string> warnings = ["No entries found for sub-account 112"];
            PeriodClosingCheckResult result = new(true, [], warnings);

            Assert.True(result.IsValid);
            Assert.Empty(result.Errors);
            Assert.Single(result.Warnings);
            Assert.Equal("No entries found for sub-account 112", result.Warnings[0]);
        }

        [Fact]
        public void PeriodClosingCheckResult_WithErrors_ReturnsInvalid()
        {
            List<string> errors =
            [
                "Unbalanced entries detected",
                "Pending transactions exist"
            ];
            PeriodClosingCheckResult result = new(false, errors, []);

            Assert.False(result.IsValid);
            Assert.Equal(2, result.Errors.Count);
            Assert.Contains("Unbalanced entries detected", result.Errors);
            Assert.Contains("Pending transactions exist", result.Errors);
            Assert.Empty(result.Warnings);
        }

        [Fact]
        public void PeriodClosingCheckResult_ValidResult_HasNoErrors()
        {
            PeriodClosingCheckResult result = new(true, [], []);

            Assert.True(result.IsValid);
            Assert.Empty(result.Errors);
        }

        // ── ClosingEntry ─────────────────────────────────────────────────────

        [Fact]
        public void ClosingEntry_CreatesWithCorrectValues()
        {
            Guid periodId = Guid.NewGuid();
            AccountingPeriod period = new(2025, 12);
            DateTime closingDate = new(2026, 1, 5, 0, 0, 0, DateTimeKind.Utc);
            Guid createdBy = Guid.NewGuid();

            ClosingEntry entry = new(periodId, period, closingDate, createdBy);

            Assert.Equal(periodId, entry.PeriodId);
            Assert.Equal(period, entry.Period);
            Assert.Equal(closingDate, entry.ClosingDate);
            Assert.Equal(createdBy, entry.CreatedBy);
        }

        [Fact]
        public void ClosingEntry_IsImmutable_RecordEquality()
        {
            Guid periodId = Guid.NewGuid();
            AccountingPeriod period = new(2025, 12);
            DateTime closingDate = new(2026, 1, 5, 0, 0, 0, DateTimeKind.Utc);
            Guid createdBy = Guid.NewGuid();

            ClosingEntry entry1 = new(periodId, period, closingDate, createdBy);
            ClosingEntry entry2 = new(periodId, period, closingDate, createdBy);

            Assert.Equal(entry1, entry2);
        }
    }
}
