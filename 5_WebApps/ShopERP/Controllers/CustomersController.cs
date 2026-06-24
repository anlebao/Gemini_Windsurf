using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VanAn.CoreHub.Infrastructure;
using VanAn.Shared.Domain;

namespace VanAn.ShopERP.Controllers
{
    /// <summary>
    /// API surface for customer management operations.
    /// Hosted in ShopERP so that KhachLink and other edge clients can access customer data
    /// without directly referencing CoreHub infrastructure.
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class CustomersController(VanAnDbContext dbContext, ILogger<CustomersController> logger) : ControllerBase
    {
        private readonly VanAnDbContext _dbContext = dbContext;
        private readonly ILogger<CustomersController> _logger = logger;

        [HttpGet("{id:guid}")]
        [AllowAnonymous]
        public async Task<ActionResult<Customer>> GetCustomer(Guid id)
        {
            try
            {
                Customer? customer = await _dbContext.Customers.FindAsync(id);
                return customer == null ? NotFound() : Ok(customer);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting customer {CustomerId}", id);
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpPost]
        [AllowAnonymous]
        public async Task<ActionResult<Customer>> CreateCustomer([FromBody] CreateCustomerRequest request)
        {
            try
            {
                var customer = new Customer(
                    new TenantId(request.TenantId),
                    request.FullName,
                    request.PhoneNumber,
                    request.Email);

                customer.UpdateCustomerDetails(
                    request.FullName,
                    request.PhoneNumber,
                    request.Email,
                    request.CustomerTier ?? "Bronze",
                    customer.DeviceId,
                    customer.IsActive);

                _ = _dbContext.Customers.Add(customer);
                _ = await _dbContext.SaveChangesAsync();

                return CreatedAtAction(nameof(GetCustomer), new { id = customer.CustomerId.Value }, customer);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating customer");
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpPut("{id:guid}")]
        [AllowAnonymous]
        public async Task<ActionResult<Customer>> UpdateCustomer(Guid id, [FromBody] UpdateCustomerRequest request)
        {
            try
            {
                Customer? customer = await _dbContext.Customers.FirstOrDefaultAsync(c => c.CustomerId.Value == id);
                if (customer == null)
                {
                    return NotFound();
                }

                customer.UpdateCustomerDetails(
                    request.FullName,
                    request.PhoneNumber,
                    request.Email,
                    request.CustomerTier,
                    customer.DeviceId,
                    customer.IsActive);

                _ = _dbContext.Customers.Update(customer);
                _ = await _dbContext.SaveChangesAsync();

                return Ok(customer);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating customer {CustomerId}", id);
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpDelete("{id:guid}")]
        [AllowAnonymous]
        public async Task<ActionResult> DeleteCustomer(Guid id)
        {
            try
            {
                Customer? customer = await _dbContext.Customers.FirstOrDefaultAsync(c => c.CustomerId.Value == id);
                if (customer == null)
                {
                    return NotFound();
                }

                customer.SoftDelete();
                _ = await _dbContext.SaveChangesAsync();

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting customer {CustomerId}", id);
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpGet("{id:guid}/rewards")]
        [AllowAnonymous]
        public async Task<ActionResult<LoyaltyRewards>> GetCustomerRewards(Guid id)
        {
            try
            {
                LoyaltyRewards? rewards = await _dbContext.LoyaltyRewards
                    .FirstOrDefaultAsync(r => r.CustomerId == id);

                return rewards == null ? NotFound() : Ok(rewards);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting rewards for customer {CustomerId}", id);
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpPost("{id:guid}/rewards/add")]
        [AllowAnonymous]
        public async Task<ActionResult> AddLoyaltyPoints(Guid id, [FromBody] AddLoyaltyPointsRequest request)
        {
            try
            {
                LoyaltyRewards? rewards = await _dbContext.LoyaltyRewards
                    .FirstOrDefaultAsync(r => r.CustomerId == id);

                if (rewards == null)
                {
                    return NotFound();
                }

                rewards.AddPoints(request.Points, request.Reason);
                _ = await _dbContext.SaveChangesAsync();

                return Ok(new { newBalance = rewards.PointBalance, pointsAdded = request.Points });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding loyalty points for customer {CustomerId}", id);
                return StatusCode(500, "Internal server error");
            }
        }
    }

    public class CreateCustomerRequest
    {
        public Guid TenantId { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public string? Email { get; set; }
        public string? CustomerTier { get; set; }
    }

    public class UpdateCustomerRequest
    {
        public string FullName { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public string? Email { get; set; }
        public string? CustomerTier { get; set; }
    }

    public class AddLoyaltyPointsRequest
    {
        public int Points { get; set; }
        public string Reason { get; set; } = string.Empty;
    }
}
