using System.Net.Http.Json;
using System.Text.Json;

namespace VanAn.KhachLink.Services.Http;

/// <summary>
/// Loyalty Alliance Phase 5B: HTTP client for the customer's cross-tenant loyalty wallet.
/// Calls Gateway endpoints (KhachLink is HTTP-only — no direct DB access):
///   GET /api/loyalty/wallet              — alliance wallet (balance + breakdown + recent transactions)
///   GET /api/tenants/{tenantId}/store-info — resolve tenant Guid → tenant name (for breakdown display)
/// All wallet methods require X-Customer-Token header (authenticated customer).
/// </summary>
public class AllianceWalletHttpService(IHttpClientFactory httpClientFactory, ILogger<AllianceWalletHttpService> logger)
{
    private readonly HttpClient _httpClient = httpClientFactory.CreateClient("gateway");
    private readonly ILogger<AllianceWalletHttpService> _logger = logger;

    private static readonly JsonSerializerOptions _jsonOpts = new() { PropertyNameCaseInsensitive = true };

    /// <summary>GET /api/loyalty/wallet — customer's cross-tenant alliance wallet.</summary>
    public async Task<AllianceWalletResult> GetWalletAsync(string customerToken)
    {
        try
        {
            var request = new HttpRequestMessage(HttpMethod.Get, "/api/loyalty/wallet");
            request.Headers.Add("X-Customer-Token", customerToken);

            var resp = await _httpClient.SendAsync(request);
            var body = await resp.Content.ReadAsStringAsync();

            if (resp.IsSuccessStatusCode)
            {
                var data = JsonSerializer.Deserialize<AllianceWalletResponse>(body, _jsonOpts);
                return new AllianceWalletResult
                {
                    Success = true,
                    Wallet = data ?? new AllianceWalletResponse()
                };
            }

            // 404 = customer has no device identity yet (not in alliance) — not an error, just empty wallet
            if (resp.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return new AllianceWalletResult
                {
                    Success = true,
                    Wallet = new AllianceWalletResponse { IsActive = false },
                    NotInAlliance = true
                };
            }

            return new AllianceWalletResult { Success = false, ErrorMessage = body };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting alliance wallet");
            return new AllianceWalletResult { Success = false, ErrorMessage = "Lỗi kết nối." };
        }
    }

    /// <summary>GET /api/tenants/{tenantId}/store-info — resolve tenant Guid to display name (anonymous).</summary>
    public async Task<string?> GetTenantNameAsync(Guid tenantId, CancellationToken ct = default)
    {
        if (tenantId == Guid.Empty) return null;
        try
        {
            var resp = await _httpClient.GetAsync($"api/tenants/{tenantId}/store-info", ct);
            if (!resp.IsSuccessStatusCode) return null;
            var dto = await resp.Content.ReadFromJsonAsync<TenantStoreNameDto>(_jsonOpts, ct);
            return dto?.Name;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error resolving tenant name for {TenantId}", tenantId);
            return null;
        }
    }

    // === Response DTOs ===

    public class AllianceWalletResponse
    {
        public Guid CustomerDeviceId { get; set; }
        public int TotalPointBalance { get; set; }
        public bool IsActive { get; set; }
        public List<WalletBreakdownItem> Breakdown { get; set; } = new();
        public List<AllianceTransactionItem> RecentTransactions { get; set; } = new();
    }

    public class WalletBreakdownItem
    {
        public Guid TenantId { get; set; }
        public int Points { get; set; }
    }

    public class AllianceTransactionItem
    {
        public Guid Id { get; set; }
        public Guid TenantId { get; set; }
        public string Type { get; set; } = string.Empty;   // EARN | REDEEM | ADJUST
        public int Points { get; set; }
        public int BalanceAfter { get; set; }
        public string Reason { get; set; } = string.Empty;
        public string? VoucherCode { get; set; }
        public DateTime TransactionAt { get; set; }
    }

    /// <summary>Minimal DTO for tenant store-info — only Name is needed for breakdown display.</summary>
    public class TenantStoreNameDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }
}

/// <summary>Result wrapper for GetWalletAsync.</summary>
public class AllianceWalletResult
{
    public bool Success { get; set; }
    public AllianceWalletHttpService.AllianceWalletResponse Wallet { get; set; } = new();
    public string? ErrorMessage { get; set; }
    /// <summary>true when the customer has no device identity yet (404 from wallet endpoint) — show "join alliance" prompt.</summary>
    public bool NotInAlliance { get; set; }
}
