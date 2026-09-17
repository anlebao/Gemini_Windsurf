using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using VanAn.CoreHub.Infrastructure;
using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Services;

/// <summary>
/// CC-S3 (Sprint 3): Chat service implementation.
/// Conversation + Message persistence. Cross-tenant via IgnoreQueryFilters.
///
/// Chat gating (fixed): Chat is available for DELIVERY orders as soon as the order
/// exists with a CustomerId. Conversation is created with placeholder ShipperId=Guid.Empty
/// if no DeliveryTask exists yet (before shipper accepts). When shipper accepts,
/// CommunityOrderService.AcceptOrderAsync updates Conversation.ShipperId via AssignShipper().
/// This allows customer to chat immediately after placing a delivery order, without
/// waiting for shipper acceptance.
/// </summary>
public class ChatService(
    IVanAnDbContext dbContext,
    ILogger<ChatService> logger) : IChatService
{
    private readonly IVanAnDbContext _dbContext = dbContext;
    private readonly ILogger<ChatService> _logger = logger;

    public async Task<Conversation?> GetOrCreateConversationAsync(Guid orderId, Guid? guestDeviceId = null)
    {
        // Check for existing conversation
        var existing = await _dbContext.Conversations
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(c => c.OrderId == orderId);

        if (existing != null)
            return existing;

        // Load order (cross-tenant)
        var order = await _dbContext.Orders
            .IgnoreQueryFilters()
            .AsNoTracking()
            .FirstOrDefaultAsync(o => o.Id == orderId);

        if (order == null)
        {
            _logger.LogWarning("GetOrCreateConversation: Order {OrderId} not found", orderId);
            return null;
        }

        // D6 (2026-09-17): resolve the customer identity. Logged-in orders carry CustomerId;
        // guest orders (CustomerId=null) are linked by CustomerDeviceId — the guest's identity
        // is then the device id itself (verified against the order's device id).
        Guid customerIdentity;
        if (order.CustomerId.HasValue && order.CustomerId.Value != Guid.Empty)
        {
            customerIdentity = order.CustomerId.Value;
        }
        else if (guestDeviceId.HasValue && guestDeviceId.Value != Guid.Empty
                 && Guid.TryParse(order.CustomerDeviceId, out var orderDeviceId)
                 && orderDeviceId == guestDeviceId.Value)
        {
            customerIdentity = guestDeviceId.Value;
            _logger.LogInformation("GetOrCreateConversation: Order {OrderId} is a guest order — using device id as identity", orderId);
        }
        else
        {
            _logger.LogWarning("GetOrCreateConversation: Order {OrderId} has no CustomerId and no matching guest device id", orderId);
            return null;
        }

        // Only DELIVERY orders have chat
        if (!string.Equals(order.OrderType, "DELIVERY", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning("GetOrCreateConversation: Order {OrderId} is not DELIVERY (type={OrderType})", orderId, order.OrderType);
            return null;
        }

        // Determine shipperId: use DeliveryTask.ShipperId if a DeliveryTask exists,
        // otherwise use Guid.Empty as placeholder (customer can chat before shipper accepts).
        var task = await _dbContext.DeliveryTasks
            .IgnoreQueryFilters()
            .AsNoTracking()
            .FirstOrDefaultAsync(dt => dt.OrderId == orderId && dt.Status != DeliveryTaskStatus.Cancelled);

        var shipperId = task?.ShipperId ?? Guid.Empty;

        var conversation = new Conversation(order.TenantId, orderId, shipperId, customerIdentity);
        _dbContext.Conversations.Add(conversation);
        await _dbContext.SaveChangesAsync();

        _logger.LogInformation("GetOrCreateConversation: Created conversation {ConvId} for order {OrderId} (shipperId={ShipperId})",
            conversation.Id, orderId, shipperId);

        return conversation;
    }

    public async Task<Message?> SendMessageAsync(Guid orderId, Guid senderId, string content, Guid? guestDeviceId = null)
    {
        if (string.IsNullOrWhiteSpace(content))
            throw new ArgumentException("Content cannot be empty", nameof(content));

        if (content.Length > 2000)
            throw new ArgumentException("Content exceeds 2000 characters", nameof(content));

        // Get or create conversation (no DeliveryTask required — see class doc)
        var conversation = await GetOrCreateConversationAsync(orderId, guestDeviceId);
        if (conversation == null)
            return null;

        // Verify sender is part of conversation.
        // CustomerId is always valid (conversation creation requires it).
        // ShipperId may be Guid.Empty (placeholder before shipper accepts) —
        // in that case, only the customer can send (shipper must accept first).
        if (senderId != conversation.ShipperId && senderId != conversation.CustomerId)
        {
            _logger.LogWarning("SendMessage: Sender {SenderId} not part of conversation {ConvId}", senderId, conversation.Id);
            throw new UnauthorizedAccessException("Sender not part of conversation");
        }

        var message = new Message(conversation.TenantId, conversation.Id, senderId, content);
        _dbContext.Messages.Add(message);
        await _dbContext.SaveChangesAsync();

        _logger.LogInformation("SendMessage: Message {MsgId} sent in conversation {ConvId} by {SenderId}",
            message.Id, conversation.Id, senderId);

        return message;
    }

    public async Task<List<Message>> GetHistoryAsync(Guid orderId)
    {
        // No DeliveryTask check — chat history is available for any DELIVERY order
        // with a conversation. Returns empty list if no conversation exists yet.
        var conversation = await _dbContext.Conversations
            .IgnoreQueryFilters()
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.OrderId == orderId);

        if (conversation == null)
            return new List<Message>();

        return await _dbContext.Messages
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(m => m.ConversationId == conversation.Id)
            .OrderBy(m => m.SentAt)
            .ToListAsync();
    }

    public async Task MarkAsReadAsync(Guid messageId)
    {
        var message = await _dbContext.Messages
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(m => m.Id == messageId);

        if (message == null)
        {
            _logger.LogWarning("MarkAsRead: Message {MsgId} not found", messageId);
            return;
        }

        message.MarkAsRead();
        await _dbContext.SaveChangesAsync();
    }

    public async Task<bool> HasActiveDeliveryTaskAsync(Guid orderId)
    {
        return await _dbContext.DeliveryTasks
            .IgnoreQueryFilters()
            .AsNoTracking()
            .AnyAsync(dt => dt.OrderId == orderId && dt.Status != DeliveryTaskStatus.Cancelled);
    }
}
