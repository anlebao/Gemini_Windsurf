using VanAn.Shared.Domain;
using Xunit;

namespace VanAn.Core.Tests.Accounting;

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
        var values = Enum.GetValues<PeriodClosingStatus>();

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
        var warnings = new List<string> { "No entries found for sub-account 112" };
        var result = new PeriodClosingCheckResult(true, new List<string>(), warnings);

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
        Assert.Single(result.Warnings);
        Assert.Equal("No entries found for sub-account 112", result.Warnings[0]);
    }

    [Fact]
    public void PeriodClosingCheckResult_WithErrors_ReturnsInvalid()
    {
        var errors = new List<string>
        {
            "Unbalanced entries detected",
            "Pending transactions exist"
        };
        var result = new PeriodClosingCheckResult(false, errors, new List<string>());

        Assert.False(result.IsValid);
        Assert.Equal(2, result.Errors.Count);
        Assert.Contains("Unbalanced entries detected", result.Errors);
        Assert.Contains("Pending transactions exist", result.Errors);
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public void PeriodClosingCheckResult_ValidResult_HasNoErrors()
    {
        var result = new PeriodClosingCheckResult(true, new List<string>(), new List<string>());

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    // ── ClosingEntry ─────────────────────────────────────────────────────

    [Fact]
    public void ClosingEntry_CreatesWithCorrectValues()
    {
        var periodId = Guid.NewGuid();
        var period = new AccountingPeriod(2025, 12);
        var closingDate = new DateTime(2026, 1, 5, 0, 0, 0, DateTimeKind.Utc);
        var createdBy = Guid.NewGuid();

        var entry = new ClosingEntry(periodId, period, closingDate, createdBy);

        Assert.Equal(periodId, entry.PeriodId);
        Assert.Equal(period, entry.Period);
        Assert.Equal(closingDate, entry.ClosingDate);
        Assert.Equal(createdBy, entry.CreatedBy);
    }

    [Fact]
    public void ClosingEntry_IsImmutable_RecordEquality()
    {
        var periodId = Guid.NewGuid();
        var period = new AccountingPeriod(2025, 12);
        var closingDate = new DateTime(2026, 1, 5, 0, 0, 0, DateTimeKind.Utc);
        var createdBy = Guid.NewGuid();

        var entry1 = new ClosingEntry(periodId, period, closingDate, createdBy);
        var entry2 = new ClosingEntry(periodId, period, closingDate, createdBy);

        Assert.Equal(entry1, entry2);
    }
}
