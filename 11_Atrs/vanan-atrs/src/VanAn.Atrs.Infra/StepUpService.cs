using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace VanAn.Atrs.Infra;

/// <summary>
/// Step-up primitive (FR-010, ATRS-3): token ngắn hạn (10') chứng minh subject vừa xác thực
/// gần đây bằng 1 method (passkey | recovery-code | password | totp). Gate hành động nhạy cảm:
/// revoke device, regenerate recovery codes, revoke credential.
/// JWT HS256 dùng chung Jwt:Secret (same signing key — ATRS tự verify, không phụ thuộc Gateway).
/// </summary>
public interface IStepUpService
{
    /// <summary>Issue step-up token cho subject sau khi xác thực thành công bằng method.</summary>
    string Issue(Guid trustSubjectId, string method, TimeSpan? ttl = null);

    /// <summary>Validate token: hợp lệ, chưa hết hạn, đúng subject. Trả method (amr) khi OK.</summary>
    string? Validate(string? token, Guid expectedTrustSubjectId);
}

public sealed class StepUpOptions
{
    public string Secret { get; set; } = "";
    public string Issuer { get; set; } = "vanan-atrs";
    public string Audience { get; set; } = "vanan-atrs-stepup";
    public int TtlMinutes { get; set; } = 10;
}

public sealed class StepUpService(IConfiguration configuration) : IStepUpService
{
    private readonly StepUpOptions _options = new()
    {
        Secret = configuration["Jwt:Secret"] ?? configuration["StepUp:Secret"] ?? "",
        Issuer = configuration["Jwt:Issuer"] ?? "vanan-atrs",
        Audience = "vanan-atrs-stepup",
        TtlMinutes = int.TryParse(configuration["StepUp:TtlMinutes"], out var ttl) ? ttl : 10
    };

    public string Issue(Guid trustSubjectId, string method, TimeSpan? ttl = null)
    {
        if (string.IsNullOrWhiteSpace(_options.Secret))
            throw new InvalidOperationException("StepUp secret not configured (Jwt:Secret / StepUp:Secret).");

        var now = DateTime.UtcNow;
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, trustSubjectId.ToString()),
            new Claim("amr", method),
            new Claim(JwtRegisteredClaimNames.Iat, new DateTimeOffset(now).ToUnixTimeSeconds().ToString())
        };
        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            notBefore: now,
            expires: now.Add(ttl ?? TimeSpan.FromMinutes(_options.TtlMinutes)),
            signingCredentials: new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.Secret)),
                SecurityAlgorithms.HmacSha256));

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public string? Validate(string? token, Guid expectedTrustSubjectId)
    {
        if (string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(_options.Secret))
            return null;

        try
        {
            var handler = new JwtSecurityTokenHandler();
            var principal = handler.ValidateToken(token, new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.Secret)),
                ValidateIssuer = true,
                ValidIssuer = _options.Issuer,
                ValidateAudience = true,
                ValidAudience = _options.Audience,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromSeconds(30)
            }, out _);

            var sub = principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
            if (sub is null || !Guid.TryParse(sub, out var subjectId) || subjectId != expectedTrustSubjectId)
                return null;

            return principal.FindFirst("amr")?.Value;
        }
        catch
        {
            return null;
        }
    }
}