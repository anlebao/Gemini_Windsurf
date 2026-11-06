using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Pgvector;
using VanAn.TenantAi.Domain;

namespace VanAn.TenantAi.Infra;

/// <summary>
/// TenantAI-owned store (PostgreSQL + pgvector — SRS v1.1 [D2]).
/// No FK to Vạn An tables — tenantId is an opaque signed caller claim (SRS [G1]/[D7]).
/// Snake_case naming applied at options level (UseSnakeCaseNamingConvention) to match SRS §7.
/// </summary>
public sealed class TenantAiDbContext(DbContextOptions<TenantAiDbContext> options) : DbContext(options)
{
    /// <summary>
    /// Embedding dims fixed at 768 (gemini-embedding-001 + outputDimensionality=768 — P7 model verify: text-embedding-004 không tồn tại).
    /// Changing dims = new migration + re-embed, per task_ai_p1_schema.
    /// </summary>
    public const int EmbeddingDimensions = 768;

    public DbSet<CallerKey> CallerKeys => Set<CallerKey>();
    public DbSet<TenantAiConfig> TenantAiConfigs => Set<TenantAiConfig>();
    public DbSet<AiPolicy> AiPolicies => Set<AiPolicy>();
    public DbSet<KnowledgeUsagePermission> KnowledgeUsagePermissions => Set<KnowledgeUsagePermission>();
    public DbSet<ProductKnowledge> ProductKnowledge => Set<ProductKnowledge>();
    public DbSet<KnowledgeDocument> KnowledgeDocuments => Set<KnowledgeDocument>();
    public DbSet<KnowledgeChunk> KnowledgeChunks => Set<KnowledgeChunk>();
    public DbSet<ConversationSession> ConversationSessions => Set<ConversationSession>();
    public DbSet<ConversationMessage> ConversationMessages => Set<ConversationMessage>();
    public DbSet<IngestLog> IngestLogs => Set<IngestLog>();
    public DbSet<IntentRoute> IntentRoutes => Set<IntentRoute>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // pgvector — extension created by migration (SRS [D2]).
        modelBuilder.HasPostgresExtension("vector");

        modelBuilder.Entity<CallerKey>(b =>
        {
            b.HasKey(e => e.Id);
            b.HasIndex(e => e.CallerId).IsUnique();
            b.Property(e => e.CallerId).HasMaxLength(64);
            b.Property(e => e.KeyHash).HasMaxLength(64);
            b.Property(e => e.ScopesJson).HasColumnType("jsonb").HasDefaultValue("[]");
            b.Property(e => e.TenantScopeJson).HasColumnType("jsonb");
        });

        // SRS §7 — append-only per version; public chat resolves newest Published.
        modelBuilder.Entity<TenantAiConfig>(b =>
        {
            b.HasIndex(e => new { e.TenantId, e.Version }).IsUnique();
            b.HasIndex(e => new { e.TenantId, e.Status });
            b.Property(e => e.Status).HasConversion<SnakeCaseEnumConverter<TenantAiConfigStatus>>().HasMaxLength(32);
            b.Property(e => e.AiName).HasMaxLength(128);
            b.Property(e => e.PersonaJson).HasColumnType("jsonb");
            b.Property(e => e.BusinessProfileJson).HasColumnType("jsonb");
            b.Property(e => e.PublishedBy).HasMaxLength(128);
        });

        modelBuilder.Entity<AiPolicy>(b =>
        {
            b.HasIndex(e => new { e.TenantId, e.ConfigVersion }).IsUnique();
            b.Property(e => e.IntentScopesJson).HasColumnType("jsonb");
            b.Property(e => e.ExternalAllowJson).HasColumnType("jsonb");
            b.Property(e => e.RefusedTopicsJson).HasColumnType("jsonb");
        });

        // [G15] intent × classification × channel matrix; TenantId null = system default.
        modelBuilder.Entity<KnowledgeUsagePermission>(b =>
        {
            b.HasIndex(e => new { e.TenantId, e.Intent, e.Channel });
            b.Property(e => e.Intent).HasMaxLength(64);
            b.Property(e => e.Classification).HasConversion<SnakeCaseEnumConverter<DocumentClassification>>().HasMaxLength(32);
            b.Property(e => e.Channel).HasMaxLength(32);
            b.Property(e => e.Permission).HasConversion<SnakeCaseEnumConverter<UsagePermission>>().HasMaxLength(32);
        });

        // Curated ShopERP mirror — one row per (tenant, product), replace-by-source sync.
        modelBuilder.Entity<ProductKnowledge>(b =>
        {
            b.HasIndex(e => e.TenantId);
            b.HasIndex(e => new { e.TenantId, e.ProductId }).IsUnique();
            b.HasIndex(e => new { e.TenantId, e.Available });
            b.Property(e => e.Name).HasMaxLength(256);
            b.Property(e => e.Price).HasPrecision(18, 2);
            b.Property(e => e.IngredientsJson).HasColumnType("jsonb");
            b.Property(e => e.AllergensJson).HasColumnType("jsonb");
            b.Property(e => e.CategoryName).HasMaxLength(128);
            b.Property(e => e.SourceVersion).HasMaxLength(64);
        });

