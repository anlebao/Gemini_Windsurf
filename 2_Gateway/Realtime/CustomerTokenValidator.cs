namespace VanAn.Gateway.Realtime;

/// <summary>
/// Realtime Platform P3 (2026-09-17): validates a logged-in customer's <c>X-Customer-Token</c>
/// by forwarding it to ShopERP <c>/api/customer-identity/me</c> — the same validation the legacy
/// <c>ChatHub</c>/<c>LocationHub</c> and <c>CommunityController</c> have always used, now in one place.
///
/// Token transport: query string <c>customerToken</c> first (SignalR browser handshakes cannot set
/// headers), then the <c>X-Customer-Token</c> header (HTTP endpoints).
/// </summary>
public class CustomerTokenValidator(
    IHttpClientFactory httpClientFactory,
    ILogger<CustomerTokenValidator> logger) : IRealtimeTokenValidator
{
    private const string QueryKey = "customerToken";
    private const string HeaderKey = "X-Customer-Token";

    private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;
    private readonly ILogger<CustomerTokenValidator> _logger = logger;

    public string Name => "CustomerToken";

    public async Task<RealtimeIdentity?> ValidateAsync(HttpContext httpContext, CancellationToken ct = default)
    {
        var token = RealtimeRequestReader.ReadCredential(httpContext, QueryKey, HeaderKey);
        if (string.IsNullOrEmpty(token))
            return null;

        try
        {
            var client = _httpClientFactory.CreateClient("shoperp");
            using var req = new HttpRequestMessage(HttpMethod.Get, "/api/customer-identity/me");
            req.Headers.Add(HeaderKey, token);

            var resp = await client.SendAsync(req, ct);
            if (!resp.IsSuccessStatusCode)
                return null;

            var me = await resp.Content.ReadFromJsonAsync<MeResponse>(ct);
            if (me?.CustomerId == null || me.CustomerId == Guid.Empty)
                return null;

            return new RealtimeIdentity(me.CustomerId.Value, RealtimeIdentityKind.Customer);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "CustomerTokenValidator: token validation failed");
            return null;
        }
    }

    private sealed class MeResponse
    {
        public Guid? CustomerId { get; set; }
    }
}
