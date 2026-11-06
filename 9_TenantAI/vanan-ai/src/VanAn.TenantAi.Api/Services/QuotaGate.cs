using System.Collections.Concurrent;
using VanAn.TenantAi.Infra;

namespace VanAn.TenantAi.Api.Services;

/// <summary>[G7] PO-1 decision for one chat request.</summary>
public enum QuotaOutcome
{
    /// <summary>Wallet verified sufficient (or metering unconfigured) — full pipeline.</summary>
    Allow,
    /// <summary>deny=true — refuse politely, never leak balance numbers.</summary>
    Refuse,
    /// <summary>Check failed + cache expired — structured-only pipeline, ZERO LLM calls.</summary>
    Degrade
}

/// <summary>
/// task_ai_p5 / SRS [G7] PO-1 — metering quota gate in front of the chat pipeline.
///
/// DEVIATION có chủ đích khỏi fail-open generic của metering (ghi nhận tại card):
/// AI call tốn tiền ngoài mỗi request nên khi quota KHÔNG xác minh được thì
/// chính sách chặt hơn — degrade structured-only thay vì allow mù.
///
/// Ma trận (serviceCode "ai.chat.message", cache 30s/tenant):
///   metering chưa cấu hình        → Allow   (staging/dev không có metering)
///   check OK + deny=false         → Allow   (cache kết quả)
///   check OK + deny=true          → Refuse  ("tính năng AI tạm ngưng")
///   check fail + cache còn hạn    → theo cached verdict
///   check fail + cache hết hạn    → Degrade (structured-only — ILlmChatClient không được gọi)
///
/// Singleton — cache persists across requests (cũng là nơi widget-config đọc cached-deny).
/// </summary>
public sealed class QuotaGate(IMeteringService metering, IConfiguration config, ILogger<QuotaGate> logger)
{
    private readonly ConcurrentDictionary<Guid, CacheEntry> _cache = new();
    private TimeSpan CacheTtl => TimeSpan.FromSeconds(config.GetValue("Metering:CheckCacheSeconds", 30));

    private sealed record CacheEntry(MeteringCheckResult Result, DateTime ExpiresUtc);

    public async Task<QuotaOutcome> EvaluateAsync(Guid tenantId, CancellationToken ct = default)
    {
        if (!metering.IsConfigured) return QuotaOutcome.Allow;

        var now = DateTime.UtcNow;
        var result = await metering.CheckBalanceAsync(tenantId, MeteringServiceCodes.ChatMessage, ct);
        if (result != null)
        {
            _cache[tenantId] = new CacheEntry(result, now + CacheTtl);
            return result.Deny ? QuotaOutcome.Refuse : QuotaOutcome.Allow;
        }

        // Check failed — [G7] matrix: fresh cache wins, otherwise degrade.
        if (_cache.TryGetValue(tenantId, out var hit) && hit.ExpiresUtc > now)
            return hit.Result.Deny ? QuotaOutcome.Refuse : QuotaOutcome.Allow;

        logger.LogWarning("Metering check failed with expired cache — degrading chat tenant={Tenant}", tenantId);
        return QuotaOutcome.Degrade;
    }
}
