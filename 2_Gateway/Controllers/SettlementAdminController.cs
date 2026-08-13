using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VanAn.CoreHub.Infrastructure;
using VanAn.Shared.Domain;

namespace VanAn.Gateway.Controllers
{
    /// <summary>
    /// Sprint B: Admin endpoint for viewing settlement transactions across tenants.
    /// SystemAdmin only — JWT Bearer auth.
    /// </summary>
    [ApiController]
    [Authorize(Policy = "SystemAdmin", AuthenticationSchemes = Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerDefaults.AuthenticationScheme)]
    [Route("api/admin/settlements")]
    public class SettlementAdminController(
        IVanAnDbContext dbContext,
        ILogger<SettlementAdminController> logger) : ControllerBase
    {
        private readonly IVanAnDbContext _dbContext = dbContext;
        private readonly ILogger<SettlementAdminController> _logger = logger;

        [HttpGet]
        public async Task<ActionResult<SettlementListResponse>> List(
            [FromQuery] Guid? tenantId,
            [FromQuery] DateTime? fromDate,
            [FromQuery] DateTime? toDate,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20,
            CancellationToken ct = default)
        {
            try
            {
                if (page < 1) page = 1;
                if (pageSize < 1 || pageSize > 100) pageSize = 20;

                var query = _dbContext.WalletTransactions
                    .IgnoreQueryFilters()
                    .AsNoTracking()
                    .Where(t => t.Type == WalletTransactionType.Settlement);

                if (tenantId.HasValue && tenantId.Value != Guid.Empty)
                    query = query.Where(t => t.TenantId.Value == tenantId.Value);

                if (fromDate.HasValue)
                    query = query.Where(t => t.CreatedAt >= fromDate.Value);

                if (toDate.HasValue)
                    query = query.Where(t => t.CreatedAt < toDate.Value.AddDays(1));

                var total = await query.CountAsync(ct);
                var items = await query
                    .OrderByDescending(t => t.CreatedAt)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .Select(t => new SettlementDto
                    {
                        Id = t.Id,
                        TenantId = t.TenantId,
                        OwnerId = t.OwnerId,
                        Amount = t.Amount,
                        BalanceAfter = t.BalanceAfter,
                        Description = t.Description,
                        RelatedOrderId = t.RelatedOrderId,
                        RelatedTransactionId = t.RelatedTransactionId,
                        CreatedAt = t.CreatedAt
                    })
                    .ToListAsync(ct);

                return Ok(new SettlementListResponse
                {
                    Total = total,
                    Page = page,
                    PageSize = pageSize,
                    Items = items
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error listing settlement transactions");
                return StatusCode(500, new { error = "Internal server error" });
            }
        }
    }

    public class SettlementListResponse
    {
        public int Total { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
        public List<SettlementDto> Items { get; set; } = new();
    }

    public class SettlementDto
    {
        public Guid Id { get; set; }
        public Guid TenantId { get; set; }
        public Guid OwnerId { get; set; }
        public decimal Amount { get; set; }
        public decimal BalanceAfter { get; set; }
        public string Description { get; set; } = string.Empty;
        public Guid? RelatedOrderId { get; set; }
        public Guid? RelatedTransactionId { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
