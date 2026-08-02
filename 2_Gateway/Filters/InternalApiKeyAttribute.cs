using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Configuration;

namespace VanAn.Gateway.Filters;

/// <summary>
/// Loyalty Consistency Fix Phase 0 (Option B): authorizes internal service-to-service
/// HTTP calls from ShopERP → Gateway internal endpoints via shared X-Internal-Api-Key header.
///
/// Multi-VPS safety: ShopERP never connects to PG directly; all Alliance operations route
/// through Gateway HTTP internal API secured by this attribute.
///
/// Config: InternalLoyalty:ApiKey (env var: InternalLoyalty__ApiKey). Returns 503 if
/// unconfigured (deployment misconfiguration), 401 if missing/wrong key.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public class InternalApiKeyAttribute : Attribute, IAsyncAuthorizationFilter
{
    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        var config = context.HttpContext.RequestServices.GetRequiredService<IConfiguration>();
        string? expectedKey = config["InternalLoyalty:ApiKey"];

        if (string.IsNullOrEmpty(expectedKey))
        {
            // Deployment misconfiguration — fail closed with 503 (service not ready)
            context.Result = new StatusCodeResult(503);
            return;
        }

        if (!context.HttpContext.Request.Headers.TryGetValue("X-Internal-Api-Key", out var provided)
            || !string.Equals(provided.ToString(), expectedKey, StringComparison.Ordinal))
        {
            context.Result = new UnauthorizedResult();
            return;
        }

        await Task.CompletedTask;
    }
}
