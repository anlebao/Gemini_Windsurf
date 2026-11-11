using VanAn.Voice.Domain;

namespace VanAn.Voice.Api.Auth;

/// <summary>
/// Resolves the authenticated identity of a VoiceHub connection by running the validator
/// chain (staff JWT, then customer token) — mirrors Gateway RealtimeIdentityResolver.
/// A connection with no valid credential is rejected at OnConnectedAsync.
/// </summary>
public sealed class VoiceIdentityResolver(IEnumerable<IVoiceTokenValidator> validators)
{
    private readonly IVoiceTokenValidator[] _validators = validators.ToArray();

    public async Task<VoiceIdentity?> ResolveAsync(HttpContext httpContext, CancellationToken ct = default)
    {
        foreach (var validator in _validators)
        {
            var identity = await validator.ValidateAsync(httpContext, ct);
            if (identity != null) return identity;
        }
        return null;
    }
}