using Microsoft.Extensions.Logging;
using VanAn.CoreHub.Repositories;
using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Services.FinancialIntelligence
{
    /// <summary>
    /// VA-FI-MVP2 (2026-08-21): Implementation of <see cref="IBreakEvenAnalysisService"/>.
    /// Single-product: aggregate Revenue + COGS (from extended IncomeStatement) + fixed costs (BusinessProfile).
    /// Multi-product: per-product CM weighted by sales mix (from OrderItem aggregation).
    /// Guard codes (P2.6): PROFILE_MISSING, INSUFFICIENT_DATA, CM_RATIO_ZERO_OR_NEG, FIXED_COST_ZERO.
    /// Pure deterministic — Trust Level 1 (NFR-14).
    /// </summary>
    public class BreakEvenAnalysisService : IBreakEvenAnalysisService
    {
        private readonly IBusinessProfileService _profileService;
        private readonly IIncomeStatementService _incomeStatementService;
        private readonly IProductRepository _productRepository;
        private readonly IOrderRepository _orderRepository;
        private readonly ILogger<BreakEvenAnalysisService> _logger;

        // Source account codes for BR-005 traceability (Vietnamese TT 99 / TT 133 chart).
        private static readonly string[] SourceAccountCodes = { "511", "632", "641", "642", "64" };

        // At-break-even tolerance: |Revenue - BreakEven| < 1% of revenue.
        private const decimal AtBreakEvenToleranceRatio = 0.01m;
        // Variable cost fallback ratio when CostPrice missing (matches OrderService.CalculateCogsAmount line 258).
        private const decimal CostPriceFallbackRatio = 0.70m;

        public BreakEvenAnalysisService(
            IBusinessProfileService profileService,
            IIncomeStatementService incomeStatementService,
            IProductRepository productRepository,
            IOrderRepository orderRepository,
            ILogger<BreakEvenAnalysisService> logger)
        {
            _profileService = profileService;
            _incomeStatementService = incomeStatementService;
            _productRepository = productRepository;
            _orderRepository = orderRepository;
            _logger = logger;
        }

        /// <inheritdoc />
        public async Task<BreakEvenAnalysis> AnalyzeAsync(TenantId tenantId, AccountingPeriod period, AccountingStandard standard, CancellationToken ct = default)
        {
            try
            {
                // Step 1 — Guard: PROFILE_MISSING
                BusinessProfile? profile = await _profileService.GetAsync(tenantId, ct).ConfigureAwait(false);
                if (profile is null)
                {
                    _logger.LogInformation("BreakEven PROFILE_MISSING for tenant {TenantId}", tenantId.Value);
                    return InsufficientData(tenantId, period, FinancialModelVersion.Initial,
                        "Chưa khai báo BusinessProfile — cần nhập fixed costs (PROFILE_MISSING)");
                }

                // Step 2 — Load IncomeStatement
                IncomeStatement income = await _incomeStatementService.GenerateAsync(tenantId, period, standard, ct).ConfigureAwait(false);

                decimal revenue = income.TotalRevenueEnding;
                decimal variableCost = income.TotalCogsEnding;

                // Guard: INSUFFICIENT_DATA — no P&L movement
                if (revenue == 0m && variableCost == 0m && income.NetProfitEnding == 0m)
                {
                    _logger.LogInformation("BreakEven INSUFFICIENT_DATA for tenant {TenantId} period {Period}", tenantId.Value, period);
                    return InsufficientData(tenantId, period, profile.Version,
                        $"Chưa có dữ liệu kế toán kỳ {period} (INSUFFICIENT_DATA)");
                }

                decimal contribution = revenue - variableCost;
                decimal cmRatio = revenue > 0m ? contribution / revenue : 0m;
                decimal fixedCost = profile.TotalMonthlyFixedCost;

                // Step 3 — Compute break-even
                decimal breakEvenRevenue = cmRatio > 0m ? fixedCost / cmRatio : decimal.MaxValue;

                // Units sold this period (sum of OrderItem.Quantity)
                int unitsSold = await GetUnitsSoldAsync(tenantId, period, ct).ConfigureAwait(false);
                decimal avgPrice = unitsSold > 0 ? revenue / unitsSold : 0m;
                decimal avgVarCost = unitsSold > 0 ? variableCost / unitsSold : 0m;
                decimal breakEvenUnits = (avgPrice - avgVarCost) > 0m && unitsSold > 0
                    ? fixedCost / (avgPrice - avgVarCost)
                    : decimal.MaxValue;

                // Step 4 — Margin of safety
                decimal mosRevenue = revenue - breakEvenRevenue;
                decimal mosPercent = revenue > 0m ? mosRevenue / revenue : 0m;

                // Step 5 — Status
                BreakEvenStatus status = ClassifyStatus(revenue, breakEvenRevenue, cmRatio);

                string? warning = BuildWarning(profile, cmRatio, unitsSold);

                return new BreakEvenAnalysis(
                    tenantId, period, DateTime.UtcNow, profile.Version,
                    TotalFixedCost: fixedCost,
                    TotalRevenue: revenue,
                    TotalVariableCost: variableCost,
                    TotalContributionMargin: contribution,
                    ContributionMarginRatio: cmRatio,
                    BreakEvenRevenue: breakEvenRevenue,
                    BreakEvenUnits: breakEvenUnits,
                    MarginOfSafetyRevenue: mosRevenue,
                    MarginOfSafetyPercent: mosPercent,
                    Status: status,
                    WarningMessage: warning,
                    SourceAccountCodes: SourceAccountCodes);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "BreakEven failed for tenant {TenantId} period {Period}", tenantId.Value, period);
                return InsufficientData(tenantId, period, FinancialModelVersion.Initial,
                    "Không thể tính BreakEven — xem log hệ thống");
            }
        }

        /// <inheritdoc />
        public async Task<MultiProductBreakEven> AnalyzeMultiProductAsync(TenantId tenantId, AccountingPeriod period, AccountingStandard standard, CancellationToken ct = default)
        {
            try
            {
                // Step 1 — Guard: PROFILE_MISSING
                BusinessProfile? profile = await _profileService.GetAsync(tenantId, ct).ConfigureAwait(false);
                if (profile is null)
                {
                    return new MultiProductBreakEven(
                        tenantId, period, DateTime.UtcNow, FinancialModelVersion.Initial,
                        TotalFixedCost: 0m,
                        WeightedContributionMargin: 0m,
                        WeightedContributionMarginRatio: 0m,
                        BreakEvenRevenue: decimal.MaxValue,
                        ProductLines: Array.Empty<ProductBreakEvenLine>());
                }

                // Step 2 — Load Products + Orders for period
                List<Product> products = await _productRepository.GetAllForManagementAsync(tenantId, ct).ConfigureAwait(false);
                if (products.Count == 0)
                {
                    return EmptyMulti(tenantId, period, profile);
                }

                DateTime periodStart = period.StartDate;
                DateTime periodEnd = period.StartDate.AddMonths(1);
                IEnumerable<Order> orders = await _orderRepository.GetByDateRangeAsync(tenantId, periodStart, periodEnd, ct).ConfigureAwait(false);

                // Step 3 — Aggregate per-product units sold + revenue
                var perProduct = new Dictionary<Guid, (int Units, decimal Revenue)>();
                foreach (Order order in orders)
                {
                    foreach (OrderItem item in order.Items)
                    {
                        if (perProduct.TryGetValue(item.ProductId, out var current))
                            perProduct[item.ProductId] = (current.Units + item.Quantity, current.Revenue + item.TotalAmount);
                        else
                            perProduct[item.ProductId] = (item.Quantity, item.TotalAmount);
                    }
                }

                if (perProduct.Count == 0)
                    return EmptyMulti(tenantId, period, profile);

                decimal totalRevenue = perProduct.Values.Sum(v => v.Revenue);
                decimal fixedCost = profile.TotalMonthlyFixedCost;

                // Step 4 — Build per-product lines (only products with sales this period)
                var lines = new List<ProductBreakEvenLine>();
                decimal weightedCm = 0m;
                foreach (var (productId, sold) in perProduct)
                {
                    Product? product = products.FirstOrDefault(p => p.Id == productId);
                    if (product is null)
                        continue;

                    decimal sellingPrice = product.Price;
                    decimal variableCost = product.CostPrice > 0m
                        ? product.CostPrice
                        : sellingPrice * CostPriceFallbackRatio; // fallback 70% UnitPrice

                    decimal cm = sellingPrice - variableCost;
                    decimal cmRatio = sellingPrice > 0m ? cm / sellingPrice : 0m;
                    decimal salesMixPercent = totalRevenue > 0m ? sold.Revenue / totalRevenue : 0m;

                    // Allocation: FixedCost × SalesMix / CM_i (units)
                    decimal productBreakEvenUnits = cm > 0m && salesMixPercent > 0m
                        ? fixedCost * salesMixPercent / cm
                        : decimal.MaxValue;

                    weightedCm += cm * salesMixPercent;

                    lines.Add(new ProductBreakEvenLine(
                        ProductId: productId,
                        ProductName: product.Name,
                        SellingPrice: sellingPrice,
                        VariableCost: variableCost,
                        ContributionMargin: cm,
                        ContributionMarginRatio: cmRatio,
                        SalesMixPercent: salesMixPercent,
                        UnitsSoldInPeriod: sold.Units,
                        ProductBreakEvenUnits: productBreakEvenUnits));
                }

                decimal weightedCmRatio = totalRevenue > 0m ? weightedCm / totalRevenue : 0m;
                decimal breakEvenRevenue = weightedCmRatio > 0m ? fixedCost / weightedCmRatio : decimal.MaxValue;

                return new MultiProductBreakEven(
                    tenantId, period, DateTime.UtcNow, profile.Version,
                    TotalFixedCost: fixedCost,
                    WeightedContributionMargin: weightedCm,
                    WeightedContributionMarginRatio: weightedCmRatio,
                    BreakEvenRevenue: breakEvenRevenue,
                    ProductLines: lines.OrderBy(l => l.ProductName).ToList());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Multi-product BreakEven failed for tenant {TenantId} period {Period}", tenantId.Value, period);
                return new MultiProductBreakEven(
                    tenantId, period, DateTime.UtcNow, FinancialModelVersion.Initial,
                    TotalFixedCost: 0m,
                    WeightedContributionMargin: 0m,
                    WeightedContributionMarginRatio: 0m,
                    BreakEvenRevenue: decimal.MaxValue,
                    ProductLines: Array.Empty<ProductBreakEvenLine>());
            }
        }

        private async Task<int> GetUnitsSoldAsync(TenantId tenantId, AccountingPeriod period, CancellationToken ct)
        {
            DateTime periodStart = period.StartDate;
            DateTime periodEnd = period.StartDate.AddMonths(1);
            IEnumerable<Order> orders = await _orderRepository.GetByDateRangeAsync(tenantId, periodStart, periodEnd, ct).ConfigureAwait(false);
            return orders.Sum(o => o.Items.Sum(i => i.Quantity));
        }

        private static BreakEvenAnalysis InsufficientData(TenantId tenantId, AccountingPeriod period, FinancialModelVersion version, string warning)
            => new(
                tenantId, period, DateTime.UtcNow, version,
                TotalFixedCost: 0m,
                TotalRevenue: 0m,
                TotalVariableCost: 0m,
                TotalContributionMargin: 0m,
                ContributionMarginRatio: 0m,
                BreakEvenRevenue: decimal.MaxValue,
                BreakEvenUnits: decimal.MaxValue,
                MarginOfSafetyRevenue: 0m,
                MarginOfSafetyPercent: 0m,
                Status: BreakEvenStatus.InsufficientData,
                WarningMessage: warning,
                SourceAccountCodes: SourceAccountCodes);

        private static MultiProductBreakEven EmptyMulti(TenantId tenantId, AccountingPeriod period, BusinessProfile profile)
            => new(
                tenantId, period, DateTime.UtcNow, profile.Version,
                TotalFixedCost: profile.TotalMonthlyFixedCost,
                WeightedContributionMargin: 0m,
                WeightedContributionMarginRatio: 0m,
                BreakEvenRevenue: decimal.MaxValue,
                ProductLines: Array.Empty<ProductBreakEvenLine>());

        private static BreakEvenStatus ClassifyStatus(decimal revenue, decimal breakEvenRevenue, decimal cmRatio)
        {
            if (cmRatio <= 0m)
                return BreakEvenStatus.BelowBreakEven; // CM ≤ 0 → never reaches break-even
            if (revenue <= 0m)
                return BreakEvenStatus.BelowBreakEven;
            decimal tolerance = revenue * AtBreakEvenToleranceRatio;
            decimal delta = revenue - breakEvenRevenue;
            if (Math.Abs(delta) <= tolerance)
                return BreakEvenStatus.AtBreakEven;
            return delta > 0m ? BreakEvenStatus.AboveBreakEven : BreakEvenStatus.BelowBreakEven;
        }

        private static string? BuildWarning(BusinessProfile profile, decimal cmRatio, int unitsSold)
        {
            if (profile.TotalMonthlyFixedCost == 0m)
                return "Chưa nhập fixed costs — BreakEvenRevenue = 0 (FIXED_COST_ZERO)";
            if (cmRatio <= 0m)
                return "Biên đóng góp âm — cần giảm giá vốn hoặc tăng giá bán (CM_RATIO_ZERO_OR_NEG)";
            if (unitsSold == 0)
                return "Chưa có đơn hàng trong kỳ — BreakEvenUnits không khả dụng";
            return null;
        }
    }
}
