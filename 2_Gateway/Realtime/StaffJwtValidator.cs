using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using VanAn.Gateway.Services;

namespace VanAn.Gateway.Realtime;

/// <summary>
/// Realtime Platform P3 (2026-09-17): validates the staff Bearer JWT issued by ShopERP.
///
/// The Gateway already registers the same scheme for its API endpoints; this validator repeats the
/// validation by hand because SignalR hub connections are authenticated per-connection from the
/// query string, not through the request pipeline's authentication middleware.
///
/// Transport: query string <c>access_token</c> (the SignalR convention for WebSocket handshakes),
/// then the <c>Authorization: Bearer</c> header.
///
/// ATRS-3 (D3): kiểm tra claim `tv` so với version hiện tại (TokenVersionService) — token đã
/// revoke (recovery COMPLETED / security event) bị từ chối kể cả qua SignalR.
/// </summary>
public class StaffJwtValidator(
    IConfiguration configuration,
    ITokenVersionService tokenVersionService,
    ILogger<StaffJwtValidator> logger) : IRealtimeTokenValidator
{
    private const string QueryKey = "access_token";
    private const string HeaderKey = "Authorization";

    private readonly IConfiguration _configuration = configuration;
    private readonly ITokenVersionService _tokenVersionService = tokenVersionService;
    private readonly ILogger<StaffJwtValidator> _logger = logger;

    public string Name => "StaffJwt";

    public async Task<RealtimeIdentity?> ValidateAsync(HttpContext httpContext, CancellationToken ct = default)
    {
        var token = RealtimeRequestReader.ReadCredential(httpContext, QueryKey, HeaderKey);
        if (string.IsNullOrEmpty(token))
            return null;

        // The header carries the scheme ("Bearer <jwt>"), the query string does not.
        if (token.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            token = token["Bearer ".Length..].Trim();

        var secret = _configuration["Jwt:Secret"];
        if (string.IsNullOrEmpty(secret))
            return null;

        try
        {
            var handler = new JwtSecurityTokenHandler();
            var principal = handler.ValidateToken(token, new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret)),
                IssuerSigningKeyResolver = (_, _, _, validationParameters) => new[] { validationParameters.IssuerSigningKey },
                ValidateIssuer = true,
                ValidIssuer = _configuration["Jwt:Issuer"] ?? "VanAnShopERP",
                ValidateAudience = true,
                ValidAudience = _configuration["Jwt:Audience"] ?? "VanAnApi",
                ValidateLifetime = true,
                ClockSkew = TimeSpan.Zero,
                RoleClaimType = ClaimTypes.Role,
                NameClaimType = "sub"
            }, out _);

            var sub = principal.FindFirst("sub")?.Value
                ?? principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (Guid.TryParse(sub, out var userId) && userId != Guid.Empty)
            {
                // ATRS-3 (D3): token version check — lệch → token đã bị thu hồi.
                var version = await _tokenVersionService.GetVersionAsync(userId, ct);
                if (version.HasValue)
                {
                    var tv = int.TryParse(principal.FindFirst("tv")?.Value, out var v) ? v : 0;
                    if (tv != version.Value)
                    {
                        _logger.LogDebug("StaffJwtValidator: token version mismatch for {UserId} (tv={Tv}, expected={Expected})", userId, tv, version.Value);
                        return null;
                    }
                }

                // P5: the staff JWT carries tenant_id (see JwtTokenService) — the shop side of a
                // Shop conversation is a tenant, so the identity carries it for the authorizer.
                Guid? tenantId = null;
                var tenantClaim = principal.FindFirst("tenant_id")?.Value
                    ?? principal.FindFirst("TenantId")?.Value;
                if (Guid.TryParse(tenantClaim, out var tid) && tid != Guid.Empty)
                    tenantId = tid;

                return new RealtimeIdentity(userId, RealtimeIdentityKind.Staff, tenantId);
            }

            _logger.LogDebug("StaffJwtValidator: token has no usable sub claim ({Sub})", sub);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "StaffJwtValidator: bearer token rejected");
            return null;
        }
    }
}