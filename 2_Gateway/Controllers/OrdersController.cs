using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using VanAn.CoreHub.Services;
using VanAn.CoreHub.Hubs;
using VanAn.Shared.Domain;
using VanAn.Shared.Services;
using VanAn.CoreHub.Commands;

namespace VanAn.Gateway.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class OrdersController(
        IOrderService orderService,
        IVietQrService vietQrService,
        IHubContext<OrderHub> orderHub,
        ILogger<OrdersController> logger) : ControllerBase
    {
        private readonly IOrderService _orderService = orderService;
        private readonly IVietQrService _vietQrService = vietQrService;
        private readonly IHubContext<OrderHub> _orderHub = orderHub;
        private readonly ILogger<OrdersController> _logger = logger;

        [HttpPost]
        public async Task<ActionResult<VietQrResponse>> CreateOrder([FromBody] CreateOrderCommand command)
        {
            try
            {
                Guid tenantId = Guid.NewGuid(); // TODO: Get from tenant provider

                // Delegate order creation to service layer - Clean Architecture
                Order createdOrder = await _orderService.CreateOrderFromCommandAsync(command, tenantId);

                // Notify ShopERP via SignalR
                await _orderHub.Clients.All.SendAsync("NewOrderReceived", new
                {
                    OrderId = createdOrder.Id,
                    createdOrder.CustomerId,
                    Status = createdOrder.Status.Value,
                    TotalAmount = createdOrder.TotalPrice,
                    createdOrder.CreatedAt,
                    Items = createdOrder.Items.Select(i => new
                    {
                        i.ProductId,
                        i.Quantity,
                        i.UnitPrice,
                        i.TotalPrice
                    }).ToList()
                });

                // Generate VietQR
                Shared.Domain.VietQrResponse payload = await _vietQrService.GenerateQrCodeAsync(new VietQrRequest
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

                VietQrResponse response = new()
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
                Guid tenantId = GetTenantId();
                Order? order = await _orderService.GetOrderByIdAsync(id, tenantId);

                return order == null ? (ActionResult<Order>)NotFound() : (ActionResult<Order>)Ok(order);
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
                Guid tenantId = GetTenantId();

                if (string.IsNullOrEmpty(status))
                {
                    DateTime today = DateTime.UtcNow.Date;
                    IEnumerable<Order> orders = await _orderService.GetOrdersByDateRangeAsync(tenantId, today, today.AddDays(1));
                    return Ok(orders);
                }
                else
                {
                    OrderStatusId statusId = new(status);
                    List<Order> orders = await _orderService.GetOrdersByStatusAsync(statusId, tenantId);
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
            string? tenantClaim = User.FindFirst("TenantId")?.Value;
            return Guid.TryParse(tenantClaim, out Guid tenantId) ? tenantId : Guid.Empty;
        }
    }

    public class CreateOrderRequest
    {
        public string CustomerDeviceId { get; set; } = string.Empty;
        public string OrderType { get; set; } = "DINEIN";
        public string CustomerNotes { get; set; } = string.Empty;
        public List<OrderItemRequest> Items { get; set; } = [];
    }

    public class OrderItemRequest
    {
        public Guid ProductId { get; set; }
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal VatRate { get; set; }
        public string Notes { get; set; } = string.Empty;
    }
}
