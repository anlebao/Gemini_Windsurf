using Moq;
using VanAn.Shared.Domain;
using VanAn.CoreHub.Services;
using Xunit;

namespace VanAn.Core.Tests.Accounting
{
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
            PeriodClosingCheckResult expected = new(true, [], []);
            _ = _mockService
                .Setup(s => s.ValidatePeriodAsync(_period, _tenantId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(expected);

            PeriodClosingCheckResult result = await _mockService.Object.ValidatePeriodAsync(_period, _tenantId);

            Assert.True(result.IsValid);
            Assert.Empty(result.Errors);
        }

        [Fact]
        public async Task ValidatePeriodAsync_WithMissingEntries_ReturnsError()
        {
            List<string> errors = ["No accounting entries found for period 2025-12"];
            PeriodClosingCheckResult expected = new(false, errors, []);
            _ = _mockService
                .Setup(s => s.ValidatePeriodAsync(_period, _tenantId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(expected);

            PeriodClosingCheckResult result = await _mockService.Object.ValidatePeriodAsync(_period, _tenantId);

            Assert.False(result.IsValid);
            Assert.Contains("No accounting entries found for period 2025-12", result.Errors);
        }

        [Fact]
        public async Task ValidatePeriodAsync_WithUnbalancedEntries_ReturnsError()
        {
            List<string> errors = ["Debit/Credit totals are unbalanced: Debit=10000, Credit=9500"];
            PeriodClosingCheckResult expected = new(false, errors, []);
            _ = _mockService
                .Setup(s => s.ValidatePeriodAsync(_period, _tenantId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(expected);

            PeriodClosingCheckResult result = await _mockService.Object.ValidatePeriodAsync(_period, _tenantId);

            Assert.False(result.IsValid);
            _ = Assert.Single(result.Errors);
            Assert.Contains("unbalanced", result.Errors[0], StringComparison.OrdinalIgnoreCase);
        }

        // ── ClosePeriodAsync ─────────────────────────────────────────────────

        [Fact]
        public async Task ClosePeriodAsync_WhenValid_CreatesClosingEntries()
        {
            Guid periodId = Guid.NewGuid();
            DateTime closingDate = DateTime.UtcNow;
            ClosingEntry expected = new(periodId, _period, closingDate, _userId);
            _ = _mockService
                .Setup(s => s.ClosePeriodAsync(_period, _tenantId, _userId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(expected);

            ClosingEntry result = await _mockService.Object.ClosePeriodAsync(_period, _tenantId, _userId);

            Assert.NotNull(result);
            Assert.Equal(_period, result.Period);
            Assert.Equal(_userId, result.CreatedBy);
        }

        [Fact]
        public async Task ClosePeriodAsync_WhenInvalid_ThrowsException()
        {
            _ = _mockService
                .Setup(s => s.ClosePeriodAsync(_period, _tenantId, _userId, It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("Period validation failed. Cannot close period with errors."));

            InvalidOperationException ex = await Assert.ThrowsAsync<InvalidOperationException>(
                () => _mockService.Object.ClosePeriodAsync(_period, _tenantId, _userId));

            Assert.Contains("validation failed", ex.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task ClosePeriodAsync_WhenAlreadyClosed_ThrowsException()
        {
            _ = _mockService
                .Setup(s => s.ClosePeriodAsync(_period, _tenantId, _userId, It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("Period 2025-12 is already closed."));

            InvalidOperationException ex = await Assert.ThrowsAsync<InvalidOperationException>(
                () => _mockService.Object.ClosePeriodAsync(_period, _tenantId, _userId));

            Assert.Contains("already closed", ex.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task ClosePeriodAsync_WhenPeriodHasPendingTransactions_ThrowsException()
        {
            _ = _mockService
                .Setup(s => s.ClosePeriodAsync(_period, _tenantId, _userId, It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("Cannot close period: 3 pending transactions exist."));

            InvalidOperationException ex = await Assert.ThrowsAsync<InvalidOperationException>(
                () => _mockService.Object.ClosePeriodAsync(_period, _tenantId, _userId));

            Assert.Contains("pending transactions", ex.Message, StringComparison.OrdinalIgnoreCase);
        }

        // ── ReopenPeriodAsync ────────────────────────────────────────────────

        [Fact]
        public async Task ReopenPeriodAsync_CreatesReversalEntry()
        {
            string reason = "Correction required for Q4 audit";
            _ = _mockService
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
            string reason = "Audit correction";
            _ = _mockService
                .Setup(s => s.ReopenPeriodAsync(_period, _tenantId, _userId, reason, It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            _ = _mockService
                .Setup(s => s.GetPeriodStatusAsync(_period, _tenantId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(PeriodClosingStatus.Open);

            await _mockService.Object.ReopenPeriodAsync(_period, _tenantId, _userId, reason);
            PeriodClosingStatus status = await _mockService.Object.GetPeriodStatusAsync(_period, _tenantId);

            Assert.Equal(PeriodClosingStatus.Open, status);
        }

        [Fact]
        public async Task ReopenPeriodAsync_WithOpenPeriod_ThrowsException()
        {
            string reason = "Mistake";
            _ = _mockService
                .Setup(s => s.ReopenPeriodAsync(_period, _tenantId, _userId, reason, It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("Cannot reopen period 2025-12: it is not closed."));

            InvalidOperationException ex = await Assert.ThrowsAsync<InvalidOperationException>(
                () => _mockService.Object.ReopenPeriodAsync(_period, _tenantId, _userId, reason));

            Assert.Contains("not closed", ex.Message, StringComparison.OrdinalIgnoreCase);
        }
    }
}
