using Microsoft.Extensions.Logging;
using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Services.FinancialIntelligence
{
    /// <summary>
    /// VA-FI-MVP2 (2026-08-21): Implementation of <see cref="IProfitSummaryService"/>.
    /// Reads the extended <see cref="IncomeStatement"/> record (Phase 1 Option 2 additive fields:
    /// TotalCogsEnding, TotalOpExEnding). Pure deterministic — Trust Level 1.
    /// </summary>
    public class ProfitSummaryService : IProfitSummaryService
    {
        private readonly IIncomeStatementService _incomeStatementService;
        private readonly ILogger<ProfitSummaryService> _logger;

        private const decimal ProfitTolerance = 0.5m; // VND — at-break-even threshold

        public ProfitSummaryService(IIncomeStatementService incomeStatementService, ILogger<ProfitSummaryService> logger)
        {
            _incomeStatementService = incomeStatementService;
            _logger = logger;
        }

        /// <inheritdoc />
        public async Task<ProfitSummary> GetAsync(TenantId tenantId, AccountingPeriod period, AccountingStandard standard, CancellationToken ct = default)
        {
            try
            {
                IncomeStatement income = await _incomeStatementService.GenerateAsync(tenantId, period, standard, ct).ConfigureAwait(false);

                decimal revenue = income.TotalRevenueEnding;
                decimal cogs = income.TotalCogsEnding;
                decimal opex = income.TotalOpExEnding;
                decimal netProfit = income.NetProfitEnding;

                // Guard: INSUFFICIENT_DATA — no accounting movement for the period.
                if (revenue == 0m && cogs == 0m && opex == 0m && netProfit == 0m)
                {
                    _logger.LogInformation("ProfitSummary INSUFFICIENT_DATA for tenant {TenantId} period {Period}", tenantId.Value, period);
                    return new ProfitSummary(
                        tenantId, period, DateTime.UtcNow,
                        Revenue: 0m, COGS: 0m, GrossProfit: 0m, GrossMarginPercent: 0m,
                        OperatingExpenses: 0m, OperatingProfit: 0m, NetProfit: 0m, NetMarginPercent: 0m,
                        Status: ProfitStatus.InsufficientData,
                        WarningMessage: $"Chưa có dữ liệu kế toán kỳ {period}");
                }

                decimal grossProfit = revenue - cogs;
                decimal grossMargin = revenue > 0m ? grossProfit / revenue : 0m;
                decimal operatingProfit = grossProfit - opex;
                decimal netMargin = revenue > 0m ? netProfit / revenue : 0m;

                ProfitStatus status = ClassifyStatus(netProfit, revenue);

                return new ProfitSummary(
                    tenantId, period, DateTime.UtcNow,
                    Revenue: revenue,
                    COGS: cogs,
                    GrossProfit: grossProfit,
                    GrossMarginPercent: grossMargin,
                    OperatingExpenses: opex,
                    OperatingProfit: operatingProfit,
                    NetProfit: netProfit,
                    NetMarginPercent: netMargin,
                    Status: status,
                    WarningMessage: BuildWarning(status, cogs, opex));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ProfitSummary failed for tenant {TenantId} period {Period}", tenantId.Value, period);
                return new ProfitSummary(
                    tenantId, period, DateTime.UtcNow,
                    Revenue: 0m, COGS: 0m, GrossProfit: 0m, GrossMarginPercent: 0m,
                    OperatingExpenses: 0m, OperatingProfit: 0m, NetProfit: 0m, NetMarginPercent: 0m,
                    Status: ProfitStatus.InsufficientData,
                    WarningMessage: "Không thể tính ProfitSummary — xem log hệ thống");
            }
        }

        private static ProfitStatus ClassifyStatus(decimal netProfit, decimal revenue)
        {
            if (Math.Abs(netProfit) <= ProfitTolerance)
                return ProfitStatus.AtBreakEven;
            return netProfit > 0m ? ProfitStatus.Profitable : ProfitStatus.Loss;
        }

        private static string? BuildWarning(ProfitStatus status, decimal cogs, decimal opex)
        {
            return status switch
            {
                ProfitStatus.Loss when cogs == 0m && opex == 0m => "Lỗ — chưa nhập COGS/OpEx cho kỳ này",
                ProfitStatus.Loss => "Lỗ — xem lại giá vốn + chi phí vận hành",
                ProfitStatus.AtBreakEven => "Hòa vốn — biên lợi nhuận gần 0",
                _ => null
            };
        }
    }
}
