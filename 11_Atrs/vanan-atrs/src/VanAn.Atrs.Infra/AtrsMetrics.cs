using System.Collections.Concurrent;

namespace VanAn.Atrs.Infra;

/// <summary>
/// ATRS-13 (NFR-006) — metrics counters nhẹ (in-memory) cho observability.
/// /metrics endpoint (Prometheus text format) đọc snapshot + gauge live từ DB.
/// KHÔNG PII/secret trong labels — chỉ event_type.
/// </summary>
public sealed class AtrsMetrics
{
    private readonly ConcurrentDictionary<string, long> _counters = new(StringComparer.Ordinal);

    public void Record(string eventType)
        => _counters.AddOrUpdate(eventType, 1, (_, v) => v + 1);

    public IReadOnlyDictionary<string, long> Snapshot()
        => _counters.OrderBy(kv => kv.Key).ToDictionary(kv => kv.Key, kv => kv.Value);
}