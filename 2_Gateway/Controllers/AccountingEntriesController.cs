using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VanAn.Shared.Domain;
using VanAn.CoreHub.Services;
using CoreAccountingEntry = VanAn.Shared.Domain.AccountingEntry;

namespace VanAn.Gateway.Controllers
{
    /// <summary>
    /// API Controller for Accounting Entries - Week 1 implementation
    /// Implements 5-layer protection: Domain, EF Core, Repository, Service, API
    ///
    /// T-07: Dual-route registration:
    ///   Primary  → /api/accounting-entries  (canonical REST name)
    ///   Alias    → /api/accounting          (short alias for E2E specs + backward compat)
    /// Both routes serve identical endpoints.
    /// </summary>
    [ApiController]
    [Route("api/accounting-entries")]
    [Route("api/accounting")]
    [Authorize(Policy = "RequireTenantAccess")]
    [Produces("application/json")]
    public class AccountingEntriesController(
        IAccountingService accountingEntryService,
        IReversalService reversalService,
        IHKDBookService hkdBookService,
        ILogger<AccountingEntriesController> logger) : ControllerBase
    {
        private readonly IAccountingService _accountingEntryService = accountingEntryService;
        private readonly IReversalService _reversalService = reversalService;
        private readonly IHKDBookService _hkdBookService = hkdBookService;
        private readonly ILogger<AccountingEntriesController> _logger = logger;

        /// <summary>
        /// Create a new revenue entry
        /// </summary>
        [HttpPost("revenue")]
        public async Task<ActionResult<CoreAccountingEntry>> CreateRevenueEntry([FromBody] CreateRevenueEntryRequest request)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                // Phase 1: Get tenant from JWT claim - fail fast if missing (ignore request.TenantId)
                Guid tenantGuid = GetTenantIdFromClaim();
                if (tenantGuid == Guid.Empty)
                {
                    _logger.LogWarning("CreateRevenueEntry rejected: missing TenantId claim");
                    return Unauthorized(new { error = "Tenant ID required in JWT claim" });
                }

                TenantId tenantId = new(tenantGuid);
                AccountingPeriod period = new(request.Year, request.Month);
                Money amount = new(request.Amount);
                Shared.DTOs.AccountingEntryDto entry = await _accountingEntryService.CreateRevenueEntryAsync(
                    tenantId, period, amount.Value, request.Description,
                    accountCode: request.AccountCode,
                    reference: request.Reference);

                _logger.LogInformation("Revenue entry created: {EntryId} for tenant {TenantId}",
                    entry.Id, tenantGuid);

                return CreatedAtAction(nameof(GetEntryById), new { id = entry.Id }, entry);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating revenue entry");
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// Create a new expense entry
        /// </summary>
        [HttpPost("expense")]
        public async Task<ActionResult<CoreAccountingEntry>> CreateExpenseEntry([FromBody] CreateExpenseEntryRequest request)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                // Phase 1: Get tenant from JWT claim - fail fast if missing (ignore request.TenantId)
                Guid tenantGuid = GetTenantIdFromClaim();
                if (tenantGuid == Guid.Empty)
                {
                    _logger.LogWarning("CreateExpenseEntry rejected: missing TenantId claim");
                    return Unauthorized(new { error = "Tenant ID required in JWT claim" });
                }

                TenantId tenantId = new(tenantGuid);
                AccountingPeriod period = new(request.Year, request.Month);
                Money amount = new(request.Amount);

                Shared.DTOs.AccountingEntryDto entry = await _accountingEntryService.CreateExpenseEntryAsync(
                    tenantId, period, amount.Value, request.Description,
                    accountCode: request.AccountCode,
                    vendor: request.Vendor,
                    category: request.Category,
                    reference: request.Reference);

                _logger.LogInformation("Expense entry created: {EntryId} for tenant {TenantId}",
                    entry.Id, tenantGuid);

                return CreatedAtAction(nameof(GetEntryById), new { id = entry.Id }, entry);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating expense entry");
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// Get an accounting entry by ID
        /// </summary>
        [HttpGet("{id}")]
        public async Task<ActionResult<CoreAccountingEntry>> GetEntryById(Guid id)
        {
            try
            {
                // Phase 1: Get tenant from JWT claim
                Guid tenantGuid = GetTenantIdFromClaim();
                if (tenantGuid == Guid.Empty)
                {
                    return Unauthorized(new { error = "Tenant ID required in JWT claim" });
                }
                TenantId tenantId = new(tenantGuid);

                // Use existing method to get entries by date range and filter by ID
                IEnumerable<Shared.DTOs.AccountingEntryDto> entries = await _accountingEntryService.GetEntriesByDateRangeAsync(tenantId, DateTime.MinValue, DateTime.MaxValue);
                Shared.DTOs.AccountingEntryDto? entry = entries.FirstOrDefault(e => e.Id == id);

                return entry == null ? (ActionResult<CoreAccountingEntry>)NotFound() : (ActionResult<CoreAccountingEntry>)Ok(entry);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving entry {EntryId}", id);
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// Get all entries for a tenant
        /// </summary>
        [HttpGet]
        public async Task<ActionResult<IEnumerable<CoreAccountingEntry>>> GetEntriesByTenant()
        {
            try
            {
                // Phase 1: Get tenant from JWT claim
                Guid tenantGuid = GetTenantIdFromClaim();
                if (tenantGuid == Guid.Empty)
                {
                    return Unauthorized(new { error = "Tenant ID required in JWT claim" });
                }
                TenantId tenantId = new(tenantGuid);

                IEnumerable<Shared.DTOs.AccountingEntryDto> entries = await _accountingEntryService.GetEntriesByDateRangeAsync(tenantId, new DateTime(2000, 1, 1), new DateTime(2100, 12, 31));
                return Ok(entries);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving entries for tenant");
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// Create a reversal entry
        /// </summary>
        [HttpPost("{id}/reversal")]
        public async Task<ActionResult<CoreAccountingEntry>> CreateReversalEntry(Guid id, [FromBody] CreateReversalEntryRequest request)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                // Phase 1: Get tenant from JWT claim
                Guid tenantGuid = GetTenantIdFromClaim();
                if (tenantGuid == Guid.Empty)
                {
                    return Unauthorized(new { error = "Tenant ID required in JWT claim" });
                }
                TenantId tenantId = new(tenantGuid);

                AccountingEntryId originalEntryId = new(id);

                // Check if entry can be reversed
                bool canReverse = await _reversalService.CanReverseEntryAsync(originalEntryId, tenantId);
                if (!canReverse)
                {
                    return BadRequest("Entry cannot be reversed");
                }

                CoreAccountingEntry reversalEntry = await _reversalService.CreateReversalEntryAsync(
                    originalEntryId, tenantId, request.Reason);

                _logger.LogInformation("Reversal entry created: {ReversalId} for original entry {OriginalId}",
                    reversalEntry.Id, id);

                return CreatedAtAction(nameof(GetEntryById), new { id = reversalEntry.Id }, reversalEntry);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating reversal entry for original entry {OriginalId}", id);
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// Get revenue summary for a period
        /// </summary>
        [HttpGet("revenue/summary")]
        public async Task<ActionResult<RevenueSummaryResponse>> GetRevenueSummary([FromQuery] int year, [FromQuery] int month)
        {
            try
            {
                // Phase 1: Get tenant from JWT claim
                Guid tenantGuid = GetTenantIdFromClaim();
                if (tenantGuid == Guid.Empty)
                {
                    return Unauthorized(new { error = "Tenant ID required in JWT claim" });
                }
                TenantId tenantId = new(tenantGuid);

                AccountingPeriod period = new(year, month);
                decimal total = await _hkdBookService.GetRevenueTotalAsync(tenantId, period);
                IEnumerable<CoreAccountingEntry> entries = await _hkdBookService.GetRevenueEntriesAsync(tenantId, period);

                RevenueSummaryResponse response = new()
                {
                    Period = $"{year}-{month:D2}",
                    TotalRevenue = total,
                    EntryCount = entries.Count(),
                    Entries = entries.ToList()
                };

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting revenue summary for period {Year}-{Month}", year, month);
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// Get expense summary for a period
        /// </summary>
        [HttpGet("expense/summary")]
        public async Task<ActionResult<ExpenseSummaryResponse>> GetExpenseSummary([FromQuery] int year, [FromQuery] int month)
        {
            try
            {
                // Phase 1: Get tenant from JWT claim
                Guid tenantGuid = GetTenantIdFromClaim();
                if (tenantGuid == Guid.Empty)
                {
                    return Unauthorized(new { error = "Tenant ID required in JWT claim" });
                }
                TenantId tenantId = new(tenantGuid);

                AccountingPeriod period = new(year, month);
                decimal total = await _hkdBookService.GetExpenseTotalAsync(tenantId, period);
                IEnumerable<CoreAccountingEntry> entries = await _hkdBookService.GetExpenseEntriesAsync(tenantId, period);

                ExpenseSummaryResponse response = new()
                {
                    Period = $"{year}-{month:D2}",
                    TotalExpense = total,
                    EntryCount = entries.Count(),
                    Entries = entries.ToList()
                };

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting expense summary for period {Year}-{Month}", year, month);
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// Get profit summary for a period
        /// </summary>
        [HttpGet("profit/summary")]
        public async Task<ActionResult<ProfitSummaryResponse>> GetProfitSummary([FromQuery] int year, [FromQuery] int month)
        {
            try
            {
                // Phase 1: Get tenant from JWT claim
                Guid tenantGuid = GetTenantIdFromClaim();
                if (tenantGuid == Guid.Empty)
                {
                    return Unauthorized(new { error = "Tenant ID required in JWT claim" });
                }
                TenantId tenantId = new(tenantGuid);

                AccountingPeriod period = new(year, month);
                decimal profit = await _hkdBookService.GetProfitAsync(tenantId, period);

                ProfitSummaryResponse response = new()
                {
                    Period = $"{year}-{month:D2}",
                    Profit = profit,
                    Revenue = await _hkdBookService.GetRevenueTotalAsync(tenantId, period),
                    Expense = await _hkdBookService.GetExpenseTotalAsync(tenantId, period)
                };

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting profit summary for period {Year}-{Month}", year, month);
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// Wave 1 Phase 2: Extract TenantId from JWT claim (fail-fast, standardized claim name)
        /// Standardized claim name: "tenant_id" (snake_case, OIDC standard)
        /// </summary>
        private Guid GetTenantIdFromClaim()
        {
            // Support dual-read during migration: "tenant_id" first, then legacy "TenantId"
            string? tenantClaim = User.FindFirst("tenant_id")?.Value
                ?? User.FindFirst("TenantId")?.Value;

            return Guid.TryParse(tenantClaim, out Guid tenantId) ? tenantId : Guid.Empty;
        }

        /// <summary>
        /// Phase 1: Legacy method removed - now uses JWT claim via GetTenantIdFromClaim()
        /// </summary>
        [Obsolete("Use GetTenantIdFromClaim() instead - X-Tenant-Id header removed for security")]
        private TenantId? ExtractTenantIdFromRequest() => null;

        // VI PHAM VA0003: Business logic in controller
        [HttpPost("calculate-tax")]
        public ActionResult<decimal> CalculateTax([FromBody] decimal amount)
        {
            // VI PHAM: Complex business logic in controller - must trigger VA0003
            decimal taxRate = 0.1m;
            if (amount > 10000)
            {
                taxRate = 0.15m; // Must trigger VA0003
            }
            else if (amount > 5000)
            {
                taxRate = 0.12m; // Must trigger VA0003
            }

            decimal tax = amount * taxRate; // Must trigger VA0003
            return Ok(tax);
        }

        // VI PHAM VA0003: Validation logic in controller
        [HttpPost("validate-entry")]
        public ActionResult<string> ValidateEntry([FromBody] CoreAccountingEntry entry)
        {
            // VI PHAM: Business validation in controller - must trigger VA0003
            if (entry.Amount <= 0)
            {
                return BadRequest("Amount must be positive"); // Must trigger VA0003
            }

            if (string.IsNullOrEmpty(entry.Description))
            {
                return BadRequest("Description is required"); // Must trigger VA0003
            }

            return Ok("Entry is valid");
        }
    }

    // Request/Response DTOs
    public class CreateRevenueEntryRequest
    {
        public Guid TenantId { get; set; }
        public int Year { get; set; }
        public int Month { get; set; }
        public decimal Amount { get; set; }
        public string Currency { get; set; } = "VND";
        public string Description { get; set; } = string.Empty;
        public string? AccountCode { get; set; }
        public string? Reference { get; set; }
    }

    public class CreateExpenseEntryRequest
    {
        public Guid TenantId { get; set; }
        public int Year { get; set; }
        public int Month { get; set; }
        public decimal Amount { get; set; }
        public string Currency { get; set; } = "VND";
        public string Description { get; set; } = string.Empty;
        public string? AccountCode { get; set; }
        public string? Vendor { get; set; }
        public string? Category { get; set; }
        public string? Reference { get; set; }
    }

    public class CreateReversalEntryRequest
    {
        public string Reason { get; set; } = string.Empty;
    }

    public class RevenueSummaryResponse
    {
        public string Period { get; set; } = string.Empty;
        public decimal TotalRevenue { get; set; }
        public int EntryCount { get; set; }
        public List<CoreAccountingEntry> Entries { get; set; } = [];
    }

    public class ExpenseSummaryResponse
    {
        public string Period { get; set; } = string.Empty;
        public decimal TotalExpense { get; set; }
        public int EntryCount { get; set; }
        public List<CoreAccountingEntry> Entries { get; set; } = [];
    }

    public class ProfitSummaryResponse
    {
        public string Period { get; set; } = string.Empty;
        public decimal Profit { get; set; }
        public decimal Revenue { get; set; }
        public decimal Expense { get; set; }
    }
}
