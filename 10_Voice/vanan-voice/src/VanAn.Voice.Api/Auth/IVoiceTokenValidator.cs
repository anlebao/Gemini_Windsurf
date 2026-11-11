using VanAn.Voice.Domain;

namespace VanAn.Voice.Api.Auth;

/// <summary>Token validator for VoiceHub connections (mirrors Gateway Realtime platform).</summary>
public interface IVoiceTokenValidator
{
    string Name { get; }
    Task<VoiceIdentity?> ValidateAsync(HttpContext httpContext, CancellationToken ct = default);
}

/// <summary>
/// Reads the credential from query string first (SignalR WebSocket handshakes cannot set
/// headers), then the header — same convention as Gateway RealtimeRequestReader.
/// </summary>
public static class VoiceRequestReader
{
    public static string? ReadCredential(HttpContext httpContext, string queryKey, string headerKey)
    {
        if (httpContext.Request.Query.TryGetValue(queryKey, out var q) && !string.IsNullOrWhiteSpace(q.ToString()))
            return q.ToString();
        if (httpContext.Request.Headers.TryGetValue(headerKey, out var h) && !string.IsNullOrWhiteSpace(h.ToString()))
            return h.ToString();
        return null;
    }
}