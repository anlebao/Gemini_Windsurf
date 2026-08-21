using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using VanAn.CoreHub.Repositories;
using VanAn.CoreHub.Services;
using VanAn.CoreHub.Services.FinancialIntelligence;
using VanAn.Shared.Domain;
using Xunit;

namespace VanAn.Core.Tests.FinancialIntelligence
{
    /// <summary>
    /// VA-FI-MVP2 Phase 2 (2026-08-21): Unit tests for TargetProfitService.
    /// Covers: feasible target, infeasible (CAPACITY_EXCEEDED), PROFILE_MISSING, CM ≤ 0.
    /// </summary>
    public class TargetProfitServiceTests
    {
        private static readonly Guid TenantGuid = Guid.NewGuid();
        private static readonly TenantId Tenant = new(TenantGuid);
        private static readonly AccountingPeriod Period = AccountingPeriod.FromDateTime(new DateTime(2026, 8, 1));

        private static BusinessProfile NewProfile(decimal fixedCost, int dailyCapacity, int operatingDays = 30)
            => new(Tenant,
                monthlyRent: fixedCost, monthlyPayroll: 0m, monthlyUtilities: 0m,
                monthlyMarketing: 0m, monthlyLogistics: 0m, monthlyOtherOpEx: 0m,
                monthlyDepreciation: 0m,
                dailyCapacityUnits: dailyCapacity, operatingDaysPerMonth: operatingDays,
                pricingModel: PricingModel.FixedPrice, notes: "test");

        private static IncomeStatement BuildIncome(decimal revenue, decimal cogs)
            => new(Tenant, Period, DateTime.UtcNow,
                TotalRevenueEnding: revenue, TotalRevenueOpening: 0m,
                NetProfitEnding: revenue - cogs, NetProfitOpening: 0m,
                Lines: Array.Empty<FinancialStatementLine>(),
                TotalCogsEnding: cogs, TotalCogsOpening: 0m,
                TotalOpExEnding: 0m, TotalOpExOpening: 0m);

