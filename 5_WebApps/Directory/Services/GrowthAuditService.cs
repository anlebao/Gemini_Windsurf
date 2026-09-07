using System.Text.Json;

namespace VanAn.Directory.Services;

/// <summary>
/// GTM Drill Machine W1 (2026-09-06): calls Gateway GET /api/v1/growth/audit —
/// public Merchant Audit backing /kiem-tra-cua-hang.
///
/// Server-side call: forwards the END-USER IP via X-Forwarded-For so the Gateway
/// growth-audit rate limiter partitions by the user's IP (Gateway UseForwardedHeaders
/// rewrites RemoteIpAddress), not this container's shared IP.
/// </summary>
public class GrowthAuditService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<GrowthAuditService> _logger;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public GrowthAuditService(HttpClient httpClient, ILogger<GrowthAuditService> logger,
        JsonSerializerOptions jsonOptions, IHttpContextAccessor httpContextAccessor)
    {
        _httpClient = httpClient;
        _logger = logger;
        _jsonOptions = jsonOptions;
        _httpContextAccessor = httpContextAccessor;
    }

    /// <summary>
    /// Audit by merchant name OR MST (all-digit input 8-15 chars is treated as MST).
    /// Never throws — returns GrowthAuditResult with the failure flag set.
    /// </summary>
    public async Task<GrowthAuditResult> AuditAsync(string query)
    {
        var isMst = System.Text.RegularExpressions.Regex.IsMatch(query, @"^\d{8,15}$");
        var param = isMst
            ? $"mst={Uri.EscapeDataString(query)}"
            : $"name={Uri.EscapeDataString(query)}";

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, $"api/v1/growth/audit?{param}");

            // Forward end-user IP (see class doc) — pass through nginx XFF chain if present
            var ctx = _httpContextAccessor.HttpContext;
            if (ctx is not null)
            {
                var xff = ctx.Request.Headers["X-Forwarded-For"].FirstOrDefault();
                var clientIp = !string.IsNullOrWhiteSpace(xff)
                    ? xff
                    : ctx.Connection.RemoteIpAddress?.ToString();
                if (!string.IsNullOrWhiteSpace(clientIp))
                    request.Headers.Add("X-Forwarded-For", clientIp);
            }

            var resp = await _httpClient.SendAsync(request);

            if (resp.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
            {
                _logger.LogWarning("Growth audit rate limited (query={Query})", query);
                return new GrowthAuditResult(RateLimited: true);
            }

            if (!resp.IsSuccessStatusCode)
            {
                _logger.LogWarning("GrowthAuditAsync: {Status} (query={Query})", resp.StatusCode, query);
                return new GrowthAuditResult(Failed: true);
            }

            var audit = await resp.Content.ReadFromJsonAsync<MerchantAuditDto>(_jsonOptions);
            return new GrowthAuditResult(Audit: audit);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GrowthAuditAsync: error (query={Query})", query);
            return new GrowthAuditResult(Failed: true);
        }
    }
}

/// <summary>Result wrapper: Audit payload on success; RateLimited/Failed flags for UI messaging.</summary>
public record GrowthAuditResult(MerchantAuditDto? Audit = null, bool RateLimited = false, bool Failed = false);

/// <summary>Mirror of Gateway MerchantAuditDto (2_Gateway/Controllers/GrowthController.cs).</summary>
public class MerchantAuditDto
{
    public bool Found { get; init; }
    public MerchantAuditTenantDto? Tenant { get; init; }
    /// <summary>Count of OTHER Active+Pending tenants with matching industry (measured from crawl data). Null when unknown.</summary>
    public int? IndustryPeerCount { get; init; }
    public string? ClaimUrl { get; init; }
    /// <summary>Self-serve signup URL — reserved for W2 Interactive Demo. Null in W1.</summary>
    public string? RegisterUrl { get; init; }
}

/// <summary>Mirror of Gateway MerchantAuditTenantDto.</summary>
public class MerchantAuditTenantDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public bool IsPending { get; init; }
    public string? Slug { get; init; }
    public string? Industry { get; init; }
    public bool HasStorefront { get; init; }
    public bool HasKhachLinkDomain { get; init; }
    public string? KhachLinkDomain { get; init; }
    public bool HasSocialLinks { get; init; }
    public string? SocialLinksFb { get; init; }
    public string? SocialLinksTiktok { get; init; }
}
