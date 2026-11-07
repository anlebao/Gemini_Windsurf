using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using VanAn.TenantAi.Ai;
using VanAn.TenantAi.Contracts;
using VanAn.TenantAi.Domain;
using VanAn.TenantAi.Infra;

namespace VanAn.TenantAi.Api.Services;

public sealed record ProductsBatchResult(int Upserted, int Removed, bool Replayed);
public sealed record ServiceResult<T>(T? Value, string? ErrorCode);

/// <summary>
/// task_ai_p2 — knowledge ingest pipeline: product sync upsert (auto-trusted per
/// 【PO G14】), document ingest → pending_review [G14], approve → sanitize+chunk+embed,
/// ingest_log idempotency [G8]. TenantId always comes from the signed ctx claim —
/// never from request body.
/// </summary>
public sealed class KnowledgeIngestService(
    TenantAiDbContext db,
    ChunkingService chunker,
    IEmbeddingProvider embedder,
    IConfiguration config,
    ILogger<KnowledgeIngestService> logger)
{
    public const string ProductsKind = "products";
    private static readonly HashSet<string> DocTypes = new(StringComparer.Ordinal)
        { "faq", "story", "policy", "menu-note", "recipe" };

    private int StaleHours => config.GetValue("Knowledge:StaleHours", 24);

    // ---------- Products (auto-trusted structured pipeline) ----------

    public async Task<ServiceResult<ProductsBatchResult>> UpsertProductsAsync(
        Guid tenantId, ProductsBatchRequest req, string idemKey, CancellationToken ct)
    {
        var prior = await FindLogAsync(tenantId, ProductsKind, idemKey, ct);
        if (prior != null)
            return new(new ProductsBatchResult(prior.ItemCount, 0, true), null);

        var productIds = req.Items.Select(i => i.ProductId).ToHashSet();
        var existing = await db.ProductKnowledge
            .Where(p => p.TenantId == tenantId && productIds.Contains(p.ProductId))
            .ToDictionaryAsync(p => p.ProductId, ct);

        var upserted = 0;
        foreach (var item in req.Items)
        {
            if (string.IsNullOrWhiteSpace(item.Name)) continue;
            var ingredients = JsonSerializer.Serialize(item.Ingredients ?? []);
            var allergens = JsonSerializer.Serialize(item.Allergens ?? []);
            if (existing.TryGetValue(item.ProductId, out var row))
            {
                row.UpdateFromSource(item.Name, item.Description, item.Price, item.Calories,
                    ingredients, allergens, item.Available, item.CategoryName, req.SourceVersion);
            }
            else
            {
                var created = ProductKnowledge.Create(tenantId, item.ProductId, item.Name, item.Price, req.SourceVersion);
                created.UpdateFromSource(item.Name, item.Description, item.Price, item.Calories,
                    ingredients, allergens, item.Available, item.CategoryName, req.SourceVersion);
                db.ProductKnowledge.Add(created);
            }
            upserted++;
        }

        var removed = 0;
        if (req.FullSync)
        {
            var stale = await db.ProductKnowledge
                .Where(p => p.TenantId == tenantId && !productIds.Contains(p.ProductId))
                .ToListAsync(ct);
            removed = stale.Count;
            db.ProductKnowledge.RemoveRange(stale);
        }

        db.IngestLogs.Add(IngestLog.Create(tenantId, ProductsKind, idemKey, upserted, IngestStatus.Succeeded));
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            // Concurrent replay hit the unique key — treat as deduped first-write-wins.
            var winner = await FindLogAsync(tenantId, ProductsKind, idemKey, ct);
            return new(new ProductsBatchResult(winner?.ItemCount ?? upserted, 0, true), null);
        }
        return new(new ProductsBatchResult(upserted, removed, false), null);
    }

    public async Task<bool> DeleteProductAsync(Guid tenantId, Guid productId, CancellationToken ct)
    {
        var row = await db.ProductKnowledge
            .FirstOrDefaultAsync(p => p.TenantId == tenantId && p.ProductId == productId, ct);
        if (row == null) return false;
        db.ProductKnowledge.Remove(row);
        await db.SaveChangesAsync(ct);
        return true;
    }

    // ---------- Documents (approval gate [G14]) ----------

    public async Task<ServiceResult<KnowledgeDocument>> AddDocumentAsync(
        Guid tenantId, DocumentIngestRequest req, CancellationToken ct)
    {
        if (!DocTypes.Contains(req.Type))
            return new(null, "invalid_type");
        if (string.IsNullOrWhiteSpace(req.Title) || string.IsNullOrWhiteSpace(req.Content))
            return new(null, "invalid_document");
        if (!Enum.TryParse<DocumentClassification>(req.Classification, true, out var classification))
            return new(null, "invalid_classification");
        // [G5] Restricted is never ingested for chat — fail closed at the boundary.
        if (classification == DocumentClassification.Restricted)
            return new(null, "restricted_not_ingestible");

        // T3 — strip instruction-like lines before they ever reach a chunk.
        var sanitized = IngestSanitizer.Sanitize(req.Content);
        if (string.IsNullOrWhiteSpace(sanitized.Text))
            return new(null, "empty_after_sanitize");

        var doc = KnowledgeDocument.Create(tenantId, req.Type, req.Title.Trim(),
            sanitized.Text, classification, sanitized: sanitized.Sanitized);
        db.KnowledgeDocuments.Add(doc);
        await db.SaveChangesAsync(ct);
        return new(doc, null);
    }

    public async Task<ServiceResult<DocumentSummaryDto>> ApproveDocumentAsync(
        Guid tenantId, Guid docId, string callerId, CancellationToken ct)
    {
        var doc = await db.KnowledgeDocuments
            .FirstOrDefaultAsync(d => d.TenantId == tenantId && d.Id == docId, ct);
        if (doc == null || doc.Status == DocumentStatus.Deleted)
            return new(null, "not_found");
        if (doc.ApprovalStatus != ApprovalStatus.PendingReview)
            return new(null, "not_pending");

        doc.Approve(callerId);

        // Chunk + embed on approve only [G14] — pending docs never get chunks.
        var chunks = chunker.Chunk(doc.Content);
        for (var i = 0; i < chunks.Count; i++)
        {
            var embedding = await SafeEmbedAsync(chunks[i], ct);
            db.KnowledgeChunks.Add(KnowledgeChunk.Create(tenantId, doc.Id, i, chunks[i],
                embedding, ChunkingService.EstimateTokens(chunks[i]), doc.Sanitized));
        }

        await db.SaveChangesAsync(ct);
        return new(ToDto(doc), null);
    }

    public async Task<ServiceResult<DocumentSummaryDto>> RejectDocumentAsync(
        Guid tenantId, Guid docId, string callerId, string reason, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(reason))
            return new(null, "reason_required");
        var doc = await db.KnowledgeDocuments
            .FirstOrDefaultAsync(d => d.TenantId == tenantId && d.Id == docId, ct);
        if (doc == null || doc.Status == DocumentStatus.Deleted)
            return new(null, "not_found");
        if (doc.ApprovalStatus != ApprovalStatus.PendingReview)
            return new(null, "not_pending");

        doc.Reject(callerId, reason.Trim());
        await db.SaveChangesAsync(ct);
        return new(ToDto(doc), null);
    }

    public async Task<bool> DeleteDocumentAsync(Guid tenantId, Guid docId, CancellationToken ct)
    {
        var doc = await db.KnowledgeDocuments
            .FirstOrDefaultAsync(d => d.TenantId == tenantId && d.Id == docId, ct);
        if (doc == null || doc.Status == DocumentStatus.Deleted) return false;
        doc.MarkDeleted();
        // Soft-deleted docs must lose their chunks — approval/deletion removes them
        // from every future retrieval set.
        var chunks = await db.KnowledgeChunks.Where(c => c.TenantId == tenantId && c.DocumentId == docId).ToListAsync(ct);
        db.KnowledgeChunks.RemoveRange(chunks);
        await db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<IReadOnlyList<DocumentSummaryDto>> GetPendingAsync(Guid tenantId, CancellationToken ct)
        => await db.KnowledgeDocuments.AsNoTracking()
            .Where(d => d.TenantId == tenantId
                && d.ApprovalStatus == ApprovalStatus.PendingReview
                && d.Status == DocumentStatus.Active)
            .OrderBy(d => d.CreatedAtUtc)
            .Select(d => ToDto(d))
            .ToListAsync(ct);

    /// <summary>
    /// task_ai_p10 — paged docs list for the admin UI (filter by approval status snake_case:
    /// pending_review|approved|rejected; invalid status → empty page, fail-closed).
    /// Includes Content — the approval gate review needs the text.
    /// </summary>
    public async Task<DocumentListResponse> GetDocumentsAsync(
        Guid tenantId, string? status, int page, int size, CancellationToken ct)
    {
        page = Math.Max(1, page);
        size = Math.Clamp(size, 1, 100);
        var query = db.KnowledgeDocuments.AsNoTracking()
            .Where(d => d.TenantId == tenantId && d.Status == DocumentStatus.Active);
        if (!string.IsNullOrWhiteSpace(status))
        {
            if (!Enum.TryParse<ApprovalStatus>(status.Replace("_", ""), true, out var approval))
                return new DocumentListResponse(page, size, 0, []);
            query = query.Where(d => d.ApprovalStatus == approval);
        }
        var total = await query.CountAsync(ct);
        var rows = await query.OrderByDescending(d => d.CreatedAtUtc)
            .Skip((page - 1) * size).Take(size)
            .ToListAsync(ct);
        var items = rows.Select(d => new DocumentListItemDto(
            d.Id, d.Type, d.Title, ToSnake(d.Classification), ToSnake(d.ApprovalStatus),
            d.ApprovedBy, d.ApprovedAtUtc, d.RejectionReason, d.CreatedAtUtc, d.Sanitized, d.Content)).ToList();
        return new DocumentListResponse(page, size, total, items);
    }

    /// <summary>
    /// Gate-test retrieval query (T11/T5): what the public channel may see —
    /// tenant + approved + Public + active docs only. The P3 retriever applies
    /// the same predicate before vector search.
    /// </summary>
    public async Task<IReadOnlyList<KnowledgeChunk>> GetPublicChunksAsync(Guid tenantId, CancellationToken ct)
        => await db.KnowledgeChunks.AsNoTracking()
            .Where(c => c.TenantId == tenantId)
            .Join(db.KnowledgeDocuments.AsNoTracking()
                    .Where(d => d.ApprovalStatus == ApprovalStatus.Approved
                        && d.Classification == DocumentClassification.Public
                        && d.Status == DocumentStatus.Active),
                c => c.DocumentId, d => d.Id, (c, _) => c)
            .OrderBy(c => c.DocumentId).ThenBy(c => c.ChunkIndex)
            .ToListAsync(ct);

    public async Task<KnowledgeStatusResponse> GetStatusAsync(Guid tenantId, CancellationToken ct)
    {
        var docStats = await db.KnowledgeDocuments.AsNoTracking()
            .Where(d => d.TenantId == tenantId && d.Status == DocumentStatus.Active)
            .GroupBy(d => d.ApprovalStatus)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(ct);
        var chunks = await db.KnowledgeChunks.AsNoTracking()
            .Where(c => c.TenantId == tenantId)
            .GroupBy(_ => 1)
            .Select(g => new { Total = g.Count(), Embedded = g.Count(c => c.Embedding != null) })
            .FirstOrDefaultAsync(ct);
        var productCount = await db.ProductKnowledge.AsNoTracking().CountAsync(p => p.TenantId == tenantId, ct);
        var lastSync = await db.ProductKnowledge.AsNoTracking()
            .Where(p => p.TenantId == tenantId)
            .OrderByDescending(p => p.SyncedAtUtc)
            .Select(p => new { p.SourceVersion, p.SyncedAtUtc })
            .FirstOrDefaultAsync(ct);

        int Count(ApprovalStatus s) => docStats.FirstOrDefault(x => x.Status == s)?.Count ?? 0;
        var stale = lastSync != null && lastSync.SyncedAtUtc < DateTime.UtcNow.AddHours(-StaleHours);
        return new KnowledgeStatusResponse(
            Count(ApprovalStatus.PendingReview), Count(ApprovalStatus.Approved), Count(ApprovalStatus.Rejected),
            chunks?.Total ?? 0, chunks?.Embedded ?? 0, (chunks?.Total ?? 0) - (chunks?.Embedded ?? 0),
            productCount, lastSync?.SourceVersion, lastSync?.SyncedAtUtc, stale, StaleHours);
    }

    /// <summary>[G16] staleness check — strip volatile claims when sync age exceeds TTL.</summary>
    public bool IsStale(ProductKnowledge product)
        => product.SyncedAtUtc < DateTime.UtcNow.AddHours(-StaleHours);

    private async Task<IngestLog?> FindLogAsync(Guid tenantId, string kind, string key, CancellationToken ct)
        => await db.IngestLogs.AsNoTracking()
            .FirstOrDefaultAsync(l => l.TenantId == tenantId && l.Kind == kind && l.IdempotencyKey == key, ct);

    private async Task<float[]?> SafeEmbedAsync(string chunk, CancellationToken ct)
    {
        if (!embedder.Enabled) return null;
        var v = await embedder.EmbedAsync(chunk, ct);
        if (v is { Length: > 0 } && v.Length != embedder.Dimensions)
        {
            logger.LogWarning("Embedding dims {Got} != schema {Want} — stored null", v.Length, embedder.Dimensions);
            return null;
        }
        return v;
    }

    private static bool IsUniqueViolation(DbUpdateException ex)
        // PG SqlState 23505; InMemory/other providers never reach the dedupe insert race anyway.
        => ex.InnerException is PostgresException { SqlState: "23505" };

    private static DocumentSummaryDto ToDto(KnowledgeDocument d)
        => new(d.Id, d.Type, d.Title, ToSnake(d.Classification), ToSnake(d.ApprovalStatus),
            d.ApprovedBy, d.ApprovedAtUtc, d.RejectionReason, d.CreatedAtUtc);

    private static string ToSnake<T>(T value) where T : struct, Enum
        => string.Concat(value.ToString().Select((c, i) =>
            i > 0 && char.IsUpper(c) ? "_" + char.ToLowerInvariant(c) : char.ToLowerInvariant(c).ToString()));
}
