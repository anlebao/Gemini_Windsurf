using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using VanAn.CoreHub.Domain.Repositories;
using VanAn.CoreHub.Infrastructure;
using VanAn.CoreHub.Services;
using VanAn.ShopERP.Filters;
using VanAn.ShopERP.Services;
using VanAn.Shared.Domain;
using VanAn.Shared.Domain.Common;
using VanAn.Shared.Services;

namespace VanAn.ShopERP.Controllers
{
    /// <summary>
    /// W17-T2: Loyalty Dashboard — returns tier, point balance, and history for a customer.
    /// Tiered Auth Phase 2: adds POST /api/loyalty/redeem with verification gate (Verified required).
    /// #99-3: adds GET /api/loyalty/dashboard — shop owner stats (4 metrics).
    /// </summary>
    [ApiController]
    [Route("api/loyalty")]
    [AllowAnonymous]
    [ResolveCustomerTenant]
    public class LoyaltyController(
        ILoyaltyRewardsService loyaltyService,
        ICustomerTokenService customerTokenService,
        ICustomerRepository customerRepository,
        VanAn.CoreHub.Services.LoyaltyReadRouter readRouter,
        IVanAnDbContext dbContext,
        ITenantProvider tenantProvider,
        IShopFeatureSettingsService? shopFeatureSettingsService,
        IOptions<LoyaltyPointsConfig>? loyaltyPointsConfig,
        ILogger<LoyaltyController> logger) : ControllerBase
    {
        private readonly ILoyaltyRewardsService _loyaltyService = loyaltyService;
        private readonly ICustomerTokenService _customerTokenService = customerTokenService;
        private readonly ICustomerRepository _customerRepository = customerRepository;
        private readonly VanAn.CoreHub.Services.LoyaltyReadRouter _readRouter = readRouter;
        private readonly IVanAnDbContext _dbContext = dbContext;
        private readonly ITenantProvider _tenantProvider = tenantProvider;
        private readonly IShopFeatureSettingsService? _shopFeatureSettingsService = shopFeatureSettingsService;
        private readonly IOptions<LoyaltyPointsConfig>? _loyaltyPointsConfig = loyaltyPointsConfig;
        private readonly ILogger<LoyaltyController> _logger = logger;

        /// <summary>GET /api/loyalty/my — returns loyalty info for the authenticated customer.</summary>
        [HttpGet("my")]
        public async Task<IActionResult> GetMyLoyalty([FromHeader(Name = "X-Customer-Token")] string? token)
        {
            var customerId = ValidateToken(token);
            if (!customerId.HasValue)
                return Unauthorized(new { error = "Token không hợp lệ hoặc đã hết hạn." });

            var rewards = await _loyaltyService.GetCustomerRewardsAsync(customerId.Value);
            if (rewards == null)
                return Ok(new LoyaltyResponse { Tier = "Bronze", PointBalance = 0, History = new() });

            // Loyalty Consistency Fix Phase 2 (BUG #4+#5): mode-aware balance.
            // Resolve customer for tenantId + deviceId (required for PG AllianceWallet lookup).
            var customer = await _customerRepository.GetByIdAsync(customerId.Value);
            int effectiveBalance = customer != null
                ? await _readRouter.GetEffectiveBalanceAsync(customer.TenantId.Value, customer.DeviceId, rewards.PointBalance)
                : rewards.PointBalance;

            var tier = CalcTier(effectiveBalance);
            var nextTierThreshold = GetNextTierThreshold(tier);

            return Ok(new LoyaltyResponse
            {
                CustomerId = customerId.Value,
                Tier = tier,
                PointBalance = effectiveBalance,
                NextTierThreshold = nextTierThreshold,
                ProgressPercent = nextTierThreshold > 0
                    ? Math.Min(100, (int)((double)effectiveBalance / nextTierThreshold * 100))
                    : 100,
                History = ParseHistory(rewards.History)
            });
        }

        /// <summary>
        /// POST /api/loyalty/redeem — DEPRECATED (Loyalty Consistency Fix Phase 1 / D3).
        /// Returns 410 Gone. Use POST /api/redemption/redeem (catalog-based) — has Alliance mode routing.
        /// </summary>
        [HttpPost("redeem")]
        [Obsolete("Use POST /api/redemption/redeem — catalog-based redeem with Alliance routing.")]
        public IActionResult Redeem()
        {
            _logger.LogInformation("Legacy /api/loyalty/redeem called — returning 410 Gone (deprecated, use /api/redemption/redeem)");
            return StatusCode(410, new { error = "Endpoint deprecated. Use POST /api/redemption/redeem." });
        }

        /// <summary>
        /// #99-3: GET /api/loyalty/dashboard — Shop owner loyalty stats (4 metrics).
        /// Returns: PointsPendingRedemption, PointsRedeemed, PointsInCampaigns, PointsReserved.
        /// Auth: Shop owner (cookie auth — ResolveCustomerTenant sets TenantId).
        /// </summary>
        [HttpGet("dashboard")]
        public async Task<IActionResult> GetDashboard()
        {
            try
            {
                Guid tenantId = _tenantProvider.TenantId;
                if (tenantId == Guid.Empty)
                {
                    return Unauthorized(new { error = "Không xác định được tenant." });
                }

                // Resolve loyalty rate (per-tenant override or global default)
                decimal rate = _loyaltyPointsConfig?.Value.PointsRate ?? 0.1m;
                if (_shopFeatureSettingsService != null)
                {
                    try
                    {
                        var settings = await _shopFeatureSettingsService.GetSettingsAsync(new TenantId(tenantId));
                        if (settings.Loyalty_PointsRate > 0m) rate = settings.Loyalty_PointsRate;
                    }
                    catch { /* fallback to global default */ }
                }

                // Metric 1: Points pending redemption (sum of all customer balances)
                int pendingRedemption = await _dbContext.LoyaltyRewards
                    .Where(lr => lr.TenantId == new TenantId(tenantId) && lr.IsActive)
                    .SumAsync(lr => (int?)lr.PointBalance) ?? 0;

                // Metric 2: Points redeemed (Fulfilled only — Cancelled already refunded)
                int redeemed = await _dbContext.RedemptionRecords
                    .Where(r => r.TenantId == new TenantId(tenantId) && r.Status == "Fulfilled")
                    .SumAsync(r => (int?)r.PointsSpent) ?? 0;

                // Metric 3: Points in active campaigns (pending orders with TrackingCode, not yet delivered/completed)
                var campaignOrderTotals = await _dbContext.Orders
                    .Where(o => o.TenantId == new TenantId(tenantId)
                        && o.TrackingCode != null
                        && o.Status.Value != "completed" && o.Status.Value != "cancelled"
                        && o.Status.Value != "delivered")
                    .Select(o => o.TotalAmount)
                    .ToListAsync();
                int pointsInCampaigns = campaignOrderTotals.Sum(a => (int)(a * rate));

                // Metric 4: Points reserved (ALL pending orders, not yet delivered/completed)
                var allPendingOrderTotals = await _dbContext.Orders
                    .Where(o => o.TenantId == new TenantId(tenantId)
                        && o.Status.Value != "completed" && o.Status.Value != "cancelled"
                        && o.Status.Value != "delivered")
                    .Select(o => o.TotalAmount)
                    .ToListAsync();
                int pointsReserved = allPendingOrderTotals.Sum(a => (int)(a * rate));

                return Ok(new LoyaltyDashboardStats
                {
                    PointsPendingRedemption = pendingRedemption,
                    PointsRedeemed = redeemed,
                    PointsInCampaigns = pointsInCampaigns,
                    PointsReserved = pointsReserved
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "#99-3: Error getting loyalty dashboard stats");
                return StatusCode(500, new { error = "Internal server error" });
            }
        }

        private Guid? ValidateToken(string? token)
        {
            if (string.IsNullOrEmpty(token)) return null;
            return _customerTokenService.ValidateToken(token);
        }

        /// <summary>
        /// Loyalty Alliance Phase 3B: GET /api/loyalty/my-identity — resolves X-Customer-Token
        /// to { customerId, deviceId, phoneNumber }. Called by Gateway wallet endpoint to
        /// resolve the customer's cross-tenant device identity before querying PG AllianceWallet.
        /// </summary>
        [HttpGet("my-identity")]
        public async Task<IActionResult> GetMyIdentity([FromHeader(Name = "X-Customer-Token")] string? token)
        {
            var customerId = ValidateToken(token);
            if (!customerId.HasValue)
                return Unauthorized(new { error = "Token không hợp lệ hoặc đã hết hạn." });

            var customer = await _customerRepository.GetByIdAsync(customerId.Value);
            if (customer == null)
                return NotFound(new { error = "Không tìm thấy khách hàng." });

            return Ok(new
            {
                customerId = customer.Id,
                deviceId = customer.DeviceId,
                phoneNumber = customer.PhoneNumber
            });
        }

        private static string CalcTier(int points) => points switch
        {
            >= 20000 => "Platinum",
            >= 5000  => "Gold",
            >= 1000  => "Silver",
            _        => "Bronze"
        };

        private static int GetNextTierThreshold(string currentTier) => currentTier switch
        {
            "Bronze"   => 1000,
            "Silver"   => 5000,
            "Gold"     => 20000,
            _          => 0
        };

        private static List<LoyaltyHistoryItem> ParseHistory(string? historyJson)
        {
            try
            {
                if (string.IsNullOrEmpty(historyJson)) return new();
                var items = System.Text.Json.JsonSerializer.Deserialize<List<LoyaltyHistoryItem>>(historyJson);
                return items?.OrderByDescending(h => h.Timestamp).Take(10).ToList() ?? new();
            }
            catch { return new(); }
        }
    }

    public class RedeemRequest
    {
        public int Points { get; set; }
        public string Reason { get; set; } = "Redeem reward";
    }

    public class RedeemResponse
    {
        public bool Success { get; set; }
        public int NewBalance { get; set; }
        public int PointsRedeemed { get; set; }
    }

    public class RedeemBlockedResponse
    {
        public string Error { get; set; } = string.Empty;
        public bool RequiresUpgrade { get; set; }
        public string CurrentLevel { get; set; } = string.Empty;
        public string RequiredLevel { get; set; } = string.Empty;
    }

    public class LoyaltyResponse
    {
        public Guid CustomerId { get; set; }
        public string Tier { get; set; } = "Bronze";
        public int PointBalance { get; set; }
        public int NextTierThreshold { get; set; }
        public int ProgressPercent { get; set; }
        public List<LoyaltyHistoryItem> History { get; set; } = new();
    }

    public class LoyaltyHistoryItem
    {
        public string Type { get; set; } = string.Empty;
        public int Points { get; set; }
        public string Reason { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        public int BalanceAfter { get; set; }
    }

    /// <summary>#99-3: Shop owner loyalty dashboard stats (4 metrics).</summary>
    public class LoyaltyDashboardStats
    {
        /// <summary>Metric 1: Total points in customer wallets (not yet redeemed).</summary>
        public int PointsPendingRedemption { get; set; }
        /// <summary>Metric 2: Total points redeemed (Fulfilled vouchers only).</summary>
        public int PointsRedeemed { get; set; }
        /// <summary>Metric 3: Points estimated for active campaign orders (pending, with TrackingCode).</summary>
        public int PointsInCampaigns { get; set; }
        /// <summary>Metric 4: Points reserved for ALL pending orders (not yet completed/delivered).</summary>
        public int PointsReserved { get; set; }
    }
}
