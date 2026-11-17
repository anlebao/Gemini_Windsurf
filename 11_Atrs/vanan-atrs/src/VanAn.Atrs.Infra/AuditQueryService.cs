using Microsoft.EntityFrameworkCore;
using VanAn.Atrs.Contracts;
using VanAn.Atrs.Domain;

namespace VanAn.Atrs.Infra;

/// <summary>
/// ATRS-10 A20 — audit explorer query. READ-ONLY (không có method update/delete):
/// AccountSecurityEvent append-only (FR-009); DTO sanitized KHÔNG payload JSON.
/// </summary>
public interface IAuditQueryService
{
    Task<SecurityEventPageDto> QueryAsync(AuditQuery query, CancellationToken ct = default);
}

public sealed class AuditQueryService(AtrsDbContext db) : IAuditQueryService
{
    public async Task<SecurityEventPageDto> QueryAsync(AuditQuery query, CancellationToken ct = default)
    {
        var q = db.AccountSecurityEvents.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.EventType))
            q = q.Where(e => e.EventType.Contains(query.EventType));
        if (!string.IsNullOrWhiteSpace(query.Outcome) && Enum.TryParse<SecurityEventOutcome>(query.Outcome, true, out var outcome))
            q = q.Where(e => e.Outcome == outcome);
        if (!string.IsNullOrWhiteSpace(query.ActorType) && Enum.TryParse<SecurityEventActorType>(query.ActorType, true, out var actorType))
            q = q.Where(e => e.ActorType == actorType);
        if (!string.IsNullOrWhiteSpace(query.SourceApplication))
            q = q.Where(e => e.SourceApplication != null && e.SourceApplication.Contains(query.SourceApplication));
        if (query.SubjectId.HasValue)
            q = q.Where(e => e.TrustSubjectId == query.SubjectId.Value);
        if (query.From.HasValue)
            q = q.Where(e => e.OccurredAt >= query.From.Value);
        if (query.To.HasValue)
            q = q.Where(e => e.OccurredAt <= query.To.Value);

        var total = await q.CountAsync(ct);
        var items = await q.OrderByDescending(e => e.OccurredAt)
            .Skip(Math.Max(0, query.Skip)).Take(Math.Clamp(query.Take, 1, 500))
            .ToListAsync(ct);

        return new SecurityEventPageDto(
            items.Select(e => new SecurityEventItemDto(
                e.Id, e.EventType, e.OccurredAt, e.ActorType.ToString(), e.ActorId,
                e.SourceApplication, e.DeviceId, e.SessionCorrelationId,
                e.Outcome.ToString(), e.ReasonCode, e.TrustSubjectId)).ToList(),
            total, Math.Max(0, query.Skip), Math.Clamp(query.Take, 1, 500));
    }
}