using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using VanAn.CoreHub.Services;
using VanAn.Shared.Domain;
using VanAn.Shared.Services;
using VanAn.Gateway.Hubs;
using VanAn.CoreHub.Commands;

namespace VanAn.Gateway.Controllers;

[ApiController]
[Route("api/[controller]")]
public class OrdersController : ControllerBase
{
    private readonly IOrderService _orderService;
    private readonly IVietQrService _vietQrService;
    private readonly IHubContext<OrderHub> _orderHub;
    private readonly ILogger<OrdersController> _logger;

    public OrdersController(
        IOrderService orderService,
        IVietQrService vietQrService,
        IHubContext<OrderHub> orderHub,
        ILogger<OrdersController> logger)
    {
        _orderService = orderService;
        _vietQrService = vietQrService;
        _orderHub = orderHub;
        _logger = logger;
    }

    [HttpPost]
    public async Task<ActionResult<VietQrResponse>> CreateOrder([FromBody] CreateOrderCommand command)
    {
        try
        {
            var tenantId = Guid.NewGuid(); // TODO: Get from tenant provider
            
            // Delegate order creation to service layer - Clean Architecture
            var createdOrder = await _orderService.CreateOrderFromCommandAsync(command, tenantId);

            // Notify ShopERP via SignalR
            await _orderHub.Clients.All.SendAsync("NewOrderReceived", new
            {
                OrderId = createdOrder.Id,
                CustomerId = createdOrder.CustomerId,
                Status = createdOrder.Status.Value,
                TotalAmount = createdOrder.TotalPrice,
                CreatedAt = createdOrder.CreatedAt,
                Items = createdOrder.Items.Select(i => new
                {
                    ProductId = i.ProductId,
                    Quantity = i.Quantity,
                    UnitPrice = i.UnitPrice,
                    TotalPrice = i.TotalPrice
                }).ToList()
            });

            // Generate VietQR
            var payload = await _vietQrService.GenerateQrCodeAsync(new VietQrRequest
            {
                BankConfig = new BankConfig
                {
                    BankId = "970418",
                    AccountNo = "1234567890",
                    AccountName = "VAN AN GROUP"
                },
                Amount = createdOrder.TotalPrice,
                OrderDescription = $"Don hang {createdOrder.Id}"
            });

            var response = new VietQrResponse
            {
                OrderId = createdOrder.Id.ToString(),
                QrImageUrl = payload.QrImageUrl,
                PaymentUrl = payload.PaymentUrl,
                Amount = createdOrder.TotalPrice,
                GeneratedAt = DateTime.UtcNow
            };

            return CreatedAtAction(nameof(GetOrder), new { id = createdOrder.Id }, response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating order");
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<Order>> GetOrder(Guid id)
    {
        try
        {
            var tenantId = GetTenantId();
            var order = await _orderService.GetOrderByIdAsync(id, tenantId);
            
            if (order == null)
            {
                return NotFound();
            }
            
            return Ok(order);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting order {OrderId}", id);
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<Order>>> GetOrders([FromQuery] string? status = null)
    {
        try
        {
            var tenantId = GetTenantId();
            
            if (string.IsNullOrEmpty(status))
            {
                var today = DateTime.UtcNow.Date;
                var orders = await _orderService.GetOrdersByDateRangeAsync(tenantId, today, today.AddDays(1));
                return Ok(orders);
            }
            else
            {
                var statusId = new OrderStatusId(status);
                var orders = await _orderService.GetOrdersByStatusAsync(statusId, tenantId);
                return Ok(orders);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting orders");
            return StatusCode(500, "Internal server error");
        }
    }

    private Guid GetTenantId()
    {
        var tenantClaim = User.FindFirst("TenantId")?.Value;
        return Guid.TryParse(tenantClaim, out var tenantId) ? tenantId : Guid.Empty;
    }
}

public class CreateOrderRequest
{
    public string CustomerDeviceId { get; set; } = string.Empty;
    public string OrderType { get; set; } = "DINEIN";
    public string CustomerNotes { get; set; } = string.Empty;
    public List<OrderItemRequest> Items { get; set; } = new();
}

public class OrderItemRequest
{
    public Guid ProductId { get; set; }
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal VatRate { get; set; }
    public string Notes { get; set; } = string.Empty;
}

public class VietQrResponse
{
    public string OrderId { get; set; } = string.Empty;
    public Uri QrImageUrl { get; set; } = null!;
    public Uri PaymentUrl { get; set; } = null!;
    public decimal Amount { get; set; }
    public DateTime GeneratedAt { get; set; }
}
