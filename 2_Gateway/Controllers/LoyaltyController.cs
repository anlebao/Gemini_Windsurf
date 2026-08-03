using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using VanAn.CoreHub.Infrastructure;
using VanAn.Shared.Domain;
using VanAn.Shared.Services;

namespace VanAn.Gateway.Controllers
{
    /// <summary>
    /// W17-T2: Gateway forward controller for Loyalty Dashboard.
    /// Forwards X-Customer-Token from KhachLink to ShopERP's LoyaltyController.
    /// Tiered Auth Phase 2: adds POST /api/loyalty/redeem forwarding.
    /// Loyalty Alliance Phase 3B: adds GET /api/loyalty/wallet (PG AllianceWallet query).
    /// Adds GET /api/loyalty/mode — public endpoint for KhachLink to query global LoyaltyMode
    /// (UI hides "Ví liên minh" when mode=Silo to avoid customer confusion).
    /// </summary>
    [ApiController]
    [Route("api/loyalty")]
    [AllowAnonymous]
    public class LoyaltyController(
        IHttpClientFactory httpClientFactory,
        IAllianceWalletService allianceWalletService,
        IVanAnDbContext dbContext,
        ILogger<LoyaltyController> logger) : ControllerBase
    {
        private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;
        private readonly IAllianceWalletService _allianceWalletService = allianceWalletService;
        private readonly IVanAnDbContext _dbContext = dbContext;
        private readonly ILogger<LoyaltyController> _logger = logger;

        /// <summary>
        /// GET /api/loyalty/mode — returns the global LoyaltyMode (Silo | Alliance).
        /// Public (anonymous) — KhachLink calls this on startup to decide whether
        /// to show "Ví liên minh" menu/icon. When mode=Silo, alliance wallet UI is hidden.
        /// </summary>
        [HttpGet("mode")]
        public async Task<IActionResult> GetGlobalMode()
        {
            var config = await _dbContext.LoyaltyGlobalConfigs.FirstOrDefaultAsync();
            var mode = config?.Mode ?? LoyaltyMode.Silo;
            return Ok(new { mode = mode.ToString() });
        }

        [HttpGet("my")]
        public async Task<IActionResult> GetMyLoyalty()
        {
            try
            {
                var client = _httpClientFactory.CreateClient("shoperp");
                var reqMsg = new HttpRequestMessage(HttpMethod.Get, "/api/loyalty/my");
                if (Request.Headers.TryGetValue("X-Customer-Token", out var token))
                    reqMsg.Headers.Add("X-Customer-Token", token.ToString());

                var response = await client.SendAsync(reqMsg);
                var content = await response.Content.ReadAsStringAsync();
                var contentType = response.Content.Headers.ContentType?.MediaType ?? "application/json";
                return new ContentResult
                {
                    StatusCode = (int)response.StatusCode,
                    Content = content,
                    ContentType = contentType
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error forwarding GetMyLoyalty to ShopERP");
                return StatusCode(500, new { error = "Internal server error" });
            }
        }

        /// <summary>
        /// Tiered Auth Phase 2: Forward POST /api/loyalty/redeem to ShopERP.
        /// Passes X-Customer-Token header and request body through.
        /// </summary>
        [HttpPost("redeem")]
        public async Task<IActionResult> Redeem()
        {
            try
            {
                var client = _httpClientFactory.CreateClient("shoperp");
                var reqMsg = new HttpRequestMessage(HttpMethod.Post, "/api/loyalty/redeem");
                if (Request.Headers.TryGetValue("X-Customer-Token", out var token))
                    reqMsg.Headers.Add("X-Customer-Token", token.ToString());

                if (Request.ContentLength > 0)
                {
                    // FIX: Buffer the request body into a string before forwarding.
                    // StreamContent(Request.Body) fails in ASP.NET Core 8 because the request
                    // body stream (PipeReader-backed) may not support synchronous reads when
                    // HttpClient sends the content. Reading into StringContent is reliable.
                    Request.EnableBuffering();
                    using var reader = new System.IO.StreamReader(Request.Body, System.Text.Encoding.UTF8, leaveOpen: true);
                    var body = await reader.ReadToEndAsync();
                    reqMsg.Content = new StringContent(body, System.Text.Encoding.UTF8,
                        Request.ContentType ?? "application/json");
                }

                var response = await client.SendAsync(reqMsg);
                var content = await response.Content.ReadAsStringAsync();
                var contentType = response.Content.Headers.ContentType?.MediaType ?? "application/json";

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Forward POST /api/loyalty/redeem to ShopERP returned {StatusCode}: {Content}",
                        (int)response.StatusCode, content);
                }

                return new ContentResult
                {
                    StatusCode = (int)response.StatusCode,
                    Content = content,
                    ContentType = contentType
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error forwarding Redeem to ShopERP");
                return StatusCode(500, new { error = "Internal server error" });
            }
        }

        /// <summary>
        /// Loyalty Alliance Phase 3B: GET /api/loyalty/wallet — customer's cross-tenant wallet.
        /// Flow: resolve X-Customer-Token via ShopERP /api/loyalty/my-identity → get deviceId →
        /// query PG AllianceWallet + AllianceTransactions → return wallet DTO.
        /// Returns 401 if token invalid, 404 if wallet not found (customer not in alliance yet).
        /// </summary>
        [HttpGet("wallet")]
        public async Task<IActionResult> GetWallet()
        {
            try
            {
                // Step 1: Resolve customer token via ShopERP to get deviceId
                if (!Request.Headers.TryGetValue("X-Customer-Token", out var token) || string.IsNullOrEmpty(token))
                    return Unauthorized(new { error = "Thiếu X-Customer-Token header." });

                var client = _httpClientFactory.CreateClient("shoperp");
                var identityReq = new HttpRequestMessage(HttpMethod.Get, "/api/loyalty/my-identity");
                identityReq.Headers.Add("X-Customer-Token", token.ToString());
                var identityResp = await client.SendAsync(identityReq);

                if (!identityResp.IsSuccessStatusCode)
                    return new ContentResult
                    {
                        StatusCode = (int)identityResp.StatusCode,
                        Content = await identityResp.Content.ReadAsStringAsync(),
                        ContentType = "application/json"
                    };

                var identityJson = await identityResp.Content.ReadAsStringAsync();
                using var identityDoc = JsonDocument.Parse(identityJson);
                var deviceIdToken = identityDoc.RootElement.GetProperty("deviceId");

                // deviceId is Guid? — if null or zero, customer has no device identity → no alliance wallet
                if (deviceIdToken.ValueKind == JsonValueKind.Null || deviceIdToken.GetGuid() == Guid.Empty)
                    return NotFound(new { error = "Khách hàng chưa có device identity — chưa tham gia liên minh điểm thưởng." });

                Guid deviceId = deviceIdToken.GetGuid();

                // Step 2: Query PG AllianceWallet by deviceId
                var wallet = await _allianceWalletService.GetWalletByDeviceIdAsync(deviceId);
                if (wallet == null)
                    return Ok(new WalletResponse
                    {
                        TotalPointBalance = 0,
                        IsActive = false,
                        RecentTransactions = new List<WalletTransactionDto>()
                    });

                // Step 3: Query recent transactions
                var transactions = await _allianceWalletService.GetTransactionsAsync(wallet.Id, limit: 20);

                // Step 4: Build breakdown by tenant (sum points per tenant from transactions)
                var breakdown = transactions
                    .GroupBy(t => t.TransactionTenantId)
                    .Select(g => new WalletBreakdownDto
                    {
                        TenantId = g.Key,
                        Points = g.Sum(t => t.Points)
                    })
                    .ToList();

                return Ok(new WalletResponse
                {
                    CustomerDeviceId = deviceId,
                    TotalPointBalance = wallet.TotalPointBalance,
                    IsActive = wallet.IsActive,
                    Breakdown = breakdown,
                    RecentTransactions = transactions.Select(t => new WalletTransactionDto
                    {
                        Id = t.Id,
                        TenantId = t.TransactionTenantId,
                        Type = t.Type.ToString(),
                        Points = t.Points,
                        BalanceAfter = t.BalanceAfter,
                        Reason = t.Reason,
                        VoucherCode = t.VoucherCode,
                        TransactionAt = t.TransactionAt
                    }).ToList()
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting alliance wallet");
                return StatusCode(500, new { error = "Internal server error" });
            }
        }
    }

    // === Wallet DTOs ===

    public class WalletResponse
    {
        public Guid CustomerDeviceId { get; set; }
        public int TotalPointBalance { get; set; }
        public bool IsActive { get; set; }
        public List<WalletBreakdownDto> Breakdown { get; set; } = new();
        public List<WalletTransactionDto> RecentTransactions { get; set; } = new();
    }

    public class WalletBreakdownDto
    {
        public Guid TenantId { get; set; }
        public int Points { get; set; }
    }

    public class WalletTransactionDto
    {
        public Guid Id { get; set; }
        public Guid TenantId { get; set; }
        public string Type { get; set; } = string.Empty;
        public int Points { get; set; }
        public int BalanceAfter { get; set; }
        public string Reason { get; set; } = string.Empty;
        public string? VoucherCode { get; set; }
        public DateTime TransactionAt { get; set; }
    }
}
