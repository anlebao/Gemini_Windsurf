using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;

namespace VanAn.Gateway.Infrastructure
{
    /// <summary>
    /// Normalizes JWT role claims so both short-form ("role") and long-form
    /// (ClaimTypes.Role URI) are understood by ASP.NET Core authorization.
    ///
    /// Problem:
    ///   - JwtTokenService (internal) uses JwtSecurityTokenHandler which serializes
    ///     ClaimTypes.Role as the long-form URI "http://schemas.microsoft.com/ws/2008/06/identity/claims/role".
    ///   - External JWTs (Python scripts, integration tests, other services) often use
    ///     the short-form "role" claim key.
    ///   - Gateway has MapInboundClaims=false + RoleClaimType=ClaimTypes.Role, so only
    ///     long-form is recognized by RequireRole() policies.
    ///
    /// Fix:
    ///   This transformer runs after authentication. If the principal has a short-form
    ///   "role" claim but no ClaimTypes.Role claim, it adds a ClaimTypes.Role claim
    ///   with the same value. This way RequireRole() finds the role regardless of
    ///   which form the JWT used.
    ///
    /// Idempotent: if ClaimTypes.Role already exists, no duplication occurs.
    /// </summary>
    public class RoleClaimNormalizer : IClaimsTransformation
    {
        public Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
        {
            // Only transform if there's a short-form "role" claim but no long-form ClaimTypes.Role
            var shortFormRoles = principal.FindAll("role").ToList();
            if (shortFormRoles.Count == 0)
                return Task.FromResult(principal);

            var identity = principal.Identity as ClaimsIdentity;
            if (identity is null)
                return Task.FromResult(principal);

            // Check if long-form already exists — if so, no normalization needed
            var longFormRoles = principal.FindAll(ClaimTypes.Role).ToList();
            if (longFormRoles.Count > 0)
                return Task.FromResult(principal);

            // Add long-form ClaimTypes.Role for each short-form "role" claim
            foreach (var shortRole in shortFormRoles)
            {
                identity.AddClaim(new Claim(ClaimTypes.Role, shortRole.Value));
            }

            return Task.FromResult(principal);
        }
    }
}
