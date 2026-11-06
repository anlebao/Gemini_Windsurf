using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Pgvector;
using Testcontainers.PostgreSql;
using VanAn.TenantAi.Domain;
using VanAn.TenantAi.Infra;
using Xunit;

namespace VanAn.TenantAi.Tests;

/// <summary>Shared PG16+pgvector container — one per test run (image pull is the slow part).</summary>
public sealed class TenantAiPgFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _pg = new PostgreSqlBuilder()
        .WithImage("pgvector/pgvector:pg16")
        .WithDatabase("vanan_ai_test")
        .WithUsername("vanan_ai")
        .WithPassword("test-pw")
        .Build();

    public string ConnectionString => _pg.GetConnectionString();

    public Task InitializeAsync() => _pg.StartAsync();

    public async Task DisposeAsync() => await _pg.DisposeAsync();
}

[CollectionDefinition("tenantai-pg")]
public sealed class TenantAiPgCollection : ICollectionFixture<TenantAiPgFixture>;

/// <summary>task_ai_p1_schema — migration + pgvector evidence trên PG thật (không InMemory).</summary>
[Collection("tenantai-pg")]
public sealed class SchemaMigrationTests(TenantAiPgFixture pg)
{
    private TenantAiDbContext CreateDb()
        => new(new DbContextOptionsBuilder<TenantAiDbContext>()
            .UseNpgsql(pg.ConnectionString, npg => npg.UseVector())
            .UseSnakeCaseNamingConvention()
            .Options);

    private static float[] OneHot(int index)
    {
        var v = new float[TenantAiDbContext.EmbeddingDimensions];
        v[index] = 1f;
        return v;
    }

    private static float[] Mix(int i, int j)
    {
        var v = new float[TenantAiDbContext.EmbeddingDimensions];
        v[i] = 0.9f;
        v[j] = 0.1f;
        return v;
    }

    private static readonly string[] ExpectedTables =
    [
        "caller_keys", "tenant_ai_configs", "ai_policies", "knowledge_usage_permissions",
        "product_knowledge", "knowledge_documents", "knowledge_chunks",
        "conversation_sessions", "conversation_messages", "ingest_logs", "intent_routes"
    ];

    [Fact]
    public async Task Migration_applies_clean_creates_all_srs_tables_extension_and_hnsw()
    {
        await using var db = CreateDb();
        await db.Database.MigrateAsync();

        // [D2] vector extension present.
        var extensions = await db.Database
            .SqlQuery<string>($"SELECT extname FROM pg_extension")
            .ToListAsync();
        extensions.Should().Contain("vector");

        // SRS §7 — every table exists with snake_case names.
        var tables = await db.Database
            .SqlQuery<string>($"SELECT table_name FROM information_schema.tables WHERE table_schema = 'public'")
            .ToListAsync();
        tables.Should().Contain(ExpectedTables);

        // HNSW cosine index on embedding.
        var indexDef = await db.Database
            .SqlQuery<string>($"SELECT indexdef FROM pg_indexes WHERE tablename = 'knowledge_chunks' AND indexname = 'ix_knowledge_chunks_embedding'")
            .ToListAsync();
        indexDef.Should().ContainSingle().Which.Should()
            .Contain("hnsw").And.Contain("vector_cosine_ops");
    }

    [Fact]
    public async Task Vector_search_orders_by_cosine_distance()
    {
        await using var db = CreateDb();
        await db.Database.MigrateAsync();

        var tenantId = Guid.NewGuid();
        var doc = KnowledgeDocument.Create(tenantId, "faq", "Menu", "content", DocumentClassification.Public);
        doc.Approve("test");
        db.KnowledgeDocuments.Add(doc);
        var near = KnowledgeChunk.Create(tenantId, doc.Id, 0, "near", Mix(0, 1));
        var exact = KnowledgeChunk.Create(tenantId, doc.Id, 1, "exact", OneHot(0));
        var far = KnowledgeChunk.Create(tenantId, doc.Id, 2, "far", OneHot(1));
        db.KnowledgeChunks.AddRange(near, exact, far);
        await db.SaveChangesAsync();

        var query = new NpgsqlParameter("v", new Vector(OneHot(0)));
        var ordered = await db.Database
            .SqlQuery<Guid>($"SELECT id FROM knowledge_chunks WHERE tenant_id = {tenantId} ORDER BY embedding <=> {query} LIMIT 10")
            .ToListAsync();

        ordered.Should().Equal(exact.Id, near.Id, far.Id);
    }

