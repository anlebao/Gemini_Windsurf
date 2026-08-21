using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VanAn.CoreHub.Services.FinancialIntelligence;
using VanAn.CoreHub.Services.FinancialIntelligence.Dtos;
using VanAn.Shared.Domain;

namespace VanAn.Gateway.Controllers
{
    /// <summary>
    /// VA-FI-MVP2 Phase 3 (2026-08-21): Financial Intelligence REST API — tenant-scoped.
    ///
    /// 7 endpoints:
    ///   GET    /api/financial/business-profile              → 200 BusinessProfileDto | 404 if not declared
    ///   PUT    /api/financial/business-profile              → 200 BusinessProfileDto (upsert)
    ///   GET    /api/financial/profit-summary?period=YYYY-MM → 200 ProfitSummaryDto
    ///   GET    /api/financial/break-even?period=YYYY-MM&standard=TT99_2025 → 200 BreakEvenAnalysisDto
    ///   GET    /api/financial/break-even/multi-product?period=YYYY-MM&standard=TT99_2025 → 200 MultiProductBreakEvenDto
    ///   GET    /api/financial/unit-economics?period=YYYY-MM → 200 UnitEconomicsReportDto
    ///   POST   /api/financial/target-profit                 → 200 TargetProfitAnalysisDto
    ///
    /// Architecture:
    ///   - W8 feature flag bypass: injects calculation services DIRECTLY (not via IncomeStatementsController).
    ///     HKD tenant (W8-blocked from VAS reports) still has access to Financial Intelligence.
    ///   - W12-G7: class-level [Authorize] with JwtBearer scheme (Pattern #10 + OcrConfigController precedent).
    ///   - TenantId extracted from JWT claim "tenant_id" (snake_case, OIDC standard — dual-read legacy "TenantId").
    ///   - W12-G1..G6 generic check covers this controller automatically (no exempt needed).
    /// </summary>
    [ApiController]
    [Route("api/financial")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public class FinancialIntelligenceController(
        IBusinessProfileService profileService,
        IProfitSummaryService profitSummaryService,
        IBreakEvenAnalysisService breakEvenService,
        IUnitEconomicsService unitEconomicsService,
        ITargetProfitService targetProfitService,
        ILogger<FinancialIntelligenceController> logger) : ControllerBase
    {
        private readonly IBusinessProfileService _profileService = profileService;
        private readonly IProfitSummaryService _profitSummaryService = profitSummaryService;
        private readonly IBreakEvenAnalysisService _breakEvenService = breakEvenService;
        private readonly IUnitEconomicsService _unitEconomicsService = unitEconomicsService;
        private readonly ITargetProfitService _targetProfitService = targetProfitService;
        private readonly ILogger<FinancialIntelligenceController> _logger = logger;

        // ── BusinessProfile ──────────────────────────────────────────────────────

        /// <summary>GET /api/financial/business-profile — tenant profile (or 404 if not yet declared).</summary>
        [HttpGet("business-profile")]
        public async Task<ActionResult<BusinessProfileDto>> GetProfile(CancellationToken ct)
        {
            Guid tenantGuid = GetTenantIdFromClaim();
            if (tenantGuid == Guid.Empty)
                return Unauthorized(new { error = "Missing tenant_id claim" });

            BusinessProfile? profile = await _profileService.GetAsync(new TenantId(tenantGuid), ct).ConfigureAwait(false);
            if (profile is null)
                return NotFound(new { error = "BusinessProfile not yet declared — PUT /api/financial/business-profile to create" });

            return Ok(MapProfile(profile));
        }

        /// <summary>PUT /api/financial/business-profile — upsert (create or update, Version increments on update).</summary>
        [HttpPut("business-profile")]
        public async Task<ActionResult<BusinessProfileDto>> UpdateProfile(
            [FromBody] UpdateBusinessProfileDto dto, CancellationToken ct)
        {
            if (dto is null)
                return BadRequest(new { error = "Request body is required" });

            Guid tenantGuid = GetTenantIdFromClaim();
            if (tenantGuid == Guid.Empty)
                return Unauthorized(new { error = "Missing tenant_id claim" });

            var cmd = new UpdateBusinessProfileCommand(
                dto.MonthlyRent, dto.MonthlyPayroll, dto.MonthlyUtilities,
                dto.MonthlyMarketing, dto.MonthlyLogistics, dto.MonthlyOtherOpEx,
                dto.MonthlyDepreciation,
                dto.DailyCapacityUnits, dto.OperatingDaysPerMonth,
                dto.PricingModel, dto.Notes);

            BusinessProfile updated = await _profileService.UpdateAsync(new TenantId(tenantGuid), cmd, ct).ConfigureAwait(false);
            _logger.LogInformation("BusinessProfile upserted for tenant {TenantId} — version {Version}", tenantGuid, updated.Version);
            return Ok(MapProfile(updated));
        }

        // ── Calculation endpoints ───────────────────────────────────────────────

        /// <summary>GET /api/financial/profit-summary?period=YYYY-MM[&amp;standard=TT99_2025]</summary>
        [HttpGet("profit-summary")]
        public async Task<ActionResult<ProfitSummaryDto>> GetProfitSummary(
            [FromQuery] string period,
            [FromQuery] AccountingStandard standard = AccountingStandard.TT99_2025,
            CancellationToken ct = default)
        {
            Guid tenantGuid = GetTenantIdFromClaim();
            if (tenantGuid == Guid.Empty)
                return Unauthorized(new { error = "Missing tenant_id claim" });

            if (!TryParsePeriod(period, out AccountingPeriod accountingPeriod))
                return BadRequest(new { error = "Invalid period format — expected YYYY-MM" });

            ProfitSummary summary = await _profitSummaryService.GetAsync(new TenantId(tenantGuid), accountingPeriod, standard, ct).ConfigureAwait(false);
            return Ok(MapProfitSummary(summary));
        }

        /// <summary>GET /api/financial/break-even?period=YYYY-MM&amp;standard=TT99_2025</summary>
        [HttpGet("break-even")]
        public async Task<ActionResult<BreakEvenAnalysisDto>> GetBreakEven(
            [FromQuery] string period,
            [FromQuery] AccountingStandard standard = AccountingStandard.TT99_2025,
            CancellationToken ct = default)
        {
            Guid tenantGuid = GetTenantIdFromClaim();
            if (tenantGuid == Guid.Empty)
                return Unauthorized(new { error = "Missing tenant_id claim" });

            if (!TryParsePeriod(period, out AccountingPeriod accountingPeriod))
                return BadRequest(new { error = "Invalid period format — expected YYYY-MM" });

            BreakEvenAnalysis analysis = await _breakEvenService.AnalyzeAsync(new TenantId(tenantGuid), accountingPeriod, standard, ct).ConfigureAwait(false);
            return Ok(MapBreakEven(analysis));
        }

        /// <summary>GET /api/financial/break-even/multi-product?period=YYYY-MM&amp;standard=TT99_2025</summary>
        [HttpGet("break-even/multi-product")]
        public async Task<ActionResult<MultiProductBreakEvenDto>> GetMultiProductBreakEven(
            [FromQuery] string period,
            [FromQuery] AccountingStandard standard = AccountingStandard.TT99_2025,
            CancellationToken ct = default)
        {
            Guid tenantGuid = GetTenantIdFromClaim();
            if (tenantGuid == Guid.Empty)
                return Unauthorized(new { error = "Missing tenant_id claim" });

            if (!TryParsePeriod(period, out AccountingPeriod accountingPeriod))
                return BadRequest(new { error = "Invalid period format — expected YYYY-MM" });

            MultiProductBreakEven result = await _breakEvenService.AnalyzeMultiProductAsync(new TenantId(tenantGuid), accountingPeriod, standard, ct).ConfigureAwait(false);
            return Ok(MapMultiProduct(result));
        }

        /// <summary>GET /api/financial/unit-economics?period=YYYY-MM</summary>
        [HttpGet("unit-economics")]
        public async Task<ActionResult<UnitEconomicsReportDto>> GetUnitEconomics(
            [FromQuery] string period,
            CancellationToken ct = default)
        {
            Guid tenantGuid = GetTenantIdFromClaim();
            if (tenantGuid == Guid.Empty)
                return Unauthorized(new { error = "Missing tenant_id claim" });

            if (!TryParsePeriod(period, out AccountingPeriod accountingPeriod))
                return BadRequest(new { error = "Invalid period format — expected YYYY-MM" });

            UnitEconomicsReport report = await _unitEconomicsService.AnalyzeAsync(new TenantId(tenantGuid), accountingPeriod, ct).ConfigureAwait(false);
            return Ok(MapUnitEconomics(report));
        }

        /// <summary>POST /api/financial/target-profit — body: { year, month, standard, targetProfit }</summary>
        [HttpPost("target-profit")]
        public async Task<ActionResult<TargetProfitAnalysisDto>> AnalyzeTargetProfit(
            [FromBody] TargetProfitRequestDto request, CancellationToken ct)
        {
            if (request is null)
                return BadRequest(new { error = "Request body is required" });
            if (request.TargetProfit < 0m)
                return BadRequest(new { error = "targetProfit must be >= 0" });

            Guid tenantGuid = GetTenantIdFromClaim();
            if (tenantGuid == Guid.Empty)
                return Unauthorized(new { error = "Missing tenant_id claim" });

            var period = new AccountingPeriod(request.Year, request.Month);
            TargetProfitAnalysis result = await _targetProfitService.AnalyzeAsync(
                new TenantId(tenantGuid), period, request.Standard, request.TargetProfit, ct).ConfigureAwait(false);
            return Ok(MapTargetProfit(result));
        }

        // ── Helpers ─────────────────────────────────────────────────────────────

        private Guid GetTenantIdFromClaim()
        {
            // Wave 1 Phase 2: standardized claim "tenant_id" (snake_case) + dual-read legacy "TenantId".
            string? tenantClaim = User.FindFirst("tenant_id")?.Value
                ?? User.FindFirst("TenantId")?.Value;
            return Guid.TryParse(tenantClaim, out Guid tenantId) ? tenantId : Guid.Empty;
        }

        /// <summary>Parse "YYYY-MM" (e.g. "2026-08") into AccountingPeriod.</summary>
        private static bool TryParsePeriod(string period, out AccountingPeriod result)
        {
            result = default;
            if (string.IsNullOrWhiteSpace(period))
                return false;
            var parts = period.Split('-', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 2 || !int.TryParse(parts[0], out int year) || !int.TryParse(parts[1], out int month))
                return false;
            if (month < 1 || month > 12 || year < 2000 || year > 2100)
                return false;
            result = new AccountingPeriod(year, month);
            return true;
        }

        private static BusinessProfileDto MapProfile(BusinessProfile p) => new(
            TenantId: p.TenantId.Value,
            MonthlyRent: p.MonthlyRent, MonthlyPayroll: p.MonthlyPayroll, MonthlyUtilities: p.MonthlyUtilities,
            MonthlyMarketing: p.MonthlyMarketing, MonthlyLogistics: p.MonthlyLogistics, MonthlyOtherOpEx: p.MonthlyOtherOpEx,
            MonthlyDepreciation: p.MonthlyDepreciation,
            TotalMonthlyFixedCost: p.TotalMonthlyFixedCost,
            DailyCapacityUnits: p.DailyCapacityUnits,
            OperatingDaysPerMonth: p.OperatingDaysPerMonth,
            PricingModel: p.PricingModel,
            Notes: p.Notes,
            Version: p.Version.ToString(),
            UpdatedAt: p.UpdatedAt);

        private static ProfitSummaryDto MapProfitSummary(ProfitSummary s) => new(
            TenantId: s.TenantId.Value,
            Year: s.Period.Year, Month: s.Period.Month,
            CalculatedAt: s.CalculatedAt,
            Revenue: s.Revenue, COGS: s.COGS, GrossProfit: s.GrossProfit, GrossMarginPercent: s.GrossMarginPercent,
            OperatingExpenses: s.OperatingExpenses, OperatingProfit: s.OperatingProfit,
            NetProfit: s.NetProfit, NetMarginPercent: s.NetMarginPercent,
            Status: s.Status, WarningMessage: s.WarningMessage);

        private static BreakEvenAnalysisDto MapBreakEven(BreakEvenAnalysis b) => new(
            TenantId: b.TenantId.Value,
            Year: b.Period.Year, Month: b.Period.Month,
            CalculatedAt: b.CalculatedAt,
            ModelVersion: b.ModelVersion.ToString(),
            TotalFixedCost: b.TotalFixedCost, TotalRevenue: b.TotalRevenue, TotalVariableCost: b.TotalVariableCost,
            TotalContributionMargin: b.TotalContributionMargin, ContributionMarginRatio: b.ContributionMarginRatio,
            BreakEvenRevenue: b.BreakEvenRevenue, BreakEvenUnits: b.BreakEvenUnits,
            MarginOfSafetyRevenue: b.MarginOfSafetyRevenue, MarginOfSafetyPercent: b.MarginOfSafetyPercent,
            Status: b.Status, WarningMessage: b.WarningMessage, SourceAccountCodes: b.SourceAccountCodes);

        private static MultiProductBreakEvenDto MapMultiProduct(MultiProductBreakEven m) => new(
            TenantId: m.TenantId.Value,
            Year: m.Period.Year, Month: m.Period.Month,
            CalculatedAt: m.CalculatedAt,
            ModelVersion: m.ModelVersion.ToString(),
            TotalFixedCost: m.TotalFixedCost,
            WeightedContributionMargin: m.WeightedContributionMargin,
            WeightedContributionMarginRatio: m.WeightedContributionMarginRatio,
            BreakEvenRevenue: m.BreakEvenRevenue,
            ProductLines: m.ProductLines.Select(l => new ProductBreakEvenLineDto(
                l.ProductId, l.ProductName, l.SellingPrice, l.VariableCost,
                l.ContributionMargin, l.ContributionMarginRatio, l.SalesMixPercent,
                l.UnitsSoldInPeriod, l.ProductBreakEvenUnits)).ToList());

        private static UnitEconomicsReportDto MapUnitEconomics(UnitEconomicsReport r) => new(
            TenantId: r.TenantId.Value,
            Year: r.Period.Year, Month: r.Period.Month,
            CalculatedAt: r.CalculatedAt,
            ModelVersion: r.ModelVersion.ToString(),
            Products: r.Products.Select(p => new UnitEconomicsLineDto(
                p.ProductId, p.ProductName, p.Category, p.SellingPrice, p.VariableCost,
                p.ContributionMargin, p.ContributionMarginPercent, p.UnitsSold, p.Revenue,
                p.ProfitContribution, p.ProfitContributionRank, p.HasMissingCostPrice)).ToList(),
            TotalProductsAnalyzed: r.TotalProductsAnalyzed,
            ProductsWithMissingCostPrice: r.ProductsWithMissingCostPrice,
            TotalContribution: r.TotalContribution,
            AverageContributionMargin: r.AverageContributionMargin);

        private static TargetProfitAnalysisDto MapTargetProfit(TargetProfitAnalysis t) => new(
            TenantId: t.TenantId.Value,
            Year: t.Period.Year, Month: t.Period.Month,
            CalculatedAt: t.CalculatedAt,
            ModelVersion: t.ModelVersion.ToString(),
            TargetProfit: t.TargetProfit, TotalFixedCost: t.TotalFixedCost,
            AverageContributionMargin: t.AverageContributionMargin,
            RequiredRevenue: t.RequiredRevenue, RequiredUnits: t.RequiredUnits,
            RequiredDailyUnits: t.RequiredDailyUnits,
            Feasible: t.Feasible, FeasibilityWarning: t.FeasibilityWarning);
    }
}
