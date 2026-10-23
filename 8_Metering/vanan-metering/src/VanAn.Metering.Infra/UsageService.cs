using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using VanAn.Metering.Contracts;
using VanAn.Metering.Domain;

namespace VanAn.Metering.Infra;

/// <summary>Outcome of a single ingest attempt — created, replayed, or rejected.</summary>
public sealed record UsageIngestResult(Guid? EventId, bool Replay, string? ErrorCode, string? ErrorMessage)
{
    public static UsageIngestResult Created(Guid id) => new(id, false, null, null);
    public static UsageIngestResult Replayed(Guid id) => new(id, true, null, null);
    public static UsageIngestResult Error(string code, string message) => new(null, false, code, message);
}

/// <summary>
/// Task P3 (task_p3_ingest.md) — usage ingest + idempotency + summary/events.
/// P7 D1: deduct-at-ingest — priced events deduct wallet credits in the SAME
/// transaction (event + ledger + wallet + alert commit together).
/// </summary>
public sealed partial class UsageService(MeteringDbContext db, WalletService wallet, RateCardService rateCards)
{
    private static readonly TimeSpan LateSkew = TimeSpan.FromMinutes(5); // D3 / [G5]

    [GeneratedRegex(@"^\d{4}-(0[1-9]|1[0-2])$")]
    private static partial Regex PeriodPattern();

    public const int MaxBatchSize = 500;      // D2
    public const int DefaultPageSize = 50;    // D7
    public const int MaxPageSize = 200;       // D7

    // D9 — caller-chosen key, enforce non-empty + <=128 chars.
    private static UsageIngestResult? ValidateKey(string? idemKey)
        => string.IsNullOrWhiteSpace(idemKey)
            ? UsageIngestResult.Error("missing_idempotency_key", "Idempotency-Key is required.")
            : idemKey.Length > 128
                ? UsageIngestResult.Error("idempotency_key_too_long", "Idempotency-Key must be <= 128 characters.")
                : null;

    // D5 — shared by single + batch paths.
    private static UsageIngestResult? Validate(UsageEventRequest req)
        => req.TenantId == Guid.Empty
            ? UsageIngestResult.Error("invalid_tenantId", "tenantId must not be empty.")
            : string.IsNullOrWhiteSpace(req.ServiceCode)
                ? UsageIngestResult.Error("invalid_serviceCode", "serviceCode is required.")
                : string.IsNullOrWhiteSpace(req.Unit)
                    ? UsageIngestResult.Error("invalid_unit", "unit is required.")
                    : req.Quantity <= 0
                        ? UsageIngestResult.Error("invalid_quantity", "quantity must be > 0.")
                        : req.OccurredAtUtc.Year < 2020
                            ? UsageIngestResult.Error("invalid_occurredAtUtc", "occurredAtUtc is implausibly old.")
                            : null;

    private static DateTime NormalizeUtc(DateTime dt)
        => dt.Kind switch
        {
            DateTimeKind.Utc => dt,
            DateTimeKind.Local => dt.ToUniversalTime(),
            _ => DateTime.SpecifyKind(dt, DateTimeKind.Utc)
        };