        // [G14] approval gate — public retrieval reads approved + active only.
        modelBuilder.Entity<KnowledgeDocument>(b =>
        {
            b.HasIndex(e => new { e.TenantId, e.Type });
            b.HasIndex(e => new { e.TenantId, e.ApprovalStatus });
            b.Property(e => e.Type).HasMaxLength(32);
            b.Property(e => e.Title).HasMaxLength(256);
            b.Property(e => e.Classification).HasConversion<SnakeCaseEnumConverter<DocumentClassification>>().HasMaxLength(32);
            b.Property(e => e.ApprovalStatus).HasConversion<SnakeCaseEnumConverter<ApprovalStatus>>().HasMaxLength(32);
            b.Property(e => e.Status).HasConversion<SnakeCaseEnumConverter<DocumentStatus>>().HasMaxLength(32);
            b.Property(e => e.ApprovedBy).HasMaxLength(128);
            b.Property(e => e.RejectionReason).HasMaxLength(512);
            b.Property(e => e.SourceRef).HasMaxLength(256);
        });

        modelBuilder.Entity<KnowledgeChunk>(b =>
        {
            b.HasIndex(e => e.TenantId);
            b.HasIndex(e => new { e.DocumentId, e.ChunkIndex }).IsUnique();
            b.HasOne<KnowledgeDocument>()
                .WithMany()
                .HasForeignKey(e => e.DocumentId)
                .OnDelete(DeleteBehavior.Cascade);
            // pgvector mapping only on Npgsql — InMemory (WAF tests) has no Vector type.
            if (Database.IsNpgsql())
            {
                b.Property(e => e.Embedding)
                    .HasConversion(
                        v => v == null ? null : new Vector(v),
                        v => v == null ? null : v.ToArray(),
                        new ValueComparer<float[]>(
                            (a, b) => a!.SequenceEqual(b!),
                            v => v.Aggregate(0, (h, f) => (h * 31) ^ f.GetHashCode()),
                            v => v.ToArray()))
                    .HasColumnType($"vector({EmbeddingDimensions})");
                // ANN index — cosine distance (<=>), HNSW default per SRS [D2].
                b.HasIndex(e => e.Embedding)
                    .HasMethod("hnsw")
                    .HasOperators("vector_cosine_ops");
            }
        });

        modelBuilder.Entity<ConversationSession>(b =>
        {
            b.HasIndex(e => new { e.TenantId, e.SessionId }).IsUnique();
            b.Property(e => e.SessionId).HasMaxLength(64);
            b.Property(e => e.Channel).HasMaxLength(32);
            b.Property(e => e.MetaJson).HasColumnType("jsonb");
        });

        // [G8] MessageId dedupe per session · [G10] expires_at cleanup index.
        modelBuilder.Entity<ConversationMessage>(b =>
        {
            b.HasIndex(e => new { e.TenantId, e.SessionId, e.MessageId }).IsUnique();
            b.HasIndex(e => new { e.TenantId, e.SessionId, e.CreatedAtUtc });
            b.HasIndex(e => e.ExpiresAtUtc);
            b.Property(e => e.SessionId).HasMaxLength(64);
            b.Property(e => e.MessageId).HasMaxLength(64);
            b.Property(e => e.Role).HasConversion<SnakeCaseEnumConverter<MessageRole>>().HasMaxLength(32);
            b.Property(e => e.Intent).HasMaxLength(64);
            b.Property(e => e.ProductRefsJson).HasColumnType("jsonb");
            b.Property(e => e.SourceIdsJson).HasColumnType("jsonb");
            b.Property(e => e.RefuseReason).HasMaxLength(512);
        });

        // [G8] ingest dedupe — replay hits the unique key instead of double-applying.
        modelBuilder.Entity<IngestLog>(b =>
        {
            b.HasIndex(e => new { e.TenantId, e.Kind, e.IdempotencyKey }).IsUnique();
            b.Property(e => e.Kind).HasMaxLength(32);
            b.Property(e => e.IdempotencyKey).HasMaxLength(128);
            b.Property(e => e.Status).HasConversion<SnakeCaseEnumConverter<IngestStatus>>().HasMaxLength(32);
            b.Property(e => e.Error).HasMaxLength(2000);
        });

        modelBuilder.Entity<IntentRoute>(b =>
        {
            b.HasIndex(e => new { e.TenantId, e.Intent }).IsUnique();
            // PG unique treats NULLs as distinct — one system default row per intent needs a partial index.
            b.HasIndex(e => e.Intent).IsUnique().HasFilter("tenant_id IS NULL");
            b.Property(e => e.Intent).HasMaxLength(64);
            b.Property(e => e.AllowedSourceFlags).HasConversion<long>();
            b.Property(e => e.RefuseTemplate).HasMaxLength(1024);
        });
    }
}
