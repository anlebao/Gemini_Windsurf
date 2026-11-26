using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;
using Pgvector;
using VanAn.TenantAi.Ai;
using VanAn.TenantAi.Domain;
using VanAn.TenantAi.Infra;

namespace VanAn.TenantAi.Api.Services;

/// <summary>
/// task_ai_p3 / SRS [G2][G3][G5][G14] — tenant-filtered retrieval:
///   · structured intents → SQL `product_knowledge` (intent-shaped: availability
///     filters Available, nutrition sorts by calories, name mentions narrow)
///   · semantic intents → pgvector cosine over chunks whose doc is
///     approved + Public + active [G14][G5]; non-relational providers (tests)
///     or missing embedding → doc-type + keyword fallback.
/// Products are treated as classification=Public for the [G15] filter.
/// </summary>
public sealed class KnowledgeRetriever(
    TenantAiDbContext db,
    IEmbeddingProvider embedder,
    IConfiguration config,
    ILogger<KnowledgeRetriever> logger)
{
    private int MaxProducts => config.GetValue("TenantAiChat:MaxProductFacts", 10);
    private int MaxChunks => config.GetValue("TenantAiChat:MaxChunkFacts", 5);

    /// <summary>Semantic intent → doc types it may draw from. null = any public type.</summary>
    private static readonly Dictionary<string, HashSet<string>> DocTypesByIntent = new(StringComparer.Ordinal)
    {
        [ChatIntents.RecipeQuery] = ["recipe"],
        [ChatIntents.BrandStory] = ["story"],
        [ChatIntents.Faq] = ["faq", "policy", "menu-note"]
    };

    public async Task<IReadOnlyList<ContextItem>> RetrieveAsync(
        Guid tenantId, string intent, string question, KnowledgeSourceFlags flags, CancellationToken ct)
    {
        var items = new List<ContextItem>();
        if (flags.HasFlag(KnowledgeSourceFlags.ProductKnowledge))
            items.AddRange(await RetrieveProductsAsync(tenantId, intent, question, ct));
        if (flags.HasFlag(KnowledgeSourceFlags.PublicDocuments))
            items.AddRange(await RetrieveChunksAsync(tenantId, intent, question, ct));
        return items;
    }

    // ---------- structured: product_knowledge ----------

    private async Task<IReadOnlyList<ContextItem>> RetrieveProductsAsync(
        Guid tenantId, string intent, string question, CancellationToken ct)
    {
        var all = await db.ProductKnowledge.AsNoTracking()
            .Where(p => p.TenantId == tenantId)
            .ToListAsync(ct);
        if (all.Count == 0) return [];

        var normalizedQ = IntentRouter.Normalize(question);
        var mentioned = all.Where(p => NameMentioned(normalizedQ, p.Name)).ToList();

        IEnumerable<ProductKnowledge> picked = intent switch
        {
            // Name mention wins; else intent-shaped defaults.
            _ when mentioned.Count > 0 => mentioned,
            ChatIntents.NutritionQuery => all.Where(p => p.Calories != null)
                .OrderBy(p => p.Calories).Take(MaxProducts),
            ChatIntents.MenuQuery => all.Where(p => p.Available)
                .OrderBy(p => p.Name).Take(MaxProducts),
            // PriceQuery/AvailabilityQuery/RecipeQuery without a name mention → no facts.
            _ => []
        };

        return picked
            .Select(p => new ContextItem(ContextItemKind.Product, $"product:{p.ProductId}",
                PromptBuilder.ProductLine(p), DocumentClassification.Public, p, null))
            .ToList();
    }

    /// <summary>
    /// Whole-name containment, all significant name tokens in the question, OR
    /// (2026-11-26) every content token of the question ⊆ name tokens — the last
    /// covers industrial/SKU names ("Ampe Kìm AC Fluke Model 305") where customers
    /// ask by category words ("giá ampe kìm") and never type the model string.
    /// </summary>
    private static bool NameMentioned(string normalizedQuestion, string productName)
    {
        var name = IntentRouter.Normalize(productName);
        if (normalizedQuestion.Contains(name, StringComparison.Ordinal)) return true;
        // len≥4 avoids generic-food false positives ("món"→"mon" appears in most
        // menu questions and would mark every product "mentioned").
        var tokens = name.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(t => t.Length >= 4).ToList();
        if (tokens.Count > 0 && tokens.All(t => normalizedQuestion.Contains(t, StringComparison.Ordinal)))
            return true;

        // Reverse match: every significant question token equals a name token
        // (exact-token compare — substring would let "do" hit "model").
        // Split trên non-alphanumeric: bỏ dấu câu ("nhieu?") + tách SKU "cc120-b-1"
        // thành {cc120,b,1} để khách gõ "cc120" vẫn match.
        var nameTokens = new HashSet<string>(SplitTokens(name), StringComparer.Ordinal);
        var qTokens = SplitTokens(normalizedQuestion)
            .Where(t => t.Length >= 3 && !QuestionStopwords.Contains(t)).ToList();
        return qTokens.Count > 0 && qTokens.All(nameTokens.Contains);
    }

    private static IEnumerable<string> SplitTokens(string s)
        => Regex.Split(s, @"[^\p{L}\p{N}]+")
            .Where(t => t.Length > 0);

    /// <summary>Filler words stripped before the reverse match — question phrasing
    /// (giá/bao nhiêu/còn không/cho hỏi…) must never be required in product names.
    /// All in normalized form (lower, no diacritics, đ→d).</summary>
    private static readonly HashSet<string> QuestionStopwords = new(StringComparer.Ordinal)
    {
        "gia", "bao", "nhieu", "tien", "la", "co", "khong", "con", "shop", "quan",
        "ben", "minh", "em", "anh", "chi", "toi", "hoi", "ve", "cua", "may",
        "cai", "loai", "dong", "mua", "ban", "tu", "van", "san", "pham", "nao",
        "gi", "dau", "nha", "nhe", "oi", "dich", "vu", "hay", "theo", "va",
        "voi", "xin", "vui", "long", "hien", "tai", "dang", "cung", "cap", "can",
        "tim", "kiem", "thong", "tin", "tiet", "xem", "duoc", "khach", "gui",
        "gom", "nhung", "mat", "hang", "thiet", "cho", "du", "di", "dc", "ko",
        "k", "kh", "ad", "admin", "oi", "nhi", "day", "nay", "kia", "do", "muon",
        "can", "thiet", "tot", "nhat", "re", "chat", "luong", "bao", "hanh"
    };

    // ---------- semantic: knowledge_chunks (pgvector / fallback) ----------

    private async Task<IReadOnlyList<ContextItem>> RetrieveChunksAsync(
        Guid tenantId, string intent, string question, CancellationToken ct)
    {
        var docTypes = DocTypesByIntent.GetValueOrDefault(intent);

        var eligible = db.KnowledgeChunks.AsNoTracking()
            .Where(c => c.TenantId == tenantId)
            .Join(db.KnowledgeDocuments.AsNoTracking()
                    .Where(d => d.ApprovalStatus == ApprovalStatus.Approved
                        && d.Classification == DocumentClassification.Public
                        && d.Status == DocumentStatus.Active),
                c => c.DocumentId, d => d.Id, (c, d) => new { Chunk = c, Doc = d });

        List<KnowledgeChunk> chunks;
        var embedding = embedder.Enabled ? await embedder.EmbedAsync(question, ct) : null;
        if (embedding != null && db.Database.IsRelational())
        {
            chunks = await RankByVectorAsync(tenantId, embedding, docTypes, ct);
        }
        else
        {
            // Test/degrade path — deterministic doc-type + keyword ranking.
            var rows = await eligible.ToListAsync(ct);
            chunks = rows
                .Where(r => docTypes == null || docTypes.Contains(r.Doc.Type))
                .Select(r => new { r.Chunk, Score = KeywordScore(question, r.Chunk.Content) })
                .Where(x => docTypes != null || x.Score > 0)
                .OrderByDescending(x => x.Score)
                .ThenBy(x => x.Chunk.DocumentId).ThenBy(x => x.Chunk.ChunkIndex)
                .Take(MaxChunks)
                .Select(x => x.Chunk)
                .ToList();
        }
        if (chunks.Count == 0) return [];

        var docIds = chunks.Select(c => c.DocumentId).Distinct().ToList();
        var docs = await db.KnowledgeDocuments.AsNoTracking()
            .Where(d => docIds.Contains(d.Id))
            .ToDictionaryAsync(d => d.Id, ct);

        return chunks
            .Where(c => docs.ContainsKey(c.DocumentId))
            .Select(c => new ContextItem(ContextItemKind.Chunk, $"chunk:{c.Id}",
                $"{docs[c.DocumentId].Title}: {c.Content}",
                docs[c.DocumentId].Classification, null, c))
            .ToList();
    }

    /// <summary>
    /// Cosine distance (<=>) over the HNSW index. Embedding is float[] in the
    /// model (converter maps to vector at the driver), so ordering happens via
    /// a scalar SQL id-ranking then EF fetch — doc-type filter applied after.
    /// </summary>
    private async Task<List<KnowledgeChunk>> RankByVectorAsync(
        Guid tenantId, float[] embedding, HashSet<string>? docTypes, CancellationToken ct)
    {
        var orderedIds = await db.Database
            .SqlQuery<Guid>($"""
                SELECT c.id FROM knowledge_chunks c
                JOIN knowledge_documents d ON d.id = c.document_id
                WHERE c.tenant_id = {tenantId} AND c.embedding IS NOT NULL
                  AND d.approval_status = 'approved' AND d.classification = 'public'
                  AND d.status = 'active'
                ORDER BY c.embedding <=> {new Vector(embedding)}
                LIMIT 20
                """)
            .ToListAsync(ct);
        if (orderedIds.Count == 0) return [];

        var idSet = orderedIds.ToHashSet();
        var rows = await db.KnowledgeChunks.AsNoTracking()
            .Where(c => idSet.Contains(c.Id))
            .Join(db.KnowledgeDocuments.AsNoTracking(),
                c => c.DocumentId, d => d.Id, (c, d) => new { Chunk = c, Doc = d })
            .ToListAsync(ct);
        // Restore vector rank order (the EF fetch is unordered).
        return rows
            .Where(r => docTypes == null || docTypes.Contains(r.Doc.Type))
            .Select(r => r.Chunk)
            .OrderBy(c => orderedIds.IndexOf(c.Id))
            .Take(MaxChunks)
            .ToList();
    }

    private static int KeywordScore(string question, string content)
    {
        var q = IntentRouter.Normalize(question);
        var c = IntentRouter.Normalize(content);
        return q.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(t => t.Length >= 4)
            .Count(t => c.Contains(t, StringComparison.Ordinal));
    }
}
