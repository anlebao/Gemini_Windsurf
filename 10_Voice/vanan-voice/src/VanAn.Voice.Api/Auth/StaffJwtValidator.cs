using System.IdentityModel.Tokens.Jwt;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using VanAn.Voice.Domain;

namespace VanAn.Voice.Api.Auth;

/// <summary>
/// Staff identity from the Vạn An JWT (HS256, shared <c>Jwt:Secret</c> — same scheme the
/// Gateway validates, SRS v1.1 O2). Claims: <c>sub</c> = userId, <c>tenant_id</c> = tenant
/// (snake_case; "system" for SystemAdmin → no tenant). Transport: query <c>access_token</c>
/// first (SignalR handshakes cannot set headers), then <c>Authorization: Bearer</c>.
/// </summary>
public sealed class StaffJwtValidator(IConfiguration configuration, ILogger<StaffJwtValidator> logger) : IVoiceTokenValidator
{
    public const string QueryKey = "access_token";
    public const string HeaderKey = "Authorization";

    public string Name => "StaffJwt";

    public async Task<VoiceIdentity?> ValidateAsync(HttpContext httpContext, CancellationToken ct = default)
    {
        var token = ReadCredential(httpContext);
        if (string.IsNullOrEmpty(token)) return null;

        var secret = configuration["Jwt:Secret"];
        var issuer = configuration["Jwt:Issuer"] ?? "VanAnShopERP";
        var audience = configuration["Jwt:Audience"] ?? "VanAnApi";
        if (string.IsNullOrEmpty(secret)) return null;

        try
        {
            var handler = new JwtSecurityTokenHandler();
            // Keep short-form claim types ("sub", "tenant_id") — same decision as the Gateway
            // JwtBearer setup (MapInboundClaims = false).
            handler.MapInboundClaims = false;
            var result = await handler.ValidateTokenAsync(token, new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret)),
                IssuerSigningKeyResolver = (_, _, _, validationParameters) => new[] { validationParameters.IssuerSigningKey },
                ValidateIssuer = true,
                ValidIssuer = issuer,
                ValidateAudience = true,
                ValidAudience = audience,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.Zero,
                NameClaimType = "sub"
            });
            if (!result.IsValid) return null;

            var sub = result.Claims.FirstOrDefault(c => c.Key == "sub").Value?.ToString();
            if (!Guid.TryParse(sub, out var userId) || userId == Guid.Empty) return null;

            Guid? tenantId = null;
            var tenantClaim = result.Claims.FirstOrDefault(c => c.Key == "tenant_id").Value?.ToString();
            if (Guid.TryParse(tenantClaim, out var tenant))
                tenantId = tenant;

            return new VoiceIdentity(userId, VoiceIdentityKind.Staff, tenantId);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "StaffJwtValidator: token validation failed");
            return null;
        }
    }

    private static string? ReadCredential(HttpContext httpContext)
    {
        if (httpContext.Request.Query.TryGetValue(QueryKey, out var q) && !string.IsNullOrWhiteSpace(q.ToString()))
            return q.ToString();

        if (httpContext.Request.Headers.TryGetValue(HeaderKey, out var h) && !string.IsNullOrWhiteSpace(h.ToString()))
        {
            var value = h.ToString();
            return value.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) ? value["Bearer ".Length..].Trim() : value;
        }
        return null;
    }
}