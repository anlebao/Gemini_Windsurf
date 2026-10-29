using System.Net.Http.Json;
using Microsoft.Extensions.Logging;

namespace VanAn.CoreHub.Services.Reconcile;

/// <summary>
/// Tra soát số liệu kế toán P2 (G8): IMirrorOrderProvider impl gọi ShopERP qua HTTP
/// (GET /api/shoperp/accounting/mirror-orders?period=). Đăng ký typed HttpClient ở
/// Gateway Program.cs — base URL ShopERP:BaseUrl + X-Internal-Api-Key (shared secret
/// InternalLoyalty:ApiKey) + timeout ngắn (&lt;3s).
/// Fail-open bắt buộc (NFR-4): mọi lỗi transport/timeout/HTTP non-2xx/JSON → trả null
/// → R11 skip với Info, KHÔNG chặn các check khác (mirror là replica, không phải SoT).
/// </summary>
public sealed class MirrorOrderHttpProvider(
    HttpClient httpClient,
    ILogger<MirrorOrderHttpProvider> logger) : IMirrorOrderProvider
{
    public async Task<IReadOnlyList<MirrorOrderDto>?> GetMirrorOrdersAsync(string period, CancellationToken ct = default)
    {
        try
        {
            List<MirrorOrderDto>? list = await httpClient.GetFromJsonAsync<List<MirrorOrderDto>>(
                $"api/shoperp/accounting/mirror-orders?period={Uri.EscapeDataString(period)}", ct);
            return list;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "R11 mirror provider: đọc ShopERP mirror thất bại (period={Period}) — skip", period);
            return null;
        }
    }
}
