using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VanAn.CoreHub.Domain.Repositories;
using VanAn.CoreHub.Services;
using VanAn.Shared.Domain;

namespace VanAn.ShopERP.Controllers
{
    /// <summary>
    /// WS-2: Customer CRM controller — per-tenant customer list + segment preview.
    /// Auth: Cookie auth, [Authorize(Policy = "OwnerOnly")] (Owner/SystemAdmin only — AF-P0-T1).
    /// </summary>
    [ApiController]
    [Route("api/customers")]
    [Authorize(Policy = "OwnerOnly")]
    public class CustomerController : ControllerBase
    {
        private readonly ICustomerRepository _customerRepository;
        private readonly ICustomerSegmentationService _customerSegmentationService;
        private readonly ILoyaltyRewardsService _loyaltyRewardsService;
        private readonly IPushSubscriptionRepository _pushSubscriptionRepository;
        private readonly ILogger<CustomerController> _logger;

        public CustomerController(
            ICustomerRepository customerRepository,
            ICustomerSegmentationService customerSegmentationService,
            ILoyaltyRewardsService loyaltyRewardsService,
            IPushSubscriptionRepository pushSubscriptionRepository,
            ILogger<CustomerController> logger)
        {
            _customerRepository = customerRepository;
            _customerSegmentationService = customerSegmentationService;
            _loyaltyRewardsService = loyaltyRewardsService;
            _pushSubscriptionRepository = pushSubscriptionRepository;
            _logger = logger;
        }

        /// <summary>
        /// AF-P2-T5: Build a HashSet of CustomerIds that have at least one active push subscription
        /// in the current tenant. Single query per request — used to enrich CustomerDto.HasPushSubscription.
        /// </summary>
        private async Task<HashSet<Guid>> GetCustomerIdsWithPushAsync()
        {
            var subs = await _pushSubscriptionRepository.GetAllActiveAsync();
            var set = new HashSet<Guid>(subs.Count);
            foreach (var s in subs)
            {
                if (s.IsActive && !s.IsDeleted) set.Add(s.CustomerId);
            }
            return set;
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

            // AF-P2-T5: batch-load push subscription CustomerIds once per request
            var pushCustomerIds = await GetCustomerIdsWithPushAsync();

            // Enrich with loyalty point balance (lookup from LoyaltyRewards table)
            var dtos = new List<CustomerDto>(pageItems.Count);
            foreach (var c in pageItems)
            {
                var rewards = await _loyaltyRewardsService.GetCustomerRewardsAsync(c.Id);
                dtos.Add(MapCustomerDto(c, rewards?.PointBalance ?? 0, pushCustomerIds.Contains(c.Id)));
            }

            return Ok(new { items = dtos, total = all.Count, page, pageSize });
        }

        /// <summary>Preview segment filter result (dry-run — no campaign created).</summary>
        [HttpPost("segment")]
        public async Task<IActionResult> PreviewSegment([FromBody] SegmentRequest request)
        {
            var criteria = BuildCriteria(request);
            var customers = await _customerSegmentationService.GetCustomersBySegmentAsync(criteria);

            // AF-P2-T5: batch-load push subscription CustomerIds once per request
            var pushCustomerIds = await GetCustomerIdsWithPushAsync();

            // Enrich with loyalty point balance
            var dtos = new List<CustomerDto>(customers.Count);
            foreach (var c in customers)
            {
                var rewards = await _loyaltyRewardsService.GetCustomerRewardsAsync(c.Id);
                dtos.Add(MapCustomerDto(c, rewards?.PointBalance ?? 0, pushCustomerIds.Contains(c.Id)));
            }

            _logger.LogInformation("PreviewSegment: {Count} customers matched criteria", customers.Count);
            return Ok(new { total = customers.Count, items = dtos });
        }

