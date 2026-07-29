using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using VanAn.CoreHub.Infrastructure;
using VanAn.Shared.Domain;

namespace VanAn.Gateway.Hubs;

/// <summary>
/// CC-S2 (Sprint 2): SignalR hub for real-time GPS delivery tracking.
/// UC-06 (GPS tracking) — shipper pushes location → customer subscribes to order group.
///
/// Auth: X-Customer-Token via query string "customerToken" (SignalR client can pass query string).
/// Same pattern as CommunityController — forward to ShopERP /api/customer-identity/me for validation.
/// KHÔNG dùng [Authorize] (JWT) — customer auth is custom X-Customer-Token.
/// </summary>
public class LocationHub(
    IVanAnDbContext dbContext,
    IHttpClientFactory httpClientFactory,
    ILogger<LocationHub> logger) : Hub
{
    private readonly IVanAnDbContext _dbContext = dbContext;
    private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;
    private readonly ILogger<LocationHub> _logger = logger;

    /// <summary>
    /// Validate customer token on connection. Token passed via query string.
    /// </summary>
    public override async Task OnConnectedAsync()
    {
        var token = Context.GetHttpContext()?.Request.Query["customerToken"].ToString();
        if (string.IsNullOrEmpty(token))
            throw new HubException("Missing customerToken");

        var customerId = await ValidateTokenAsync(token);
        if (customerId == null)
            throw new HubException("Invalid customerToken");

        Context.Items["CustomerId"] = customerId.Value;
        _logger.LogInformation("LocationHub: Customer {CustomerId} connected", customerId.Value);
        await base.OnConnectedAsync();
    }

    /// <summary>
    /// Join order tracking group — shipper or customer subscribes to location updates.
    /// Verifies the caller is the ShipperId or CustomerId of the order.
    /// </summary>
    public async Task JoinOrderTracking(string orderId)
    {
        if (!Guid.TryParse(orderId, out var orderGuid))
            throw new HubException("Invalid orderId");

        var customerId = (Guid?)Context.Items["CustomerId"];
        if (customerId == null)
            throw new HubException("Not authenticated");

        // Verify customer has rights: is ShipperId of DeliveryTask OR CustomerId of Order
        var hasAccess = await _dbContext.DeliveryTasks
            .IgnoreQueryFilters()
            .AsNoTracking()
            .AnyAsync(dt => dt.OrderId == orderGuid && dt.ShipperId == customerId.Value);

        if (!hasAccess)
        {
            // Also check if customer is the order creator (CustomerId on Order)
            hasAccess = await _dbContext.Orders
                .IgnoreQueryFilters()
                .AsNoTracking()
                .AnyAsync(o => o.Id == orderGuid && o.CustomerId == customerId.Value);
        }

        if (!hasAccess)
            throw new HubException("Access denied: not shipper or customer of this order");

        await Groups.AddToGroupAsync(Context.ConnectionId, $"order_{orderId}");
        _logger.LogInformation("LocationHub: Customer {CustomerId} joined order_{OrderId}", customerId.Value, orderId);
    }

    /// <summary>
    /// Leave order tracking group.
    /// </summary>
    public Task LeaveOrderTracking(string orderId)
        => Groups.RemoveFromGroupAsync(Context.ConnectionId, $"order_{orderId}");

    /// <summary>
    /// Validate customer token by forwarding to ShopERP /api/customer-identity/me.
    /// </summary>
    private async Task<Guid?> ValidateTokenAsync(string token)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("shoperp");
            var req = new HttpRequestMessage(HttpMethod.Get, "/api/customer-identity/me");
            req.Headers.Add("X-Customer-Token", token);

            var resp = await client.SendAsync(req);
            if (!resp.IsSuccessStatusCode)
                return null;

            var content = await resp.Content.ReadFromJsonAsync<MeResponse>();
            return content?.CustomerId;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "LocationHub: Error validating customer token");
            return null;
        }
    }

    private class MeResponse
    {
        public Guid? CustomerId { get; set; }
    }
}
