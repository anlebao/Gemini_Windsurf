using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using VanAn.Shared.Domain;
using VanAn.CoreHub.Services;
using CoreAccountingEntry = VanAn.Shared.Domain.AccountingEntry;

namespace VanAn.Gateway.Controllers;

/// <summary>
/// API Controller for Accounting Entries - Week 1 implementation
/// Implements 5-layer protection: Domain, EF Core, Repository, Service, API
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class AccountingEntriesController : ControllerBase
{
    private readonly IAccountingService _accountingEntryService;
    private readonly IReversalService _reversalService;
    private readonly IHKDBookService _hkdBookService;
    private readonly ILogger<AccountingEntriesController> _logger;
    
    public AccountingEntriesController(
        IAccountingService accountingEntryService,
        IReversalService reversalService,
        IHKDBookService hkdBookService,
        ILogger<AccountingEntriesController> logger)
    {
        _accountingEntryService = accountingEntryService;
        _reversalService = reversalService;
        _hkdBookService = hkdBookService;
        _logger = logger;
    }
    
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
            
            var tenantId = new TenantId(request.TenantId);
            var period = new AccountingPeriod(request.Year, request.Month);
            var amount = new Money(request.Amount);
            
            var entry = await _accountingEntryService.CreateEntryAsync(
                new CoreAccountingEntry
                {
                    TenantId = tenantId,
                    Amount = amount.Value,
                    EntryType = AccountingEntryType.Revenue,
                    Description = request.Description,
                    CreatedAt = DateTime.UtcNow,
                    AccountingBookType = AccountingBookType.RevenueBook,
                    PeriodYear = request.Year,
                    PeriodMonth = request.Month
                });
            
            _logger.LogInformation("Revenue entry created: {EntryId} for tenant {TenantId}", 
                entry.Id, request.TenantId);
            
            return CreatedAtAction(nameof(GetEntryById), new { id = entry.Id }, entry);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating revenue entry for tenant {TenantId}", request.TenantId);
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
            
            var tenantId = new TenantId(request.TenantId);
            var period = new AccountingPeriod(request.Year, request.Month);
            var amount = new Money(request.Amount);
            
            var entry = await _accountingEntryService.CreateEntryAsync(
                new CoreAccountingEntry
                {
                    TenantId = tenantId,
                    Amount = amount.Value,
                    EntryType = AccountingEntryType.Expense,
                    Description = request.Description,
                    CreatedAt = DateTime.UtcNow,
                    AccountingBookType = AccountingBookType.ExpenseBook,
                    PeriodYear = request.Year,
                    PeriodMonth = request.Month
                });
            
            _logger.LogInformation("Expense entry created: {EntryId} for tenant {TenantId}", 
                entry.Id, request.TenantId);
            
            return CreatedAtAction(nameof(GetEntryById), new { id = entry.Id }, entry);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating expense entry for tenant {TenantId}", request.TenantId);
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
            var tenantId = ExtractTenantIdFromRequest();
            if (tenantId == null)
            {
                return Unauthorized("Tenant ID required");
            }
            
            // Use existing method to get entries by date range and filter by ID
            var entries = await _accountingEntryService.GetEntriesByDateRangeAsync(tenantId, DateTime.MinValue, DateTime.MaxValue);
            var entry = entries.FirstOrDefault(e => e.Id == id);
            
            if (entry == null)
            {
                return NotFound();
            }
            
            return Ok(entry);
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
            var tenantId = ExtractTenantIdFromRequest();
            if (tenantId == null)
            {
                return Unauthorized("Tenant ID required");
            }
            
            var entries = await _accountingEntryService.GetEntriesByDateRangeAsync(tenantId, DateTime.MinValue, DateTime.MaxValue);
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
            
            var tenantId = ExtractTenantIdFromRequest();
            if (tenantId == null)
            {
                return Unauthorized("Tenant ID required");
            }
            
            var originalEntryId = new AccountingEntryId(id);
            
            // Check if entry can be reversed
            var canReverse = await _reversalService.CanReverseEntryAsync(originalEntryId, tenantId);
            if (!canReverse)
            {
                return BadRequest("Entry cannot be reversed");
            }
            
            var reversalEntry = await _reversalService.CreateReversalEntryAsync(
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
            var tenantId = ExtractTenantIdFromRequest();
            if (tenantId == null)
            {
                return Unauthorized("Tenant ID required");
            }
            
            var period = new AccountingPeriod(year, month);
            var total = await _hkdBookService.GetRevenueTotalAsync(tenantId, period);
            var entries = await _hkdBookService.GetRevenueEntriesAsync(tenantId, period);
            
            var response = new RevenueSummaryResponse
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
            var tenantId = ExtractTenantIdFromRequest();
            if (tenantId == null)
            {
                return Unauthorized("Tenant ID required");
            }
            
            var period = new AccountingPeriod(year, month);
            var total = await _hkdBookService.GetExpenseTotalAsync(tenantId, period);
            var entries = await _hkdBookService.GetExpenseEntriesAsync(tenantId, period);
            
            var response = new ExpenseSummaryResponse
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
            var tenantId = ExtractTenantIdFromRequest();
            if (tenantId == null)
            {
                return Unauthorized("Tenant ID required");
            }
            
            var period = new AccountingPeriod(year, month);
            var profit = await _hkdBookService.GetProfitAsync(tenantId, period);
            
            var response = new ProfitSummaryResponse
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
    
    private TenantId? ExtractTenantIdFromRequest()
    {
        // For Week 1, we'll extract from header
        // In production, this would come from JWT claims or other auth mechanism
        if (Request.Headers.TryGetValue("X-Tenant-Id", out var tenantIdValue))
        {
            if (Guid.TryParse(tenantIdValue, out var tenantId))
            {
                return new TenantId(tenantId);
            }
        }
        
        return null;
    }
    
    // VI PHAM VA0003: Business logic in controller
    [HttpPost("calculate-tax")]
    public ActionResult<decimal> CalculateTax([FromBody] decimal amount)
    {
        // VI PHAM: Complex business logic in controller - must trigger VA0003
        var taxRate = 0.1m;
        if (amount > 10000)
        {
            taxRate = 0.15m; // Must trigger VA0003
        }
        else if (amount > 5000)
        {
            taxRate = 0.12m; // Must trigger VA0003
        }
        
        var tax = amount * taxRate; // Must trigger VA0003
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
}

public class CreateExpenseEntryRequest
{
    public Guid TenantId { get; set; }
    public int Year { get; set; }
    public int Month { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "VND";
    public string Description { get; set; } = string.Empty;
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
    public List<CoreAccountingEntry> Entries { get; set; } = new();
}

public class ExpenseSummaryResponse
{
    public string Period { get; set; } = string.Empty;
    public decimal TotalExpense { get; set; }
    public int EntryCount { get; set; }
    public List<CoreAccountingEntry> Entries { get; set; } = new();
}

public class ProfitSummaryResponse
{
    public string Period { get; set; } = string.Empty;
    public decimal Profit { get; set; }
    public decimal Revenue { get; set; }
    public decimal Expense { get; set; }
}
