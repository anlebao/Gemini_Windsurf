using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Services;

/// <summary>
/// Stateless JWT token service — issues and validates JWT tokens for VanAn ecosystem.
/// Algorithm: HS256 (symmetric key). Token expiry: 8 hours.
/// Claims: sub, email, role, tenant_id (snake_case for HttpContextTenantProvider compatibility), exp.
/// </summary>
public class JwtTokenService : IJwtTokenService
{
    private readonly string _secret;
    private readonly string _issuer;
    private readonly string _audience;
    private readonly TimeSpan _tokenExpiry = TimeSpan.FromHours(8);

    public JwtTokenService(IConfiguration configuration)
    {
        _secret = configuration["Jwt:Secret"]
            ?? throw new InvalidOperationException("Jwt:Secret configuration is required.");
        _issuer = configuration["Jwt:Issuer"]
            ?? throw new InvalidOperationException("Jwt:Issuer configuration is required.");
        _audience = configuration["Jwt:Audience"]
            ?? throw new InvalidOperationException("Jwt:Audience configuration is required.");

        if (_secret.Length < 32)
            throw new InvalidOperationException("Jwt:Secret must be at least 32 characters (256-bit) for HS256.");
    }

    public string GenerateToken(
        Guid userId,
        string email,
        UserRole role,
        Guid tenantId,
        IEnumerable<Claim>? additionalClaims = null)
    {
        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_secret));
        var credentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new(JwtRegisteredClaimNames.Email, email),
            // dual claim: snake_case for HttpContextTenantProvider + PascalCase for backward compat
            new("tenant_id", tenantId.ToString()),
            new("TenantId", tenantId.ToString()),
            // standard role claim compatible with RequireRole() policies
            new(ClaimTypes.Role, role.ToString()),
        };

        if (additionalClaims != null)
            claims.AddRange(additionalClaims);

        var token = new JwtSecurityToken(
            issuer: _issuer,
            audience: _audience,
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: DateTime.UtcNow.Add(_tokenExpiry),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public ClaimsPrincipal ValidateToken(string token)
    {
        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_secret));

        var validationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = signingKey,
            ValidateIssuer = true,
            ValidIssuer = _issuer,
            ValidateAudience = true,
            ValidAudience = _audience,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero
        };

        return new JwtSecurityTokenHandler().ValidateToken(token, validationParameters, out _);
    }
}
