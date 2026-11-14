using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.JSInterop;
using VanAn.KhachLink.Models;

namespace VanAn.KhachLink.Services.Http
{
    /// <summary>
    /// W2-1/W2-2 (2026-11-14): Client cho public cooperative directory endpoints
    /// (GET /api/cooperatives/by-domain/{domain}, GET /api/cooperatives/{id}/members).
    /// Anonymous, read-only — portal landing data. Graceful: mọi lỗi → list rỗng.
    /// </summary>
    public class CooperativeHttpService(
        IHttpClientFactory httpClientFactory,
        IJSRuntime jsRuntime,
        ILogger<CooperativeHttpService> logger)
    {
        private readonly HttpClient _httpClient = httpClientFactory.CreateClient("gateway");
        private readonly IJSRuntime _jsRuntime = jsRuntime;
        private readonly ILogger<CooperativeHttpService> _logger = logger;

        private static readonly JsonSerializerOptions _jsonOpts = new(JsonSerializerDefaults.Web)
        {
            Converters = { new JsonStringEnumConverter() }
        };

        /// <summary>Resolve cooperatives theo domain hiện tại (portal → all HTX; HTX subdomain → 1).</summary>
        public async Task<List<CooperativeDto>> GetByCurrentDomainAsync()
        {
            try
            {
                var hostname = await _jsRuntime.InvokeAsync<string>("eval", "window.location.hostname");
                if (string.IsNullOrWhiteSpace(hostname))
                    return new List<CooperativeDto>();

                var result = await _httpClient.GetFromJsonAsync<List<CooperativeDto>>(
                    $"api/cooperatives/by-domain/{Uri.EscapeDataString(hostname)}", _jsonOpts);
                return result ?? new List<CooperativeDto>();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "CooperativeHttpService: by-domain fetch failed");
                return new List<CooperativeDto>();
            }
        }

        /// <summary>Member shops (tenant-member Active) của 1 HTX.</summary>
        public async Task<List<CooperativeMemberDto>> GetMembersAsync(Guid htxTenantId)
        {
            try
            {
                var result = await _httpClient.GetFromJsonAsync<List<CooperativeMemberDto>>(
                    $"api/cooperatives/{htxTenantId}/members", _jsonOpts);
                return result ?? new List<CooperativeMemberDto>();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "CooperativeHttpService: members fetch failed for {HtxId}", htxTenantId);
                return new List<CooperativeMemberDto>();
            }
        }
    }
}
