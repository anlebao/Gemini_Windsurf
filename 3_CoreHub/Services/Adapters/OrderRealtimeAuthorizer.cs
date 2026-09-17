using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using VanAn.CoreHub.Infrastructure;
using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Services.Adapters;

/// <summary>
/// Realtime Platform P2 (2026-09-17): authorizer for <see cref="RealtimeSubjectType.Order"/>.
/// Moves the access rules previously inlined in Gateway <c>ChatHub.JoinConversation</c> and
/// <c>LocationHub.JoinOrderTracking</c> into one place, so the legacy hubs and the new generic
/// hubs share a single source of truth.
///
/// Access = assigned shipper (active DeliveryTask) OR conversation participant
/// (ShipperId/CustomerId or a ConversationParticipant row) OR order owner (CustomerId) OR the
/// guest that placed the order (Order.CustomerDeviceId == userId).
/// </summary>
public class OrderRealtimeAuthorizer(
    IVanAnDbContext dbContext,
    ILogger<OrderRealtimeAuthorizer> logger) : IRealtimeParticipantAuthorizer
{
    private readonly IVanAnDbContext _dbContext = dbContext;
    private readonly ILogger<OrderRealtimeAuthorizer> _logger = logger;

    public async Task<bool> CanAccessAsync(RealtimeSubjectType subjectType, Guid subjectId, Guid userId, CancellationToken ct = default)
    {
        if (userId == Guid.Empty || subjectId == Guid.Empty)
            return false;

        // SubjectId for an Order subject is the order id.
        var orderId = subjectId;

        var isShipper = await _dbContext.DeliveryTasks
            .IgnoreQueryFilters()
            .AsNoTracking()
            .AnyAsync(dt => dt.OrderId == orderId
                         && dt.ShipperId == userId
                         && dt.Status != DeliveryTaskStatus.Cancelled, ct);

        if (isShipper)
            return true;

        var isConversationParty = await _dbContext.Conversations
            .IgnoreQueryFilters()
            .AsNoTracking()
            .AnyAsync(c => c.OrderId == orderId && (c.ShipperId == userId || c.CustomerId == userId), ct);

        if (isConversationParty)
            return true;

        // Generic participant rows on any conversation of this order (device guests, extra parties).
        var conversationIds = await _dbContext.Conversations
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(c => c.OrderId == orderId)
            .Select(c => c.Id)
            .ToListAsync(ct);

        if (conversationIds.Count > 0)
        {
            var isParticipant = await _dbContext.ConversationParticipants
                .IgnoreQueryFilters()
                .AsNoTracking()
                .AnyAsync(p => p.ParticipantId == userId && p.IsActive && conversationIds.Contains(p.ConversationId), ct);

            if (isParticipant)
                return true;
        }

        // Order owner, or the guest device that placed the order (userId IS the device guid).
        var deviceId = userId.ToString();
        var isOwner = await _dbContext.Orders
            .IgnoreQueryFilters()
            .AsNoTracking()
            .AnyAsync(o => o.Id == orderId
                        && (o.CustomerId == userId || o.CustomerDeviceId == deviceId), ct);

        if (!isOwner)
            _logger.LogDebug("OrderRealtimeAuthorizer: denied user {UserId} for order {OrderId}", userId, orderId);

        return isOwner;
    }
}
