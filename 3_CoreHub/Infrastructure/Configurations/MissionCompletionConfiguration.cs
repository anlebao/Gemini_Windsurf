using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Infrastructure.Configurations;

/// <summary>
/// Loyalty-C WS-B: EF Core configuration for MissionCompletion entity.
/// Tenant-scoped (ShopERP SQLite). Records each mission completion by a customer.
/// </summary>
public class MissionCompletionConfiguration : IEntityTypeConfiguration<MissionCompletion>, IEntityConfiguration
{
    public void Configure(EntityTypeBuilder<MissionCompletion> builder)
    {
        builder.ToTable("MissionCompletions");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedOnAdd();

        builder.Property("TenantId").IsRequired();
        builder.Property(e => e.CustomerId).IsRequired();
        builder.Property(e => e.MissionId).IsRequired();
        builder.Property(e => e.CompletedAt).IsRequired();
        builder.Property(e => e.PointsAwarded).IsRequired();
        builder.Property(e => e.Metadata).HasMaxLength(2000); // JSON — e.g., share URL

        // Standard audit fields from BaseEntity
        builder.Property(e => e.CreatedAt).IsRequired();
        builder.Property(e => e.UpdatedAt).IsRequired();
        builder.Property(e => e.IsDeleted).IsRequired().HasDefaultValue(false);

        // FK: MissionCompletion → Mission
        builder.HasOne(e => e.Mission)
            .WithMany()
            .HasForeignKey(e => e.MissionId)
            .OnDelete(DeleteBehavior.Restrict);

        // Index: customer + mission (for completion history query + one-time/daily cap check)
        builder.HasIndex(e => e.CustomerId);
        builder.HasIndex(e => new { e.CustomerId, e.MissionId, e.CompletedAt });
    }
}
