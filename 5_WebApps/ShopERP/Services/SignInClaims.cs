using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace VanAn.ShopERP.Services
{
    /// <summary>
    /// W4 (P1.1, 2026-10-18): gắn claims "client_ip"/"client_ua" vào auth cookie lúc sign-in.
    /// AuditTrailService đọc lại các claim này trong Blazor Server circuit (HttpContext = null)
    /// để ghi IP Address + User-Agent vào AuditLog (Điều 28 TT 99/2025 — vết kiểm toán).
    /// </summary>
    public static class SignInClaims
    {
        public const string ClientIpClaim = "client_ip";
        public const string ClientUserAgentClaim = "client_ua";

        /// <summary>Append client_ip + client_ua claims vào danh sách claims trước SignInAsync.
        /// Dedupe: gỡ claim cũ cùng type trước (impersonate/exit copy claims từ principal cũ).</summary>
        public static void AppendClientInfo(List<Claim> claims, HttpContext httpContext)
        {
            claims.RemoveAll(c => c.Type is ClientIpClaim or ClientUserAgentClaim);

            string? ip = ExtractClientIp(httpContext);
            if (!string.IsNullOrWhiteSpace(ip))
            {
                claims.Add(new Claim(ClientIpClaim, ip));
            }

            string? userAgent = httpContext.Request.Headers.UserAgent;
            if (!string.IsNullOrWhiteSpace(userAgent))
            {
                claims.Add(new Claim(ClientUserAgentClaim, userAgent));
            }
        }

        /// <summary>IP client thật: X-Forwarded-For (hop đầu — nginx/Gateway forward) → RemoteIpAddress.</summary>
        private static string? ExtractClientIp(HttpContext httpContext)
        {
            string? forwarded = httpContext.Request.Headers["X-Forwarded-For"].FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(forwarded))
            {
                return forwarded.Split(',')[0].Trim();
            }
            return httpContext.Connection.RemoteIpAddress?.ToString();
        }
    }
}