    /// <summary>
    /// D1/D3 — dedupe by (CallerId, IdempotencyKey), flag LateEvent when skew > ±5min,
    /// insert append-only. Concurrent same-key insert → unique index throws → replay
    /// the winner's row (never 500, card §4 race).
    /// </summary>
    public async Task<UsageIngestResult> IngestAsync(
        string callerId, string? idemKey, UsageEventRequest req, DateTime nowUtc, CancellationToken ct = default)
    {
        var invalid = ValidateKey(idemKey) ?? Validate(req);
        if (invalid != null) return invalid;

        var existing = await db.UsageEvents.AsNoTracking()
            .Where(e => e.CallerId == callerId && e.IdempotencyKey == idemKey)
            .Select(e => e.Id)
            .FirstOrDefaultAsync(ct);
        if (existing != Guid.Empty) return UsageIngestResult.Replayed(existing);

        var occurred = NormalizeUtc(req.OccurredAtUtc);
        var late = Math.Abs((occurred - nowUtc).TotalMinutes) > LateSkew.TotalMinutes;
        var ev = UsageEvent.Create(
            callerId, idemKey!, req.TenantId, req.ServiceCode.Trim(), req.Quantity, req.Unit.Trim(),
            occurred, nowUtc, req.ShopInstanceId, req.RequestedBy, req.TaskRef, req.MetadataJson, late);
        db.UsageEvents.Add(ev);
        try
        {
            // P7 D1/D2 — priced event → deduct inside the same commit (relational tx;
            // in-memory provider treats the tx wrapper as a no-op).
            var pricedVersion = await rateCards.ResolveAsync(ev.ServiceCode, occurred, ct);
            if (pricedVersion != null)
            {
                // Relational providers get a real tx; InMemory (tests) treats it as no-op.
                await using var tx = db.Database.IsRelational()
                    ? await db.Database.BeginTransactionAsync(ct)
                    : null;
                var required = ev.Quantity * pricedVersion.CreditRate;
                var applied = await wallet.DeductInTransactionAsync(
                    ev.TenantId, ev.Id, required, $"usage:{ev.ServiceCode}", nowUtc, ct);
                ev.ApplyDeduction(applied, required - applied);
                await wallet.EvaluateAlertsAsync(ev.TenantId, nowUtc, ct);
                await db.SaveChangesAsync(ct);
                if (tx != null) await tx.CommitAsync(ct);
            }
            else
            {
                await db.SaveChangesAsync(ct);
            }
        }
        catch (DbUpdateException)
        {
            db.Entry(ev).State = EntityState.Detached;
            var winner = await db.UsageEvents.AsNoTracking()
                .Where(e => e.CallerId == callerId && e.IdempotencyKey == idemKey)
                .Select(e => e.Id)
                .FirstAsync(ct);
            return UsageIngestResult.Replayed(winner);
        }
        return UsageIngestResult.Created(ev.Id);
    }

    /// <summary>
    /// D2 — per-item semantics (no all-or-nothing): validate → in-batch dedupe →
    /// ingest; each item gets accepted/replayed/rejected independently.
    /// </summary>
    public async Task<UsageBatchResponse> IngestBatchAsync(
        string callerId, IReadOnlyList<UsageBatchItem> items, DateTime nowUtc, CancellationToken ct = default)
    {
        var results = new List<UsageBatchItemResult>(items.Count);
        var seen = new Dictionary<string, Guid>(StringComparer.Ordinal); // card §4 batch-c
        var accepted = 0; var replayed = 0; var rejected = 0;

        for (var i = 0; i < items.Count; i++)
        {
            var item = items[i];
            var invalid = ValidateKey(item.IdempotencyKey) ?? Validate(item.ToRequest());
            if (invalid != null)
            {
                rejected++;
                results.Add(new(i, "rejected", null, invalid.ErrorCode));
                continue;
            }

            if (seen.TryGetValue(item.IdempotencyKey, out var seenId))
            {
                replayed++;
                results.Add(new(i, "replayed", seenId, null));
                continue;
            }

            var r = await IngestAsync(callerId, item.IdempotencyKey, item.ToRequest(), nowUtc, ct);
            if (r.ErrorCode != null)
            {
                rejected++;
                results.Add(new(i, "rejected", null, r.ErrorCode));
            }
            else
            {
                seen[item.IdempotencyKey] = r.EventId!.Value;
                if (r.Replay)
                {
                    replayed++;
                    results.Add(new(i, "replayed", r.EventId, null));
                }
                else
                {
                    accepted++;
                    results.Add(new(i, "accepted", r.EventId, null));
                }
            }
        }
        return new UsageBatchResponse(accepted, replayed, rejected, results);
    }

