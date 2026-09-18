using Microsoft.Extensions.Logging;
using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Services.Adapters;

/// <summary>
/// Realtime Platform P5 (2026-09-18): authorizer for <see cref="RealtimeSubjectType.Shop"/>.
///
/// A Shop conversation is a shared thread scoped to a tenant (SubjectId == tenant id,
/// see RealtimeSubjectResolver) — the shop side is a tenant, not a user.
///
/// Access model (P6 revision — public chat widget):
///   - Staff: the caller's tenant (staff JWT <c>tenant_id</c> claim) must equal the shop's
///     tenant id — staff members are the shop side of the chat and see the whole thread.
///   - Customer/guest: ANY visitor may message the shop (the widget is public). Privacy is NOT
///     enforced here — it is enforced at the API layer: history is filtered per caller (own
///     messages + shop replies only) and live pushes go to per-user SignalR groups, so no
///     customer ever sees another customer's messages. The shop's existence is validated by the
///     subject resolver before this authorizer is reached (nonexistent shop → 404).
/// </summary>
public class ShopRealtimeAuthorizer(
    ILogger<ShopRealtimeAuthorizer> logger) : IRealtimeParticipantAuthorizer
{
    private readonly ILogger<ShopRealtimeAuthorizer> _logger = logger;

    public Task<bool> CanAccessAsync(RealtimeSubjectType subjectType, Guid subjectId, Guid userId, CancellationToken ct = default)
        => CanAccessAsync(subjectType, subjectId, userId, tenantId: null, ct);

    public Task<bool> CanAccessAsync(
        RealtimeSubjectType subjectType, Guid subjectId, Guid userId, Guid? tenantId, CancellationToken ct = default)
    {
        if (userId == Guid.Empty || subjectId == Guid.Empty)
            return Task.FromResult(false);

        // Staff side: the caller's tenant (JWT tenant_id claim) must be the shop itself.
        if (tenantId.HasValue && tenantId.Value != Guid.Empty)
        {
            var isStaffOfShop = tenantId.Value == subjectId;
            if (!isStaffOfShop)
                _logger.LogDebug("ShopRealtimeAuthorizer: staff {UserId} tenant {TenantId} != shop {ShopTenantId}", userId, tenantId.Value, subjectId);
            return Task.FromResult(isStaffOfShop);
        }

        // Customer/guest: public widget — any visitor may message the shop. See class doc for
        // where privacy is actually enforced (per-caller history filter + per-user push groups).
        return Task.FromResult(true);
    }
}
