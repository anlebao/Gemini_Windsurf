using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VanAn.Shared.Domain;
using VanAn.Shared.Domain.Aggregates.MembershipAggregate;
using VanAn.CoreHub.Infrastructure.ValueConverters;
// QUAN TRỌNG: trỏ đúng TenantAggregate.Tenant (bảng "Tenants"), KHÔNG phải record obsolete VanAn.Shared.Domain.Tenant (bảng "Tenant")
using Tenant = VanAn.Shared.Domain.Aggregates.TenantAggregate.Tenant;

namespace VanAn.CoreHub.Infrastructure.Configurations
{
    /// <summary>
    /// Membership Infrastructure (2026-09-29): EF Core configuration cho MembershipApplication aggregate.
    /// Maps to "MembershipApplications" table trong PostgreSQL (CoreHub — Gateway source of truth, A3).
    /// NOT mirrored to ShopERP SQLite.
    ///
    /// Data Integrity Contract (master_plan mục 6):
    /// - BaseEntity.TenantId = htx_id (FK Tenants — Restrict).
    /// - ApplicantCustomerId = FK Customers.Id (Restrict) — Vạn An Network ID.
    /// - BusinessTenantId? = FK Tenants.Id (Restrict) — tư cách HKD/DN (self-membership guard ở domain).
    /// - Single-Identity Pattern: mọi FK là Guid/Guid? — KHÔNG business key VO.
    ///
    /// Auto-discovered via ApplyConfigurationsFromAssembly trong VanAnDbContext.OnModelCreating.
    /// </summary>
    public class MembershipApplicationConfiguration : IEntityTypeConfiguration<MembershipApplication>, IEntityConfiguration
    {
        public void Configure(EntityTypeBuilder<MembershipApplication> builder)
        {
            builder.ToTable("MembershipApplications");

            _ = builder.HasKey(e => e.Id);

            // ── Party (polymorphic — applicant luôn là customer; business là optional) ──
            _ = builder.Property(e => e.ApplicantCustomerId).IsRequired();
            // TenantId? VO → TEXT (pattern Tenant.PredecessorTenantId — global convention không cover nullable)
            _ = builder.Property(e => e.BusinessTenantId)
                .HasConversion(new TenantIdConverter());

            // ── Enums → int ──
            _ = builder.Property(e => e.MembershipType).HasConversion<int>().IsRequired();
            _ = builder.Property(e => e.Status).HasConversion<int>().IsRequired();
            _ = builder.Property(e => e.ExpectedRole).HasConversion<int>().IsRequired();
            _ = builder.Property(e => e.IdentityVerificationLevel).HasConversion<int>().IsRequired();
            _ = builder.Property(e => e.CapitalFeeStatus).HasConversion<int>().IsRequired();

            // ── Applicant Profile snapshot (SRS §13) ──
            _ = builder.Property(e => e.FullName).IsRequired().HasMaxLength(200);
            _ = builder.Property(e => e.PhoneNumber).IsRequired().HasMaxLength(50);
            _ = builder.Property(e => e.Email).HasMaxLength(200);
            _ = builder.Property(e => e.Region).HasMaxLength(200);

            // ── Consent snapshot (SRS §14) ──
            _ = builder.Property(e => e.ConsentVersion).HasMaxLength(50);
            _ = builder.Property(e => e.CharterVersion).HasMaxLength(50);

            // ── Lifecycle ──
            _ = builder.Property(e => e.SubmittedAt);
            _ = builder.Property(e => e.ReviewedByUserId);
            _ = builder.Property(e => e.ReviewedAt);
            _ = builder.Property(e => e.RejectionReason).HasMaxLength(1000);
            _ = builder.Property(e => e.NeedInfoReason).HasMaxLength(1000);

            // ── Audit fields từ BaseEntity ──
            _ = builder.Property(e => e.CreatedAt);
            _ = builder.Property(e => e.UpdatedAt);

            // ── FKs (Data Integrity Contract — FK thật, không Guid-ref) ──
            // htx_id qua BaseEntity.TenantId (TenantIdConverter global) — Restrict
            builder.HasOne<Tenant>()
                .WithMany()
                .HasForeignKey(e => e.TenantId)
                .OnDelete(DeleteBehavior.Restrict);
            // Vạn An Network ID
            builder.HasOne<Customer>()
                .WithMany()
                .HasForeignKey(e => e.ApplicantCustomerId)
                .OnDelete(DeleteBehavior.Restrict);
            // Tư cách HKD/DN (nullable)
            builder.HasOne<Tenant>()
                .WithMany()
                .HasForeignKey(e => e.BusinessTenantId)
                .OnDelete(DeleteBehavior.Restrict);

            // ── Indexes ──
            _ = builder.HasIndex(e => e.TenantId).HasDatabaseName("IX_MembershipApplications_TenantId");
            _ = builder.HasIndex(e => e.Status).HasDatabaseName("IX_MembershipApplications_Status");
            _ = builder.HasIndex(e => new { e.TenantId, e.ApplicantCustomerId })
                .HasDatabaseName("IX_MembershipApplications_HtxApplicant");
        }
    }
}
