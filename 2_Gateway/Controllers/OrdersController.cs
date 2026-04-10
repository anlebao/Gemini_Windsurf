using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using VanAn.Shared.Domain;
using VanAn.Shared.Services;
using VanAn.CoreHub.Services;
using VanAn.CoreHub.Infrastructure;
using VanAn.Gateway.Hubs;

namespace VanAn.Gateway.Controllers;

[ApiController]
[Route("api/[controller]")]
public class OrdersController : ControllerBase
{
    private readonly VanAnDbContext _context;
    private readonly IVietQrService _vietQrService;
    private readonly IOrderWorkflowService _orderWorkflowService;
    private readonly IHubContext<OrderHub> _orderHub;
    private readonly ILogger<OrdersController> _logger;

    public OrdersController(
        VanAnDbContext context,
        IVietQrService vietQrService,
        IOrderWorkflowService orderWorkflowService,
        IHubContext<OrderHub> orderHub,
        ILogger<OrdersController> logger)
    {
        _context = context;
        _vietQrService = vietQrService;
        _orderWorkflowService = orderWorkflowService;
        _orderHub = orderHub;
        _logger = logger;
    }

    [HttpPost]
    public async Task<ActionResult<VietQrResponse>> CreateOrder([FromBody] CreateOrderRequest request)
    {
        try
        {
            // Create Order
            var order = new Order
            {
                OrderId = new OrderId(Guid.NewGuid()),
                CustomerDeviceId = request.CustomerDeviceId,
                OrderType = request.OrderType,
                Status = new OrderStatusId("Draft"),
                CustomerNotes = request.CustomerNotes,
                TenantId = Guid.NewGuid() // TODO: Get from tenant provider
            };

            // Add OrderItems
            foreach (var itemRequest in request.Items)
            {
                var orderItem = new OrderItem
                {
                    OrderItemId = new OrderItemId(Guid.NewGuid()),
                    OrderId = order.Id,
                    ProductId = itemRequest.ProductId,
                    Quantity = itemRequest.Quantity,
                    UnitPrice = itemRequest.UnitPrice,
                    VatRate = itemRequest.VatRate,
                    Notes = itemRequest.Notes,
                    TenantId = order.TenantId
                };
                order.Items.Add(orderItem);
            }

            // Calculate totals
            order.CalculateTotals();

            // Save to database
            _context.Orders.Add(order);
            await _context.SaveChangesAsync();

            // Notify ShopERP via SignalR
            await _orderHub.Clients.All.SendAsync("NewOrderReceived", new
            {
                OrderId = order.OrderId.Value,
                CustomerDeviceId = order.CustomerDeviceId,
                OrderType = order.OrderType,
                Status = order.Status.Value,
                TotalAmount = order.TotalAmount,
                OrderDate = order.OrderDate,
                Items = order.Items.Select(i => new
                {
                    ProductName = i.Product?.Name ?? "Unknown",
                    Quantity = i.Quantity,
                    UnitPrice = i.UnitPrice,
                    TotalAmount = i.TotalAmount
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
                Amount = order.TotalAmount,
                OrderDescription = $"Don hang {order.OrderId.Value}"
            });

            // Generate QR image URL (using VietQR image service)
            var qrImageUrl = $"https://img.vietqr.io/image/970418-1234567890-compact.jpg?amount={order.TotalAmount}&addInfo={Uri.EscapeDataString($"Don hang {order.OrderId.Value}")}";

            var response = new VietQrResponse
            {
                OrderId = order.OrderId.Value.ToString(),
                QrImageUrl = payload.QrImageUrl,
                PaymentUrl = payload.PaymentUrl,
                Amount = order.TotalAmount,
                GeneratedAt = DateTime.UtcNow
            };

            return CreatedAtAction(nameof(GetOrder), new { id = order.Id }, response);
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
        var order = await _context.Orders
            .Include(o => o.Items)
            .ThenInclude(i => i.Product)
            .FirstOrDefaultAsync(o => o.Id == id);

        if (order == null)
        {
            return NotFound();
        }

        return order;
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
