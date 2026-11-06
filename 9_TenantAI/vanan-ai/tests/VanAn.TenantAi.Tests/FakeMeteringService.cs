using VanAn.TenantAi.Api.Services;
using VanAn.TenantAi.Infra;

namespace VanAn.TenantAi.Tests;

/// <summary>task_ai_p5 — deterministic metering stub: mutable check behavior, records ingests.</summary>
public sealed class FakeMeteringService : IMeteringService
{
    public bool IsConfigured { get; set; } = true;
    public int CheckCalls;
    public int IngestCalls;
    public Func<Guid, string, MeteringCheckResult?>? CheckResponder;

    public Task<MeteringCheckResult?> CheckBalanceAsync(Guid tenantId, string serviceCode, CancellationToken ct = default)
    {
        CheckCalls++;
        return Task.FromResult(CheckResponder?.Invoke(tenantId, serviceCode));
    }

    public Task<bool> IngestAsync(MeteringUsageRecord evt, CancellationToken ct = default)
    {
        IngestCalls++;
        return Task.FromResult(true);
    }
}

/// <summary>task_ai_p5 — synchronous ingest queue stub: records events (or throws to prove fail-safe).</summary>
public sealed class RecordingIngestQueue : IMeteringIngestQueue
{
    public List<MeteringUsageRecord> Events = [];
    public bool ThrowOnEnqueue;

    public void Enqueue(MeteringUsageRecord evt)
    {
        if (ThrowOnEnqueue) throw new InvalidOperationException("ingest queue down");
        Events.Add(evt);
    }
}
