using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Text.Json;
using VanAn.CoreHub.Infrastructure;
using VanAn.CoreHub.Services;
using VanAn.Shared.Domain;
using VanAn.Shared.Services;

namespace VanAn.Gateway.Controllers
{
    /// <summary>
    /// W17-T2: Gateway forward controller for Loyalty Dashboard.
    /// Forwards X-Customer-Token from KhachLink to ShopERP's LoyaltyController.
    /// Tiered Auth Phase 2: adds POST /api/loyalty/redeem forwarding.
    /// Loyalty Alliance Phase 3B: adds GET /api/loyalty/wallet (PG AllianceWallet query).
    /// Adds GET /api/loyalty/mode — public endpoint for KhachLink to query global LoyaltyMode
    /// (UI hides "Ví liên minh" when mode=Silo to avoid customer confusion).
    /// </summary>
    [ApiController]
    [Route("api/loyalty")]
    [AllowAnonymous]
    public class LoyaltyController(
        IHttpClientFactory httpClientFactory,
        IAllianceWalletService allianceWalletService,
        IVanAnDbContext dbContext,
        // Loyalty Points Integrity (Batch 4, T4.3): checkout estimate — server-side formula
        // resolution (tenant settings → PG LoyaltyGlobalConfig → appsettings) + mode.
        IShopFeatureSettingsService? shopFeatureSettingsService,
        IOptions<LoyaltyPointsConfig>? loyaltyPointsConfig,
        ILoyaltyModeResolver? loyaltyModeResolver,
        ILogger<LoyaltyController> logger) : ControllerBase
    {
        private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;
        private readonly IAllianceWalletService _allianceWalletService = allianceWalletService;
        private readonly IVanAnDbContext _dbContext = dbContext;
        private readonly IShopFeatureSettingsService? _shopFeatureSettingsService = shopFeatureSettingsService;
        private readonly IOptions<LoyaltyPointsConfig>? _loyaltyPointsConfig = loyaltyPointsConfig;
        private readonly ILoyaltyModeResolver? _loyaltyModeResolver = loyaltyModeResolver;
        private readonly ILogger<LoyaltyController> _logger = logger;

        /// <summary>
        /// GET /api/loyalty/mode — returns the global LoyaltyMode (Silo | Alliance).
        /// Public (anonymous) — KhachLink calls this on startup to decide whether
        /// to show "Ví liên minh" menu/icon. When mode=Silo, alliance wallet UI is hidden.
        /// </summary>
        /// <summary>
        /// Loyalty Points Integrity (Batch 4, T4.3): checkout points ESTIMATE computed server-side
        /// with the SAME single formula as the award — LoyaltyPointsCalculator, decision D1:
        /// base = NET revenue (subTotal − discountAmount; VAT + shipping excluded), clamped to
        /// [Min, Max]. KhachLink calls this instead of replicating the formula client-side
        /// (the old client code had no clamp, no discount, and included VAT + shipping).
        /// Response: { loyaltyEnabled, points, mode, netRevenue }.
        /// </summary>
        [HttpGet("estimate")]
        public async Task<IActionResult> GetPointsEstimate(
            [FromQuery] Guid tenantId,
            [FromQuery] decimal subTotal,
            [FromQuery] decimal discountAmount)
        {
            try
            {
                decimal netRevenue = LoyaltyPointsCalculator.NetRevenue(subTotal, discountAmount);

                // Config resolution (unchanged chain — mirrors the award path): appsettings
                // LoyaltyPoints → PG LoyaltyGlobalConfig (int % → /100) → tenant settings.
                decimal rate = _loyaltyPointsConfig?.Value.PointsRate ?? LoyaltyPointsCalculator.DefaultRate;
                int minPoints = _loyaltyPointsConfig?.Value.MinPointsPerOrder ?? 10;
                int? maxPoints = _loyaltyPointsConfig?.Value.MaxPointsPerOrder;
                bool awardOnAll = _loyaltyPointsConfig?.Value.AwardOnAllOrders ?? true;
                bool loyaltyEnabled = true;

                var globalConfig = await _dbContext.LoyaltyGlobalConfigs.FirstOrDefaultAsync();
                if (globalConfig != null && globalConfig.PointsRate > 0)
                {
                    rate = globalConfig.PointsRate / 100m;
                    minPoints = globalConfig.MinPointsPerOrder;
                    maxPoints = globalConfig.MaxPointsPerOrder;
                }

                if (_shopFeatureSettingsService is not null)
                {
                    try
                    {
                        var tenantSettings = await _shopFeatureSettingsService.GetSettingsAsync(tenantId);
                        loyaltyEnabled = tenantSettings.Loyalty_Program_Enabled;
                        if (tenantSettings.Loyalty_PointsRate > 0m) rate = tenantSettings.Loyalty_PointsRate;
                        if (tenantSettings.Loyalty_MinPointsPerOrder > 0) minPoints = tenantSettings.Loyalty_MinPointsPerOrder;
                        if (tenantSettings.Loyalty_MaxPointsPerOrder.HasValue) maxPoints = tenantSettings.Loyalty_MaxPointsPerOrder;
                        awardOnAll = tenantSettings.Loyalty_AwardOnAllOrders;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Estimate: failed to load tenant loyalty settings for tenant {TenantId} — using global default", tenantId);
                    }
                }

                // Mirror the award gate: loyalty disabled → 0 points; AwardOnAllOrders=false →
                // points only for campaign-referred orders (unknowable at checkout) → 0 (the
                // tracking banner then shows the REAL award). Never over-promise in the estimate.
                if (!loyaltyEnabled || !awardOnAll)
                {
                    return Ok(new LoyaltyEstimateDto { LoyaltyEnabled = loyaltyEnabled, Points = 0, Mode = "Silo", NetRevenue = netRevenue });
                }

                PointsFormulaMode mode = PointsFormulaMode.Silo;
                if (_loyaltyModeResolver is not null)
                {
                    try
                    {
                        if (await _loyaltyModeResolver.GetEffectiveModeAsync(tenantId) == LoyaltyMode.Alliance
                            && await _loyaltyModeResolver.IsAllianceMemberAsync(tenantId))
                        {
                            mode = PointsFormulaMode.Alliance;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Estimate: mode resolution failed for tenant {TenantId} — defaulting to Silo", tenantId);
                    }
                }

                int points = LoyaltyPointsCalculator.Calculate(netRevenue, new PointsFormula(rate, minPoints, maxPoints, mode));
                return Ok(new LoyaltyEstimateDto
                {
                    LoyaltyEnabled = true,
                    Points = points,
                    Mode = mode.ToString(),
                    NetRevenue = netRevenue
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error computing loyalty estimate for tenant {TenantId}", tenantId);
                return StatusCode(500, new { error = "Internal server error" });
            }
        }

        [HttpGet("mode")]
        public async Task<IActionResult> GetGlobalMode()
        {
            var config = await _dbContext.LoyaltyGlobalConfigs.FirstOrDefaultAsync();
            var mode = config?.Mode ?? LoyaltyMode.Silo;
            return Ok(new { mode = mode.ToString() });
        }

        [HttpGet("my")]
        public async Task<IActionResult> GetMyLoyalty()
        {
            try
            {
                var client = _httpClientFactory.CreateClient("shoperp");
                var reqMsg = new HttpRequestMessage(HttpMethod.Get, "/api/loyalty/my");
                if (Request.Headers.TryGetValue("X-Customer-Token", out var token))
                    reqMsg.Headers.Add("X-Customer-Token", token.ToString());

                var response = await client.SendAsync(reqMsg);
                var content = await response.Content.ReadAsStringAsync();
                var contentType = response.Content.Headers.ContentType?.MediaType ?? "application/json";
                return new ContentResult
                {
                    StatusCode = (int)response.StatusCode,
                    Content = content,
                    ContentType = contentType
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error forwarding GetMyLoyalty to ShopERP");
                return StatusCode(500, new { error = "Internal server error" });
            }
        }

        /// <summary>
        /// Tiered Auth Phase 2: Forward POST /api/loyalty/redeem to ShopERP.
        /// Passes X-Customer-Token header and request body through.
        /// </summary>
        [HttpPost("redeem")]
        public async Task<IActionResult> Redeem()
        {
            try
            {
                var client = _httpClientFactory.CreateClient("shoperp");
                var reqMsg = new HttpRequestMessage(HttpMethod.Post, "/api/loyalty/redeem");
                if (Request.Headers.TryGetValue("X-Customer-Token", out var token))
                    reqMsg.Headers.Add("X-Customer-Token", token.ToString());

                if (Request.ContentLength > 0)
                {
                    // FIX: Buffer the request body into a string before forwarding.
                    // StreamContent(Request.Body) fails in ASP.NET Core 8 because the request
                    // body stream (PipeReader-backed) may not support synchronous reads when
                    // HttpClient sends the content. Reading into StringContent is reliable.
                    Request.EnableBuffering();
                    using var reader = new System.IO.StreamReader(Request.Body, System.Text.Encoding.UTF8, leaveOpen: true);
                    var body = await reader.ReadToEndAsync();
                    reqMsg.Content = new StringContent(body, System.Text.Encoding.UTF8,
                        (Request.ContentType ?? "application/json").Split(';', StringSplitOptions.TrimEntries)[0]);
                }

                var response = await client.SendAsync(reqMsg);
                var content = await response.Content.ReadAsStringAsync();
                var contentType = response.Content.Headers.ContentType?.MediaType ?? "application/json";

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Forward POST /api/loyalty/redeem to ShopERP returned {StatusCode}: {Content}",
                        (int)response.StatusCode, content);
                }

                return new ContentResult
                {
                    StatusCode = (int)response.StatusCode,
                    Content = content,
                    ContentType = contentType
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error forwarding Redeem to ShopERP");
                return StatusCode(500, new { error = "Internal server error" });
            }
        }

        /// <summary>
        /// Loyalty Alliance Phase 3B: GET /api/loyalty/wallet — customer's cross-tenant wallet.
        /// Flow: resolve X-Customer-Token via ShopERP /api/loyalty/my-identity → get deviceId →
        /// query PG AllianceWallet + AllianceTransactions → return wallet DTO.
        /// Returns 401 if token invalid, 404 if wallet not found (customer not in alliance yet).
        /// </summary>
        [HttpGet("wallet")]
        public async Task<IActionResult> GetWallet()
        {
            try
            {
                // Step 1: Resolve customer token via ShopERP to get deviceId
                if (!Request.Headers.TryGetValue("X-Customer-Token", out var token) || string.IsNullOrEmpty(token))
                    return Unauthorized(new { error = "Thiếu X-Customer-Token header." });

                var client = _httpClientFactory.CreateClient("shoperp");
                var identityReq = new HttpRequestMessage(HttpMethod.Get, "/api/loyalty/my-identity");
                identityReq.Headers.Add("X-Customer-Token", token.ToString());
                var identityResp = await client.SendAsync(identityReq);

                if (!identityResp.IsSuccessStatusCode)
                    return new ContentResult
                    {
                        StatusCode = (int)identityResp.StatusCode,
                        Content = await identityResp.Content.ReadAsStringAsync(),
                        ContentType = "application/json"
                    };

                var identityJson = await identityResp.Content.ReadAsStringAsync();
                using var identityDoc = JsonDocument.Parse(identityJson);
                var deviceIdToken = identityDoc.RootElement.GetProperty("deviceId");

                // deviceId is Guid? — if null or zero, customer has no device identity → no alliance wallet
                if (deviceIdToken.ValueKind == JsonValueKind.Null || deviceIdToken.GetGuid() == Guid.Empty)
                    return NotFound(new { error = "Khách hàng chưa có device identity — chưa tham gia liên minh điểm thưởng." });

                Guid deviceId = deviceIdToken.GetGuid();

                // Step 2: Query PG AllianceWallet by deviceId
                var wallet = await _allianceWalletService.GetWalletByDeviceIdAsync(deviceId);
                if (wallet == null)
                    return Ok(new WalletResponse
                    {
                        TotalPointBalance = 0,
                        IsActive = false,
                        RecentTransactions = new List<WalletTransactionDto>()
                    });

                // Step 3: Query recent transactions
                var transactions = await _allianceWalletService.GetTransactionsAsync(wallet.Id, limit: 20);

                // Step 4: Build breakdown by tenant — Batch 5 (T5.1) attribution-correct net balance.
                // REDEEMs are charged to SourceTenantId (the tenant that OWNS the points), so a
                // cross-tenant redemption does not wrongly zero out the redeeming tenant. Negative
                // net (legacy edge cases) is clamped to 0 for display; sum over breakdown ≤ balance.
                var tenantBalances = await _allianceWalletService.GetTenantBalancesAsync(wallet.Id);
                var breakdown = tenantBalances
                    .Select(b => new WalletBreakdownDto
                    {
                        TenantId = b.TenantId,
                        Points = Math.Max(0, b.NetPoints)
                    })
                    .ToList();

                return Ok(new WalletResponse
                {
                    CustomerDeviceId = deviceId,
                    TotalPointBalance = wallet.TotalPointBalance,
                    IsActive = wallet.IsActive,
                    Breakdown = breakdown,
                    RecentTransactions = transactions.Select(t => new WalletTransactionDto
                    {
                        Id = t.Id,
                        TenantId = t.TransactionTenantId,
                        Type = t.Type.ToString(),
                        Points = t.Points,
                        BalanceAfter = t.BalanceAfter,
                        Reason = t.Reason,
                        VoucherCode = t.VoucherCode,
                        TransactionAt = t.TransactionAt
                    }).ToList()
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting alliance wallet");
                return StatusCode(500, new { error = "Internal server error" });
            }
        }
    }

    // === Estimate DTO ===

    public class LoyaltyEstimateDto
    {
        public bool LoyaltyEnabled { get; set; }
        public int Points { get; set; }
        public string Mode { get; set; } = "Silo";
        public decimal NetRevenue { get; set; }
    }

    // === Wallet DTOs ===

    public class WalletResponse
    {
        public Guid CustomerDeviceId { get; set; }
        public int TotalPointBalance { get; set; }
        public bool IsActive { get; set; }
        public List<WalletBreakdownDto> Breakdown { get; set; } = new();
        public List<WalletTransactionDto> RecentTransactions { get; set; } = new();
    }

    public class WalletBreakdownDto
    {
        public Guid TenantId { get; set; }
        public int Points { get; set; }
    }

    public class WalletTransactionDto
    {
        public Guid Id { get; set; }
        public Guid TenantId { get; set; }
        public string Type { get; set; } = string.Empty;
        public int Points { get; set; }
        public int BalanceAfter { get; set; }
        public string Reason { get; set; } = string.Empty;
        public string? VoucherCode { get; set; }
        public DateTime TransactionAt { get; set; }
    }
}
