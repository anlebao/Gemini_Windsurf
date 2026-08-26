using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VanAn.Shared.Domain.Aggregates.TenantAggregate;

namespace VanAn.CoreHub.Infrastructure.Configurations
{
    /// <summary>
    /// Crawl-to-Onboard Pipeline (2026-08-25): EF Core configuration for TenantClaimRequest aggregate.
    /// Maps to "TenantClaimRequests" table in PostgreSQL (CoreHub — Gateway source of truth per Option C).
    /// NOT mirrored to ShopERP SQLite (PG-only).
    ///
    /// Single-Identity Pattern (correction C1): FK to Tenants is via BaseEntity.TenantId
    /// (TenantId value object — TenantId.Value == Tenants.Id PK after Single-Identity refactor).
    /// No separate Guid TenantId property — BaseEntity.TenantId IS the FK.
    /// TenantId mapped via global TenantIdConverter (no explicit HasConversion needed).
    ///
    /// Auto-discovered via ApplyConfigurationsFromAssembly in VanAnDbContext.OnModelCreating.
    /// </summary>
    public class TenantClaimRequestConfiguration : IEntityTypeConfiguration<TenantClaimRequest>, IEntityConfiguration
    {
        public void Configure(EntityTypeBuilder<TenantClaimRequest> builder)
        {
            builder.ToTable("TenantClaimRequests");

            _ = builder.HasKey(e => e.Id);

            // Claimant info (from owner Claim form)
            _ = builder.Property(e => e.ClaimantName).IsRequired().HasMaxLength(200);
            _ = builder.Property(e => e.ClaimantPhone).IsRequired().HasMaxLength(50);
            _ = builder.Property(e => e.ClaimantEmail).HasMaxLength(200);
            _ = builder.Property(e => e.GpkdImageUrl).IsRequired().HasMaxLength(1000);
            _ = builder.Property(e => e.TaxCodeSubmitted).IsRequired().HasMaxLength(20);

            // Lifecycle: ClaimStatus enum → int (Submitted=0, Approved=1, Rejected=2)
            _ = builder.Property(e => e.Status)
                .HasConversion<int>()
                .IsRequired();

            _ = builder.Property(e => e.SubmittedAt).IsRequired();

            // Review (set by SysAdmin)
            _ = builder.Property(e => e.ReviewedByUserId);
            _ = builder.Property(e => e.ReviewedAt);
            _ = builder.Property(e => e.RejectionReason).HasMaxLength(1000);

            // Audit fields from BaseEntity
            _ = builder.Property(e => e.CreatedAt);
            _ = builder.Property(e => e.UpdatedAt);

            // FK to Tenants via BaseEntity.TenantId — Restrict delete (don't delete claims when tenant deleted).
            // TenantId is the FK column (mapped via global TenantIdConverter in VanAnDbContext.OnModelCreating).
            builder.HasOne<Tenant>()
                .WithMany()
                .HasForeignKey(e => e.TenantId)
                .OnDelete(DeleteBehavior.Restrict);

            // Indexes for queue queries
            _ = builder.HasIndex(e => e.TenantId).HasDatabaseName("IX_TenantClaimRequests_TenantId");
            _ = builder.HasIndex(e => e.Status).HasDatabaseName("IX_TenantClaimRequests_Status");
        }
    }
}
