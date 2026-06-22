using System.Security.Claims;
using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Services;

/// <summary>
/// Issues and validates JWT tokens for VanAn authentication.
/// Tokens contain claims: sub, email, role, tenant_id, exp.
/// </summary>
public interface IJwtTokenService
{
    /// <summary>
    /// Generates a JWT access token with the required claims.
    /// </summary>
    /// <param name="userId">User's unique identifier (sub claim)</param>
    /// <param name="email">User's email</param>
    /// <param name="role">User's role (role claim)</param>
    /// <param name="tenantId">Tenant ID (tenant_id claim, snake_case)</param>
    /// <param name="additionalClaims">Optional extra claims for future extensibility (Wave 5/6)</param>
    /// <returns>Signed JWT string</returns>
    string GenerateToken(
        Guid userId,
        string email,
        UserRole role,
        Guid tenantId,
        IEnumerable<Claim>? additionalClaims = null);

    /// <summary>
    /// Validates a JWT token and returns the ClaimsPrincipal if valid.
    /// Throws SecurityTokenException if invalid, expired, or tampered.
    /// </summary>
    ClaimsPrincipal ValidateToken(string token);
}
