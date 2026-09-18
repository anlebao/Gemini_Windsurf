using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using VanAn.CoreHub.Infrastructure;
using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Services.Adapters;

/// <summary>
/// Realtime Platform P5 (2026-09-18): authorizer for <see cref="RealtimeSubjectType.Shop"/>.
///
/// A Shop conversation is scoped to a tenant (SubjectId == tenant id, see RealtimeSubjectResolver).
/// Access rules:
///   - Staff: the caller's tenant (from the staff JWT <c>tenant_id</c> claim, threaded through
///     <see cref="CanAccessAsync(RealtimeSubjectType, Guid, Guid, Guid?, CancellationToken)"/>)
///     must equal the shop's tenant id — the staff member IS the shop side of the chat.
///   - Customer/guest: a participant of a Shop conversation for that tenant — the conversation
///     initiator (CustomerId) or a ConversationParticipant row (guest device id).
///
/// Unregistered identity kinds are denied. Multi-tenancy: every query filters by the subject's
/// tenant id first (IgnoreQueryFilters is deliberate — Gateway has no ambient tenant, see
/// RealtimeMessagingService doc).
/// </summary>
public class ShopRealtimeAuthorizer(
    IVanAnDbContext dbContext,
    ILogger<ShopRealtimeAuthorizer> logger) : IRealtimeParticipantAuthorizer
{
    private readonly IVanAnDbContext _dbContext = dbContext;
    private readonly ILogger<ShopRealtimeAuthorizer> _logger = logger;

    public Task<bool> CanAccessAsync(RealtimeSubjectType subjectType, Guid subjectId, Guid userId, CancellationToken ct = default)
        => CanAccessAsync(subjectType, subjectId, userId, tenantId: null, ct);

    public async Task<bool> CanAccessAsync(
        RealtimeSubjectType subjectType, Guid subjectId, Guid userId, Guid? tenantId, CancellationToken ct = default)
    {
        if (userId == Guid.Empty || subjectId == Guid.Empty)
            return false;

        var shopTenantId = subjectId;

        // Staff side: the caller's tenant (JWT tenant_id claim) must be the shop itself.
        if (tenantId.HasValue && tenantId.Value != Guid.Empty)
        {
            var isStaffOfShop = tenantId.Value == shopTenantId;
            if (isStaffOfShop)
                return true;

            _logger.LogDebug("ShopRealtimeAuthorizer: staff {UserId} tenant {TenantId} != shop {ShopTenantId}", userId, tenantId.Value, shopTenantId);
            return false;
        }

        // Customer/guest side: participant of a Shop conversation of this tenant.
        var conversationIds = await _dbContext.Conversations
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(c => c.SubjectType == RealtimeSubjectType.Shop.ToString()
                     && c.SubjectId == shopTenantId)
            .Select(c => c.Id)
            .ToListAsync(ct);

        if (conversationIds.Count == 0)
        {
            _logger.LogDebug("ShopRealtimeAuthorizer: no Shop conversation for tenant {ShopTenantId}", shopTenantId);
            return false;
        }

        var isInitiator = await _dbContext.Conversations
            .IgnoreQueryFilters()
            .AsNoTracking()
            .AnyAsync(c => conversationIds.Contains(c.Id) && c.CustomerId == userId, ct);

        if (isInitiator)
            return true;

        var isParticipant = await _dbContext.ConversationParticipants
            .IgnoreQueryFilters()
            .AsNoTracking()
            .AnyAsync(p => p.ParticipantId == userId && p.IsActive && conversationIds.Contains(p.ConversationId), ct);

        if (!isParticipant)
            _logger.LogDebug("ShopRealtimeAuthorizer: denied user {UserId} for shop {ShopTenantId}", userId, shopTenantId);

        return isParticipant;
    }
}
