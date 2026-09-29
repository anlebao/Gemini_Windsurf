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
    /// Membership Infrastructure (2026-09-29): EF Core configuration cho MembershipDocument (SRS §17.2).
    /// PG-only (A3). Append-only evidence — không sửa/xóa.
    ///
    /// Data Integrity Contract: FK thật — ApplicationId FK MembershipApplications.Id (Restrict),
    /// TenantId (htx) FK Tenants (Restrict).
    ///
    /// Auto-discovered via ApplyConfigurationsFromAssembly trong VanAnDbContext.OnModelCreating.
    /// </summary>
    public class MembershipDocumentConfiguration : IEntityTypeConfiguration<MembershipDocument>, IEntityConfiguration
    {
        public void Configure(EntityTypeBuilder<MembershipDocument> builder)
        {
            builder.ToTable("MembershipDocuments");

            _ = builder.HasKey(e => e.Id);

            _ = builder.Property(e => e.ApplicationId).IsRequired();
            _ = builder.Property(e => e.DocumentType).HasConversion<int>().IsRequired();
            _ = builder.Property(e => e.DocumentVersion).HasMaxLength(50);
            _ = builder.Property(e => e.StorageReference).IsRequired().HasMaxLength(1000);
            _ = builder.Property(e => e.Hash).HasMaxLength(128);

            // ── Audit fields từ BaseEntity ──
            _ = builder.Property(e => e.CreatedAt);
            _ = builder.Property(e => e.UpdatedAt);

            // ── FKs (FK thật + Restrict — Data Integrity Contract) ──
            builder.HasOne<MembershipApplication>()
                .WithMany()
                .HasForeignKey(e => e.ApplicationId)
                .OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<Tenant>()
                .WithMany()
                .HasForeignKey(e => e.TenantId)
                .OnDelete(DeleteBehavior.Restrict);

            // ── Indexes ──
            _ = builder.HasIndex(e => e.TenantId).HasDatabaseName("IX_MembershipDocuments_TenantId");
            _ = builder.HasIndex(e => e.ApplicationId).HasDatabaseName("IX_MembershipDocuments_ApplicationId");
        }
    }
}
