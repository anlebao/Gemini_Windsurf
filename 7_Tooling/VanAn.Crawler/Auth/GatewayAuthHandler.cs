using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using VanAn.Crawler.Options;

namespace VanAn.Crawler.Auth;

/// <summary>
/// DelegatingHandler that authenticates with Gateway via POST /api/platform/login,
/// caches the JWT Bearer token, and attaches it to every outgoing request.
/// Re-authenticates on 401 (token expired).
/// </summary>
public sealed class GatewayAuthHandler : DelegatingHandler
{
    private readonly CrawlerOptions _options;
    private readonly ILogger<GatewayAuthHandler> _logger;
    private string? _cachedToken;
    private DateTime _tokenExpiry;

    public GatewayAuthHandler(CrawlerOptions options, ILogger<GatewayAuthHandler> logger)
    {
        _options = options;
        _logger = logger;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken ct)
    {
        // Attach token if we have one
        if (_cachedToken is null || DateTime.UtcNow >= _tokenExpiry)
            await LoginAsync(ct);

        if (_cachedToken is not null)
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _cachedToken);

        var response = await base.SendAsync(request, ct);

        // Re-login on 401 and retry once
        if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized && _cachedToken is not null)
        {
            _logger.LogWarning("Gateway returned 401 — re-authenticating and retrying");
            _cachedToken = null;
            await LoginAsync(ct);
            if (_cachedToken is not null)
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _cachedToken);
                response.Dispose();
                response = await base.SendAsync(request, ct);
            }
        }

        return response;
    }

    private async Task LoginAsync(CancellationToken ct)
    {
        try
        {
            using var loginReq = new HttpRequestMessage(HttpMethod.Post,
                $"{(_options.AuthBaseUrl ?? _options.GatewayBaseUrl).TrimEnd('/')}/api/platform/login")
            {
                Content = JsonContent.Create(new
                {
                    username = _options.GatewayUsername,
                    password = _options.GatewayPassword
                })
            };
            using var loginResp = await base.SendAsync(loginReq, ct);
            if (!loginResp.IsSuccessStatusCode)
            {
                _logger.LogError("Gateway login failed: {Status} {Reason}",
                    loginResp.StatusCode, loginResp.ReasonPhrase);
                return;
            }
            var json = await loginResp.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(json);
            _cachedToken = doc.RootElement.TryGetProperty("token", out var tokenEl)
                ? tokenEl.GetString()
                : doc.RootElement.TryGetProperty("accessToken", out var accessEl)
                    ? accessEl.GetString()
                    : null;
            _tokenExpiry = DateTime.UtcNow.AddHours(7); // JWT expires in 8h, refresh at 7h
            _logger.LogInformation("Gateway login successful, token cached until {Expiry:O}", _tokenExpiry);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Gateway login exception");
        }
    }
}
