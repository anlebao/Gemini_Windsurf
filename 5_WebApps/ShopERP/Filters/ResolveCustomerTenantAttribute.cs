using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using VanAn.CoreHub.Infrastructure;
using VanAn.Shared.Domain;
using VanAn.Shared.Domain.Common;
using VanAn.Shared.Services;
using VanAn.ShopERP.Services;

namespace VanAn.ShopERP.Filters;

/// <summary>
/// KhachLink bugs fix (2026-07-27): Resolves the customer's TenantId from the
/// X-Customer-Token (or Authorization: Bearer) header and sets it on ITenantProvider
/// before the controller action runs.
///
/// Root cause: Customer-facing endpoints are [AllowAnonymous] (token auth, not cookie).
/// ITenantProvider.TenantId = Guid.Empty because there's no tenant_id claim in the
/// auth state. The global TenantId query filter on VanAnDbContext excludes all
/// customer/mission/loyalty/push-subscription records → endpoints return 404/empty.
///
/// Fix: This filter validates the customer token, loads the customer's TenantId
/// (using IgnoreQueryFilters for the initial lookup), and calls
/// ITenantProvider.SetTenant() so all subsequent queries in the request scope
/// use the correct tenant context.
/// </summary>
public class ResolveCustomerTenantAttribute : Attribute, IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var httpContext = context.HttpContext;
        var sp = httpContext.RequestServices;

        // Read token from X-Customer-Token header or Authorization: Bearer header
        string? token = httpContext.Request.Headers["X-Customer-Token"].FirstOrDefault();
        if (string.IsNullOrEmpty(token))
        {
            string? authHeader = httpContext.Request.Headers["Authorization"].FirstOrDefault();
            if (!string.IsNullOrEmpty(authHeader) &&
                authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            {
                token = authHeader["Bearer ".Length..];
            }
        }

        if (!string.IsNullOrEmpty(token))
        {
            var tokenService = sp.GetRequiredService<ICustomerTokenService>();
            var customerId = tokenService.ValidateToken(token);

            if (customerId.HasValue)
            {
                // Load customer's TenantId using IgnoreQueryFilters
                // (the scoped DbContext has TenantId=Guid.Empty, so normal query returns null)
                var dbContext = sp.GetRequiredService<IVanAnDbContext>();
                var tenantInfo = await dbContext.Customers
                    .IgnoreQueryFilters()
                    .Where(c => c.Id == customerId.Value && !c.IsDeleted)
                    .Select(c => new { c.TenantId })
                    .FirstOrDefaultAsync();

                if (tenantInfo?.TenantId is { } tenantId && tenantId.Value != Guid.Empty)
                {
                    var tenantProvider = sp.GetRequiredService<ITenantProvider>();
                    tenantProvider.SetTenant(tenantId.Value);
                }
            }
        }

        await next();
    }
}
