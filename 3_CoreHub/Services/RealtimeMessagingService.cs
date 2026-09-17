using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using VanAn.CoreHub.Infrastructure;
using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Services;

/// <summary>
/// Realtime Platform P2 (2026-09-17): generic implementation of <see cref="IRealtimeMessagingService"/>.
///
/// Multi-tenancy: every query uses IgnoreQueryFilters() deliberately — the same reason ChatService
/// does. Gateway serves customers/staff whose tenant is resolved per request (from the order, the
/// device id or the staff JWT), so there is no ambient tenant scope to filter on; the caller passes
/// an explicit TenantId and authorization is enforced by IRealtimeParticipantAuthorizer.
/// </summary>
public class RealtimeMessagingService(
    IVanAnDbContext dbContext,
    ILogger<RealtimeMessagingService> logger) : IRealtimeMessagingService
{
    private const int MaxContentLength = 2000;

    private readonly IVanAnDbContext _dbContext = dbContext;
    private readonly ILogger<RealtimeMessagingService> _logger = logger;

    public async Task<Conversation?> EnsureConversationAsync(
        TenantId tenantId,
        RealtimeSubjectType subjectType,
        Guid subjectId,
        Guid initiatorId,
        Guid counterpartId,
        string initiatorRole = RealtimeParticipantRole.Customer,
        string counterpartRole = RealtimeParticipantRole.Shop,
        CancellationToken ct = default)
    {
        if (subjectId == Guid.Empty)
        {
            _logger.LogWarning("EnsureConversation: empty subjectId for {SubjectType}", subjectType);
            return null;
        }

        var subjectCode = subjectType.ToString();

        var conversation = await _dbContext.Conversations
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(c => c.TenantId == tenantId
                                   && c.SubjectType == subjectCode
                                   && c.SubjectId == subjectId, ct);

        if (conversation == null)
        {
            conversation = new Conversation(tenantId, subjectType, subjectId, initiatorId, counterpartId);
            _dbContext.Conversations.Add(conversation);

            try
            {
                await _dbContext.SaveChangesAsync(ct);
                _logger.LogInformation("EnsureConversation: created {ConvId} for {SubjectType}/{SubjectId}",
                    conversation.Id, subjectCode, subjectId);
            }
            catch (DbUpdateException ex)
            {
                // Two callers raced on the same subject — the unique index (TenantId, SubjectType,
                // SubjectId) rejected the loser. Re-read the winner's row.
                _logger.LogInformation(ex, "EnsureConversation: race on {SubjectType}/{SubjectId} — re-reading", subjectCode, subjectId);

                // Drop the losing insert from the change tracker so it is not retried on the next save.
                if (_dbContext is DbContext context)
                    context.Entry(conversation).State = EntityState.Detached;

                conversation = await _dbContext.Conversations
                    .IgnoreQueryFilters()
                    .FirstOrDefaultAsync(c => c.TenantId == tenantId
                                           && c.SubjectType == subjectCode
                                           && c.SubjectId == subjectId, ct);
                if (conversation == null)
                    return null;
            }
        }

        await EnsureParticipantAsync(conversation, initiatorId, initiatorRole, ct);
        if (counterpartId != Guid.Empty)
            await EnsureParticipantAsync(conversation, counterpartId, counterpartRole, ct);

        return conversation;
    }

    public async Task<Conversation?> GetConversationAsync(
        RealtimeSubjectType subjectType,
        Guid subjectId,
        CancellationToken ct = default)
    {
        var subjectCode = subjectType.ToString();

        return await _dbContext.Conversations
            .IgnoreQueryFilters()
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.SubjectType == subjectCode && c.SubjectId == subjectId, ct);
    }

    public async Task<Message?> SendMessageAsync(Guid conversationId, Guid senderId, string content, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(content))
            throw new ArgumentException("Content cannot be empty", nameof(content));

        if (content.Length > MaxContentLength)
            throw new ArgumentException($"Content exceeds {MaxContentLength} characters", nameof(content));

        var conversation = await _dbContext.Conversations
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(c => c.Id == conversationId, ct);

        if (conversation == null)
        {
            _logger.LogWarning("SendMessage: Conversation {ConvId} not found", conversationId);
            return null;
        }

        if (!await IsParticipantAsync(conversationId, senderId, ct))
        {
            _logger.LogWarning("SendMessage: Sender {SenderId} not a participant of {ConvId}", senderId, conversationId);
            throw new UnauthorizedAccessException("Sender not part of conversation");
        }

        var message = new Message(conversation.TenantId, conversationId, senderId, content);
        _dbContext.Messages.Add(message);
        await _dbContext.SaveChangesAsync(ct);

        _logger.LogInformation("SendMessage: Message {MsgId} sent in conversation {ConvId} by {SenderId}",
            message.Id, conversationId, senderId);

        return message;
    }

    public async Task<List<Message>> GetHistoryAsync(Guid conversationId, int take = 100, CancellationToken ct = default)
    {
        if (take <= 0)
            return new List<Message>();

        // Newest-first with a bound, then reversed — keeps the most recent `take` messages
        // for a long conversation instead of the oldest ones.
        var messages = await _dbContext.Messages
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(m => m.ConversationId == conversationId)
            .OrderByDescending(m => m.SentAt)
            .Take(take)
            .ToListAsync(ct);

        messages.Reverse();
        return messages;
    }

    public async Task MarkAsReadAsync(Guid messageId, CancellationToken ct = default)
    {
        var message = await _dbContext.Messages
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(m => m.Id == messageId, ct);

        if (message == null)
        {
            _logger.LogWarning("MarkAsRead: Message {MsgId} not found", messageId);
            return;
        }

        message.MarkAsRead();
        await _dbContext.SaveChangesAsync(ct);
    }

    public async Task<bool> IsParticipantAsync(Guid conversationId, Guid userId, CancellationToken ct = default)
    {
        if (userId == Guid.Empty)
            return false;

        // Generic participant rows (realtime platform subjects).
        var isParticipant = await _dbContext.ConversationParticipants
            .IgnoreQueryFilters()
            .AsNoTracking()
            .AnyAsync(p => p.ConversationId == conversationId && p.ParticipantId == userId && p.IsActive, ct);

        if (isParticipant)
            return true;

        // Legacy pair — order conversations created before/outside the participant table.
        return await _dbContext.Conversations
            .IgnoreQueryFilters()
            .AsNoTracking()
            .AnyAsync(c => c.Id == conversationId
                        && (c.ShipperId == userId || c.CustomerId == userId), ct);
    }

    private async Task EnsureParticipantAsync(Conversation conversation, Guid participantId, string roleCode, CancellationToken ct)
    {
        if (participantId == Guid.Empty)
            return;

        var existing = await _dbContext.ConversationParticipants
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(p => p.ConversationId == conversation.Id && p.ParticipantId == participantId, ct);

        if (existing != null)
        {
            if (!existing.IsActive)
            {
                existing.Reactivate();
                await _dbContext.SaveChangesAsync(ct);
            }
            return;
        }

        _dbContext.ConversationParticipants.Add(
            new ConversationParticipant(conversation.TenantId, conversation.Id, participantId, roleCode));

        await _dbContext.SaveChangesAsync(ct);
    }
}
