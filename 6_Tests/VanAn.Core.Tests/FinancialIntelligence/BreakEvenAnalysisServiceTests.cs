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
    /// VA-FI-MVP2 Phase 2 (2026-08-21): Unit tests for BreakEvenAnalysisService.
    /// Covers: happy path (AboveBreakEven), PROFILE_MISSING, INSUFFICIENT_DATA, CM ≤ 0, multi-product.
    /// </summary>
    public class BreakEvenAnalysisServiceTests
    {
        private static readonly Guid TenantGuid = Guid.NewGuid();
        private static readonly TenantId Tenant = new(TenantGuid);
        private static readonly AccountingPeriod Period = AccountingPeriod.FromDateTime(new DateTime(2026, 8, 1));

        private static BusinessProfile NewProfile(decimal fixedCost, int capacity = 200, int days = 30)
        {
            // Single fixed-cost bucket (rent) carries the full fixed cost for test simplicity.
            return new BusinessProfile(
                Tenant,
                monthlyRent: fixedCost, monthlyPayroll: 0m, monthlyUtilities: 0m,
                monthlyMarketing: 0m, monthlyLogistics: 0m, monthlyOtherOpEx: 0m,
                monthlyDepreciation: 0m,
                dailyCapacityUnits: capacity, operatingDaysPerMonth: days,
                pricingModel: PricingModel.FixedPrice, notes: "test");
        }

        private static IncomeStatement BuildIncome(decimal revenue, decimal cogs)
            => new(Tenant, Period, DateTime.UtcNow,
                TotalRevenueEnding: revenue, TotalRevenueOpening: 0m,
                NetProfitEnding: revenue - cogs, NetProfitOpening: 0m,
                Lines: Array.Empty<FinancialStatementLine>(),
                TotalCogsEnding: cogs, TotalCogsOpening: 0m,
                TotalOpExEnding: 0m, TotalOpExOpening: 0m);

        private static BreakEvenAnalysisService NewService(
            BusinessProfile? profile,
            IncomeStatement income,
            int unitsSold = 100,
            IEnumerable<Order>? orders = null,
            List<ProductSnapshot>? products = null)
        {
            var profileSvcMock = new Mock<IBusinessProfileService>();
            profileSvcMock.Setup(s => s.GetAsync(It.IsAny<TenantId>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(profile);
            var incomeMock = new Mock<IIncomeStatementService>();
            incomeMock.Setup(s => s.GenerateAsync(It.IsAny<TenantId>(), It.IsAny<AccountingPeriod>(), It.IsAny<AccountingStandard>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(income);
            // Bug 3 fix: IProductRepository → IShopErpProductCatalogService
            var catalogMock = new Mock<IShopErpProductCatalogService>();
            catalogMock.Setup(c => c.GetProductsAsync(It.IsAny<TenantId>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(products ?? new List<ProductSnapshot>());
            var orderRepoMock = new Mock<IOrderRepository>();
            orderRepoMock.Setup(r => r.GetByDateRangeAsync(It.IsAny<TenantId>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(orders ?? Enumerable.Empty<Order>());
            // unitsSold is implicit via orders (sum of OrderItem.Quantity). For single-product tests we synthesize orders.
            return new BreakEvenAnalysisService(profileSvcMock.Object, incomeMock.Object, catalogMock.Object, orderRepoMock.Object, NullLogger<BreakEvenAnalysisService>.Instance);
        }

        private static Order NewOrder(int quantity, decimal unitPrice)
        {
            // Build minimal aggregate via Order.Create (DDD factory) + OrderItem.Create.
            var orderId = Guid.NewGuid();
            var itemId = Guid.NewGuid();
            var productId = Guid.NewGuid();
            var item = OrderItem.Create(
                id: itemId,
                tenantId: Tenant,
                orderId: orderId,
                productId: productId,
                quantity: quantity,
                unitPrice: unitPrice,
                productName: "TestProduct");
            return Order.Create(orderId, Tenant, customerId: null, items: new List<OrderItem> { item });
        }

        [Fact]
        public async Task AnalyzeAsync_ProfileMissing_ReturnsInsufficientDataWithProfileWarning()
        {
            var svc = NewService(profile: null, income: BuildIncome(0m, 0m));

            BreakEvenAnalysis result = await svc.AnalyzeAsync(Tenant, Period, AccountingStandard.TT99_2025);

            result.Status.Should().Be(BreakEvenStatus.InsufficientData);
            result.WarningMessage.Should().Contain("PROFILE_MISSING");
        }

        [Fact]
        public async Task AnalyzeAsync_NoPAndL_ReturnsInsufficientData()
        {
            var svc = NewService(profile: NewProfile(20_000_000m), income: BuildIncome(0m, 0m));

            BreakEvenAnalysis result = await svc.AnalyzeAsync(Tenant, Period, AccountingStandard.TT99_2025);

            result.Status.Should().Be(BreakEvenStatus.InsufficientData);
            result.WarningMessage.Should().Contain("INSUFFICIENT_DATA");
        }

        [Fact]
        public async Task AnalyzeAsync_RevenueAboveBreakEven_ReturnsAboveBreakEven()
        {
            // Revenue 100M, COGS 40M → CM = 60M, CMRatio = 0.6 → BreakEvenRevenue = 20M / 0.6 = 33.33M
            // Revenue 100M > BreakEven 33.33M → AboveBreakEven
            var order = NewOrder(quantity: 100, unitPrice: 1_000_000m);
            var svc = NewService(profile: NewProfile(20_000_000m), income: BuildIncome(100_000_000m, 40_000_000m), orders: new[] { order });

            BreakEvenAnalysis result = await svc.AnalyzeAsync(Tenant, Period, AccountingStandard.TT99_2025);

            result.Status.Should().Be(BreakEvenStatus.AboveBreakEven);
            result.ContributionMarginRatio.Should().Be(0.6m);
            result.BreakEvenRevenue.Should().BeApproximately(33_333_333.33m, 1m);
            result.MarginOfSafetyRevenue.Should().BePositive();
        }

        [Fact]
        public async Task AnalyzeAsync_CogsExceedsRevenue_ReturnsBelowBreakEvenWithCmWarning()
        {
            // Revenue 30M, COGS 40M → CM = -10M, CMRatio < 0 → BelowBreakEven + CM warning
            var order = NewOrder(quantity: 30, unitPrice: 1_000_000m);
            var svc = NewService(profile: NewProfile(20_000_000m), income: BuildIncome(30_000_000m, 40_000_000m), orders: new[] { order });

            BreakEvenAnalysis result = await svc.AnalyzeAsync(Tenant, Period, AccountingStandard.TT99_2025);

            result.Status.Should().Be(BreakEvenStatus.BelowBreakEven);
            result.ContributionMarginRatio.Should().BeNegative();
            result.WarningMessage.Should().Contain("CM_RATIO_ZERO_OR_NEG");
        }

        [Fact]
        public async Task AnalyzeAsync_FixedCostZero_ReturnsZeroBreakEvenRevenueWithWarning()
        {
            var order = NewOrder(quantity: 50, unitPrice: 1_000_000m);
            var svc = NewService(profile: NewProfile(0m), income: BuildIncome(50_000_000m, 20_000_000m), orders: new[] { order });

            BreakEvenAnalysis result = await svc.AnalyzeAsync(Tenant, Period, AccountingStandard.TT99_2025);

            result.TotalFixedCost.Should().Be(0m);
            result.BreakEvenRevenue.Should().Be(0m);
            result.Status.Should().Be(BreakEvenStatus.AboveBreakEven); // 50M > 0
            result.WarningMessage.Should().Contain("FIXED_COST_ZERO");
        }

        [Fact]
        public async Task AnalyzeMultiProductAsync_ProfileMissing_ReturnsEmptyLines()
        {
            var svc = NewService(profile: null, income: BuildIncome(0m, 0m));

            MultiProductBreakEven result = await svc.AnalyzeMultiProductAsync(Tenant, Period, AccountingStandard.TT99_2025);

            result.ProductLines.Should().BeEmpty();
            result.TotalFixedCost.Should().Be(0m);
        }

        [Fact]
        public async Task AnalyzeMultiProductAsync_NoSalesInPeriod_ReturnsEmptyLines()
        {
            var svc = NewService(profile: NewProfile(20_000_000m), income: BuildIncome(100_000_000m, 40_000_000m), orders: Enumerable.Empty<Order>());

            MultiProductBreakEven result = await svc.AnalyzeMultiProductAsync(Tenant, Period, AccountingStandard.TT99_2025);

            result.ProductLines.Should().BeEmpty();
            result.TotalFixedCost.Should().Be(20_000_000m);
            // Bug 2 fix: decimal.MaxValue → 0m when no sales/CM=0
            result.BreakEvenRevenue.Should().Be(0m);
        }
    }
}