        /// <summary>
        /// AF-P1-T1: List ALL active customers across ALL tenants (SystemAdmin-only cross-tenant view).
        /// Action-level [Authorize(Policy = "SystemAdmin")] combines with the controller-level
        /// [Authorize(Policy = "OwnerOnly")] to require SystemAdmin specifically (Owner/Staff → 403).
        /// Repository uses IgnoreQueryFilters to bypass the global TenantId filter.
        /// Optional filters: points range, last-order-within-days, birthday month, total-spent range.
        /// </summary>
        [HttpGet("global")]
        [Authorize(Policy = "SystemAdmin")]
        public async Task<IActionResult> ListGlobal(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20,
            [FromQuery] int? minPoints = null,
            [FromQuery] int? maxPoints = null,
            [FromQuery] int? lastOrderWithinDays = null,
            [FromQuery] int? birthdayMonth = null,
            [FromQuery] decimal? minTotalSpent = null,
            [FromQuery] decimal? maxTotalSpent = null)
        {
            if (page < 1) page = 1;
            if (pageSize < 1 || pageSize > 200) pageSize = 20;

            IReadOnlyList<Shared.Domain.Customer> all = await _customerRepository.GetAllCustomersAcrossTenantsAsync();

            // Apply optional SystemAdmin filters in-memory (cross-tenant set is bounded by active customers)
            IEnumerable<Shared.Domain.Customer> filtered = all;
            if (minPoints.HasValue || maxPoints.HasValue)
            {
                // Points live in LoyaltyRewards — resolve per customer (bounded set, acceptable for admin UI)
                var pointsByCustomer = new Dictionary<Guid, int>();
                foreach (var c in all)
                {
                    var rewards = await _loyaltyRewardsService.GetCustomerRewardsAsync(c.Id);
                    pointsByCustomer[c.Id] = rewards?.PointBalance ?? 0;
                }
                filtered = filtered.Where(c =>
                    (!minPoints.HasValue || pointsByCustomer.GetValueOrDefault(c.Id) >= minPoints.Value) &&
                    (!maxPoints.HasValue || pointsByCustomer.GetValueOrDefault(c.Id) <= maxPoints.Value));
            }

            if (lastOrderWithinDays.HasValue && lastOrderWithinDays.Value > 0)
            {
                DateTime cutoff = DateTime.UtcNow.AddDays(-lastOrderWithinDays.Value);
                filtered = filtered.Where(c => c.LastOrderDate.HasValue && c.LastOrderDate.Value >= cutoff);
            }

            if (birthdayMonth.HasValue && birthdayMonth.Value >= 1 && birthdayMonth.Value <= 12)
                filtered = filtered.Where(c => c.Birthday.HasValue && c.Birthday.Value.Month == birthdayMonth.Value);

            if (minTotalSpent.HasValue)
                filtered = filtered.Where(c => c.TotalSpent >= minTotalSpent.Value);
            if (maxTotalSpent.HasValue)
                filtered = filtered.Where(c => c.TotalSpent <= maxTotalSpent.Value);

            var materialized = filtered.ToList();
            var pageItems = materialized
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            var dtos = new List<GlobalCustomerDto>(pageItems.Count);
            foreach (var c in pageItems)
            {
                var rewards = await _loyaltyRewardsService.GetCustomerRewardsAsync(c.Id);
                dtos.Add(MapGlobalCustomerDto(c, rewards?.PointBalance ?? 0));
            }

            _logger.LogInformation("ListGlobal: {Total} cross-tenant customers (page {Page}/{PageSize})",
                materialized.Count, page, pageSize);

            return Ok(new { items = dtos, total = materialized.Count, page, pageSize });
        }

        /// <summary>
        /// AF-P3-T3: Export the current segment filter result as CSV.
        /// Accepts the same SegmentRequest body as POST /segment. Returns a CSV file with
        /// columns: Name, Phone, Tier, Points, TotalSpent, LastOrder, Birthday, IdentityLevel.
        /// Auth: OwnerOnly (controller-level). Tenant-scoped via repository global filter.
        /// </summary>
        [HttpPost("export")]
        public async Task<IActionResult> ExportCsv([FromBody] SegmentRequest request)
        {
            var criteria = BuildCriteria(request);
            IReadOnlyList<Customer> customers = await _customerSegmentationService.GetCustomersBySegmentAsync(criteria);

            // AF-P2-T5: batch-load push subscription CustomerIds once per request
            var pushCustomerIds = await GetCustomerIdsWithPushAsync();

            var sb = new System.Text.StringBuilder();
            sb.AppendLine("Name,Phone,Tier,Points,TotalSpent,LastOrder,Birthday,IdentityLevel,HasPush");
            foreach (var c in customers)
            {
                var rewards = await _loyaltyRewardsService.GetCustomerRewardsAsync(c.Id);
                int points = rewards?.PointBalance ?? 0;
                sb.Append(CsvEscape(c.FullName)).Append(',')
                  .Append(CsvEscape(c.PhoneNumber ?? string.Empty)).Append(',')
                  .Append(CsvEscape(c.CustomerTier ?? string.Empty)).Append(',')
                  .Append(points).Append(',')
                  .Append(c.TotalSpent).Append(',')
                  .Append(c.LastOrderDate?.ToString("yyyy-MM-dd") ?? string.Empty).Append(',')
                  .Append(c.Birthday?.ToString("yyyy-MM-dd") ?? string.Empty).Append(',')
                  .Append(CsvEscape(c.IdentityLevel.ToString())).Append(',')
                  .Append(pushCustomerIds.Contains(c.Id) ? "true" : "false")
                  .AppendLine();
            }

            _logger.LogInformation("ExportCsv: {Count} customers exported", customers.Count);

            byte[] bytes = System.Text.Encoding.UTF8.GetBytes(sb.ToString());
            return File(bytes, "text/csv", "customers.csv");
        }

        /// <summary>Minimal CSV field escaping — wraps in quotes if it contains comma/quote/newline, doubles inner quotes.</summary>
        private static string CsvEscape(string? field)
        {
            if (string.IsNullOrEmpty(field)) return string.Empty;
            if (field.IndexOfAny(new[] { ',', '"', '\n', '\r' }) < 0) return field;
            return "\"" + field.Replace("\"", "\"\"") + "\"";
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

        internal static CustomerDto MapCustomerDto(Customer c, int pointBalance, bool hasPushSubscription = false) => new()
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
            IsActive = c.IsActive,
            HasPushSubscription = hasPushSubscription
        };

        internal static GlobalCustomerDto MapGlobalCustomerDto(Customer c, int pointBalance) => new()
        {
            Id = c.Id,
            TenantId = c.TenantId.Value,
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
        /// <summary>AF-P2-T5: true if customer has ≥1 active push subscription in this tenant.</summary>
        public bool HasPushSubscription { get; set; }
    }

    /// <summary>
    /// AF-P1-T1: Cross-tenant customer DTO for SystemAdmin global view.
    /// Adds TenantId (not present in tenant-scoped CustomerDto) so the UI can group/sort by tenant.
    /// </summary>
    public class GlobalCustomerDto
    {
        public Guid Id { get; set; }
        public Guid TenantId { get; set; }
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
