using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace VanAn.KhachLink.Services.Http;

/// <summary>
/// CC-S5 (Sprint 5): HTTP client for Wallet + COD endpoints.
/// KhachLink calls Gateway → CommunityController wallet endpoints.
/// All methods require X-Customer-Token header (authenticated shipper/customer/shop owner).
/// </summary>
public class WalletHttpService(IHttpClientFactory httpClientFactory, ILogger<WalletHttpService> logger)
{
    private readonly HttpClient _httpClient = httpClientFactory.CreateClient("gateway");
    private readonly ILogger<WalletHttpService> _logger = logger;

    /// <summary>GET /api/community/wallet — wallet balance + transaction history.</summary>
    public async Task<WalletSummaryResult> GetWalletAsync(string customerToken)
    {
        try
        {
            var request = new HttpRequestMessage(HttpMethod.Get, "/api/community/wallet");
            request.Headers.Add("X-Customer-Token", customerToken);

            var resp = await _httpClient.SendAsync(request);
            var body = await resp.Content.ReadAsStringAsync();

            if (resp.IsSuccessStatusCode)
            {
                var data = System.Text.Json.JsonSerializer.Deserialize<WalletSummaryResponse>(body,
                    new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                return new WalletSummaryResult
                {
                    Success = true,
                    Balance = data?.Balance ?? 0m,
                    Transactions = data?.Transactions ?? new List<WalletTransactionResponse>()
                };
            }

            return new WalletSummaryResult { Success = false, ErrorMessage = body };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting wallet");
            return new WalletSummaryResult { Success = false, ErrorMessage = "Lỗi kết nối." };
        }
    }

    /// <summary>POST /api/community/wallet/confirm-cod — shipper confirms COD collection.</summary>
    public async Task<WalletTxResult> ConfirmCodAsync(string customerToken, Guid orderId, decimal amount)
    {
        try
        {
            var request = new HttpRequestMessage(HttpMethod.Post, "/api/community/wallet/confirm-cod");
            request.Headers.Add("X-Customer-Token", customerToken);
            request.Content = JsonContent.Create(new { OrderId = orderId, Amount = amount });

            var resp = await _httpClient.SendAsync(request);
            var body = await resp.Content.ReadAsStringAsync();

            if (resp.IsSuccessStatusCode)
            {
                var data = System.Text.Json.JsonSerializer.Deserialize<WalletTxResponse>(body,
                    new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                return new WalletTxResult { Success = true, TransactionId = data?.TransactionId ?? Guid.Empty, BalanceAfter = data?.BalanceAfter ?? 0m };
            }

            return new WalletTxResult { Success = false, ErrorMessage = body };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error confirming COD for order {OrderId}", orderId);
            return new WalletTxResult { Success = false, ErrorMessage = "Lỗi kết nối." };
        }
    }

    /// <summary>POST /api/community/wallet/confirm-advance — shipper confirms advance payment to shop.</summary>
    public async Task<WalletTxResult> ConfirmAdvanceAsync(string customerToken, Guid orderId, decimal amount)
    {
        try
        {
            var request = new HttpRequestMessage(HttpMethod.Post, "/api/community/wallet/confirm-advance");
            request.Headers.Add("X-Customer-Token", customerToken);
            request.Content = JsonContent.Create(new { OrderId = orderId, Amount = amount });

            var resp = await _httpClient.SendAsync(request);
            var body = await resp.Content.ReadAsStringAsync();

            if (resp.IsSuccessStatusCode)
            {
                var data = System.Text.Json.JsonSerializer.Deserialize<WalletTxResponse>(body,
                    new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                return new WalletTxResult { Success = true, TransactionId = data?.TransactionId ?? Guid.Empty, BalanceAfter = data?.BalanceAfter ?? 0m };
            }

            return new WalletTxResult { Success = false, ErrorMessage = body };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error confirming advance for order {OrderId}", orderId);
            return new WalletTxResult { Success = false, ErrorMessage = "Lỗi kết nối." };
        }
    }

    /// <summary>GET /api/community/wallet/pending-advances — shop owner lists pending advance confirmations.</summary>
    public async Task<PendingAdvancesResult> GetPendingAdvancesAsync(string customerToken)
    {
        try
        {
            var request = new HttpRequestMessage(HttpMethod.Get, "/api/community/wallet/pending-advances");
            request.Headers.Add("X-Customer-Token", customerToken);

            var resp = await _httpClient.SendAsync(request);
            var body = await resp.Content.ReadAsStringAsync();

            if (resp.IsSuccessStatusCode)
            {
                var data = System.Text.Json.JsonSerializer.Deserialize<List<PendingAdvanceResponse>>(body,
                    new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                return new PendingAdvancesResult { Success = true, Advances = data ?? new List<PendingAdvanceResponse>() };
            }

            return new PendingAdvancesResult { Success = false, ErrorMessage = body };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting pending advances");
            return new PendingAdvancesResult { Success = false, ErrorMessage = "Lỗi kết nối." };
        }
    }

    /// <summary>POST /api/community/wallet/confirm-advance-received — shop confirms advance receipt.</summary>
    public async Task<WalletTxResult> ConfirmAdvanceReceivedAsync(string customerToken, Guid advanceTransactionId)
    {
        try
        {
            var request = new HttpRequestMessage(HttpMethod.Post, "/api/community/wallet/confirm-advance-received");
            request.Headers.Add("X-Customer-Token", customerToken);
            request.Content = JsonContent.Create(new { AdvanceTransactionId = advanceTransactionId });

            var resp = await _httpClient.SendAsync(request);
            var body = await resp.Content.ReadAsStringAsync();

            if (resp.IsSuccessStatusCode)
            {
                var data = System.Text.Json.JsonSerializer.Deserialize<WalletTxResponse>(body,
                    new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                return new WalletTxResult { Success = true, TransactionId = data?.TransactionId ?? Guid.Empty, BalanceAfter = data?.BalanceAfter ?? 0m };
            }

            return new WalletTxResult { Success = false, ErrorMessage = body };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error confirming advance received {TxId}", advanceTransactionId);
            return new WalletTxResult { Success = false, ErrorMessage = "Lỗi kết nối." };
        }
    }

    // === Response DTOs ===
    public class WalletSummaryResponse
    {
        public decimal Balance { get; set; }
        public List<WalletTransactionResponse> Transactions { get; set; } = new();
    }

    public class WalletTransactionResponse
    {
        public Guid Id { get; set; }
        public string Type { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public decimal BalanceAfter { get; set; }
        public string Description { get; set; } = string.Empty;
        public Guid? RelatedOrderId { get; set; }
        public Guid? RelatedTransactionId { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class WalletTxResponse
    {
        public Guid TransactionId { get; set; }
        public decimal BalanceAfter { get; set; }
    }

    public class PendingAdvanceResponse
    {
        public Guid TransactionId { get; set; }
        public Guid ShipperId { get; set; }
        public Guid OrderId { get; set; }
        public decimal Amount { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    // === Result types ===
    public class WalletSummaryResult
    {
        public bool Success { get; set; }
        public decimal Balance { get; set; }
        public List<WalletTransactionResponse> Transactions { get; set; } = new();
        public string? ErrorMessage { get; set; }
    }

    public class WalletTxResult
    {
        public bool Success { get; set; }
        public Guid TransactionId { get; set; }
        public decimal BalanceAfter { get; set; }
        public string? ErrorMessage { get; set; }
    }

    public class PendingAdvancesResult
    {
        public bool Success { get; set; }
        public List<PendingAdvanceResponse> Advances { get; set; } = new();
        public string? ErrorMessage { get; set; }
    }
}
