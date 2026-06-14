using Microsoft.EntityFrameworkCore;
using VanAn.CoreHub.Infrastructure.ProjectMemory.Entities;

namespace VanAn.CoreHub.Infrastructure.ProjectMemory;

/// <summary>
/// Phase 6: Project Memory - DbContext for AI task/history tracking
/// PostgreSQL with UUID, JSONB, TIMESTAMP WITH TIME ZONE
/// SQLite fallback for development/testing
/// CEI Standard: Configuration by Explicit Injection
/// </summary>
public class ProjectMemoryDbContext : DbContext
{
    private readonly bool _usePostgresFeatures;

    public ProjectMemoryDbContext(
        DbContextOptions<ProjectMemoryDbContext> options,
        bool usePostgresFeatures = false) // Default to SQLite for tests
        : base(options)
    {
        _usePostgresFeatures = usePostgresFeatures;
    }

    // Entities
    public DbSet<AiTask> Tasks { get; set; } = null!;
    public DbSet<AiFeature> Features { get; set; } = null!;
    public DbSet<AiFeatureTask> FeatureTasks { get; set; } = null!;
    public DbSet<AiDecision> Decisions { get; set; } = null!;
    public DbSet<AiAgentHistory> AgentHistories { get; set; } = null!;
    public DbSet<AiSession> Sessions { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Tasks configuration - Database-specific type mapping
        modelBuilder.Entity<AiTask>(entity =>
        {
            entity.ToTable("ai_tasks");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Title).IsRequired().HasMaxLength(500);
            entity.Property(e => e.AgentName).HasMaxLength(100);
            entity.Property(e => e.Status).HasMaxLength(50);
            entity.Property(e => e.GitBranch).HasMaxLength(200);
            entity.Property(e => e.GitCommitHash).HasMaxLength(40);

            // Apply PostgreSQL-specific features only if enabled
            if (_usePostgresFeatures)
            {
                entity.Property(e => e.Id).HasDefaultValueSql("gen_random_uuid()"); // PostgreSQL UUID
                entity.Property(e => e.MetadataJson).HasColumnType("jsonb"); // PostgreSQL JSONB
            }
            else
            {
                // Default to SQLite-compatible configuration
                entity.Property(e => e.MetadataJson).HasColumnType("TEXT"); // SQLite TEXT
            }

            entity.HasIndex(e => e.AgentName);
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.CreatedAt);
        });

        // Features configuration - Database-specific JSON handling
        modelBuilder.Entity<AiFeature>(entity =>
        {
            entity.ToTable("ai_features");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(500);
            entity.Property(e => e.Status).HasMaxLength(50);

            if (_usePostgresFeatures)
            {
                entity.Property(e => e.Id).HasDefaultValueSql("gen_random_uuid()"); // PostgreSQL UUID
                entity.Property(e => e.RelatedAdrIdsJson).HasColumnType("jsonb"); // PostgreSQL JSONB
            }
            else
            {
                entity.Property(e => e.RelatedAdrIdsJson).HasColumnType("TEXT"); // SQLite TEXT
            }
        });

        // Feature-Task junction
        modelBuilder.Entity<AiFeatureTask>(entity =>
        {
            entity.ToTable("ai_feature_tasks");
            entity.HasKey(e => new { e.FeatureId, e.TaskId });

            entity.HasOne(e => e.Feature)
                .WithMany(f => f.FeatureTasks)
                .HasForeignKey(e => e.FeatureId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Task)
                .WithMany(t => t.FeatureTasks)
                .HasForeignKey(e => e.TaskId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Decisions configuration - Database-specific JSON handling
        modelBuilder.Entity<AiDecision>(entity =>
        {
            entity.ToTable("ai_decisions");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.AdrId).HasMaxLength(50);
            entity.Property(e => e.MadeBy).HasMaxLength(100);

            if (_usePostgresFeatures)
            {
                entity.Property(e => e.Id).HasDefaultValueSql("gen_random_uuid()"); // PostgreSQL UUID
                entity.Property(e => e.ConsequencesJson).HasColumnType("jsonb"); // PostgreSQL JSONB
            }
            else
            {
                entity.Property(e => e.ConsequencesJson).HasColumnType("TEXT"); // SQLite TEXT
            }

            entity.HasIndex(e => e.AdrId);
            entity.HasIndex(e => e.MadeAt);
        });

        // Agent History configuration - Database-specific JSON handling
        modelBuilder.Entity<AiAgentHistory>(entity =>
        {
            entity.ToTable("ai_agent_history");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.AgentName).HasMaxLength(100);
            entity.Property(e => e.Action).HasMaxLength(200);

            if (_usePostgresFeatures)
            {
                entity.Property(e => e.Id).HasDefaultValueSql("gen_random_uuid()"); // PostgreSQL UUID
                entity.Property(e => e.FilesModifiedJson).HasColumnType("jsonb"); // PostgreSQL JSONB
            }
            else
            {
                entity.Property(e => e.FilesModifiedJson).HasColumnType("TEXT"); // SQLite TEXT
            }

            entity.HasOne(e => e.Task)
                .WithMany(t => t.AgentHistories)
                .HasForeignKey(e => e.TaskId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasIndex(e => e.AgentName);
            entity.HasIndex(e => e.ExecutedAt);
        });

        // Sessions configuration - Database-specific UUID default
        modelBuilder.Entity<AiSession>(entity =>
        {
            entity.ToTable("ai_sessions");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.SessionCode).HasMaxLength(10);
            entity.Property(e => e.FeatureName).HasMaxLength(500);
            entity.Property(e => e.Status).HasMaxLength(50);

            if (_usePostgresFeatures)
            {
                entity.Property(e => e.Id).HasDefaultValueSql("gen_random_uuid()"); // PostgreSQL UUID
            }

            entity.HasIndex(e => e.SessionCode);
            entity.HasIndex(e => e.FeatureName);
        });
    }
}
