using System.Security.Cryptography;
using System.Text;

namespace VanAn.Voice.Api.Services;

public sealed record TurnCredentials(
    IReadOnlyList<string> Urls,
    string Username,
    string Credential,
    int TtlSeconds,
    DateTime ExpiresAtUtc);

/// <summary>
/// Short-lived TURN credentials for the WebRTC client (SRS v1.1 §8.2, SEC-004).
/// Issued via the Coturn TURN REST API scheme: username = <c>"{unixTimestamp}:{callerId}"</c>,
/// credential = base64(HMAC-SHA1(TURN secret, username)). Coturn (use-auth-secret +
/// static-auth-secret) recomputes the HMAC and enforces the timestamp lifetime — no password
/// storage, no secret in the frontend.
///
/// Config (env: <c>TURN__SECRET</c>, <c>TURN__PUBLIC_HOST</c>, ...):
///   TURN:PublicHost / UdpPort / TcpPort / TlsPort / Secret / Realm / CredentialTtlSeconds
/// When TURN is not configured the issuer returns null and the SDK falls back to P2P/STUN only.
/// </summary>
public sealed class TurnCredentialService(IConfiguration configuration, ILogger<TurnCredentialService> logger)
{
    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(configuration["TURN:PublicHost"])
        && !string.IsNullOrWhiteSpace(configuration["TURN:Secret"]);

    public int TtlSeconds => configuration.GetValue("TURN:CredentialTtlSeconds", 1800);

    public TurnCredentials? Issue(Guid callerId, DateTime nowUtc)
    {
        if (!IsConfigured)
        {
            logger.LogWarning("TURN not configured — client falls back to P2P/STUN only.");
            return null;
        }

        var host = configuration["TURN:PublicHost"]!;
        var udpPort = configuration.GetValue("TURN:UdpPort", 3478);
        var tcpPort = configuration.GetValue("TURN:TcpPort", 3478);
        var tlsPort = configuration.GetValue("TURN:TlsPort", 5349);
        var secret = configuration["TURN:Secret"]!;

        var unix = new DateTimeOffset(nowUtc).ToUnixTimeSeconds();
        var username = $"{unix}:{callerId:N}";
        var credential = Convert.ToBase64String(HMACSHA1(secret, username));

        var urls = new List<string>
        {
            $"turn:{host}:{udpPort}?transport=udp",
            $"turn:{host}:{tcpPort}?transport=tcp"
        };
        if (tlsPort > 0)
            urls.Add($"turns:{host}:{tlsPort}?transport=tcp");
        if (!string.IsNullOrWhiteSpace(configuration["TURN:Realm"]))
            logger.LogDebug("TURN realm configured: {Realm}", configuration["TURN:Realm"]);

        return new TurnCredentials(urls, username, credential, TtlSeconds, nowUtc.AddSeconds(TtlSeconds));
    }

    private static byte[] HMACSHA1(string key, string message)
    {
        using var hmac = new HMACSHA1(Encoding.UTF8.GetBytes(key));
        return hmac.ComputeHash(Encoding.UTF8.GetBytes(message));
    }
}