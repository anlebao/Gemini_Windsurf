using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using VanAn.TenantAi.Contracts;
using VanAn.TenantAi.Domain;
using VanAn.TenantAi.Infra;

namespace VanAn.TenantAi.Api.Auth;

/// <summary>
/// SRS v1.1 [D7] — 3-layer caller auth for every /v1/* endpoint (except /v1/health):
///   L1 API key (X-Caller-Id + X-Ai-Key → SHA-256 match on CallerKeys)
///   L2 caller scope (RequireScope metadata vs caller scopes_json)
///   L3 signed tenant context (RequireTenantContext → X-Tenant-Ctx + Sig HMAC verify,
///      ±5min skew, callerId match, caller tenant_scope allowlist)
/// </summary>
public static class TenantAiAuthMiddleware
{
    private const long SkewSeconds = 300; // quy ước metering G5

    public static IApplicationBuilder UseTenantAiAuth(this IApplicationBuilder app)
        => app.Use(InvokeAsync);

    private static async Task InvokeAsync(HttpContext ctx, RequestDelegate next)
    {
        var path = ctx.Request.Path;
        if (!path.StartsWithSegments("/v1", StringComparison.Ordinal) || path == "/v1/health")
        {
            await next(ctx);
            return;
        }

        var logger = ctx.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("TenantAi.Auth");
        string? callerId = null;
        Guid? tenantId = null;

        try
        {
            // ---- L1: API key ----
            callerId = ctx.Request.Headers["X-Caller-Id"].FirstOrDefault();
            var apiKey = ctx.Request.Headers["X-Ai-Key"].FirstOrDefault();
            if (string.IsNullOrEmpty(callerId) || string.IsNullOrEmpty(apiKey))
            {
                await Reject(ctx, StatusCodes.Status401Unauthorized, "missing_credentials",
                    "X-Caller-Id and X-Ai-Key headers are required.");
                return;
            }

            var db = ctx.RequestServices.GetRequiredService<TenantAiDbContext>();
            var key = await db.CallerKeys.AsNoTracking()
                .FirstOrDefaultAsync(k => k.CallerId == callerId && k.IsActive);
            if (key == null || !Crypto.HashEquals(key.KeyHash, Crypto.Sha256Hex(apiKey)))
            {
                await Reject(ctx, StatusCodes.Status401Unauthorized, "invalid_credentials",
                    "Unknown caller or invalid key.");
                return;
            }

            var scopes = ParseStringArray(key.ScopesJson);

            // ---- L2: endpoint scope ----
            var endpoint = ctx.GetEndpoint();
            var requiredScope = endpoint?.Metadata.GetMetadata<RequireScopeAttribute>()?.Scope;
            if (requiredScope != null && !scopes.Contains(requiredScope, StringComparer.Ordinal))
            {
                await Reject(ctx, StatusCodes.Status403Forbidden, "forbidden",
                    $"Caller lacks required scope: {requiredScope}.");
                return;
            }

            // ---- L3: signed tenant context ----
            if (endpoint?.Metadata.GetMetadata<RequireTenantContextAttribute>() != null)
            {
                var ctxHeader = ctx.Request.Headers["X-Tenant-Ctx"].FirstOrDefault();
                var sigHeader = ctx.Request.Headers["X-Tenant-Ctx-Sig"].FirstOrDefault();
                if (string.IsNullOrEmpty(ctxHeader) || string.IsNullOrEmpty(sigHeader))
                {
                    await Reject(ctx, StatusCodes.Status401Unauthorized, "missing_tenant_context",
                        "X-Tenant-Ctx and X-Tenant-Ctx-Sig headers are required.");
                    return;
                }

                var secret = ctx.RequestServices.GetRequiredService<IConfiguration>()["TenantAi:CtxSigningSecret"];
                if (string.IsNullOrEmpty(secret))
                {
                    logger.LogError("TenantAi:CtxSigningSecret not configured — fail closed.");
                    await Reject(ctx, StatusCodes.Status401Unauthorized, "invalid_tenant_context",
                        "tenant context rejected: not configured.");
                    return;
                }

                if (!TenantContextSigner.Verify(ctxHeader, sigHeader, secret))
                {
                    await Reject(ctx, StatusCodes.Status401Unauthorized, "invalid_tenant_context",
                        "tenant context rejected: bad signature.");
                    return;
                }

                var payload = TenantContextSigner.Parse(ctxHeader);
                if (payload == null)
                {
                    await Reject(ctx, StatusCodes.Status401Unauthorized, "invalid_tenant_context",
                        "tenant context rejected: malformed payload.");
                    return;
                }
                if (!string.Equals(payload.CallerId, callerId, StringComparison.Ordinal))
                {
                    await Reject(ctx, StatusCodes.Status401Unauthorized, "invalid_tenant_context",
                        "tenant context rejected: caller mismatch.");
                    return;
                }

                var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                if (payload.Exp < now - SkewSeconds || payload.Iat > now + SkewSeconds)
                {
                    await Reject(ctx, StatusCodes.Status401Unauthorized, "invalid_tenant_context",
                        "tenant context rejected: expired.");
                    return;
                }

                if (!TenantScopeAllows(key.TenantScopeJson, payload.TenantId))
                {
                    await Reject(ctx, StatusCodes.Status403Forbidden, "forbidden",
                        "tenant outside caller scope.");
                    return;
                }

                tenantId = payload.TenantId;
                ctx.Items["TenantId"] = payload.TenantId;
                ctx.Items["RequestId"] = payload.RequestId;
            }

            ctx.Items["CallerId"] = callerId;
            ctx.Items["CallerIsAdmin"] = key.IsAdmin;
            ctx.Items["CallerScopesJson"] = key.ScopesJson;

            await next(ctx);
        }
        finally
        {
            logger.LogInformation("req {Caller} {Tenant} {Method} {Path} -> {Status}",
                callerId ?? "-", tenantId?.ToString() ?? "-", ctx.Request.Method, path, ctx.Response.StatusCode);
        }
    }

    /// <summary>null scope or contains "*" → all tenants allowed (gateway). Else guid allowlist.</summary>
    private static bool TenantScopeAllows(string? tenantScopeJson, Guid tenantId)
    {
        var entries = ParseStringArray(tenantScopeJson);
        if (entries.Count == 0 || entries.Contains("*", StringComparer.Ordinal)) return true;
        return entries.Any(e => Guid.TryParse(e, out var g) && g == tenantId);
    }

    private static List<string> ParseStringArray(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];
        try { return JsonSerializer.Deserialize<List<string>>(json) ?? []; }
        catch (JsonException) { return []; }
    }

    private static async Task Reject(HttpContext ctx, int status, string code, string message)
    {
        ctx.Response.StatusCode = status;
        await ctx.Response.WriteAsJsonAsync(new { error = new { code, message, retryable = false } });
    }
}

internal static class Crypto
{
    public static string Sha256Hex(string input)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(input)));

    /// <summary>Constant-time compare to avoid hash-oracle timing leaks.</summary>
    public static bool HashEquals(string storedHex, string candidateHex)
        => storedHex.Length == candidateHex.Length
           && CryptographicOperations.FixedTimeEquals(
               Encoding.UTF8.GetBytes(storedHex), Encoding.UTF8.GetBytes(candidateHex));
}
