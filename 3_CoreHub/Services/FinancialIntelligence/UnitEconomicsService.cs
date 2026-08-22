using Microsoft.Extensions.Logging;
using VanAn.CoreHub.Repositories;
using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Services.FinancialIntelligence
{
    /// <summary>
    /// VA-FI-MVP2 (2026-08-21): Implementation of <see cref="IUnitEconomicsService"/>.
    /// Loads Products (active management view) + Orders in period, aggregates per-product
    /// Revenue/UnitsSold, computes ContributionMargin = Price - CostPrice (with fallback 70%
    /// when CostPrice = 0 — matches OrderService.CalculateCogsAmount precedent), then ranks
    /// by ProfitContribution (CM × UnitsSold) DESC.
    /// Guard codes (P2.6): PROFILE_MISSING, COST_PRICE_MISSING (flag, not block).
    /// Pure deterministic — Trust Level 1 (NFR-14).
    ///
    /// Bug 3 fix (2026-08-22): Products fetched from ShopERP SQLite via
    /// <see cref="IShopErpProductCatalogService"/> (HTTP, routed by ShopInstanceId).
    /// Gateway PG Products table is empty per Option C Phase 3.
    /// </summary>
    public class UnitEconomicsService : IUnitEconomicsService
    {
        private readonly IShopErpProductCatalogService _productCatalog;
        private readonly IOrderRepository _orderRepository;
        private readonly ILogger<UnitEconomicsService> _logger;

        // Variable cost fallback ratio when CostPrice missing (matches BreakEven service + OrderService precedent).
        private const decimal CostPriceFallbackRatio = 0.70m;

        public UnitEconomicsService(
            IShopErpProductCatalogService productCatalog,
            IOrderRepository orderRepository,
            ILogger<UnitEconomicsService> logger)
        {
            _productCatalog = productCatalog;
            _orderRepository = orderRepository;
            _logger = logger;
        }

        /// <inheritdoc />
        public async Task<UnitEconomicsReport> AnalyzeAsync(TenantId tenantId, AccountingPeriod period, CancellationToken ct = default)
        {
            try
            {
                // Step 1 — Load all managed products for the tenant from ShopERP SQLite (HTTP).
                // Bug 3 fix: was IProductRepository.GetAllForManagementAsync (Gateway PG — empty per Option C).
                List<ProductSnapshot> products = await _productCatalog.GetProductsAsync(tenantId, ct).ConfigureAwait(false);
                if (products.Count == 0)
                {
                    return Empty(tenantId, period, FinancialModelVersion.Initial);
                }

                // Step 2 — Load Orders in period; aggregate OrderItem per-product.
                DateTime periodStart = period.StartDate;
                DateTime periodEnd = period.StartDate.AddMonths(1);
                IEnumerable<Order> orders = await _orderRepository.GetByDateRangeAsync(tenantId, periodStart, periodEnd, ct).ConfigureAwait(false);

                var perProduct = new Dictionary<Guid, (int Units, decimal Revenue)>();
                foreach (Order order in orders)
                {
                    foreach (OrderItem item in order.Items)
                    {
                        if (perProduct.TryGetValue(item.ProductId, out var cur))
                            perProduct[item.ProductId] = (cur.Units + item.Quantity, cur.Revenue + item.TotalAmount);
                        else
                            perProduct[item.ProductId] = (item.Quantity, item.TotalAmount);
                    }
                }

                // Step 3 — Build per-product lines (only products with sales this period).
                var lines = new List<UnitEconomicsLine>();
                foreach (ProductSnapshot product in products)
                {
                    if (!perProduct.TryGetValue(product.ProductId, out var sold))
                        continue; // Skip products with no sales this period (no contribution to rank).

                    bool missingCost = product.CostPrice == 0m;
                    decimal variableCost = missingCost
                        ? product.Price * CostPriceFallbackRatio
                        : product.CostPrice;

                    decimal cm = product.Price - variableCost;
                    decimal cmPercent = product.Price > 0m ? cm / product.Price : 0m;
                    decimal profitContribution = cm * sold.Units;

                    lines.Add(new UnitEconomicsLine(
                        ProductId: product.ProductId,
                        ProductName: product.Name,
                        Category: product.Category,
                        SellingPrice: product.Price,
                        VariableCost: variableCost,
                        ContributionMargin: cm,
                        ContributionMarginPercent: cmPercent,
                        UnitsSold: sold.Units,
                        Revenue: sold.Revenue,
                        ProfitContribution: profitContribution,
                        ProfitContributionRank: 0, // assigned after sort
                        HasMissingCostPrice: missingCost));
                }

                // Step 4 — Rank by ProfitContribution DESC.
                lines = lines.OrderByDescending(l => l.ProfitContribution).ToList();
                var ranked = new List<UnitEconomicsLine>(lines.Count);
                for (int i = 0; i < lines.Count; i++)
                    ranked.Add(lines[i] with { ProfitContributionRank = i + 1 });

                // Step 5 — Aggregate metrics + warnings.
                int missingCount = ranked.Count(l => l.HasMissingCostPrice);
                decimal totalContribution = ranked.Sum(l => l.ProfitContribution);
                decimal totalRevenue = ranked.Sum(l => l.Revenue);
                decimal avgCm = totalRevenue > 0m ? totalContribution / totalRevenue : 0m;

                if (ranked.Count == 0)
                {
                    _logger.LogInformation("UnitEconomics no sales for tenant {TenantId} period {Period}", tenantId.Value, period);
                    return Empty(tenantId, period, FinancialModelVersion.Initial);
                }

                return new UnitEconomicsReport(
                    tenantId, period, DateTime.UtcNow, FinancialModelVersion.Initial,
                    Products: ranked,
                    TotalProductsAnalyzed: ranked.Count,
                    ProductsWithMissingCostPrice: missingCount,
                    TotalContribution: totalContribution,
                    AverageContributionMargin: avgCm);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "UnitEconomics failed for tenant {TenantId} period {Period}", tenantId.Value, period);
                return Empty(tenantId, period, FinancialModelVersion.Initial);
            }
        }

        private static UnitEconomicsReport Empty(TenantId tenantId, AccountingPeriod period, FinancialModelVersion version)
            => new(
                tenantId, period, DateTime.UtcNow, version,
                Products: Array.Empty<UnitEconomicsLine>(),
                TotalProductsAnalyzed: 0,
                ProductsWithMissingCostPrice: 0,
                TotalContribution: 0m,
                AverageContributionMargin: 0m);
    }
}
