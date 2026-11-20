using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VanAn.CoreHub.Domain.Repositories;
using VanAn.CoreHub.Infrastructure.Entities;
using VanAn.CoreHub.Services;
using VanAn.ShopERP.Infrastructure;
using VanAn.ShopERP.Services;
using VanAn.Shared.Domain;
using VanAn.Shared.Services;

namespace VanAn.ShopERP.Controllers
{
    /// <summary>
    /// KhachLink Profile Contextual UX v2: aggregation endpoint — 1 call trả về toàn bộ
    /// tín hiệu cần cho /profile context cards (profile+tier, orders, missions, push,
    /// vouchers sắp hết hạn, dismissed cards). Thay cho 4 call song song từ KhachLink.
    ///
    /// Mỗi section best-effort: section lỗi → trả default, KHÔNG fail cả response.
    /// Auth: X-Customer-Token (+ optional X-Customer-Device-Id cho guest-checkout orders).
    /// </summary>
    [ApiController]
    [Route("api/customer-context")]
    [AllowAnonymous]
    public class CustomerContextController(
        ShopERPDbContext dbContext,
        ICustomerTokenService customerTokenService,
        ICustomerRepository customerRepository,
        ILoyaltyRewardsService loyaltyRewardsService,
        VanAn.CoreHub.Services.LoyaltyReadRouter readRouter,
        IMissionService missionService,
        IPushSubscriptionRepository pushSubscriptionRepository,
        IRedemptionService redemptionService,
        ILogger<CustomerContextController> logger) : ControllerBase
    {
        private readonly ShopERPDbContext _dbContext = dbContext;
        private readonly ICustomerTokenService _customerTokenService = customerTokenService;
        private readonly ICustomerRepository _customerRepository = customerRepository;
        private readonly ILoyaltyRewardsService _loyaltyRewardsService = loyaltyRewardsService;
        private readonly VanAn.CoreHub.Services.LoyaltyReadRouter _readRouter = readRouter;
        private readonly IMissionService _missionService = missionService;
        private readonly IPushSubscriptionRepository _pushSubscriptionRepository = pushSubscriptionRepository;
        private readonly IRedemptionService _redemptionService = redemptionService;
        private readonly ILogger<CustomerContextController> _logger = logger;

        /// <summary>Voucher được coi "sắp hết hạn" trong N ngày tới (card voucher-expiring).</summary>
        private const int VoucherExpiringWindowDays = 7;
        /// <summary>Ngưỡng ngày không đặt đơn để trigger win-back card.</summary>
        private const int WinBackDaysThreshold = 30;

        // NOTE: active-order count uses OrderStatusId equality OR-chain below —
        // HashSet.Contains on Status.Value VO property is NOT EF-translatable (500 in RV).

        /// <summary>
        /// GET /api/customer-context/me — aggregated profile context for /profile page.
        /// </summary>
        [HttpGet("me")]
        public async Task<IActionResult> GetMyContext(
            [FromHeader(Name = "X-Customer-Token")] string? token,
            [FromHeader(Name = "X-Customer-Device-Id")] string? deviceId)
        {
            var customerId = _customerTokenService.ValidateToken(token ?? "");
            if (!customerId.HasValue)
                return Unauthorized(new { error = "Token không hợp lệ hoặc đã hết hạn." });

            var customer = await _customerRepository.GetByIdAsync(customerId.Value);
            if (customer == null) return NotFound(new { error = "Không tìm thấy khách hàng." });

            // Profile + loyalty balance/tier (same logic as GET /api/customers/me)
            var rewards = await _loyaltyRewardsService.GetOrCreateCustomerRewardsAsync(customerId.Value, customer.TenantId);
            int pointBalance = await _readRouter.GetEffectiveBalanceAsync(customer.TenantId.Value, customer.DeviceId, rewards.PointBalance);
            var tier = LoyaltyTierCalculator.Calc(pointBalance);
            var nextTier = LoyaltyTierCalculator.NextThreshold(pointBalance);

            // Orders — same predicate as CustomerOrdersController (CustomerId OR device guest checkout)
            string? devId = string.IsNullOrEmpty(deviceId) ? null : deviceId;
            var orderQuery = _dbContext.Orders
                .Where(o => !o.IsDeleted && (
                    o.CustomerId == customerId.Value ||
                    (devId != null && o.CustomerDeviceId == devId)));
            var activeOrderCount = await orderQuery.CountAsync(o =>
                o.Status == new OrderStatusId("pending") ||
                o.Status == new OrderStatusId("confirmed") ||
                o.Status == new OrderStatusId("preparing") ||
                o.Status == new OrderStatusId("ready") ||
                o.Status == new OrderStatusId("delivering"));
            var orderStats = await orderQuery
                .GroupBy(_ => 1)
                .Select(g => new
                {
                    Total = g.Count(),
                    LastAt = g.Max(o => o.CreatedAt)
                })
                .FirstOrDefaultAsync();

            // Missions — same shape as GET /api/missions/my/progress
            List<MissionContextDto> missions = new();
            try
            {
                var missionList = await _missionService.GetCustomerProgressAsync(customerId.Value);
                var completions = await _missionService.GetCustomerCompletionsAsync(customerId.Value);
                missions = missionList.Select(m =>
                {
                    var mc = completions.Where(c => c.MissionId == m.Id).ToList();
                    return new MissionContextDto
                    {
                        MissionId = m.Id,
                        Title = m.Title,
                        PointsReward = m.PointsReward,
                        IsOneTime = m.IsOneTime,
                        CompletionCount = mc.Count,
                        LastCompletedAt = mc.FirstOrDefault()?.CompletedAt,
                        IsCompleted = m.IsOneTime && mc.Count > 0
                    };
                }).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "customer-context: missions section failed for {CustomerId}", customerId.Value);
            }

            // Push subscriptions
            bool pushEnabled = false;
            try
            {
                var subs = await _pushSubscriptionRepository.GetByCustomerIdAsync(customerId.Value);
                pushEnabled = subs.Count > 0;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "customer-context: push section failed for {CustomerId}", customerId.Value);
            }

