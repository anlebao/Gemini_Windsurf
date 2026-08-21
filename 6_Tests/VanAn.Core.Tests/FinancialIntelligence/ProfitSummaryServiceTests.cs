using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using VanAn.CoreHub.Services;
using VanAn.CoreHub.Services.FinancialIntelligence;
using VanAn.Shared.Domain;
using Xunit;

namespace VanAn.Core.Tests.FinancialIntelligence
{
    /// <summary>
    /// VA-FI-MVP2 Phase 2 (2026-08-21): Unit tests for ProfitSummaryService.
    /// Covers: happy path (profitable), InsufficientData guard, loss path, at-break-even.
    /// </summary>
    public class ProfitSummaryServiceTests
    {
        private static readonly Guid TenantGuid = Guid.NewGuid();
        private static readonly TenantId Tenant = new(TenantGuid);
        private static readonly AccountingPeriod Period = AccountingPeriod.FromDateTime(new DateTime(2026, 8, 1));

        private static IncomeStatement BuildIncome(decimal revenue, decimal cogs, decimal opex, decimal net)
            => new(Tenant, Period, DateTime.UtcNow,
                TotalRevenueEnding: revenue, TotalRevenueOpening: 0m,
                NetProfitEnding: net, NetProfitOpening: 0m,
                Lines: Array.Empty<FinancialStatementLine>(),
                TotalCogsEnding: cogs, TotalCogsOpening: 0m,
                TotalOpExEnding: opex, TotalOpExOpening: 0m);

        private static ProfitSummaryService NewService(Func<TenantId, AccountingPeriod, AccountingStandard, IncomeStatement> generator)
        {
            var incomeMock = new Mock<IIncomeStatementService>();
            incomeMock.Setup(s => s.GenerateAsync(It.IsAny<TenantId>(), It.IsAny<AccountingPeriod>(), It.IsAny<AccountingStandard>(), It.IsAny<CancellationToken>()))
                .Returns((TenantId t, AccountingPeriod p, AccountingStandard st, CancellationToken _) => Task.FromResult(generator(t, p, st)));
            return new ProfitSummaryService(incomeMock.Object, NullLogger<ProfitSummaryService>.Instance);
        }

        [Fact]
        public async Task GetAsync_Profitable_ReturnsProfitableStatus()
        {
            // Revenue 100M, COGS 40M, OpEx 20M, Net 40M
            var svc = NewService((_, _, _) => BuildIncome(100_000_000m, 40_000_000m, 20_000_000m, 40_000_000m));

            ProfitSummary result = await svc.GetAsync(Tenant, Period, AccountingStandard.TT99_2025);

            result.Status.Should().Be(ProfitStatus.Profitable);
            result.Revenue.Should().Be(100_000_000m);
            result.COGS.Should().Be(40_000_000m);
            result.GrossProfit.Should().Be(60_000_000m);
            result.GrossMarginPercent.Should().Be(0.6m);
            result.OperatingExpenses.Should().Be(20_000_000m);
            result.OperatingProfit.Should().Be(40_000_000m);
            result.NetProfit.Should().Be(40_000_000m);
            result.NetMarginPercent.Should().Be(0.4m);
            result.WarningMessage.Should().BeNull();
        }

        [Fact]
        public async Task GetAsync_NoMovement_ReturnsInsufficientData()
        {
            var svc = NewService((_, _, _) => BuildIncome(0m, 0m, 0m, 0m));

            ProfitSummary result = await svc.GetAsync(Tenant, Period, AccountingStandard.TT99_2025);

            result.Status.Should().Be(ProfitStatus.InsufficientData);
            result.WarningMessage.Should().Contain("Chưa có dữ liệu kế toán");
        }

        [Fact]
        public async Task GetAsync_NegativeNet_ReturnsLoss()
        {
            // Revenue 50M, COGS 40M, OpEx 20M, Net -10M
            var svc = NewService((_, _, _) => BuildIncome(50_000_000m, 40_000_000m, 20_000_000m, -10_000_000m));

            ProfitSummary result = await svc.GetAsync(Tenant, Period, AccountingStandard.TT99_2025);

            result.Status.Should().Be(ProfitStatus.Loss);
            result.NetProfit.Should().Be(-10_000_000m);
            result.WarningMessage.Should().Contain("Lỗ");
        }

        [Fact]
        public async Task GetAsync_NetZero_ReturnsAtBreakEven()
        {
            var svc = NewService((_, _, _) => BuildIncome(20_000_000m, 12_000_000m, 8_000_000m, 0m));

            ProfitSummary result = await svc.GetAsync(Tenant, Period, AccountingStandard.TT99_2025);

            result.Status.Should().Be(ProfitStatus.AtBreakEven);
            result.WarningMessage.Should().Contain("Hòa vốn");
        }

        [Fact]
        public async Task GetAsync_IncomeServiceThrows_ReturnsInsufficientDataSafeFallback()
        {
            var incomeMock = new Mock<IIncomeStatementService>();
            incomeMock.Setup(s => s.GenerateAsync(It.IsAny<TenantId>(), It.IsAny<AccountingPeriod>(), It.IsAny<AccountingStandard>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("DB down"));
            var svc = new ProfitSummaryService(incomeMock.Object, NullLogger<ProfitSummaryService>.Instance);

            ProfitSummary result = await svc.GetAsync(Tenant, Period, AccountingStandard.TT99_2025);

            result.Status.Should().Be(ProfitStatus.InsufficientData);
            result.WarningMessage.Should().Contain("Không thể tính ProfitSummary");
        }
    }
}
