using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace VanAn.Gateway.Services;

/// <summary>
/// Task AI-P4 / SRS v1.1 [D7] — signed tenant context for the vanan-ai service.
/// Per-host copy of <c>VanAn.TenantAi.Contracts.TenantContextSigner</c> (no shared DLL —
/// same convention as the per-host MeteringApiClient copies, task_p4 D2).
/// Wire format: X-Tenant-Ctx = base64url(json payload); X-Tenant-Ctx-Sig =
/// base64url(HMAC-SHA256(ctxBytes, TenantAi:CtxSigningSecret)).
/// </summary>
public sealed record TenantAiContextPayload(
    Guid TenantId,
    string CallerId,
    long Iat,
    long Exp,
    string RequestId);

public static class TenantAiContextSigner
{
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    public static (string Ctx, string Sig) Sign(TenantAiContextPayload payload, string secret)
    {
        var ctx = Base64UrlEncode(JsonSerializer.SerializeToUtf8Bytes(payload, JsonOpts));
        return (ctx, Base64UrlEncode(HmacSha256(secret, ctx)));
    }

    /// <summary>Build a payload expiring <paramref name="ttlSeconds"/> from now (SRS: +2ph).</summary>
    public static TenantAiContextPayload CreatePayload(
        Guid tenantId, string callerId, string requestId, int ttlSeconds = 120, DateTime? nowUtc = null)
    {
        var now = new DateTimeOffset(nowUtc ?? DateTime.UtcNow).ToUnixTimeSeconds();
        return new TenantAiContextPayload(tenantId, callerId, now, now + ttlSeconds, requestId);
    }

    private static byte[] HmacSha256(string secret, string ctx)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        return hmac.ComputeHash(Encoding.UTF8.GetBytes(ctx));
    }

    private static string Base64UrlEncode(byte[] bytes)
        => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
