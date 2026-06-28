using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VanAn.CoreHub.Services;
using VanAn.ShopERP.Services;

namespace VanAn.ShopERP.Controllers
{
    /// <summary>
    /// W17-T2: Loyalty Dashboard — returns tier, point balance, and history for a customer.
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [AllowAnonymous]
    public class LoyaltyController(
        ILoyaltyRewardsService loyaltyService,
        ICustomerTokenService customerTokenService,
        ILogger<LoyaltyController> logger) : ControllerBase
    {
        private readonly ILoyaltyRewardsService _loyaltyService = loyaltyService;
        private readonly ICustomerTokenService _customerTokenService = customerTokenService;
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

            var tier = CalcTier(rewards.PointBalance);
            var nextTierThreshold = GetNextTierThreshold(tier);

            return Ok(new LoyaltyResponse
            {
                CustomerId = customerId.Value,
                Tier = tier,
                PointBalance = rewards.PointBalance,
                NextTierThreshold = nextTierThreshold,
                ProgressPercent = nextTierThreshold > 0
                    ? Math.Min(100, (int)((double)rewards.PointBalance / nextTierThreshold * 100))
                    : 100,
                History = ParseHistory(rewards.History)
            });
        }

        private Guid? ValidateToken(string? token)
        {
            if (string.IsNullOrEmpty(token)) return null;
            return _customerTokenService.ValidateToken(token);
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
}
