using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using VanAn.CoreHub.Services;

namespace VanAn.ShopERP.Services;

/// <summary>
/// VALCN v2.0 Phase 3 — ShopERP HTTP proxy for ILoyaltyBudgetService.
/// Calls Gateway internal API (/api/internal/loyalty-budget/*) with X-Internal-Api-Key header.
/// ShopERP's SQLite ignores LoyaltyTenantConfig (PG-only) → budget checks/updates route through Gateway.
///
/// Graceful degradation:
///   - Gateway unreachable / 5xx → CheckAndAdjustPointsAsync returns original points (no cap applied, safe fallback).
///   - RecordIssuance/DecrementIssuance failures are logged but don't fail the order (loyalty already awarded).
///
/// ResetAllDailyCountersAsync / ResetAllMonthlyCountersAsync are NOT proxied — they are only called
/// by Gateway-side reset jobs (LoyaltyBudgetDailyResetJob / LoyaltyBudgetMonthlyResetJob) which have
/// direct PG access. If called from ShopERP, throws NotImplementedException (should never happen).
/// </summary>
public sealed class LoyaltyBudgetServiceHttpProxy(
    IHttpClientFactory httpClientFactory,
    ILogger<LoyaltyBudgetServiceHttpProxy> logger) : ILoyaltyBudgetService
{
    private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;
    private readonly ILogger<LoyaltyBudgetServiceHttpProxy> _logger = logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public async Task<int> CheckAndAdjustPointsAsync(
        Guid tenantId, Guid customerId, decimal orderAmount, int requestedPoints, CancellationToken ct = default)
    {
        if (requestedPoints <= 0) return 0;

        try
        {
            var client = _httpClientFactory.CreateClient("GatewayInternal");
            var req = new
            {
                TenantId = tenantId,
                CustomerId = customerId,
                OrderAmount = orderAmount,
                RequestedPoints = requestedPoints
            };
            var resp = await client.PostAsJsonAsync("api/internal/loyalty-budget/check-adjust", req, JsonOptions, ct);
            if (!resp.IsSuccessStatusCode)
            {
                _logger.LogWarning("LoyaltyBudget HTTP check-adjust failed for tenant {TenantId}: {Status} — returning original points (no cap)",
                    tenantId, resp.StatusCode);
                return requestedPoints;  // Safe fallback: no cap applied
            }

            var result = await resp.Content.ReadFromJsonAsync<CheckAdjustResponse>(JsonOptions, ct);
            return result?.AdjustedPoints ?? requestedPoints;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "LoyaltyBudget HTTP check-adjust unreachable for tenant {TenantId} — returning original points (no cap)", tenantId);
            return requestedPoints;  // Safe fallback: no cap applied
        }
    }

    public async Task RecordIssuanceAsync(Guid tenantId, int pointsIssued, CancellationToken ct = default)
    {
        if (pointsIssued <= 0) return;

        try
        {
            var client = _httpClientFactory.CreateClient("GatewayInternal");
            var req = new { TenantId = tenantId, PointsIssued = pointsIssued };
            var resp = await client.PostAsJsonAsync("api/internal/loyalty-budget/record", req, JsonOptions, ct);
            if (!resp.IsSuccessStatusCode)
            {
                _logger.LogWarning("LoyaltyBudget HTTP record failed for tenant {TenantId}: {Status} — counters may be stale",
                    tenantId, resp.StatusCode);
            }
        }
        catch (Exception ex)
        {
            // Non-fatal: loyalty already awarded, counters may be stale (will be corrected by reset jobs)
            _logger.LogWarning(ex, "LoyaltyBudget HTTP record unreachable for tenant {TenantId} — counters may be stale", tenantId);
        }
    }

    public async Task DecrementIssuanceAsync(Guid tenantId, int pointsToReverse, CancellationToken ct = default)
    {
        if (pointsToReverse <= 0) return;

        try
        {
            var client = _httpClientFactory.CreateClient("GatewayInternal");
            var req = new { TenantId = tenantId, PointsToReverse = pointsToReverse };
            var resp = await client.PostAsJsonAsync("api/internal/loyalty-budget/decrement", req, JsonOptions, ct);
            if (!resp.IsSuccessStatusCode)
            {
                _logger.LogWarning("LoyaltyBudget HTTP decrement failed for tenant {TenantId}: {Status} — counters may be stale",
                    tenantId, resp.StatusCode);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "LoyaltyBudget HTTP decrement unreachable for tenant {TenantId} — counters may be stale", tenantId);
        }
    }

    public Task ResetAllDailyCountersAsync(CancellationToken ct = default)
    {
        // Only called by Gateway-side reset jobs (direct PG access) — should never be called from ShopERP
        throw new NotImplementedException("ResetAllDailyCountersAsync is only callable from Gateway-side reset jobs (direct PG access).");
    }

    public Task ResetAllMonthlyCountersAsync(CancellationToken ct = default)
    {
        // Only called by Gateway-side reset jobs (direct PG access) — should never be called from ShopERP
        throw new NotImplementedException("ResetAllMonthlyCountersAsync is only callable from Gateway-side reset jobs (direct PG access).");
    }

    // DTO matching Gateway CheckAdjustResponse
    private sealed class CheckAdjustResponse
    {
        public int AdjustedPoints { get; set; }
    }
}
