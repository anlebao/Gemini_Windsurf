using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VanAn.CoreHub.Domain.Repositories;
using VanAn.CoreHub.Services;
using VanAn.Shared.Domain;

namespace VanAn.ShopERP.Controllers
{
    /// <summary>
    /// WS-2: Customer CRM controller — per-tenant customer list + segment preview.
    /// Auth: Cookie auth, [Authorize] (Owner/SystemAdmin — same as RedemptionController).
    /// </summary>
    [ApiController]
    [Route("api/customers")]
    [Authorize]
    public class CustomerController : ControllerBase
    {
        private readonly ICustomerRepository _customerRepository;
        private readonly ICustomerSegmentationService _customerSegmentationService;
        private readonly ILoyaltyRewardsService _loyaltyRewardsService;
        private readonly ILogger<CustomerController> _logger;

        public CustomerController(
            ICustomerRepository customerRepository,
            ICustomerSegmentationService customerSegmentationService,
            ILoyaltyRewardsService loyaltyRewardsService,
            ILogger<CustomerController> logger)
        {
            _customerRepository = customerRepository;
            _customerSegmentationService = customerSegmentationService;
            _loyaltyRewardsService = loyaltyRewardsService;
            _logger = logger;
        }

        /// <summary>List all active customers (paginated, tenant-scoped).</summary>
        [HttpGet]
        public async Task<IActionResult> List([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
        {
            if (page < 1) page = 1;
            if (pageSize < 1 || pageSize > 200) pageSize = 20;

            // Get all active customers then paginate in-memory (tenant filter applied by repository).
            var all = await _customerRepository.GetAllActiveAsync();
            var pageItems = all
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            // Enrich with loyalty point balance (lookup from LoyaltyRewards table)
            var dtos = new List<CustomerDto>(pageItems.Count);
            foreach (var c in pageItems)
            {
                var rewards = await _loyaltyRewardsService.GetCustomerRewardsAsync(c.Id);
                dtos.Add(MapCustomerDto(c, rewards?.PointBalance ?? 0));
            }

            return Ok(new { items = dtos, total = all.Count, page, pageSize });
        }

        /// <summary>Preview segment filter result (dry-run — no campaign created).</summary>
        [HttpPost("segment")]
        public async Task<IActionResult> PreviewSegment([FromBody] SegmentRequest request)
        {
            var criteria = BuildCriteria(request);
            var customers = await _customerSegmentationService.GetCustomersBySegmentAsync(criteria);

            // Enrich with loyalty point balance
            var dtos = new List<CustomerDto>(customers.Count);
            foreach (var c in customers)
            {
                var rewards = await _loyaltyRewardsService.GetCustomerRewardsAsync(c.Id);
                dtos.Add(MapCustomerDto(c, rewards?.PointBalance ?? 0));
            }

            _logger.LogInformation("PreviewSegment: {Count} customers matched criteria", customers.Count);
            return Ok(new { total = customers.Count, items = dtos });
        }

        // === Helpers ===

        internal static CustomerSegmentCriteria BuildCriteria(SegmentRequest r)
        {
            DateTime? lastOrderAfter = null;
            if (r.LastOrderAfter.HasValue) lastOrderAfter = r.LastOrderAfter.Value;

            return new CustomerSegmentCriteria(
                CustomerTier: string.IsNullOrWhiteSpace(r.CustomerTier) ? null : r.CustomerTier,
                MinIdentityLevel: r.MinIdentityLevel,
                MinTotalSpent: r.MinTotalSpent,
                MaxTotalSpent: r.MaxTotalSpent,
                LastOrderAfter: lastOrderAfter,
                LastOrderBefore: r.LastOrderBefore,
                HasPushSubscription: r.HasPushSubscription,
                MinPointBalance: r.MinPointBalance,
                MaxPointBalance: r.MaxPointBalance,
                BirthdayMonth: r.BirthdayMonth,
                LastOrderWithinDays: r.LastOrderWithinDays);
        }

        internal static CustomerDto MapCustomerDto(Customer c, int pointBalance) => new()
        {
            Id = c.Id,
            FullName = c.FullName,
            PhoneNumber = c.PhoneNumber,
            CustomerTier = c.CustomerTier,
            PointBalance = pointBalance,
            TotalSpent = c.TotalSpent,
            LastOrderDate = c.LastOrderDate,
            Birthday = c.Birthday,
            IdentityLevel = c.IdentityLevel.ToString(),
            IsActive = c.IsActive
        };
    }

    // === DTOs ===

    public class SegmentRequest
    {
        public string? CustomerTier { get; set; }
        public IdentityLevel? MinIdentityLevel { get; set; }
        public decimal? MinTotalSpent { get; set; }
        public decimal? MaxTotalSpent { get; set; }
        public DateTime? LastOrderAfter { get; set; }
        public DateTime? LastOrderBefore { get; set; }
        public bool HasPushSubscription { get; set; }
        // WS-2 filters
        public int? MinPointBalance { get; set; }
        public int? MaxPointBalance { get; set; }
        public int? BirthdayMonth { get; set; }
        public int? LastOrderWithinDays { get; set; }
    }

    public class CustomerDto
    {
        public Guid Id { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string? PhoneNumber { get; set; }
        public string CustomerTier { get; set; } = string.Empty;
        public int PointBalance { get; set; }
        public decimal TotalSpent { get; set; }
        public DateTime? LastOrderDate { get; set; }
        public DateTime? Birthday { get; set; }
        public string IdentityLevel { get; set; } = string.Empty;
        public bool IsActive { get; set; }
    }
}
