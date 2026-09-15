using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Services;

/// <summary>
/// CC-S3 (Sprint 3): Chat service — Conversation + Message persistence for shipper ↔ customer chat.
/// UC-07 (Chat). Chat is available for DELIVERY orders as soon as the order has a CustomerId.
/// Conversation is created with placeholder ShipperId=Guid.Empty if no DeliveryTask exists yet.
/// </summary>
public interface IChatService
{
    /// <summary>
    /// Get or create a Conversation for the given order.
    /// Creates if not exists — requires Order to exist with CustomerId and OrderType=DELIVERY.
    /// Uses ShipperId=Guid.Empty as placeholder if no DeliveryTask exists (before shipper accepts).
    /// </summary>
    Task<Conversation?> GetOrCreateConversationAsync(Guid orderId);

    /// <summary>
    /// Send a message in the conversation for the given order.
    /// Creates conversation if not exists. Verifies sender is ShipperId or CustomerId.
    /// </summary>
    Task<Message?> SendMessageAsync(Guid orderId, Guid senderId, string content);

    /// <summary>
    /// Get chat history for the given order, sorted by SentAt ascending.
    /// Returns empty list if no conversation exists yet.
    /// </summary>
    Task<List<Message>> GetHistoryAsync(Guid orderId);

    /// <summary>
    /// Mark a message as read.
    /// </summary>
    Task MarkAsReadAsync(Guid messageId);

    /// <summary>
    /// Check if a DeliveryTask exists for the given order (any status except Cancelled).
    /// </summary>
    Task<bool> HasActiveDeliveryTaskAsync(Guid orderId);
}
