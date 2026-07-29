using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using VanAn.CoreHub.Infrastructure;
using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Services;

/// <summary>
/// CC-S3 (Sprint 3): Chat service implementation.
/// Conversation + Message persistence. Cross-tenant via IgnoreQueryFilters.
/// Chat gating: DeliveryTask must exist (any status except Cancelled).
/// </summary>
public class ChatService(
    IVanAnDbContext dbContext,
    ILogger<ChatService> logger) : IChatService
{
    private readonly IVanAnDbContext _dbContext = dbContext;
    private readonly ILogger<ChatService> _logger = logger;

    public async Task<Conversation?> GetOrCreateConversationAsync(Guid orderId)
    {
        // Check for existing conversation
        var existing = await _dbContext.Conversations
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(c => c.OrderId == orderId);

        if (existing != null)
            return existing;

        // Need DeliveryTask to create conversation
        var task = await _dbContext.DeliveryTasks
            .IgnoreQueryFilters()
            .AsNoTracking()
            .FirstOrDefaultAsync(dt => dt.OrderId == orderId && dt.Status != DeliveryTaskStatus.Cancelled);

        if (task == null)
        {
            _logger.LogWarning("GetOrCreateConversation: No DeliveryTask for order {OrderId}", orderId);
            return null;
        }

        // Get CustomerId from Order
        var order = await _dbContext.Orders
            .IgnoreQueryFilters()
            .AsNoTracking()
            .FirstOrDefaultAsync(o => o.Id == orderId);

        if (order == null || order.CustomerId == null)
        {
            _logger.LogWarning("GetOrCreateConversation: Order {OrderId} not found or no CustomerId", orderId);
            return null;
        }

        var conversation = new Conversation(task.TenantId, orderId, task.ShipperId, order.CustomerId.Value);
        _dbContext.Conversations.Add(conversation);
        await _dbContext.SaveChangesAsync();

        _logger.LogInformation("GetOrCreateConversation: Created conversation {ConvId} for order {OrderId}",
            conversation.Id, orderId);

        return conversation;
    }

    public async Task<Message?> SendMessageAsync(Guid orderId, Guid senderId, string content)
    {
        if (string.IsNullOrWhiteSpace(content))
            throw new ArgumentException("Content cannot be empty", nameof(content));

        if (content.Length > 2000)
            throw new ArgumentException("Content exceeds 2000 characters", nameof(content));

        // Verify DeliveryTask exists
        if (!await HasActiveDeliveryTaskAsync(orderId))
        {
            _logger.LogWarning("SendMessage: No DeliveryTask for order {OrderId}", orderId);
            return null;
        }

        // Get or create conversation
        var conversation = await GetOrCreateConversationAsync(orderId);
        if (conversation == null)
            return null;

        // Verify sender is part of conversation
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
        // Verify DeliveryTask exists
        if (!await HasActiveDeliveryTaskAsync(orderId))
            return new List<Message>();

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
