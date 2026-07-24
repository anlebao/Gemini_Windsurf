using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Infrastructure.Configurations;

/// <summary>
/// Loyalty-C WS-B: EF Core configuration for Mission entity.
/// Tenant-scoped (ShopERP SQLite). Admin-managed gamification tasks.
/// </summary>
public class MissionConfiguration : IEntityTypeConfiguration<Mission>, IEntityConfiguration
{
    public void Configure(EntityTypeBuilder<Mission> builder)
    {
        builder.ToTable("Missions");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedOnAdd();

        builder.Property("TenantId").IsRequired();
        builder.Property(e => e.MissionType).IsRequired().HasConversion<int>();
        builder.Property(e => e.Title).IsRequired().HasMaxLength(200);
        builder.Property(e => e.Description).HasMaxLength(1000);
        builder.Property(e => e.PointsReward).IsRequired();
        builder.Property(e => e.IsOneTime).IsRequired();
        builder.Property(e => e.DailyCap); // nullable int
        builder.Property(e => e.IsActive).IsRequired();
        builder.Property(e => e.SortOrder).IsRequired();
        builder.Property(e => e.Config).HasMaxLength(2000); // JSON config

        // Standard audit fields from BaseEntity
        builder.Property(e => e.CreatedAt).IsRequired();
        builder.Property(e => e.UpdatedAt).IsRequired();
        builder.Property(e => e.IsDeleted).IsRequired().HasDefaultValue(false);

        // Index: tenant + active missions (for customer browse)
        builder.HasIndex("TenantId", nameof(Mission.IsActive));
    }
}
