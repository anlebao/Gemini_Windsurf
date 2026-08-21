using Microsoft.Extensions.Logging;
using VanAn.CoreHub.Repositories;
using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Services.FinancialIntelligence
{
    /// <summary>
    /// VA-FI-MVP2 (2026-08-21): Implementation of <see cref="ITargetProfitService"/>.
    /// RequiredRevenue = (FixedCost + TargetProfit) / CMRatio.
    /// RequiredUnits = RequiredRevenue / AvgPrice (from period IncomeStatement + OrderItem aggregation).
    /// RequiredDailyUnits = RequiredUnits / OperatingDaysPerMonth.
    /// Feasible = RequiredDailyUnits &lt;= DailyCapacityUnits (CAPACITY_EXCEEDED guard when false).
    /// Pure deterministic — Trust Level 1 (NFR-14).
    /// </summary>
    public class TargetProfitService : ITargetProfitService
    {
        private readonly IBusinessProfileService _profileService;
        private readonly IIncomeStatementService _incomeStatementService;
        private readonly IOrderRepository _orderRepository;
        private readonly ILogger<TargetProfitService> _logger;

        public TargetProfitService(
            IBusinessProfileService profileService,
            IIncomeStatementService incomeStatementService,
            IOrderRepository orderRepository,
            ILogger<TargetProfitService> logger)
        {
            _profileService = profileService;
            _incomeStatementService = incomeStatementService;
            _orderRepository = orderRepository;
            _logger = logger;
        }

        /// <inheritdoc />
        public async Task<TargetProfitAnalysis> AnalyzeAsync(
            TenantId tenantId,
            AccountingPeriod period,
            AccountingStandard standard,
            decimal targetProfit,
            CancellationToken ct = default)
        {
            try
            {
                // Step 1 — Guard: PROFILE_MISSING
                BusinessProfile? profile = await _profileService.GetAsync(tenantId, ct).ConfigureAwait(false);
                if (profile is null)
                {
                    return Infeasible(tenantId, period, FinancialModelVersion.Initial,
                        targetProfit, 0m, 0m, 0m, 0m,
                        "Chưa khai báo BusinessProfile — cần nhập fixed costs (PROFILE_MISSING)");
                }

                // Step 2 — Load IncomeStatement (for CM ratio + avg price)
                IncomeStatement income = await _incomeStatementService.GenerateAsync(tenantId, period, standard, ct).ConfigureAwait(false);
                decimal revenue = income.TotalRevenueEnding;
                decimal variableCost = income.TotalCogsEnding;
                decimal contribution = revenue - variableCost;
                decimal cmRatio = revenue > 0m ? contribution / revenue : 0m;

                // Guard: INSUFFICIENT_DATA — no P&L movement (CM cannot be derived)
                if (revenue == 0m && variableCost == 0m)
                {
                    return Infeasible(tenantId, period, profile.Version,
                        targetProfit, profile.TotalMonthlyFixedCost, 0m, 0m, 0m,
                        $"Chưa có dữ liệu kế toán kỳ {period} (INSUFFICIENT_DATA)");
                }

                // Guard: CM_RATIO_ZERO_OR_NEG
                if (cmRatio <= 0m)
                {
                    return Infeasible(tenantId, period, profile.Version,
                        targetProfit, profile.TotalMonthlyFixedCost, cmRatio, 0m, 0m,
                        "Biên đóng góp ≤ 0 — không thể đạt lợi nhuận mục tiêu (CM_RATIO_ZERO_OR_NEG)");
                }

                // Step 3 — Required revenue / units
                decimal fixedCost = profile.TotalMonthlyFixedCost;
                decimal requiredRevenue = (fixedCost + targetProfit) / cmRatio;

                // Avg selling price from period orders
                int unitsSold = await GetUnitsSoldAsync(tenantId, period, ct).ConfigureAwait(false);
                decimal avgPrice = unitsSold > 0 ? revenue / unitsSold : 0m;
                decimal requiredUnits = avgPrice > 0m ? requiredRevenue / avgPrice : 0m;

                // Step 4 — Daily required + feasibility
                int operatingDays = profile.OperatingDaysPerMonth > 0 ? profile.OperatingDaysPerMonth : 30;
                decimal requiredDaily = requiredUnits / operatingDays;

                bool feasible = profile.DailyCapacityUnits > 0 && requiredDaily <= profile.DailyCapacityUnits;
                string? warning = feasible ? null
                    : profile.DailyCapacityUnits == 0
                        ? "Chưa nhập DailyCapacityUnits — không kiểm tra được tính khả thi (CAPACITY_EXCEEDED)"
                        : $"Cần {Math.Ceiling(requiredDaily):N0} đơn vị/ngày nhưng capacity chỉ {profile.DailyCapacityUnits}/ngày (CAPACITY_EXCEEDED)";

                return new TargetProfitAnalysis(
                    tenantId, period, DateTime.UtcNow, profile.Version,
                    TargetProfit: targetProfit,
                    TotalFixedCost: fixedCost,
                    AverageContributionMargin: cmRatio,
                    RequiredRevenue: requiredRevenue,
                    RequiredUnits: requiredUnits,
                    RequiredDailyUnits: requiredDaily,
                    Feasible: feasible,
                    FeasibilityWarning: warning);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "TargetProfit failed for tenant {TenantId} period {Period}", tenantId.Value, period);
                return Infeasible(tenantId, period, FinancialModelVersion.Initial,
                    targetProfit, 0m, 0m, 0m, 0m,
                    "Không thể tính TargetProfit — xem log hệ thống");
            }
        }

        private async Task<int> GetUnitsSoldAsync(TenantId tenantId, AccountingPeriod period, CancellationToken ct)
        {
            DateTime periodStart = period.StartDate;
            DateTime periodEnd = period.StartDate.AddMonths(1);
            IEnumerable<Order> orders = await _orderRepository.GetByDateRangeAsync(tenantId, periodStart, periodEnd, ct).ConfigureAwait(false);
            return orders.Sum(o => o.Items.Sum(i => i.Quantity));
        }

        private static TargetProfitAnalysis Infeasible(
            TenantId tenantId, AccountingPeriod period, FinancialModelVersion version,
            decimal targetProfit, decimal fixedCost, decimal cmRatio,
            decimal requiredRevenue, decimal requiredDaily,
            string warning)
            => new(
                tenantId, period, DateTime.UtcNow, version,
                TargetProfit: targetProfit,
                TotalFixedCost: fixedCost,
                AverageContributionMargin: cmRatio,
                RequiredRevenue: requiredRevenue,
                RequiredUnits: 0m,
                RequiredDailyUnits: requiredDaily,
                Feasible: false,
                FeasibilityWarning: warning);
    }
}
