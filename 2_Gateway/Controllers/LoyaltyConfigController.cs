using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VanAn.CoreHub.Infrastructure;
using VanAn.Shared.Domain;
using VanAn.Shared.Services;

namespace VanAn.Gateway.Controllers
{
    /// <summary>
    /// Loyalty Alliance Phase 3A: SystemAdmin API for LoyaltyGlobalConfig + LoyaltyTenantConfig CRUD.
    /// Phase 5A: added POST /migrate endpoint wiring Phase 4 Consolidate/SplitWalletsAsync.
    /// Auth: SystemAdmin policy (JWT) on all endpoints.
    /// GET endpoints are read-only (any SystemAdmin can view).
    /// PUT/POST endpoints mutate config + record LastChangedBy from JWT sub claim.
    /// Spec: docs/specs/loyalty-alliance-spec.md v1.0.
    /// </summary>
    [ApiController]
    [Route("api/platform/loyalty")]
    [Authorize(Policy = "SystemAdmin")]
    public class LoyaltyConfigController(
        IVanAnDbContext dbContext,
        IAllianceWalletService allianceWalletService,
        ILogger<LoyaltyConfigController> logger) : ControllerBase
    {
        private readonly IVanAnDbContext _dbContext = dbContext;
        private readonly IAllianceWalletService _allianceWalletService = allianceWalletService;
        private readonly ILogger<LoyaltyConfigController> _logger = logger;

        // === Global Config ===

        /// <summary>
        /// GET /api/platform/loyalty/config — returns the single global config row.
        /// If no row exists (fresh deployment), returns default values without creating a row.
        /// </summary>
        [HttpGet("config")]
        public async Task<IActionResult> GetGlobalConfig()
        {
            var config = await _dbContext.LoyaltyGlobalConfigs.FirstOrDefaultAsync();
            if (config == null)
            {
                // Return defaults without persisting (LoyaltyModeResolver seeds on first access)
                return Ok(new GlobalConfigDto
                {
                    Mode = LoyaltyMode.Silo,
                    PointsRate = 1,
                    MinPointsPerOrder = 10,
                    MaxPointsPerOrder = 30,
                    MaxWalletPoints = 100000
                });
            }

            return Ok(GlobalConfigDto.From(config));
        }

        /// <summary>
        /// PUT /api/platform/loyalty/config — updates the global config (creates if not exists).
        /// Body: { mode, pointsRate, minPointsPerOrder, maxPointsPerOrder, maxWalletPoints }
        /// Issue #118: PointsRate + MinPointsPerOrder now editable (were "reserved for future").
        /// PointsRate is int percent (1 = 1% of order total → 0.01 decimal rate in calculation).
        /// </summary>
        [HttpPut("config")]
        public async Task<IActionResult> UpdateGlobalConfig([FromBody] UpdateGlobalConfigRequest body)
        {
            if (body == null)
                return BadRequest(new { error = "Body không được để trống." });

            if (!Enum.IsDefined(typeof(LoyaltyMode), body.Mode))
                return BadRequest(new { error = "Mode không hợp lệ (Silo=0, Alliance=1)." });

            if (body.MaxPointsPerOrder < 0 || body.MaxWalletPoints < 0)
                return BadRequest(new { error = "Giới hạn điểm không được âm." });

            if (body.PointsRate < 0 || body.PointsRate > 100)
                return BadRequest(new { error = "PointsRate phải từ 0 đến 100 (phần trăm)." });

            if (body.MinPointsPerOrder < 0)
                return BadRequest(new { error = "MinPointsPerOrder không được âm." });

            string changedBy = GetChangedBy();

            var config = await _dbContext.LoyaltyGlobalConfigs.FirstOrDefaultAsync();
            if (config == null)
            {
                config = new LoyaltyGlobalConfig();
                _ = _dbContext.LoyaltyGlobalConfigs.Add(config);
            }

            config.UpdateMode(body.Mode, changedBy);
            config.UpdateLimits(body.MaxPointsPerOrder, body.MaxWalletPoints, changedBy);
            // Issue #118: apply PointsRate + MinPointsPerOrder (now editable)
            config.UpdatePointsFormula(body.PointsRate, body.MinPointsPerOrder, changedBy);

            _ = await _dbContext.SaveChangesAsync();
            _logger.LogInformation("LoyaltyConfig: global config updated by {User} — mode={Mode}, pointsRate={Rate}%, minPoints={Min}, maxPointsPerOrder={MaxPoints}, maxWalletPoints={MaxWallet}",
                changedBy, body.Mode, body.PointsRate, body.MinPointsPerOrder, body.MaxPointsPerOrder, body.MaxWalletPoints);

            return Ok(GlobalConfigDto.From(config));
        }

        // === Per-Tenant Config ===

        /// <summary>
        /// GET /api/platform/loyalty/tenant/{tenantId}/config — returns per-tenant override.
        /// If no row exists, returns defaults (null mode = inherit global, IsAllianceMember=false).
        /// </summary>
        [HttpGet("tenant/{tenantId}/config")]
        public async Task<IActionResult> GetTenantConfig(Guid tenantId)
        {
            if (tenantId == Guid.Empty)
                return BadRequest(new { error = "TenantId không hợp lệ." });

            var tenantIdValue = new TenantId(tenantId);
            var config = await _dbContext.LoyaltyTenantConfigs
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(c => c.TenantId == tenantIdValue);

            if (config == null)
            {
                return Ok(new TenantConfigDto
                {
                    TenantId = tenantId,
                    Mode = null, // inherit global
                    IsAllianceMember = false,
                    MaxWalletPoints = null, // inherit global
                    MonthlyPointsBudget = null, // unlimited
                    DailyPointsBudget = null,   // unlimited
                    PerCustomerDailyLimit = null, // unlimited
                    PerOrderRateCap = null      // no cap
                });
            }

            return Ok(TenantConfigDto.From(config));
        }

        /// <summary>
        /// PUT /api/platform/loyalty/tenant/{tenantId}/config — updates or creates per-tenant override.
        /// Body: { mode, isAllianceMember, maxWalletPoints, monthlyPointsBudget, dailyPointsBudget,
        ///        perCustomerDailyLimit, perOrderRateCap }
        /// Null mode = inherit global. Null maxWalletPoints = inherit global.
        /// Batch 3: budget caps (all nullable = unlimited) — monthlyPointsBudget/dailyPointsBudget/
        /// perCustomerDailyLimit are int points, perOrderRateCap is a decimal fraction (0.03 = 3%).
        /// </summary>
        [HttpPut("tenant/{tenantId}/config")]
        public async Task<IActionResult> UpdateTenantConfig(Guid tenantId, [FromBody] UpdateTenantConfigRequest body)
        {
            if (tenantId == Guid.Empty)
                return BadRequest(new { error = "TenantId không hợp lệ." });

            if (body == null)
                return BadRequest(new { error = "Body không được để trống." });

            if (body.Mode.HasValue && !Enum.IsDefined(typeof(LoyaltyMode), body.Mode.Value))
                return BadRequest(new { error = "Mode không hợp lệ (Silo=0, Alliance=1, null=inherit)." });

            if (body.MaxWalletPoints.HasValue && body.MaxWalletPoints.Value < 0)
                return BadRequest(new { error = "MaxWalletPoints không được âm." });

            // Batch 3: budget cap validation (null = unlimited)
            if (body.MonthlyPointsBudget.HasValue && body.MonthlyPointsBudget.Value < 0)
                return BadRequest(new { error = "MonthlyPointsBudget không được âm." });
            if (body.DailyPointsBudget.HasValue && body.DailyPointsBudget.Value < 0)
                return BadRequest(new { error = "DailyPointsBudget không được âm." });
            if (body.PerCustomerDailyLimit.HasValue && body.PerCustomerDailyLimit.Value < 0)
                return BadRequest(new { error = "PerCustomerDailyLimit không được âm." });
            if (body.PerOrderRateCap.HasValue && (body.PerOrderRateCap.Value < 0 || body.PerOrderRateCap.Value > 1))
                return BadRequest(new { error = "PerOrderRateCap phải từ 0 đến 1 (0.03 = 3% giá trị đơn)." });

            string changedBy = GetChangedBy();
            var tenantIdValue = new TenantId(tenantId);

            var config = await _dbContext.LoyaltyTenantConfigs
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(c => c.TenantId == tenantIdValue);

            if (config == null)
            {
                config = new LoyaltyTenantConfig(tenantIdValue);
                _ = _dbContext.LoyaltyTenantConfigs.Add(config);
            }

            config.SetMode(body.Mode, changedBy);
            config.SetAllianceMembership(body.IsAllianceMember, changedBy);
            config.SetMaxWalletPoints(body.MaxWalletPoints, changedBy);
            config.SetBudgetCaps(body.MonthlyPointsBudget, body.DailyPointsBudget, body.PerCustomerDailyLimit, body.PerOrderRateCap, changedBy);

            _ = await _dbContext.SaveChangesAsync();
            _logger.LogInformation("LoyaltyConfig: tenant {TenantId} config updated by {User} — mode={Mode}, isMember={IsMember}, maxWallet={MaxWallet}, monthlyBudget={Monthly}, dailyBudget={Daily}, perCustomerDaily={PerCustomerDaily}, perOrderRateCap={RateCap}",
                tenantId, changedBy, body.Mode?.ToString() ?? "inherit", body.IsAllianceMember, body.MaxWalletPoints?.ToString() ?? "inherit",
                body.MonthlyPointsBudget?.ToString() ?? "unlimited", body.DailyPointsBudget?.ToString() ?? "unlimited",
                body.PerCustomerDailyLimit?.ToString() ?? "unlimited", body.PerOrderRateCap?.ToString() ?? "unlimited");

            return Ok(TenantConfigDto.From(config));
        }

        /// <summary>
        /// POST /api/platform/loyalty/tenant/{tenantId}/reset-counters — resets this tenant's runtime
        /// budget counters (PointsIssuedToday / PointsIssuedThisMonth). SystemAdmin only.
        /// Body: { scope: "daily" | "monthly" }. Used by the LoyaltyConfigAdmin "Reset" button.
        /// Automatic global resets still run via LoyaltyBudgetDailyResetJob / LoyaltyBudgetMonthlyResetJob.
        /// </summary>
        [HttpPost("tenant/{tenantId}/reset-counters")]
        public async Task<IActionResult> ResetTenantCounters(Guid tenantId, [FromBody] ResetTenantCountersRequest body)
        {
            if (tenantId == Guid.Empty)
                return BadRequest(new { error = "TenantId không hợp lệ." });

            if (body == null || (body.Scope != "daily" && body.Scope != "monthly"))
                return BadRequest(new { error = "Scope không hợp lệ (phải là 'daily' hoặc 'monthly')." });

            var tenantIdValue = new TenantId(tenantId);
            var config = await _dbContext.LoyaltyTenantConfigs
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(c => c.TenantId == tenantIdValue);

            if (config == null)
                return NotFound(new { error = "Tenant chưa có cấu hình loyalty." });

            string changedBy = GetChangedBy();
            if (body.Scope == "daily")
            {
                config.ResetDailyCounter();
                _logger.LogInformation("LoyaltyConfig: tenant {TenantId} daily counter reset by {User}", tenantId, changedBy);
            }
            else
            {
                config.ResetMonthlyCounter();
                _logger.LogInformation("LoyaltyConfig: tenant {TenantId} monthly counter reset by {User}", tenantId, changedBy);
            }

            _ = await _dbContext.SaveChangesAsync();
            return Ok(TenantConfigDto.From(config));
        }

        // === Alliance Settlement Report (Batch 5, T5.2) ===

        /// <summary>
        /// GET /api/platform/loyalty/settlement?tenantId={id} — per-tenant Alliance settlement report.
        /// Gives SystemAdmin the data to decide cross-tenant compensation:
        ///   - pointsEarnedAtTenant:             points awarded (EARN) at this tenant.
        ///   - pointsConsumedAtTenant:           points of THIS tenant consumed anywhere (SourceTenantId == tenant).
        ///   - pointsConsumedAtOtherTenants:     subset of the above — consumed at OTHER tenants ("chi hộ").
        ///   - pointsRedeemedByCustomersAtTenant: points customers spent AT this tenant (TransactionTenantId == tenant).
        ///   - outstandingPoints:                pointsEarnedAtTenant − pointsConsumedAtTenant (net obligation).
        /// Report-only — no automatic settlement/money movement. SystemAdmin only.
        /// </summary>
        [HttpGet("settlement")]
        public async Task<IActionResult> GetSettlement([FromQuery] Guid tenantId)
        {
            if (tenantId == Guid.Empty)
                return BadRequest(new { error = "TenantId không hợp lệ." });

            var transactions = await _dbContext.AllianceTransactions
                .Where(t => t.TransactionTenantId == tenantId || t.SourceTenantId == tenantId)
                .ToListAsync();

            int pointsEarnedAtTenant = transactions
                .Where(t => t.TransactionTenantId == tenantId && t.Type == AllianceTransactionType.EARN)
                .Sum(t => t.Points);
            int pointsConsumedAtTenant = transactions
                .Where(t => t.SourceTenantId == tenantId && t.Type == AllianceTransactionType.REDEEM)
                .Sum(t => Math.Abs(t.Points));
            int pointsConsumedAtOtherTenants = transactions
                .Where(t => t.SourceTenantId == tenantId && t.TransactionTenantId != tenantId && t.Type == AllianceTransactionType.REDEEM)
                .Sum(t => Math.Abs(t.Points));
            int pointsRedeemedByCustomersAtTenant = transactions
                .Where(t => t.TransactionTenantId == tenantId && t.Type == AllianceTransactionType.REDEEM)
                .Sum(t => Math.Abs(t.Points));

            _logger.LogInformation("LoyaltyConfig: settlement report for tenant {TenantId} — earned={Earned}, consumed={Consumed} (atOther={AtOther}), redeemedAtTenant={Redeemed}",
                tenantId, pointsEarnedAtTenant, pointsConsumedAtTenant, pointsConsumedAtOtherTenants, pointsRedeemedByCustomersAtTenant);

            return Ok(new SettlementReportDto
            {
                TenantId = tenantId,
                PointsEarnedAtTenant = pointsEarnedAtTenant,
                PointsConsumedAtTenant = pointsConsumedAtTenant,
                PointsConsumedAtOtherTenants = pointsConsumedAtOtherTenants,
                PointsRedeemedByCustomersAtTenant = pointsRedeemedByCustomersAtTenant,
                OutstandingPoints = pointsEarnedAtTenant - pointsConsumedAtTenant
            });
        }

        // === Mode Switch Migration (Phase 5A — wires Phase 4 Consolidate/Split) ===

        /// <summary>
        /// POST /api/platform/loyalty/migrate — triggers Silo↔Alliance wallet migration for a tenant.
        /// Body: { direction, tenantId, customerBalances? }
        /// - direction="consolidate" (Silo→Alliance): caller MUST supply customerBalances from ShopERP SQLite
        ///   (Gateway is PG-only and cannot query per-tenant SQLite). Creates/credits AllianceWallet + ADJUST tx per customer.
        /// - direction="split" (Alliance→Silo): no balances needed. Calculates net EARN per-tenant from tx log,
        ///   distributes TotalPointBalance proportionally, freezes wallet, returns allocations so caller updates SQLite.
        /// Returns MigrationResultDto. SystemAdmin only.
        /// </summary>
        [HttpPost("migrate")]
        public async Task<IActionResult> Migrate([FromBody] MigrateRequest body)
        {
            if (body == null)
                return BadRequest(new { error = "Body không được để trống." });

            if (body.TenantId == Guid.Empty)
                return BadRequest(new { error = "TenantId không hợp lệ." });

            string changedBy = GetChangedBy();

            if (string.Equals(body.Direction, "consolidate", StringComparison.OrdinalIgnoreCase))
            {
                if (body.CustomerBalances == null || body.CustomerBalances.Count == 0)
                    return BadRequest(new { error = "Consolidate (Silo→Alliance) yêu cầu customerBalances từ ShopERP SQLite." });

                var balances = body.CustomerBalances
                    .Select(b => new CustomerBalanceInput(b.CustomerDeviceId, b.PointBalance, b.PhoneNumber))
                    .ToList();

                _logger.LogInformation("LoyaltyConfig: consolidate migration triggered by {User} for tenant {TenantId} — {Count} customers",
                    changedBy, body.TenantId, balances.Count);

                MigrationResult result = await _allianceWalletService.ConsolidateWalletsAsync(body.TenantId, balances, changedBy);
                return Ok(MigrationResultDto.From(result));
            }

            if (string.Equals(body.Direction, "split", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogInformation("LoyaltyConfig: split migration triggered by {User} for tenant {TenantId}",
                    changedBy, body.TenantId);

                MigrationResult result = await _allianceWalletService.SplitWalletsAsync(body.TenantId, changedBy);
                return Ok(MigrationResultDto.From(result));
            }

            return BadRequest(new { error = "Direction không hợp lệ (phải là 'consolidate' hoặc 'split')." });
        }

        // === Helpers ===

        private string GetChangedBy()
        {
            return User.FindFirst("sub")?.Value
                ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                ?? User.FindFirst("userId")?.Value
                ?? "unknown";
        }
    }

    // === DTOs ===

    public class GlobalConfigDto
    {
        public LoyaltyMode Mode { get; set; }
        public int PointsRate { get; set; }
        public int MinPointsPerOrder { get; set; }
        public int MaxPointsPerOrder { get; set; }
        public int MaxWalletPoints { get; set; }
        public DateTime? LastChangedAt { get; set; }
        public string? LastChangedBy { get; set; }

        public static GlobalConfigDto From(LoyaltyGlobalConfig c) => new()
        {
            Mode = c.Mode,
            PointsRate = c.PointsRate,
            MinPointsPerOrder = c.MinPointsPerOrder,
            MaxPointsPerOrder = c.MaxPointsPerOrder,
            MaxWalletPoints = c.MaxWalletPoints,
            LastChangedAt = c.LastChangedAt,
            LastChangedBy = c.LastChangedBy
        };
    }

    public class UpdateGlobalConfigRequest
    {
        public LoyaltyMode Mode { get; set; }
        public int PointsRate { get; set; } = 1;          // Issue #118: editable (1 = 1% of order total)
        public int MinPointsPerOrder { get; set; } = 10;  // Issue #118: editable
        public int MaxPointsPerOrder { get; set; }
        public int MaxWalletPoints { get; set; }
    }

    public class TenantConfigDto
    {
        public Guid TenantId { get; set; }
        public LoyaltyMode? Mode { get; set; } // null = inherit global
        public bool IsAllianceMember { get; set; }
        public int? MaxWalletPoints { get; set; } // null = inherit global
        // Batch 3 — budget caps (null = unlimited / no cap)
        public int? MonthlyPointsBudget { get; set; }
        public int? DailyPointsBudget { get; set; }
        public int? PerCustomerDailyLimit { get; set; }
        public decimal? PerOrderRateCap { get; set; } // fraction, e.g. 0.03m = 3%
        // Batch 3 — runtime counters (used for display in LoyaltyConfigAdmin)
        public int PointsIssuedThisMonth { get; set; }
        public int PointsIssuedToday { get; set; }
        public DateTime? LastChangedAt { get; set; }
        public string? LastChangedBy { get; set; }

        public static TenantConfigDto From(LoyaltyTenantConfig c) => new()
        {
            TenantId = c.TenantId.Value,
            Mode = c.Mode,
            IsAllianceMember = c.IsAllianceMember,
            MaxWalletPoints = c.MaxWalletPoints,
            MonthlyPointsBudget = c.MonthlyPointsBudget,
            DailyPointsBudget = c.DailyPointsBudget,
            PerCustomerDailyLimit = c.PerCustomerDailyLimit,
            PerOrderRateCap = c.PerOrderRateCap,
            PointsIssuedThisMonth = c.PointsIssuedThisMonth,
            PointsIssuedToday = c.PointsIssuedToday,
            LastChangedAt = c.LastChangedAt,
            LastChangedBy = c.LastChangedBy
        };
    }

    public class UpdateTenantConfigRequest
    {
        public LoyaltyMode? Mode { get; set; } // null = inherit global
        public bool IsAllianceMember { get; set; }
        public int? MaxWalletPoints { get; set; } // null = inherit global
        // Batch 3 — budget caps (null = unlimited / no cap)
        public int? MonthlyPointsBudget { get; set; }
        public int? DailyPointsBudget { get; set; }
        public int? PerCustomerDailyLimit { get; set; }
        public decimal? PerOrderRateCap { get; set; } // fraction, e.g. 0.03m = 3%
    }

    public class ResetTenantCountersRequest
    {
        /// <summary>"daily" or "monthly".</summary>
        public string Scope { get; set; } = "daily";
    }

    // === Migration DTOs (Phase 5A) ===

    public class MigrateRequest
    {
        /// <summary>"consolidate" (Silo→Alliance) or "split" (Alliance→Silo).</summary>
        public string Direction { get; set; } = "consolidate";
        public Guid TenantId { get; set; }
        /// <summary>Required for consolidate (caller queries ShopERP SQLite). Ignored for split.</summary>
        public List<CustomerBalanceInputDto>? CustomerBalances { get; set; }
    }

    public class CustomerBalanceInputDto
    {
        public Guid CustomerDeviceId { get; set; }
        public int PointBalance { get; set; }
        public string? PhoneNumber { get; set; }
    }

    public class MigrationResultDto
    {
        public int CustomersProcessed { get; set; }
        public int TotalPointsTransferred { get; set; }
        public List<WalletAllocationDto> Allocations { get; set; } = new();
        public string? Error { get; set; }
        public bool Success { get; set; }

        public static MigrationResultDto From(MigrationResult r) => new()
        {
            CustomersProcessed = r.CustomersProcessed,
            TotalPointsTransferred = r.TotalPointsTransferred,
            Allocations = r.Allocations
                .Select(a => new WalletAllocationDto { CustomerDeviceId = a.CustomerDeviceId, TenantId = a.TenantId, Points = a.Points })
                .ToList(),
            Error = r.Error,
            Success = r.Success
        };
    }

    public class WalletAllocationDto
    {
        public Guid CustomerDeviceId { get; set; }
        public Guid TenantId { get; set; }
        public int Points { get; set; }
    }

    // === Settlement DTO (Batch 5, T5.2) ===

    public class SettlementReportDto
    {
        public Guid TenantId { get; set; }
        /// <summary>Points awarded (EARN) at this tenant.</summary>
        public int PointsEarnedAtTenant { get; set; }
        /// <summary>Points of this tenant consumed anywhere (REDEEM with SourceTenantId == tenant).</summary>
        public int PointsConsumedAtTenant { get; set; }
        /// <summary>Points of this tenant consumed at OTHER tenants ("chi hộ" — subset of PointsConsumedAtTenant).</summary>
        public int PointsConsumedAtOtherTenants { get; set; }
        /// <summary>Points customers redeemed AT this tenant (REDEEM with TransactionTenantId == tenant).</summary>
        public int PointsRedeemedByCustomersAtTenant { get; set; }
        /// <summary>PointsEarnedAtTenant − PointsConsumedAtTenant — net outstanding obligation.</summary>
        public int OutstandingPoints { get; set; }
    }
}
