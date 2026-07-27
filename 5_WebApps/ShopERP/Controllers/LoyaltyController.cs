using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VanAn.CoreHub.Domain.Repositories;
using VanAn.CoreHub.Services;
using VanAn.ShopERP.Filters;
using VanAn.ShopERP.Services;
using VanAn.Shared.Domain;

namespace VanAn.ShopERP.Controllers
{
    /// <summary>
    /// W17-T2: Loyalty Dashboard — returns tier, point balance, and history for a customer.
    /// Tiered Auth Phase 2: adds POST /api/loyalty/redeem with verification gate (Verified required).
    /// </summary>
    [ApiController]
    [Route("api/loyalty")]
    [AllowAnonymous]
    [ResolveCustomerTenant]
    public class LoyaltyController(
        ILoyaltyRewardsService loyaltyService,
        ICustomerTokenService customerTokenService,
        ICustomerRepository customerRepository,
        ILogger<LoyaltyController> logger) : ControllerBase
    {
        private readonly ILoyaltyRewardsService _loyaltyService = loyaltyService;
        private readonly ICustomerTokenService _customerTokenService = customerTokenService;
        private readonly ICustomerRepository _customerRepository = customerRepository;
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

        /// <summary>
        /// POST /api/loyalty/redeem — deduct points from the authenticated customer.
        /// Tiered Auth Phase 2: requires IdentityLevel >= Verified. If customer is Social/Guest,
        /// returns HTTP 403 with upgrade-required payload so KhachLink UI can prompt OTP upgrade.
        /// </summary>
        [HttpPost("redeem")]
        public async Task<IActionResult> Redeem([FromHeader(Name = "X-Customer-Token")] string? token, [FromBody] RedeemRequest request)
        {
            var customerId = ValidateToken(token);
            if (!customerId.HasValue)
                return Unauthorized(new { error = "Token không hợp lệ hoặc đã hết hạn." });

            if (request.Points <= 0)
                return BadRequest(new { error = "Số điểm cần đổi phải lớn hơn 0." });

            // Fetch customer for proactive identity level check (avoids leaking exception details in non-dev).
            // The service-layer gate (SubtractPointsAsync) remains the authoritative enforcement point.
            var customer = await _customerRepository.GetByIdAsync(customerId.Value);
            if (customer == null)
                return NotFound(new { error = "Không tìm thấy khách hàng." });

            if (customer.IdentityLevel < IdentityLevel.Verified)
            {
                _logger.LogWarning("Redeem blocked at controller: customer {CustomerId} IdentityLevel={Current} < Verified",
                    customerId.Value, customer.IdentityLevel);
                return StatusCode(403, new RedeemBlockedResponse
                {
                    Error = "Tài khoản chưa được xác thực. Vui lòng nâng cấp qua OTP để đổi điểm.",
                    RequiresUpgrade = true,
                    CurrentLevel = customer.IdentityLevel.ToString(),
                    RequiredLevel = IdentityLevel.Verified.ToString()
                });
            }

            try
            {
                var success = await _loyaltyService.SubtractPointsAsync(customerId.Value, request.Points, request.Reason);
                if (!success)
                {
                    return BadRequest(new { error = "Không đủ điểm để đổi. Vui lòng kiểm tra số dư." });
                }

                var rewards = await _loyaltyService.GetCustomerRewardsAsync(customerId.Value);
                return Ok(new RedeemResponse
                {
                    Success = true,
                    NewBalance = rewards?.PointBalance ?? 0,
                    PointsRedeemed = request.Points
                });
            }
            catch (IdentityLevelNotSufficientException ex)
            {
                // Defense-in-depth: service gate should have prevented reaching here, but handle gracefully.
                _logger.LogWarning(ex, "IdentityLevelNotSufficientException reached controller for customer {CustomerId}", customerId.Value);
                return StatusCode(403, new RedeemBlockedResponse
                {
                    Error = "Tài khoản chưa được xác thực. Vui lòng nâng cấp qua OTP để đổi điểm.",
                    RequiresUpgrade = true,
                    CurrentLevel = ex.CurrentLevel.ToString(),
                    RequiredLevel = ex.RequiredLevel.ToString()
                });
            }
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
}
