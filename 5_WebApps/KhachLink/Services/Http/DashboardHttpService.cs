using VanAn.CoreHub.Services;

namespace VanAn.KhachLink.Services.Http
{
    /// <summary>
    /// Thin HTTP client wrapper for CoreHub dashboard operations.
    /// KhachLink uses this adapter instead of referencing CoreHub services directly.
    /// </summary>
    public class DashboardHttpService : IDashboardService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<DashboardHttpService> _logger;

        public DashboardHttpService(IHttpClientFactory httpClientFactory, ILogger<DashboardHttpService> logger)
        {
            _httpClient = httpClientFactory.CreateClient("gateway");
            _logger = logger;
        }

        public async Task<DashboardMetrics> GetPostgreSQLMetricsAsync()
        {
            var response = await _httpClient.GetAsync("api/dashboard/postgresql-metrics");
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<DashboardMetrics>()
                ?? throw new InvalidOperationException("GetPostgreSQLMetrics returned empty response");
        }

        public async Task<SQLiteMetrics> GetSQLiteMetricsAsync(string nodeType)
        {
            var response = await _httpClient.GetAsync($"api/dashboard/sqlite-metrics/{Uri.EscapeDataString(nodeType)}");
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<SQLiteMetrics>()
                ?? throw new InvalidOperationException("GetSQLiteMetrics returned empty response");
        }

        public async Task<SyncStatus> GetSyncStatusAsync()
        {
            var response = await _httpClient.GetAsync("api/dashboard/sync-status");
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<SyncStatus>()
                ?? throw new InvalidOperationException("GetSyncStatus returned empty response");
        }

        public async Task<SystemHealth> GetSystemHealthAsync()
        {
            var response = await _httpClient.GetAsync("api/dashboard/system-health");
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<SystemHealth>()
                ?? throw new InvalidOperationException("GetSystemHealth returned empty response");
        }
    }
}
