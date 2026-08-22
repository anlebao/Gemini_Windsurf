using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using VanAn.CoreHub.Repositories;
using VanAn.CoreHub.Services.FinancialIntelligence;
using VanAn.Shared.Domain;
using Xunit;

namespace VanAn.Core.Tests.FinancialIntelligence
{
    /// <summary>
    /// VA-FI-MVP2 Phase 2 (2026-08-21): Unit tests for UnitEconomicsService.
    /// Covers: ranking by ProfitContribution DESC, missing CostPrice flag, no sales, no products.
    /// </summary>
    public class UnitEconomicsServiceTests
    {
        private static readonly Guid TenantGuid = Guid.NewGuid();
        private static readonly TenantId Tenant = new(TenantGuid);
        private static readonly AccountingPeriod Period = AccountingPeriod.FromDateTime(new DateTime(2026, 8, 1));

        private static ProductSnapshot NewProduct(Guid id, string name, decimal price, decimal costPrice, string category = "F&B")
            => new(id, name, price, costPrice, category);

        private static OrderItem NewItem(Guid productId, int qty, decimal unitPrice)
            => OrderItem.Create(Guid.NewGuid(), Tenant, Guid.NewGuid(), productId, qty, unitPrice, "Test");

        private static Order NewOrder(params OrderItem[] items)
            => Order.Create(Guid.NewGuid(), Tenant, customerId: null, items: items.ToList());

        private static UnitEconomicsService NewService(List<ProductSnapshot> products, IEnumerable<Order> orders)
        {
            // Bug 3 fix: IProductRepository → IShopErpProductCatalogService
            var catalogMock = new Mock<IShopErpProductCatalogService>();
            catalogMock.Setup(c => c.GetProductsAsync(It.IsAny<TenantId>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(products);
            var orderRepoMock = new Mock<IOrderRepository>();
            orderRepoMock.Setup(r => r.GetByDateRangeAsync(It.IsAny<TenantId>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(orders);
            return new UnitEconomicsService(catalogMock.Object, orderRepoMock.Object, NullLogger<UnitEconomicsService>.Instance);
        }

        [Fact]
        public async Task AnalyzeAsync_NoProducts_ReturnsEmptyReport()
        {
            var svc = NewService(new List<ProductSnapshot>(), Enumerable.Empty<Order>());

            UnitEconomicsReport result = await svc.AnalyzeAsync(Tenant, Period);

            result.TotalProductsAnalyzed.Should().Be(0);
            result.Products.Should().BeEmpty();
        }

        [Fact]
        public async Task AnalyzeAsync_NoSalesInPeriod_ReturnsEmptyReport()
        {
            var p = NewProduct(Guid.NewGuid(), "Cafe", 30_000m, 12_000m);
            var svc = NewService(new List<ProductSnapshot> { p }, Enumerable.Empty<Order>());

            UnitEconomicsReport result = await svc.AnalyzeAsync(Tenant, Period);

            result.TotalProductsAnalyzed.Should().Be(0);
            result.Products.Should().BeEmpty();
        }

        [Fact]
        public async Task AnalyzeAsync_RanksByProfitContributionDesc()
        {
            var p1 = NewProduct(Guid.NewGuid(), "Cafe", 30_000m, 12_000m); // CM 18k
            var p2 = NewProduct(Guid.NewGuid(), "TraDa", 10_000m, 4_000m);  // CM 6k
            var p3 = NewProduct(Guid.NewGuid(), "BanhMi", 25_000m, 20_000m); // CM 5k

            // p1 sold 100 (contribution 1.8M), p2 sold 300 (1.8M), p3 sold 50 (250k)
            var orders = new[]
            {
                NewOrder(NewItem(p1.ProductId, 100, 30_000m)),
                NewOrder(NewItem(p2.ProductId, 300, 10_000m)),
                NewOrder(NewItem(p3.ProductId, 50, 25_000m)),
            };
            var svc = NewService(new List<ProductSnapshot> { p1, p2, p3 }, orders);

            UnitEconomicsReport result = await svc.AnalyzeAsync(Tenant, Period);

            result.TotalProductsAnalyzed.Should().Be(3);
            // p1 (1.8M) and p2 (1.8M) tie — both should outrank p3 (250k). Either p1 or p2 first.
            result.Products[0].ProfitContribution.Should().Be(1_800_000m);
            result.Products[2].ProductName.Should().Be("BanhMi");
            result.Products[2].ProfitContributionRank.Should().Be(3);
            result.AverageContributionMargin.Should().BePositive();
        }

        [Fact]
        public async Task AnalyzeAsync_MissingCostPrice_FlagsAndFallsBackTo70Percent()
        {
            var p = NewProduct(Guid.NewGuid(), "Cafe", 30_000m, costPrice: 0m); // missing → fallback 70%
            var orders = new[] { NewOrder(NewItem(p.ProductId, 100, 30_000m)) };
            var svc = NewService(new List<ProductSnapshot> { p }, orders);

            UnitEconomicsReport result = await svc.AnalyzeAsync(Tenant, Period);

            result.TotalProductsAnalyzed.Should().Be(1);
            result.ProductsWithMissingCostPrice.Should().Be(1);
            result.Products[0].HasMissingCostPrice.Should().BeTrue();
            // Fallback var cost = 30k × 0.7 = 21k → CM = 9k
            result.Products[0].VariableCost.Should().Be(21_000m);
            result.Products[0].ContributionMargin.Should().Be(9_000m);
        }
    }
}
