using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Services;

/// <summary>
/// CC-S3 (Sprint 3): Chat service — Conversation + Message persistence for shipper ↔ customer chat.
/// UC-07 (Chat). Chat only allowed when DeliveryTask exists (active or completed).
/// </summary>
public interface IChatService
{
    /// <summary>
    /// Get or create a Conversation for the given order.
    /// Creates if not exists — requires DeliveryTask to exist (active or completed).
    /// </summary>
    Task<Conversation?> GetOrCreateConversationAsync(Guid orderId);

    /// <summary>
    /// Send a message in the conversation for the given order.
    /// Verifies DeliveryTask exists + sender is ShipperId or CustomerId.
    /// </summary>
    Task<Message?> SendMessageAsync(Guid orderId, Guid senderId, string content);

    /// <summary>
    /// Get chat history for the given order, sorted by SentAt ascending.
    /// Verifies DeliveryTask exists.
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
