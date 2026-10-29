using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Configuration;

namespace VanAn.ShopERP.Filters;

/// <summary>
/// Tra soát số liệu kế toán (P2): authorizes internal service-to-service HTTP calls
/// from Gateway → ShopERP internal endpoints via shared X-Internal-Api-Key header.
/// Mirror của `2_Gateway/Filters/InternalApiKeyAttribute.cs` (ShopERP direction ngược:
/// Gateway là caller, ShopERP là callee — cùng shared secret InternalLoyalty:ApiKey).
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
