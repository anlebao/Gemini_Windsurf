using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VanAn.ShopERP.Infrastructure;
using VanAn.ShopERP.Services;
using VanAn.Shared.Domain;

namespace VanAn.ShopERP.Controllers
{
    /// <summary>
    /// W17-T3: Customer Order History — returns orders for an authenticated customer.
    /// </summary>
    [ApiController]
    [Route("api/customerorders")]
    [AllowAnonymous]
    public class CustomerOrdersController(
        ShopERPDbContext dbContext,
        ICustomerTokenService customerTokenService,
        ILogger<CustomerOrdersController> logger) : ControllerBase
    {
        private readonly ShopERPDbContext _dbContext = dbContext;
        private readonly ICustomerTokenService _customerTokenService = customerTokenService;
        private readonly ILogger<CustomerOrdersController> _logger = logger;

        /// <summary>
        /// GET /api/customerorders?status=&page=&pageSize=
        /// Returns paginated order history for the authenticated customer.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetMyOrders(
            [FromHeader(Name = "X-Customer-Token")] string? token,
            [FromQuery] string? status,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10)
        {
            var customerId = _customerTokenService.ValidateToken(token ?? "");
            if (!customerId.HasValue)
                return Unauthorized(new { error = "Token không hợp lệ hoặc đã hết hạn." });

            IQueryable<Order> query = _dbContext.Orders
                .Where(o => o.CustomerId == customerId.Value && !o.IsDeleted)
                .OrderByDescending(o => o.CreatedAt);

            if (!string.IsNullOrEmpty(status))
                query = query.Where(o => o.Status.Value == status);

            var total = await query.CountAsync();
            var orders = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(o => new CustomerOrderDto
                {
                    OrderId   = o.Id,
                    Status    = o.Status.Value,
                    TotalPrice = o.TotalPrice,
                    TotalVatAmount = o.TotalVatAmount,
                    CreatedAt = o.CreatedAt,
                    ItemCount = o.Items.Count
                })
                .ToListAsync();

            return Ok(new { total, page, pageSize, orders });
        }
    }

    public class CustomerOrderDto
    {
        public Guid OrderId { get; set; }
        public string Status { get; set; } = string.Empty;
        public decimal TotalPrice { get; set; }
        public decimal TotalVatAmount { get; set; }
        public DateTime CreatedAt { get; set; }
        public int ItemCount { get; set; }
    }
}
