using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VanAn.Shared.Domain.Aggregates.TenantAggregate;

namespace VanAn.CoreHub.Infrastructure.Configurations
{
    /// <summary>
    /// GTM Drill Machine W2 (2026-09-08, D3): EF Core configuration for TenantRegistration entity.
    /// Maps to "TenantRegistrations" table in PostgreSQL (CoreHub — Gateway source of truth per Option C).
    /// NOT mirrored to ShopERP SQLite (PG-only — registration leads live with Gateway).
    ///
    /// NOT tenant-scoped: TenantId = Guid.Empty sentinel (merchant chưa thuộc tenant nào).
    /// NO FK to Tenants (unlike CrawlSource — registration is pre-tenant, no tenant exists yet).
    /// Excluded from multi-tenancy query filter in VanAnDbContext.ApplyMultiTenancyFilters.
    ///
    /// Auto-discovered via ApplyConfigurationsFromAssembly in VanAnDbContext.OnModelCreating.
    /// </summary>
    public class TenantRegistrationConfiguration : IEntityTypeConfiguration<TenantRegistration>, IEntityConfiguration
    {
        public void Configure(EntityTypeBuilder<TenantRegistration> builder)
        {
            builder.ToTable("TenantRegistrations");

            _ = builder.HasKey(e => e.Id);

            // Merchant info
            _ = builder.Property(e => e.ShopName).IsRequired().HasMaxLength(200);
            _ = builder.Property(e => e.Industry).HasMaxLength(100);
            _ = builder.Property(e => e.LogoUrl).HasMaxLength(1000);

            // Contact info
            _ = builder.Property(e => e.ContactName).IsRequired().HasMaxLength(200);
            _ = builder.Property(e => e.ContactPhone).IsRequired().HasMaxLength(50);
            _ = builder.Property(e => e.ContactEmail).HasMaxLength(200);

            // Source tracking
            _ = builder.Property(e => e.Source).IsRequired().HasMaxLength(20);

            // Anti-bot
            _ = builder.Property(e => e.TurnstileVerified).IsRequired();

            // Lifecycle
            _ = builder.Property(e => e.Status).IsRequired();
            _ = builder.Property(e => e.SubmittedAt).IsRequired();

            // Review
            _ = builder.Property(e => e.ReviewedByUserId);
            _ = builder.Property(e => e.ReviewedAt);
            _ = builder.Property(e => e.RejectionReason).HasMaxLength(500);

            // Onboarding result
            _ = builder.Property(e => e.OnboardedTenantId);

            // Audit fields from BaseEntity
            _ = builder.Property(e => e.CreatedAt);
            _ = builder.Property(e => e.UpdatedAt);

            // Indexes for admin queue queries
            _ = builder.HasIndex(e => e.Status).HasDatabaseName("IX_TenantRegistrations_Status");
            _ = builder.HasIndex(e => e.SubmittedAt).HasDatabaseName("IX_TenantRegistrations_SubmittedAt");
        }
    }
}
