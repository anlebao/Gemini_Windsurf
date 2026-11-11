using System.Net.Http.Json;
using VanAn.Voice.Domain;

namespace VanAn.Voice.Api.Auth;

/// <summary>
/// Logged-in customer identity: <c>X-Customer-Token</c> validated by forwarding to ShopERP
/// <c>/api/customer-identity/me</c> — the same validation the Gateway realtime layer uses
/// (SRS v1.1 O2). Customers carry no tenant claim; the call tenant comes from the verified
/// context (O6). Disabled (returns null) when <c>Voice:ShopErpBaseUrl</c> is not configured.
/// </summary>
public sealed class CustomerTokenValidator(
    IHttpClientFactory httpClientFactory,
    IConfiguration configuration,
    ILogger<CustomerTokenValidator> logger) : IVoiceTokenValidator
{
    public const string QueryKey = "customerToken";
    public const string HeaderKey = "X-Customer-Token";

    public string Name => "CustomerToken";

    public async Task<VoiceIdentity?> ValidateAsync(HttpContext httpContext, CancellationToken ct = default)
    {
        var baseUrl = configuration["Voice:ShopErpBaseUrl"];
        if (string.IsNullOrWhiteSpace(baseUrl)) return null;

        var token = VoiceRequestReader.ReadCredential(httpContext, QueryKey, HeaderKey);
        if (string.IsNullOrEmpty(token)) return null;

        try
        {
            var client = httpClientFactory.CreateClient("shoperp");
            using var req = new HttpRequestMessage(HttpMethod.Get, new Uri(new Uri(baseUrl), "/api/customer-identity/me"));
            req.Headers.Add(HeaderKey, token);
            using var resp = await client.SendAsync(req, ct);
            if (!resp.IsSuccessStatusCode) return null;

            var me = await resp.Content.ReadFromJsonAsync<MeResponse>(ct);
            if (me?.CustomerId == null || me.CustomerId == Guid.Empty) return null;
            return new VoiceIdentity(me.CustomerId.Value, VoiceIdentityKind.Customer);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "CustomerTokenValidator: token validation failed");
            return null;
        }
    }

    public sealed class MeResponse
    {
        public Guid? CustomerId { get; set; }
    }
}