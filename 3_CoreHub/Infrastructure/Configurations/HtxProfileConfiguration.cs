using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VanAn.Shared.Domain;
using VanAn.Shared.Domain.Aggregates.MembershipAggregate;
// QUAN TRỌNG: trỏ đúng TenantAggregate.Tenant (bảng "Tenants"), KHÔNG phải record obsolete VanAn.Shared.Domain.Tenant (bảng "Tenant")
using Tenant = VanAn.Shared.Domain.Aggregates.TenantAggregate.Tenant;

namespace VanAn.CoreHub.Infrastructure.Configurations
{
    /// <summary>
    /// Membership Infrastructure (2026-09-29): EF Core configuration cho HtxProfile.
    /// Đánh dấu tenant là HTX (quyết định A1) + phiên bản Điều lệ hiện hành (consent — SRS §14).
    ///
    /// 1 HtxProfile / tenant (unique index TenantId — pattern BusinessProfileConfiguration).
    /// KHÔNG sửa TenantSettings (tránh phá 12 With methods).
    ///
    /// Auto-discovered via ApplyConfigurationsFromAssembly trong VanAnDbContext.OnModelCreating.
    /// </summary>
    public class HtxProfileConfiguration : IEntityTypeConfiguration<HtxProfile>, IEntityConfiguration
    {
        public void Configure(EntityTypeBuilder<HtxProfile> builder)
        {
            builder.ToTable("HtxProfiles");

            _ = builder.HasKey(e => e.Id);

            _ = builder.Property(e => e.CharterVersion).IsRequired().HasMaxLength(50);
            _ = builder.Property(e => e.TermsVersion).IsRequired().HasMaxLength(50);
            _ = builder.Property(e => e.CharterUrl).HasMaxLength(1000);

            // ── Audit fields từ BaseEntity ──
            _ = builder.Property(e => e.CreatedAt);
            _ = builder.Property(e => e.UpdatedAt);

            // ── FK: htx_id qua BaseEntity.TenantId — Restrict ──
            builder.HasOne<Tenant>()
                .WithMany()
                .HasForeignKey(e => e.TenantId)
                .OnDelete(DeleteBehavior.Restrict);

            // ── Indexes: 1 HtxProfile per tenant ──
            _ = builder.HasIndex(e => e.TenantId).IsUnique().HasDatabaseName("UX_HtxProfiles_TenantId");
        }
    }
}
