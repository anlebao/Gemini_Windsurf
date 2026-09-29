using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VanAn.Shared.Domain;
using VanAn.Shared.Domain.Aggregates.MembershipAggregate;
// QUAN TRỌNG: trỏ đúng TenantAggregate.Tenant (bảng "Tenants"), KHÔNG phải record obsolete VanAn.Shared.Domain.Tenant (bảng "Tenant")
using Tenant = VanAn.Shared.Domain.Aggregates.TenantAggregate.Tenant;

namespace VanAn.CoreHub.Infrastructure.Configurations
{
    /// <summary>
    /// Membership Infrastructure (2026-09-29): EF Core configuration cho ConsentRecord (SRS §14).
    /// PG-only. Audit entity — không lifecycle, chỉ ghi nhận (append-only, không sửa/xóa).
    ///
    /// Data Integrity Contract: FK thật — ApplicantCustomerId FK Customers.Id (Restrict),
    /// TenantId (htx) FK Tenants (Restrict). KHÔNG lưu nội dung tài liệu — chỉ version + evidence ref.
    ///
    /// Auto-discovered via ApplyConfigurationsFromAssembly trong VanAnDbContext.OnModelCreating.
    /// </summary>
    public class ConsentRecordConfiguration : IEntityTypeConfiguration<ConsentRecord>, IEntityConfiguration
    {
        public void Configure(EntityTypeBuilder<ConsentRecord> builder)
        {
            builder.ToTable("ConsentRecords");

            _ = builder.HasKey(e => e.Id);

            _ = builder.Property(e => e.ApplicantCustomerId).IsRequired();
            _ = builder.Property(e => e.DocumentType).HasConversion<int>().IsRequired();
            _ = builder.Property(e => e.DocumentVersion).IsRequired().HasMaxLength(50);
            _ = builder.Property(e => e.Timestamp).IsRequired();
            _ = builder.Property(e => e.EvidenceReference).HasMaxLength(500);

            // ── Audit fields từ BaseEntity ──
            _ = builder.Property(e => e.CreatedAt);
            _ = builder.Property(e => e.UpdatedAt);

            // ── FKs (FK thật + Restrict) ──
            builder.HasOne<Tenant>()
                .WithMany()
                .HasForeignKey(e => e.TenantId)
                .OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<Customer>()
                .WithMany()
                .HasForeignKey(e => e.ApplicantCustomerId)
                .OnDelete(DeleteBehavior.Restrict);

            // ── Indexes ──
            _ = builder.HasIndex(e => e.TenantId).HasDatabaseName("IX_ConsentRecords_TenantId");
            _ = builder.HasIndex(e => new { e.TenantId, e.ApplicantCustomerId })
                .HasDatabaseName("IX_ConsentRecords_HtxApplicant");
        }
    }
}
