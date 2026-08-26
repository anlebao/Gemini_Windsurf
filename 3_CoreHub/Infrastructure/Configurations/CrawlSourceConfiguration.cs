using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VanAn.Shared.Domain.Aggregates.TenantAggregate;

namespace VanAn.CoreHub.Infrastructure.Configurations
{
    /// <summary>
    /// Crawl-to-Onboard Pipeline (2026-08-25): EF Core configuration for CrawlSource audit entity.
    /// Maps to "CrawlSources" table in PostgreSQL (CoreHub — Gateway source of truth per Option C).
    /// NOT mirrored to ShopERP SQLite (PG-only — audit trail lives with Gateway).
    ///
    /// Single-Identity Pattern (correction C1): FK to Tenants is via BaseEntity.TenantId
    /// (TenantId value object — TenantId.Value == Tenants.Id PK after Single-Identity refactor).
    /// No separate Guid TenantId property — BaseEntity.TenantId IS the FK.
    /// TenantId mapped via global TenantIdConverter (no explicit HasConversion needed).
    ///
    /// Cascade delete: if tenant deleted, audit trail deleted with it (audit provenance
    /// is meaningless without the tenant it refers to).
    ///
    /// Auto-discovered via ApplyConfigurationsFromAssembly in VanAnDbContext.OnModelCreating.
    /// </summary>
    public class CrawlSourceConfiguration : IEntityTypeConfiguration<CrawlSource>, IEntityConfiguration
    {
        public void Configure(EntityTypeBuilder<CrawlSource> builder)
        {
            builder.ToTable("CrawlSources");

            _ = builder.HasKey(e => e.Id);

            // Provenance fields
            _ = builder.Property(e => e.SourceSite).IsRequired().HasMaxLength(100);
            _ = builder.Property(e => e.SourceUrl).IsRequired().HasMaxLength(1000);
            // RawJson: unbounded text — store full API response or scraped HTML for audit.
            // PostgreSQL 'text' type (no max length) — handles large responses.
            _ = builder.Property(e => e.RawJson).IsRequired();
            _ = builder.Property(e => e.CrawledAt).IsRequired();

            // Audit fields from BaseEntity
            _ = builder.Property(e => e.CreatedAt);
            _ = builder.Property(e => e.UpdatedAt);

            // FK to Tenants via BaseEntity.TenantId — Cascade delete
            // (audit trail deleted with tenant — provenance meaningless without tenant)
            builder.HasOne<Tenant>()
                .WithMany()
                .HasForeignKey(e => e.TenantId)
                .OnDelete(DeleteBehavior.Cascade);

            // Index for tenant-scoped queries (list crawl sources for a tenant)
            _ = builder.HasIndex(e => e.TenantId).HasDatabaseName("IX_CrawlSources_TenantId");
        }
    }
}