    [Fact]
    public async Task Cross_tenant_vector_and_sql_queries_return_zero_leak()
    {
        await using var db = CreateDb();
        await db.Database.MigrateAsync();

        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        foreach (var (t, name) in new[] { (tenantA, "doc-a"), (tenantB, "doc-b") })
        {
            var doc = KnowledgeDocument.Create(t, "faq", name, "c", DocumentClassification.Public);
            doc.Approve("test");
            db.KnowledgeDocuments.Add(doc);
            db.KnowledgeChunks.Add(KnowledgeChunk.Create(t, doc.Id, 0, name, OneHot(0)));
            db.ProductKnowledge.Add(ProductKnowledge.Create(t, Guid.NewGuid(), name, 50_000m, "v1"));
        }
        await db.SaveChangesAsync();
        var chunkA = db.KnowledgeChunks.Single(c => c.TenantId == tenantA).Id;

        // Vector search filtered to tenant A → only A's chunk, B invisible (T1).
        var hits = await db.Database
            .SqlQuery<Guid>($"SELECT id FROM knowledge_chunks WHERE tenant_id = {tenantA} ORDER BY embedding <=> {new NpgsqlParameter("v", new Vector(OneHot(0)))} LIMIT 10")
            .ToListAsync();
        hits.Should().Equal(chunkA);

        var products = await db.ProductKnowledge.Where(p => p.TenantId == tenantA).ToListAsync();
        products.Should().ContainSingle().Which.Name.Should().Be("doc-a");
    }

    [Fact]
    public async Task Ingest_log_idempotency_key_is_unique_per_tenant_kind()
    {
        await using var db = CreateDb();
        await db.Database.MigrateAsync();

        var tenantId = Guid.NewGuid();
        db.IngestLogs.Add(IngestLog.Create(tenantId, "products", "key-1", 3, IngestStatus.Succeeded));
        await db.SaveChangesAsync();

        db.IngestLogs.Add(IngestLog.Create(tenantId, "products", "key-1", 3, IngestStatus.Succeeded));
        var act = () => db.SaveChangesAsync();
        await act.Should().ThrowAsync<DbUpdateException>();

        // Same key under a different kind (or tenant) is a separate idempotency scope.
        await using var db2 = CreateDb();
        db2.IngestLogs.Add(IngestLog.Create(tenantId, "documents", "key-1", 1, IngestStatus.Succeeded));
        await db2.SaveChangesAsync();
    }

    [Fact]
    public async Task Product_knowledge_replay_updates_instead_of_duplicating()
    {
        await using var db = CreateDb();
        await db.Database.MigrateAsync();

        var tenantId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        db.ProductKnowledge.Add(ProductKnowledge.Create(tenantId, productId, "Phở chay", 50_000m, "v1"));
        await db.SaveChangesAsync();

        // Same (tenant, product) raw insert → unique violation.
        db.ProductKnowledge.Add(ProductKnowledge.Create(tenantId, productId, "Phở chay dup", 50_000m, "v1"));
        var act = () => db.SaveChangesAsync();
        await act.Should().ThrowAsync<DbUpdateException>();

        // Replay via update path: same product row, new price + version (G16 freshness).
        await using var db2 = CreateDb();
        var product = await db2.ProductKnowledge.SingleAsync(p => p.TenantId == tenantId && p.ProductId == productId);
        product.UpdateFromSource("Phở chay", null, 55_000m, null, "[]", "[]", true, "Món chính", "v2");
        await db2.SaveChangesAsync();

        var rows = await db2.ProductKnowledge.Where(p => p.TenantId == tenantId && p.ProductId == productId).ToListAsync();
        rows.Should().ContainSingle().Which.Price.Should().Be(55_000m);
    }
}