            // Vouchers — valid + expiring within window
            int expiringVouchers = 0;
            DateTime? soonestVoucherExpiry = null;
            try
            {
                var vouchers = await _redemptionService.GetCustomerVouchersAsync(customerId.Value);
                var now = DateTime.UtcNow;
                var windowEnd = now.AddDays(VoucherExpiringWindowDays);
                var expiring = vouchers
                    .Where(v => v.IsValid && v.ExpiresAt <= windowEnd)
                    .OrderBy(v => v.ExpiresAt)
                    .ToList();
                expiringVouchers = expiring.Count;
                soonestVoucherExpiry = expiring.FirstOrDefault()?.ExpiresAt;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "customer-context: vouchers section failed for {CustomerId}", customerId.Value);
            }

            // Dismissed context cards (active TTL only)
            var dismissedCardIds = new List<string>();
            try
            {
                var nowUtc = DateTime.UtcNow;
                dismissedCardIds = await _dbContext.CustomerContextDismissals
                    .Where(d => d.CustomerId == customerId.Value && d.ExpiresAt > nowUtc)
                    .Select(d => d.CardId)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "customer-context: dismissals section failed for {CustomerId}", customerId.Value);
            }

            int? daysSinceLastOrder = null;
            if (orderStats != null)
                daysSinceLastOrder = Math.Max(0, (int)(DateTime.UtcNow - orderStats.LastAt).TotalDays);

            return Ok(new CustomerContextResponse
            {
                Profile = new CustomerContextProfileDto
                {
                    CustomerId = customer.Id,
                    FullName = customer.FullName,
                    PhoneNumber = customer.PhoneNumber,
                    Tier = tier,
                    PointBalance = pointBalance,
                    IdentityLevel = customer.IdentityLevel.ToString(),
                    Birthday = customer.Birthday,
                    NextTierThreshold = nextTier
                },
                ActiveOrderCount = activeOrderCount,
                TotalOrderCount = orderStats?.Total ?? 0,
                LastOrderAt = orderStats?.LastAt,
                DaysSinceLastOrder = daysSinceLastOrder,
                Missions = missions,
                PushEnabled = pushEnabled,
                ExpiringVoucherCount = expiringVouchers,
                SoonestVoucherExpiry = soonestVoucherExpiry,
                DismissedCardIds = dismissedCardIds
            });
        }

        /// <summary>
        /// POST /api/customer-context/me/dismiss — ẩn 1 context card (TTL 7 ngày, upsert).
        /// </summary>
        [HttpPost("me/dismiss")]
        public async Task<IActionResult> DismissCard(
            [FromHeader(Name = "X-Customer-Token")] string? token,
            [FromBody] DismissCardRequest request)
        {
            var customerId = _customerTokenService.ValidateToken(token ?? "");
            if (!customerId.HasValue)
                return Unauthorized(new { error = "Token không hợp lệ hoặc đã hết hạn." });

            if (request == null || string.IsNullOrWhiteSpace(request.CardId) || request.CardId.Length > 64)
                return BadRequest(new { error = "CardId không hợp lệ." });

            var customer = await _customerRepository.GetByIdAsync(customerId.Value);
            if (customer == null) return NotFound(new { error = "Không tìm thấy khách hàng." });

            var cardId = request.CardId.Trim();
            var existing = await _dbContext.CustomerContextDismissals
                .FirstOrDefaultAsync(d => d.CustomerId == customerId.Value && d.CardId == cardId);

            if (existing != null)
            {
                existing.Refresh();
            }
            else
            {
                _dbContext.CustomerContextDismissals.Add(
                    new CustomerContextDismissalEntity(customer.TenantId, customerId.Value, cardId));
            }

            await _dbContext.SaveChangesAsync();
            return Ok(new { cardId, expiresInDays = CustomerContextDismissalEntity.DefaultTtlDays });
        }

        /// <summary>
        /// DELETE /api/customer-context/me/dismiss/{cardId} — khôi phục card đã ẩn (best-effort).
        /// </summary>
        [HttpDelete("me/dismiss/{cardId}")]
        public async Task<IActionResult> RestoreCard(
            [FromHeader(Name = "X-Customer-Token")] string? token,
            string cardId)
        {
            var customerId = _customerTokenService.ValidateToken(token ?? "");
            if (!customerId.HasValue)
                return Unauthorized(new { error = "Token không hợp lệ hoặc đã hết hạn." });

            var removed = await _dbContext.CustomerContextDismissals
                .Where(d => d.CustomerId == customerId.Value && d.CardId == cardId)
                .ExecuteDeleteAsync();

            return Ok(new { cardId, removed });
        }
    }

    // === DTOs ===

    public class CustomerContextResponse
    {
        public CustomerContextProfileDto Profile { get; set; } = new();
        public int ActiveOrderCount { get; set; }
        public int TotalOrderCount { get; set; }
        public DateTime? LastOrderAt { get; set; }
        public int? DaysSinceLastOrder { get; set; }
        public List<MissionContextDto> Missions { get; set; } = new();
        public bool PushEnabled { get; set; }
        public int ExpiringVoucherCount { get; set; }
        public DateTime? SoonestVoucherExpiry { get; set; }
        public List<string> DismissedCardIds { get; set; } = new();
    }

    public class CustomerContextProfileDto
    {
        public Guid CustomerId { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public string Tier { get; set; } = "Bronze";
        public int PointBalance { get; set; }
        public string IdentityLevel { get; set; } = "Social";
        public DateTime? Birthday { get; set; }
        public int? NextTierThreshold { get; set; }
    }

    public class MissionContextDto
    {
        public Guid MissionId { get; set; }
        public string Title { get; set; } = string.Empty;
        public int PointsReward { get; set; }
        public bool IsOneTime { get; set; }
        public int CompletionCount { get; set; }
        public DateTime? LastCompletedAt { get; set; }
        public bool IsCompleted { get; set; }
    }

    public class DismissCardRequest
    {
        public string CardId { get; set; } = string.Empty;
    }
}
