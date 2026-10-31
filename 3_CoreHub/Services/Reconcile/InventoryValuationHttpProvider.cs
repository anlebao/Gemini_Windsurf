using System.Net.Http.Json;
using Microsoft.Extensions.Logging;

namespace VanAn.CoreHub.Services.Reconcile;

/// <summary>
/// SRS v1.2 W6/E: IInventoryValuationProvider impl gọi ShopERP qua HTTP
/// (GET /api/shoperp/inventory/valuation?tenantId=&period=). Đăng ký typed HttpClient
/// ở Gateway Program.cs — base URL ShopERP:BaseUrl + X-Internal-Api-Key (shared secret
/// InternalLoyalty:ApiKey) + timeout ngắn (<3s) — pattern MirrorOrderHttpProvider.
/// Fail-open bắt buộc (NFR-4): mọi lỗi transport/timeout/HTTP non-2xx/JSON → trả null
/// → R13 skip với Info, KHÔNG chặn các check khác (kho là replica, không phải SoT).
/// </summary>
public sealed class InventoryValuationHttpProvider(
    HttpClient httpClient,
    ILogger<InventoryValuationHttpProvider> logger) : IInventoryValuationProvider
{
    public async Task<InventoryValuationDto?> GetValuationAsync(Guid tenantId, string period, CancellationToken ct = default)
    {
        try
        {
            InventoryValuationDto? dto = await httpClient.GetFromJsonAsync<InventoryValuationDto>(
                $"api/shoperp/inventory/valuation?tenantId={tenantId:D}&period={Uri.EscapeDataString(period)}", ct);
            return dto;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "R13 inventory provider: đọc ShopERP valuation thất bại (tenant={TenantId}, period={Period}) — skip",
                tenantId, period);
            return null;
        }
    }
}
