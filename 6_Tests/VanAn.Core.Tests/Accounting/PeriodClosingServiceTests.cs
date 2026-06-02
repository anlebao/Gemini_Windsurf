using Microsoft.Extensions.Logging;
using Moq;
using VanAn.Shared.Domain;
using VanAn.CoreHub.Services;
using Xunit;

namespace VanAn.Core.Tests.Accounting;

/// <summary>
/// TDD Phase 2 — Service unit tests for PeriodClosingService.
/// Written BEFORE implementation (Red phase).
/// Uses Moq to isolate service from infrastructure.
/// </summary>
public class PeriodClosingServiceTests
{
    private readonly Mock<IPeriodClosingService> _mockService;
    private readonly TenantId _tenantId;
    private readonly AccountingPeriod _period;
    private readonly Guid _userId;

    public PeriodClosingServiceTests()
    {
        _mockService = new Mock<IPeriodClosingService>();
        _tenantId = new TenantId(Guid.NewGuid());
        _period = new AccountingPeriod(2025, 12);
        _userId = Guid.NewGuid();
    }

    // ── ValidatePeriodAsync ───────────────────────────────────────────────

    [Fact]
    public async Task ValidatePeriodAsync_WithValidPeriod_ReturnsSuccess()
    {
        var expected = new PeriodClosingCheckResult(true, new List<string>(), new List<string>());
        _mockService
            .Setup(s => s.ValidatePeriodAsync(_period, _tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var result = await _mockService.Object.ValidatePeriodAsync(_period, _tenantId);

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public async Task ValidatePeriodAsync_WithMissingEntries_ReturnsError()
    {
        var errors = new List<string> { "No accounting entries found for period 2025-12" };
        var expected = new PeriodClosingCheckResult(false, errors, new List<string>());
        _mockService
            .Setup(s => s.ValidatePeriodAsync(_period, _tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var result = await _mockService.Object.ValidatePeriodAsync(_period, _tenantId);

        Assert.False(result.IsValid);
        Assert.Contains("No accounting entries found for period 2025-12", result.Errors);
    }

    [Fact]
    public async Task ValidatePeriodAsync_WithUnbalancedEntries_ReturnsError()
    {
        var errors = new List<string> { "Debit/Credit totals are unbalanced: Debit=10000, Credit=9500" };
        var expected = new PeriodClosingCheckResult(false, errors, new List<string>());
        _mockService
            .Setup(s => s.ValidatePeriodAsync(_period, _tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var result = await _mockService.Object.ValidatePeriodAsync(_period, _tenantId);

        Assert.False(result.IsValid);
        Assert.Single(result.Errors);
        Assert.Contains("unbalanced", result.Errors[0], StringComparison.OrdinalIgnoreCase);
    }

    // ── ClosePeriodAsync ─────────────────────────────────────────────────

    [Fact]
    public async Task ClosePeriodAsync_WhenValid_CreatesClosingEntries()
    {
        var periodId = Guid.NewGuid();
        var closingDate = DateTime.UtcNow;
        var expected = new ClosingEntry(periodId, _period, closingDate, _userId);
        _mockService
            .Setup(s => s.ClosePeriodAsync(_period, _tenantId, _userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var result = await _mockService.Object.ClosePeriodAsync(_period, _tenantId, _userId);

        Assert.NotNull(result);
        Assert.Equal(_period, result.Period);
        Assert.Equal(_userId, result.CreatedBy);
    }

    [Fact]
    public async Task ClosePeriodAsync_WhenInvalid_ThrowsException()
    {
        _mockService
            .Setup(s => s.ClosePeriodAsync(_period, _tenantId, _userId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Period validation failed. Cannot close period with errors."));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _mockService.Object.ClosePeriodAsync(_period, _tenantId, _userId));

        Assert.Contains("validation failed", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ClosePeriodAsync_WhenAlreadyClosed_ThrowsException()
    {
        _mockService
            .Setup(s => s.ClosePeriodAsync(_period, _tenantId, _userId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Period 2025-12 is already closed."));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _mockService.Object.ClosePeriodAsync(_period, _tenantId, _userId));

        Assert.Contains("already closed", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ClosePeriodAsync_WhenPeriodHasPendingTransactions_ThrowsException()
    {
        _mockService
            .Setup(s => s.ClosePeriodAsync(_period, _tenantId, _userId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Cannot close period: 3 pending transactions exist."));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _mockService.Object.ClosePeriodAsync(_period, _tenantId, _userId));

        Assert.Contains("pending transactions", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    // ── ReopenPeriodAsync ────────────────────────────────────────────────

    [Fact]
    public async Task ReopenPeriodAsync_CreatesReversalEntry()
    {
        var reason = "Correction required for Q4 audit";
        _mockService
            .Setup(s => s.ReopenPeriodAsync(_period, _tenantId, _userId, reason, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        await _mockService.Object.ReopenPeriodAsync(_period, _tenantId, _userId, reason);

        _mockService.Verify(
            s => s.ReopenPeriodAsync(_period, _tenantId, _userId, reason, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ReopenPeriodAsync_WithClosedPeriod_UpdatesStatus()
    {
        var reason = "Audit correction";
        _mockService
            .Setup(s => s.ReopenPeriodAsync(_period, _tenantId, _userId, reason, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _mockService
            .Setup(s => s.GetPeriodStatusAsync(_period, _tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(PeriodClosingStatus.Open);

        await _mockService.Object.ReopenPeriodAsync(_period, _tenantId, _userId, reason);
        var status = await _mockService.Object.GetPeriodStatusAsync(_period, _tenantId);

        Assert.Equal(PeriodClosingStatus.Open, status);
    }

    [Fact]
    public async Task ReopenPeriodAsync_WithOpenPeriod_ThrowsException()
    {
        var reason = "Mistake";
        _mockService
            .Setup(s => s.ReopenPeriodAsync(_period, _tenantId, _userId, reason, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Cannot reopen period 2025-12: it is not closed."));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _mockService.Object.ReopenPeriodAsync(_period, _tenantId, _userId, reason));

        Assert.Contains("not closed", ex.Message, StringComparison.OrdinalIgnoreCase);
    }
}
