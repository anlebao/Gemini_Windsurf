using System.Security.Claims;
using VanAn.Atrs.Domain;
using VanAn.Atrs.Infra;

namespace VanAn.Atrs.Api.Auth;

/// <summary>
/// ATRS-10 subset: extract staff identity (sub → DemoUserId) + roles từ JWT,
/// check permission backend-authoritative qua IAtrsAuthorizationService.
/// SystemAdmin role = break-glass (FR-014) — mọi permission.
/// </summary>
public static class StaffPrincipal
{
    public static Guid UserId(ClaimsPrincipal user)
    {
        var sub = user.FindFirst("sub")?.Value ?? user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(sub, out var id) ? id : Guid.Empty;
    }

    public static List<string> Roles(ClaimsPrincipal user)
        => user.FindAll(ClaimTypes.Role).Select(c => c.Value).ToList();

    public static async Task<bool> HasPermissionAsync(
        IAtrsAuthorizationService authz, ClaimsPrincipal user, string permission,
        Guid? scopeTenantId = null, CancellationToken ct = default)
    {
        var userId = UserId(user);
        if (userId == Guid.Empty)
            return false;
        return await authz.HasAsync(userId, Roles(user), permission, scopeTenantId, ct);
    }

    public static bool IsSystemAdmin(ClaimsPrincipal user)
        => user.IsInRole("SystemAdmin");
}
