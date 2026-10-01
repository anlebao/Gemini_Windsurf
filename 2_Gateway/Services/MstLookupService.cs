using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using VanAn.CoreHub.Infrastructure;
using VanAn.Shared.Domain.Aggregates.TenantAggregate;

namespace VanAn.Gateway.Services;

/// <summary>Thrown when the daily doanhnghiep.vn quota is exhausted → controller maps to 429.</summary>
public sealed class BusinessLookupRateLimitedException(string message) : Exception(message);

/// <summary>Thrown when doanhnghiep.vn is unreachable / API key missing / bad response → controller maps to 502.</summary>
public sealed class BusinessLookupUnavailableException(string message) : Exception(message);

/// <summary>
/// MST (tax code) → business info lookup. 2026-10-01: replaced the VietQR stub.
/// Local-first: search VanAn PG tenants (crawled/onboarded) before calling doanhnghiep.vn —
/// avoids burning the free 100 req/day quota for companies already in the ecosystem.
/// Remote lookups are cached 24h in-memory + rate-limited (BusinessLookupOptions.DailyLimit).
/// </summary>
public class MstLookupService(
    IHttpClientFactory httpClientFactory,
    IMemoryCache cache,
    IOptions<BusinessLookupOptions> options,
    IVanAnDbContext dbContext,
    ILogger<MstLookupService> logger) : IMstLookupService
{
    private static readonly object RateLock = new();
    private static readonly Dictionary<DateOnly, int> DailyCounts = new();

    public async Task<BusinessLookupResult?> LookupByTaxCodeAsync(string taxCode, CancellationToken ct = default)
    {
        var mst = taxCode.Trim();
        if (string.IsNullOrEmpty(mst)) return null;

        // 1. Local-first: PG tenants (crawl/onboard pipeline già lưu Name/Address/TaxCode).
        var local = await FindLocalAsync(mst, ct);
        if (local is not null)
        {
            logger.LogInformation("MstLookup: taxCode={Mst} found locally (tax code {LocalTaxCode})", mst, local.TaxCode);
            return local;
        }

        // 2. doanhnghiep.vn fallback — cache + daily quota.
        var cacheKey = $"mst-lookup:{mst}";
        if (cache.TryGetValue(cacheKey, out BusinessLookupResult? cached) && cached is not null)
        {
            logger.LogDebug("MstLookup: taxCode={Mst} cache hit", mst);
            return cached;
        }

        if (!TryAcquireDailyQuota())
        {
            logger.LogWarning("MstLookup: daily quota exceeded ({Limit}/day) — taxCode={Mst}", options.Value.DailyLimit, mst);
            throw new BusinessLookupRateLimitedException(
                $"Đã đạt giới hạn tra cứu doanhnghiep.vn hôm nay ({options.Value.DailyLimit} lượt/ngày). Vui lòng thử lại vào ngày mai.");
        }

        var result = await LookupRemoteAsync(mst, ct);
        if (result is not null)
        {
            cache.Set(cacheKey, result, TimeSpan.FromMinutes(Math.Max(1, options.Value.CacheMinutes)));
            logger.LogInformation("MstLookup: taxCode={Mst} → {Name} (doanhnghiep.vn)", mst, result.BusinessName);
        }
        else
        {
            logger.LogInformation("MstLookup: taxCode={Mst} not found on doanhnghiep.vn", mst);
        }
        return result;
    }

    private async Task<BusinessLookupResult?> FindLocalAsync(string mst, CancellationToken ct)
    {
        var normalized = NormalizeMst(mst);
        if (string.IsNullOrEmpty(normalized)) return null;

        // IgnoreQueryFilters: Tenants is subject to the global tenant query filter
        // (IMustHaveTenant — VanAnDbContext.OnModelCreating), but business info lookup
        // is GLOBAL data (directory profiles, public per Luật Doanh nghiệp 2020), NOT
        // tenant-scoped. Without this, the ambient tenant of the calling JWT filters out
        // every tenant row → local-first always misses → burns doanhnghiep.vn quota.
        var tenant = await dbContext.Tenants
            .AsNoTracking()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(t =>
                t.Settings.TaxCode != null
                && (t.Settings.TaxCode == mst || t.Settings.TaxCode.Replace("-", "") == normalized), ct);
        if (tenant is null) return null;

        return new BusinessLookupResult(
            tenant.Settings.TaxCode ?? mst,
            tenant.Name,
            tenant.Settings.Address,
            MapStatus(tenant.Status),
            Source: "vanan");
    }

    private async Task<BusinessLookupResult?> LookupRemoteAsync(string mst, CancellationToken ct)
    {
        var opt = options.Value;
        if (string.IsNullOrWhiteSpace(opt.ApiKey))
        {
            throw new BusinessLookupUnavailableException(
                "Thiếu API key doanhnghiep.vn (BusinessLookup:ApiKey). Liên hệ admin để cấu hình.");
        }

        var client = httpClientFactory.CreateClient("doanhnghiep");
        var url = $"/api/v1/companies/{Uri.EscapeDataString(mst)}";

        HttpResponseMessage resp;
        try
        {
            resp = await client.GetAsync(url, ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            logger.LogWarning(ex, "MstLookup: doanhnghiep.vn request failed for {Mst}", mst);
            throw new BusinessLookupUnavailableException("Không kết nối được doanhnghiep.vn. Vui lòng thử lại sau.");
        }

        using (resp)
        {
            if (resp.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                logger.LogWarning("MstLookup: doanhnghiep.vn rejected API key (401)");
                throw new BusinessLookupUnavailableException("API key doanhnghiep.vn bị từ chối (401). Liên hệ admin kiểm tra BusinessLookup:ApiKey.");
            }
            if (resp.StatusCode == System.Net.HttpStatusCode.NotFound) return null;
            if (!resp.IsSuccessStatusCode)
            {
                logger.LogWarning("MstLookup: doanhnghiep.vn returned {Status} for {Mst}", resp.StatusCode, mst);
                throw new BusinessLookupUnavailableException($"doanhnghiep.vn trả về lỗi {(int)resp.StatusCode}.");
            }

            var json = await resp.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(json);
            var d = doc.RootElement;

            // ValueKind guard: doanhnghiep.vn returns "industry":null / "province":null for
            // unknown MSTs (200 with sparse data) — TryGetProperty on a Null element then
            // TryGetProperty("name_vi") throws InvalidOperationException. Guard with ValueKind.
            return new BusinessLookupResult(
                d.TryGetProperty("mst", out var mstEl) ? mstEl.GetString() ?? mst : mst,
                d.TryGetProperty("name_vi", out var nameEl) ? nameEl.GetString() ?? "" : "",
                d.TryGetProperty("address_full", out var addrEl) ? addrEl.GetString() : null,
                d.TryGetProperty("status", out var statusEl) ? statusEl.GetString() : null,
                d.TryGetProperty("legal_rep_name", out var repEl) ? repEl.GetString() : null,
                d.TryGetProperty("industry", out var indEl) && indEl.ValueKind == System.Text.Json.JsonValueKind.Object
                    && indEl.TryGetProperty("name_vi", out var indNameEl) ? indNameEl.GetString() : null,
                d.TryGetProperty("province", out var provEl) && provEl.ValueKind == System.Text.Json.JsonValueKind.Object
                    && provEl.TryGetProperty("name_vi", out var provNameEl) ? provNameEl.GetString() : null,
                Source: "doanhnghiep.vn");
        }
    }

    /// <summary>Test hook: clear the shared daily quota counter (static state persists across tests).</summary>
    internal static void ResetDailyQuotaForTesting()
    {
        lock (RateLock)
        {
            DailyCounts.Clear();
        }
    }

    private bool TryAcquireDailyQuota()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var limit = options.Value.DailyLimit > 0 ? options.Value.DailyLimit : 50;
        lock (RateLock)
        {
            if (DailyCounts.Count > 0 && !DailyCounts.ContainsKey(today))
                DailyCounts.Clear();
            var count = DailyCounts.GetValueOrDefault(today);
            if (count >= limit) return false;
            DailyCounts[today] = count + 1;
            return true;
        }
    }

    private static string NormalizeMst(string mst) => mst.Replace("-", "").Replace(" ", "").Trim();

    /// <summary>Map VanAn tenant lifecycle → doanhnghiep.vn-style status string.</summary>
    private static string? MapStatus(TenantStatus status) => status switch
    {
        TenantStatus.Active => "active",
        TenantStatus.Suspended => "suspended",
        TenantStatus.Inactive => "dissolved",
        TenantStatus.Converted => "converted",
        _ => "pending"
    };
}
