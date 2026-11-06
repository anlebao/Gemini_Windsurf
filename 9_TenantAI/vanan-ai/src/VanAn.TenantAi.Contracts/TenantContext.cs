using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace VanAn.TenantAi.Contracts;

/// <summary>
/// SRS v1.1 [D7] — signed tenant context. Gateway (or trusted caller) signs this
/// envelope so a leaked API key alone cannot forge tenant claims.
/// Wire format: X-Tenant-Ctx = base64url(json payload); X-Tenant-Ctx-Sig =
/// base64url(HMAC-SHA256(ctxBytes, secret)).
/// </summary>
public sealed record TenantContextPayload(
    Guid TenantId,
    string CallerId,
    long Iat,
    long Exp,
    string RequestId);

public static class TenantContextSigner
{
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    public static (string Ctx, string Sig) Sign(TenantContextPayload payload, string secret)
    {
        var ctx = Base64UrlEncode(JsonSerializer.SerializeToUtf8Bytes(payload, JsonOpts));
        return (ctx, Base64UrlEncode(HmacSha256(secret, ctx)));
    }

    /// <summary>Constant-time signature check over the raw ctx header value.</summary>
    public static bool Verify(string ctx, string sig, string secret)
    {
        var expected = HmacSha256(secret, ctx);
        byte[] actual;
        try { actual = Base64UrlDecode(sig); }
        catch (FormatException) { return false; }
        return actual.Length == expected.Length
            && CryptographicOperations.FixedTimeEquals(actual, expected);
    }

    public static TenantContextPayload? Parse(string ctx)
    {
        try
        {
            return JsonSerializer.Deserialize<TenantContextPayload>(Base64UrlDecode(ctx), JsonOpts);
        }
        catch (Exception e) when (e is JsonException or FormatException or ArgumentException)
        {
            return null;
        }
    }

    /// <summary>Build a payload expiring <paramref name="ttlSeconds"/> from <paramref name="nowUtc"/>.</summary>
    public static TenantContextPayload CreatePayload(
        Guid tenantId, string callerId, string requestId, int ttlSeconds = 120, DateTime? nowUtc = null)
    {
        var now = new DateTimeOffset(nowUtc ?? DateTime.UtcNow).ToUnixTimeSeconds();
        return new TenantContextPayload(tenantId, callerId, now, now + ttlSeconds, requestId);
    }

    private static byte[] HmacSha256(string secret, string ctx)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        return hmac.ComputeHash(Encoding.UTF8.GetBytes(ctx));
    }

    private static string Base64UrlEncode(byte[] bytes)
        => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static byte[] Base64UrlDecode(string s)
    {
        var padded = s.Replace('-', '+').Replace('_', '/');
        padded = padded.PadRight(padded.Length + (4 - padded.Length % 4) % 4, '=');
        return Convert.FromBase64String(padded);
    }
}