        private static TargetProfitService NewService(BusinessProfile? profile, IncomeStatement income, IEnumerable<Order> orders)
        {
            var profileSvcMock = new Mock<IBusinessProfileService>();
            profileSvcMock.Setup(s => s.GetAsync(It.IsAny<TenantId>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(profile);
            var incomeMock = new Mock<IIncomeStatementService>();
            incomeMock.Setup(s => s.GenerateAsync(It.IsAny<TenantId>(), It.IsAny<AccountingPeriod>(), It.IsAny<AccountingStandard>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(income);
            var orderRepoMock = new Mock<IOrderRepository>();
            orderRepoMock.Setup(r => r.GetByDateRangeAsync(It.IsAny<TenantId>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(orders);
            return new TargetProfitService(profileSvcMock.Object, incomeMock.Object, orderRepoMock.Object, NullLogger<TargetProfitService>.Instance);
        }

        private static Order NewOrder(int qty, decimal unitPrice)
        {
            var orderId = Guid.NewGuid();
            var item = OrderItem.Create(Guid.NewGuid(), Tenant, orderId, Guid.NewGuid(), qty, unitPrice, "Test");
            return Order.Create(orderId, Tenant, customerId: null, items: new List<OrderItem> { item });
        }

        [Fact]
        public async Task AnalyzeAsync_ProfileMissing_ReturnsInfeasibleWithProfileWarning()
        {
            var svc = NewService(profile: null, income: BuildIncome(0m, 0m), orders: Enumerable.Empty<Order>());

            TargetProfitAnalysis result = await svc.AnalyzeAsync(Tenant, Period, AccountingStandard.TT99_2025, targetProfit: 50_000_000m);

            result.Feasible.Should().BeFalse();
            result.FeasibilityWarning.Should().Contain("PROFILE_MISSING");
        }

        [Fact]
        public async Task AnalyzeAsync_NoPAndL_ReturnsInfeasibleWithInsufficientData()
        {
            var svc = NewService(profile: NewProfile(20_000_000m, 200), income: BuildIncome(0m, 0m), orders: Enumerable.Empty<Order>());

            TargetProfitAnalysis result = await svc.AnalyzeAsync(Tenant, Period, AccountingStandard.TT99_2025, targetProfit: 50_000_000m);

            result.Feasible.Should().BeFalse();
            result.FeasibilityWarning.Should().Contain("INSUFFICIENT_DATA");
        }

        [Fact]
        public async Task AnalyzeAsync_CmNegative_ReturnsInfeasibleWithCmWarning()
        {
            // Revenue 30M, COGS 40M → CM -10M, CMRatio < 0
            var svc = NewService(profile: NewProfile(20_000_000m, 200), income: BuildIncome(30_000_000m, 40_000_000m), orders: new[] { NewOrder(30, 1_000_000m) });

            TargetProfitAnalysis result = await svc.AnalyzeAsync(Tenant, Period, AccountingStandard.TT99_2025, targetProfit: 10_000_000m);

            result.Feasible.Should().BeFalse();
            result.FeasibilityWarning.Should().Contain("CM_RATIO_ZERO_OR_NEG");
        }

        [Fact]
        public async Task AnalyzeAsync_FeasibleTarget_ReturnsTrueAndRequiredRevenue()
        {
            // Revenue 100M, COGS 40M → CM 60M, CMRatio 0.6
            // Fixed 20M + Target 10M = 30M; RequiredRevenue = 30M / 0.6 = 50M
            // UnitsSold 100, avgPrice 1M → RequiredUnits 50 → Daily = 50/30 ≈ 1.67 ≤ 200 capacity → Feasible
            var svc = NewService(profile: NewProfile(20_000_000m, 200), income: BuildIncome(100_000_000m, 40_000_000m), orders: new[] { NewOrder(100, 1_000_000m) });

            TargetProfitAnalysis result = await svc.AnalyzeAsync(Tenant, Period, AccountingStandard.TT99_2025, targetProfit: 10_000_000m);

            result.Feasible.Should().BeTrue();
            result.RequiredRevenue.Should().BeApproximately(50_000_000m, 1m);
            result.RequiredUnits.Should().BeApproximately(50m, 0.01m);
            result.RequiredDailyUnits.Should().BeLessThanOrEqualTo(200m);
            result.FeasibilityWarning.Should().BeNull();
        }

        [Fact]
        public async Task AnalyzeAsync_RequiredDailyExceedsCapacity_ReturnsInfeasibleWithCapacityWarning()
        {
            // Target very high → required daily > capacity (set capacity = 1 to force infeasibility)
            var svc = NewService(profile: NewProfile(20_000_000m, dailyCapacity: 1), income: BuildIncome(100_000_000m, 40_000_000m), orders: new[] { NewOrder(100, 1_000_000m) });

            TargetProfitAnalysis result = await svc.AnalyzeAsync(Tenant, Period, AccountingStandard.TT99_2025, targetProfit: 1_000_000_000m);

            result.Feasible.Should().BeFalse();
            result.FeasibilityWarning.Should().Contain("CAPACITY_EXCEEDED");
        }

        [Fact]
        public async Task AnalyzeAsync_ZeroDailyCapacity_ReturnsInfeasibleWithCapacityWarning()
        {
            var svc = NewService(profile: NewProfile(20_000_000m, dailyCapacity: 0), income: BuildIncome(100_000_000m, 40_000_000m), orders: new[] { NewOrder(100, 1_000_000m) });

            TargetProfitAnalysis result = await svc.AnalyzeAsync(Tenant, Period, AccountingStandard.TT99_2025, targetProfit: 10_000_000m);

            result.Feasible.Should().BeFalse();
            result.FeasibilityWarning.Should().Contain("CAPACITY_EXCEEDED");
        }
    }
}
