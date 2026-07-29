using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using VanAn.CoreHub.Infrastructure;
using VanAn.Shared.Domain;

namespace VanAn.Gateway.Hubs;

/// <summary>
/// CC-S3 (Sprint 3): SignalR hub for real-time shipper ↔ customer chat.
/// UC-07 (Chat) — send message → push ReceiveMessage to chat_{orderId} group.
///
/// Auth: X-Customer-Token via query string "customerToken" (same pattern as LocationHub).
/// KHÔNG dùng [Authorize] (JWT) — customer auth is custom X-Customer-Token.
/// </summary>
public class ChatHub(
    IVanAnDbContext dbContext,
    IHttpClientFactory httpClientFactory,
    ILogger<ChatHub> logger) : Hub
{
    private readonly IVanAnDbContext _dbContext = dbContext;
    private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;
    private readonly ILogger<ChatHub> _logger = logger;

    public override async Task OnConnectedAsync()
    {
        var token = Context.GetHttpContext()?.Request.Query["customerToken"].ToString();
        if (string.IsNullOrEmpty(token))
            throw new HubException("Missing customerToken");

        var customerId = await ValidateTokenAsync(token);
        if (customerId == null)
            throw new HubException("Invalid customerToken");

        Context.Items["CustomerId"] = customerId.Value;
        _logger.LogInformation("ChatHub: Customer {CustomerId} connected", customerId.Value);
        await base.OnConnectedAsync();
    }

    /// <summary>
    /// Join conversation group — shipper or customer subscribes to chat messages.
    /// Verifies the caller is the ShipperId or CustomerId of the conversation.
    /// </summary>
    public async Task JoinConversation(string orderId)
    {
        if (!Guid.TryParse(orderId, out var orderGuid))
            throw new HubException("Invalid orderId");

        var customerId = (Guid?)Context.Items["CustomerId"];
        if (customerId == null)
            throw new HubException("Not authenticated");

        // Verify access: is ShipperId of DeliveryTask OR CustomerId of Conversation/Order
        var hasAccess = await _dbContext.DeliveryTasks
            .IgnoreQueryFilters()
            .AsNoTracking()
            .AnyAsync(dt => dt.OrderId == orderGuid && dt.ShipperId == customerId.Value);

        if (!hasAccess)
        {
            hasAccess = await _dbContext.Conversations
                .IgnoreQueryFilters()
                .AsNoTracking()
                .AnyAsync(c => c.OrderId == orderGuid && (c.ShipperId == customerId.Value || c.CustomerId == customerId.Value));
        }

        if (!hasAccess)
        {
            hasAccess = await _dbContext.Orders
                .IgnoreQueryFilters()
                .AsNoTracking()
                .AnyAsync(o => o.Id == orderGuid && o.CustomerId == customerId.Value);
        }

        if (!hasAccess)
            throw new HubException("Access denied: not shipper or customer of this order");

        await Groups.AddToGroupAsync(Context.ConnectionId, $"chat_{orderId}");
        _logger.LogInformation("ChatHub: Customer {CustomerId} joined chat_{OrderId}", customerId.Value, orderId);
    }

    public Task LeaveConversation(string orderId)
        => Groups.RemoveFromGroupAsync(Context.ConnectionId, $"chat_{orderId}");

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
            _logger.LogError(ex, "ChatHub: Error validating customer token");
            return null;
        }
    }

    private class MeResponse
    {
        public Guid? CustomerId { get; set; }
    }
}