    /// <summary>
    /// D6/D8 — shadow report: group period events by (serviceCode, unit); credits per
    /// event = quantity × rate resolved AT that event's occurredAtUtc (never the
    /// current rate — [G7]). Any unpriced event → Credits=null, Priced=false,
    /// UnpricedEvents reports how many missed (D4).
    /// </summary>
    public async Task<(UsageSummaryResponse? Response, string? ErrorCode, string? ErrorMessage)>
        GetSummaryAsync(Guid tenantId, string period, CancellationToken ct = default)
    {
        if (tenantId == Guid.Empty)
            return (null, "invalid_tenantId", "tenantId must not be empty.");
        if (string.IsNullOrEmpty(period) || !PeriodPattern().IsMatch(period))
            return (null, "invalid_period", "period must be YYYY-MM.");

        var start = new DateTime(int.Parse(period[..4], CultureInfo.InvariantCulture), int.Parse(period[5..7], CultureInfo.InvariantCulture), 1, 0, 0, 0, DateTimeKind.Utc);
        var end = start.AddMonths(1);

        var events = await db.UsageEvents.AsNoTracking()
            .Where(e => e.TenantId == tenantId && e.OccurredAtUtc >= start && e.OccurredAtUtc < end)
            .Select(e => new { e.ServiceCode, e.Unit, e.Quantity, e.OccurredAtUtc })
            .ToListAsync(ct);

        var codes = events.Select(e => e.ServiceCode).Distinct().ToList();
        var versions = codes.Count == 0
            ? []
            : await db.RateCardVersions.AsNoTracking()
                .Where(v => codes.Contains(v.ServiceCode) && v.IsActive && v.EffectiveFrom < end)
                .ToListAsync(ct);

        var items = events
            .GroupBy(e => (e.ServiceCode, e.Unit))
            .Select(g =>
            {
                var vs = versions.Where(v => v.ServiceCode == g.Key.ServiceCode).ToList();
                decimal credits = 0;
                var unpriced = 0;
                foreach (var e in g)
                {
                    var rate = PickAt(vs, e.OccurredAtUtc)?.CreditRate;
                    if (rate == null) unpriced++;
                    else credits += e.Quantity * rate.Value;
                }
                var priced = unpriced == 0;
                return new UsageSummaryItem(
                    g.Key.ServiceCode, g.Key.Unit, g.Sum(x => x.Quantity),
                    priced ? credits : null, priced, unpriced);
            })
            .OrderBy(i => i.ServiceCode)
            .ToList();

        return (new UsageSummaryResponse(period, items), null, null);
    }

    /// <summary>D7 — event history: filters + paging, newest occurredAtUtc first.</summary>
    public async Task<UsageEventListResponse> ListEventsAsync(
        Guid tenantId, string? serviceCode, DateTime? fromUtc, DateTime? toUtc,
        int page, int pageSize, CancellationToken ct = default)
    {
        var q = db.UsageEvents.AsNoTracking().Where(e => e.TenantId == tenantId);
        if (!string.IsNullOrWhiteSpace(serviceCode)) q = q.Where(e => e.ServiceCode == serviceCode);
        if (fromUtc.HasValue) q = q.Where(e => e.OccurredAtUtc >= fromUtc.Value);
        if (toUtc.HasValue) q = q.Where(e => e.OccurredAtUtc < toUtc.Value);

        var items = await q
            .OrderByDescending(e => e.OccurredAtUtc)
            .ThenByDescending(e => e.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(e => new UsageEventDto(
                e.Id, e.TenantId, e.ShopInstanceId, e.ServiceCode, e.Quantity, e.Unit,
                e.OccurredAtUtc, e.ReceivedAtUtc, e.LateEvent, e.RequestedBy, e.TaskRef, e.CallerId))
            .ToListAsync(ct);

        return new UsageEventListResponse(items, page, pageSize);
    }

    // Same predicate as RateCardService.ResolveAsync, evaluated in memory for
    // per-event pricing inside a summary (avoids N+1 queries).
    private static RateCardVersion? PickAt(IReadOnlyList<RateCardVersion> versions, DateTime atUtc)
        => versions
            .Where(v => v.EffectiveFrom <= atUtc && (v.EffectiveTo == null || v.EffectiveTo > atUtc))
            .OrderByDescending(v => v.EffectiveFrom)
            .FirstOrDefault();
}
