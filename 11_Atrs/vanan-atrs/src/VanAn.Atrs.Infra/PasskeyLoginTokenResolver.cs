using Microsoft.EntityFrameworkCore;
using VanAn.Atrs.Contracts;
using VanAn.Atrs.Domain;

namespace VanAn.Atrs.Infra;

/// <summary>
/// ATRS-21 — resolve TrustSubject (sau passkey assertion verify) → customerId → ShopERP token.
/// Branches: Customer link → by-id; còn lại + deviceId → by-device (find-or-create stub);
/// không deviceId → NoCustomerIdentity. Audit mọi outcome vào AccountSecurityEvent.
/// </summary>
public sealed class PasskeyLoginTokenResolver(AtrsDbContext db, IPasskeyLoginTokenIssuer issuer)
{
    public async Task<PasskeyTokenResult> ResolveAsync(Guid trustSubjectId, string? deviceId, CancellationToken ct = default)
    {
        var links = await db.TrustSubjectLinks
            .Where(l => l.TrustSubjectId == trustSubjectId)
            .ToListAsync(ct);

        PasskeyTokenResult result;
        try
        {
            var customerLink = links.FirstOrDefault(l => l.IdentityType == TrustIdentityType.Customer);
            if (customerLink is not null)
            {
                result = await issuer.IssueByCustomerIdAsync(customerLink.IdentityId, ct);
            }
            else if (!string.IsNullOrWhiteSpace(deviceId))
            {
                var tenantId = links.Select(l => l.TenantId).FirstOrDefault(t => t.HasValue);
                result = await issuer.IssueByDeviceAsync(deviceId, tenantId, ct);
            }
            else
            {
                result = new PasskeyTokenResult(false, "NoCustomerIdentity", null, null, false);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            result = new PasskeyTokenResult(false, "TokenIssuanceFailed", null, null, false);
        }

        db.AccountSecurityEvents.Add(AccountSecurityEvent.Create(
            id: Guid.NewGuid(),
            trustSubjectId: trustSubjectId,
            eventType: result.Success ? "passkey.login.success" : "passkey.login.fail",
            actorType: SecurityEventActorType.User,
            actorId: null,
            sourceApplication: "atrs-passkey-login",
            deviceId: null,
            sessionCorrelationId: null,
            outcome: result.Success ? SecurityEventOutcome.Success : SecurityEventOutcome.Failure,
            reasonCode: result.Success ? null : result.ReasonCode));
        await db.SaveChangesAsync(ct);
        return result;
    }
}
