using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Infrastructure.Configurations
{
    /// <summary>
    /// EF Core configuration for CommunityRole entity (Community Commerce Sprint 0).
    /// </summary>
    public class CommunityRoleConfiguration : IEntityTypeConfiguration<CommunityRole>
    {
        public void Configure(EntityTypeBuilder<CommunityRole> builder)
        {
            _ = builder.HasKey(e => e.Id);
            _ = builder.Property(e => e.CustomerId).IsRequired();
            _ = builder.Property(e => e.RoleType).HasConversion<int>().IsRequired();
            _ = builder.Property(e => e.ActivatedBy).IsRequired();
            _ = builder.Property(e => e.ActivatedAt).IsRequired();
            _ = builder.Property(e => e.IsActive).IsRequired();
            _ = builder.Property(e => e.SalesmanCode).HasMaxLength(10);
            // Filtered unique index — only non-null SalesmanCode values are unique (Shipper has null)
            _ = builder.HasIndex(e => e.SalesmanCode)
                .IsUnique()
                .HasFilter("\"SalesmanCode\" IS NOT NULL");
            _ = builder.HasIndex(e => new { e.CustomerId, e.RoleType, e.IsActive });
            _ = builder.Property(e => e.TenantId).IsRequired();
        }
    }
}
