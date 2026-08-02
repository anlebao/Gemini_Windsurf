using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VanAn.CoreHub.Infrastructure;
using VanAn.Shared.Domain;

namespace VanAn.Gateway.Controllers
{
    /// <summary>
    /// Loyalty Alliance Phase 3A: SystemAdmin API for LoyaltyGlobalConfig + LoyaltyTenantConfig CRUD.
    /// Auth: SystemAdmin policy (JWT) on all endpoints.
    /// GET endpoints are read-only (any SystemAdmin can view).
    /// PUT endpoints mutate config + record LastChangedBy from JWT sub claim.
    /// Spec: docs/specs/loyalty-alliance-spec.md v1.0.
    /// </summary>
    [ApiController]
    [Route("api/platform/loyalty")]
    [Authorize(Policy = "SystemAdmin")]
    public class LoyaltyConfigController(
        IVanAnDbContext dbContext,
        ILogger<LoyaltyConfigController> logger) : ControllerBase
    {
        private readonly IVanAnDbContext _dbContext = dbContext;
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
        /// Body: { mode, maxPointsPerOrder, maxWalletPoints }
        /// PointsRate + MinPointsPerOrder are not editable via this endpoint (reserved for future).
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

            string changedBy = GetChangedBy();

            var config = await _dbContext.LoyaltyGlobalConfigs.FirstOrDefaultAsync();
            if (config == null)
            {
                config = new LoyaltyGlobalConfig();
                _ = _dbContext.LoyaltyGlobalConfigs.Add(config);
            }

            config.UpdateMode(body.Mode, changedBy);
            config.UpdateLimits(body.MaxPointsPerOrder, body.MaxWalletPoints, changedBy);

            _ = await _dbContext.SaveChangesAsync();
            _logger.LogInformation("LoyaltyConfig: global config updated by {User} — mode={Mode}, maxPointsPerOrder={MaxPoints}, maxWalletPoints={MaxWallet}",
                changedBy, body.Mode, body.MaxPointsPerOrder, body.MaxWalletPoints);

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
                    MaxWalletPoints = null // inherit global
                });
            }

            return Ok(TenantConfigDto.From(config));
        }

        /// <summary>
        /// PUT /api/platform/loyalty/tenant/{tenantId}/config — updates or creates per-tenant override.
        /// Body: { mode, isAllianceMember, maxWalletPoints }
        /// Null mode = inherit global. Null maxWalletPoints = inherit global.
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

            _ = await _dbContext.SaveChangesAsync();
            _logger.LogInformation("LoyaltyConfig: tenant {TenantId} config updated by {User} — mode={Mode}, isMember={IsMember}, maxWallet={MaxWallet}",
                tenantId, changedBy, body.Mode?.ToString() ?? "inherit", body.IsAllianceMember, body.MaxWalletPoints?.ToString() ?? "inherit");

            return Ok(TenantConfigDto.From(config));
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
        public int MaxPointsPerOrder { get; set; }
        public int MaxWalletPoints { get; set; }
    }

    public class TenantConfigDto
    {
        public Guid TenantId { get; set; }
        public LoyaltyMode? Mode { get; set; } // null = inherit global
        public bool IsAllianceMember { get; set; }
        public int? MaxWalletPoints { get; set; } // null = inherit global
        public DateTime? LastChangedAt { get; set; }
        public string? LastChangedBy { get; set; }

        public static TenantConfigDto From(LoyaltyTenantConfig c) => new()
        {
            TenantId = c.TenantId.Value,
            Mode = c.Mode,
            IsAllianceMember = c.IsAllianceMember,
            MaxWalletPoints = c.MaxWalletPoints,
            LastChangedAt = c.LastChangedAt,
            LastChangedBy = c.LastChangedBy
        };
    }

    public class UpdateTenantConfigRequest
    {
        public LoyaltyMode? Mode { get; set; } // null = inherit global
        public bool IsAllianceMember { get; set; }
        public int? MaxWalletPoints { get; set; } // null = inherit global
    }
}
