using System.Security.Cryptography;
using System.Text;

namespace VanAn.Voice.Api.Services;

public sealed record TurnCredentials(
    IReadOnlyList<string> Urls,
    string Username,
    string Credential,
    int TtlSeconds,
    DateTime ExpiresAtUtc)
{
    /// <summary>
    /// STUN URLs (video MVP, 2026-11-22). Separate from <see cref="Urls"/> — a single
    /// RTCIceServer applies username/credential to every url, so STUN entries must be a
    /// separate RTCIceServer or the TURN credentials would leak into STUN requests.
    /// Additive JSON field; old SDK versions ignore it.
    /// </summary>
    public IReadOnlyList<string> StunUrls { get; init; } = Array.Empty<string>();
}

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

        // STUN urls (video MVP): Coturn serves STUN natively on the same UDP port —
        // default is stun:{host}:3478, overridable via TURN:StunUrls (csv) for
        // multi-node/geo or public STUN fallback.
        var stunConfigured = configuration["TURN:StunUrls"];
        var stunUrls = !string.IsNullOrWhiteSpace(stunConfigured)
            ? stunConfigured.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            : new[] { $"stun:{host}:{udpPort}" };

        return new TurnCredentials(urls, username, credential, TtlSeconds, nowUtc.AddSeconds(TtlSeconds))
        {
            StunUrls = stunUrls
        };
    }

    private static byte[] HMACSHA1(string key, string message)
    {
        using var hmac = new HMACSHA1(Encoding.UTF8.GetBytes(key));
        return hmac.ComputeHash(Encoding.UTF8.GetBytes(message));
    }
}