namespace VanAn.Gateway.Realtime;

/// <summary>
/// Realtime Platform P3 (2026-09-17): resolves the caller identity for a hub connection or a
/// realtime HTTP endpoint by running every registered <see cref="IRealtimeTokenValidator"/> in
/// registration order and returning the first identity that resolves.
///
/// Registration order (see Program.cs) is Customer → Device → Staff, so a signed-in customer who
/// also carries a device guid is treated as the customer — the higher-trust identity wins.
/// </summary>
public class RealtimeIdentityResolver(
    IEnumerable<IRealtimeTokenValidator> validators,
    ILogger<RealtimeIdentityResolver> logger)
{
    private readonly IReadOnlyList<IRealtimeTokenValidator> _validators = validators.ToList();
    private readonly ILogger<RealtimeIdentityResolver> _logger = logger;

    public async Task<RealtimeIdentity?> ResolveAsync(HttpContext httpContext, CancellationToken ct = default)
    {
        foreach (var validator in _validators)
        {
            var identity = await validator.ValidateAsync(httpContext, ct);
            if (identity != null)
                return identity;
        }

        _logger.LogDebug("RealtimeIdentityResolver: no validator accepted the request ({Count} tried)", _validators.Count);
        return null;
    }
}
