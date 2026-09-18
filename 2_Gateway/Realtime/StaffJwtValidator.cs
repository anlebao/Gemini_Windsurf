using System.IdentityModel.Tokens.Jwt;
using System.Text;
using Microsoft.IdentityModel.Tokens;

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
/// Staff identity is the JWT <c>sub</c> claim. No authorizer is registered for staff-only subjects
/// yet, so a staff token is accepted but still denied access by the default-deny authorizer lookup —
/// this validator exists so a future Shop/staff subject can plug in without touching the hub.
/// </summary>
public class StaffJwtValidator(
    IConfiguration configuration,
    ILogger<StaffJwtValidator> logger) : IRealtimeTokenValidator
{
    private const string QueryKey = "access_token";
    private const string HeaderKey = "Authorization";

    private readonly IConfiguration _configuration = configuration;
    private readonly ILogger<StaffJwtValidator> _logger = logger;

    public string Name => "StaffJwt";

    public Task<RealtimeIdentity?> ValidateAsync(HttpContext httpContext, CancellationToken ct = default)
    {
        var token = RealtimeRequestReader.ReadCredential(httpContext, QueryKey, HeaderKey);
        if (string.IsNullOrEmpty(token))
            return Task.FromResult<RealtimeIdentity?>(null);

        // The header carries the scheme ("Bearer <jwt>"), the query string does not.
        if (token.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            token = token["Bearer ".Length..].Trim();

        var secret = _configuration["Jwt:Secret"];
        if (string.IsNullOrEmpty(secret))
            return Task.FromResult<RealtimeIdentity?>(null);

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
                RoleClaimType = System.Security.Claims.ClaimTypes.Role,
                NameClaimType = "sub"
            }, out _);

            var sub = principal.FindFirst("sub")?.Value
                ?? principal.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

            if (Guid.TryParse(sub, out var userId) && userId != Guid.Empty)
                return Task.FromResult<RealtimeIdentity?>(new RealtimeIdentity(userId, RealtimeIdentityKind.Staff));

            _logger.LogDebug("StaffJwtValidator: token has no usable sub claim ({Sub})", sub);
            return Task.FromResult<RealtimeIdentity?>(null);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "StaffJwtValidator: bearer token rejected");
            return Task.FromResult<RealtimeIdentity?>(null);
        }
    }
}
