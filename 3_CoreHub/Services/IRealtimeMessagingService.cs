using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Services;

/// <summary>
/// Realtime Platform P2 (2026-09-17): subject-agnostic conversation + message service.
/// The generic counterpart to <see cref="IChatService"/> (which stays order/DELIVERY-specific
/// and is kept as a thin adapter for existing callers and tests).
///
/// A "subject" is any business object two parties talk about — an order, a shop profile,
/// a support ticket, a shipment. TenantId is an explicit parameter (not ambient) because
/// Conversation.TenantId is required and Gateway has no tenant scope — multi-tenancy must be
/// stated by the caller, never inferred.
/// </summary>
public interface IRealtimeMessagingService
{
    /// <summary>
    /// Get or create the conversation for (tenantId, subjectType, subjectId).
    /// Idempotent: a second call returns the existing conversation and only tops up missing
    /// participant rows. Counterpart may be <see cref="Guid.Empty"/> when the other side is not
    /// known yet (mirrors the legacy "placeholder shipper" behaviour) and can be assigned later
    /// via <see cref="Conversation.AssignCounterpart"/>.
    /// </summary>
    Task<Conversation?> EnsureConversationAsync(
        TenantId tenantId,
        RealtimeSubjectType subjectType,
        Guid subjectId,
        Guid initiatorId,
        Guid counterpartId,
        string initiatorRole = RealtimeParticipantRole.Customer,
        string counterpartRole = RealtimeParticipantRole.Shop,
        CancellationToken ct = default);

    /// <summary>Get the conversation for a subject, or null when none exists yet.</summary>
    Task<Conversation?> GetConversationAsync(
        RealtimeSubjectType subjectType,
        Guid subjectId,
        CancellationToken ct = default);

    /// <summary>
    /// Send a message. Throws <see cref="UnauthorizedAccessException"/> when the sender is not a
    /// participant of the conversation.
    /// </summary>
    Task<Message?> SendMessageAsync(Guid conversationId, Guid senderId, string content, CancellationToken ct = default);

    /// <summary>History for a conversation, oldest first (bounded by <paramref name="take"/>).</summary>
    Task<List<Message>> GetHistoryAsync(Guid conversationId, int take = 100, CancellationToken ct = default);

    /// <summary>All conversations for a subject — used by the shop inbox (P5) to list every
    /// customer conversation of a tenant. Bounded by <paramref name="take"/>.</summary>
    Task<List<Conversation>> GetConversationsAsync(
        RealtimeSubjectType subjectType,
        Guid subjectId,
        int take = 100,
        CancellationToken ct = default);

    /// <summary>
    /// Add (or reactivate) a participant row for a conversation. P5: the shop side of a Shop
    /// conversation is a tenant, not a user — a staff member's id is not a conversation party,
    /// so the gateway ensures the staff sender is a participant before SendMessageAsync's
    /// participant check. Safe after the authorizer has already granted access.
    /// </summary>
    Task EnsureParticipantAsync(Conversation conversation, Guid participantId, string roleCode, CancellationToken ct = default);

    Task MarkAsReadAsync(Guid messageId, CancellationToken ct = default);

    /// <summary>True when the user is an active participant (generic rows or the legacy pair).</summary>
    Task<bool> IsParticipantAsync(Guid conversationId, Guid userId, CancellationToken ct = default);
}
