using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace VanAn.Metering.Infra;

/// <summary>
/// P8 — Tingee signature scheme (developers.tingee.vn/docs/config-info + /docs/webhook):
///   request/webhook: x-signature = HEX(HMAC_SHA512(secret, timestamp + ":" + rawBody)) lowercase
///   url-validation:  encryptedToken = HEX(HMAC_SHA256(secret, plainToken))
/// timestamp format: yyyyMMddHHmmssfff at UTC+7.
/// </summary>
public static class TingeeSignature
{
    public static string TimestampUtc7(DateTime utcNow)
        => utcNow.AddHours(7).ToString("yyyyMMddHHmmssfff", CultureInfo.InvariantCulture);

    /// <summary>Sign an outbound API request body (exact string that will be sent).</summary>
    public static string SignRequest(string timestamp, string body, string secret)
        => HmacHex(Encoding.UTF8.GetBytes(secret), $"{timestamp}:{body}", sha256: false);

    /// <summary>Verify an inbound webhook x-signature against the RAW body string.</summary>
    public static bool VerifySignature(string timestamp, string rawBody, string? signature, string secret)
    {
        if (string.IsNullOrEmpty(signature) || string.IsNullOrEmpty(secret)) return false;
        var expected = SignRequest(timestamp, rawBody, secret);
        return CryptographicOperations.FixedTimeEquals(
            Encoding.ASCII.GetBytes(expected),
            Encoding.ASCII.GetBytes(signature.Trim().ToLowerInvariant()));
    }

    /// <summary>URL-validation challenge response: HMAC-SHA256(plainToken, verifySecret).</summary>
    public static string EncryptedToken(string plainToken, string secret)
        => HmacHex(Encoding.UTF8.GetBytes(secret), plainToken, sha256: true);

    private static string HmacHex(byte[] key, string message, bool sha256)
    {
        using var hmac = sha256 ? (HMAC)new HMACSHA256(key) : new HMACSHA512(key);
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(message));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
