using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VanAn.Gateway.Services;

namespace VanAn.Gateway.Controllers
{
    /// <summary>
    /// "Cửa hàng của tôi" (2026-11-28): GET /api/customers/my-stores — list tenant
    /// khách đã mua/tích điểm + số dư điểm per-tenant. Pure-read PG (MyStoresService).
    /// deviceId resolution: X-Customer-Token → ShopERP /api/loyalty/my-identity
    /// (token-validated, pattern LoyaltyController.GetWallet); fallback
    /// X-Customer-Device-Id header cho guest (pattern CustomerOrdersController).
    /// Không gì → 401.
    /// </summary>
    [ApiController]
    [Route("api/customers")]
    [AllowAnonymous]
    public class CustomerStoresController(
        IMyStoresService myStores,
        IHttpClientFactory httpClientFactory,
        ILogger<CustomerStoresController> logger) : ControllerBase
    {
        [HttpGet("my-stores")]
        public async Task<IActionResult> GetMyStores(CancellationToken ct)
        {
            try
            {
                Guid? deviceId;

                if (Request.Headers.TryGetValue("X-Customer-Token", out var token)
                    && !string.IsNullOrWhiteSpace(token))
                {
                    // Token path — validated qua ShopERP my-identity (pattern GetWallet).
                    // Token lỗi → propagate status, KHÔNG silent downgrade sang header.
                    var client = httpClientFactory.CreateClient("shoperp");
                    var identityReq = new HttpRequestMessage(HttpMethod.Get, "/api/loyalty/my-identity");
                    identityReq.Headers.Add("X-Customer-Token", token.ToString());
                    var identityResp = await client.SendAsync(identityReq, ct);

                    if (!identityResp.IsSuccessStatusCode)
                        return new ContentResult
                        {
                            StatusCode = (int)identityResp.StatusCode,
                            Content = await identityResp.Content.ReadAsStringAsync(ct),
                            ContentType = "application/json"
                        };

                    using var doc = JsonDocument.Parse(await identityResp.Content.ReadAsStringAsync(ct));
                    if (!doc.RootElement.TryGetProperty("deviceId", out var deviceEl)
                        || deviceEl.ValueKind != JsonValueKind.String
                        || !Guid.TryParse(deviceEl.GetString(), out var parsed)
                        || parsed == Guid.Empty)
                    {
                        return Unauthorized(new { error = "Không xác định được thiết bị khách hàng." });
                    }
                    deviceId = parsed;
                }
                else if (Request.Headers.TryGetValue("X-Customer-Device-Id", out var deviceHeader)
                    && Guid.TryParse(deviceHeader.ToString(), out var headerDeviceId)
                    && headerDeviceId != Guid.Empty)
                {
                    // Guest path — unvalidated device id (cùng exposure customerorders).
                    deviceId = headerDeviceId;
                }
                else
                {
                    return Unauthorized(new { error = "Thiếu X-Customer-Token hoặc X-Customer-Device-Id header." });
                }

                var stores = await myStores.GetMyStoresAsync(deviceId.Value, ct);
                return Ok(new { stores });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error resolving my-stores");
                return StatusCode(500, new { error = "Internal server error" });
            }
        }
    }
}
