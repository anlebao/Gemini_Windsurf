using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Infrastructure.Configurations
{
    /// <summary>
    /// EF Core configuration for Shop entity
    /// </summary>
    public class ShopConfiguration : IEntityTypeConfiguration<Shop>, IEntityConfiguration
    {
        public void Configure(EntityTypeBuilder<Shop> builder)
        {
            _ = builder.HasKey(e => e.Id);


            _ = builder.Property(e => e.Name)
                .IsRequired()
                .HasMaxLength(200);

            _ = builder.Property(e => e.Address)
                .HasMaxLength(500);

            _ = builder.Property(e => e.Phone)
                .HasMaxLength(20);

            _ = builder.Property(e => e.Email)
                .HasMaxLength(100);

            _ = builder.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP");

            // Indexes
            _ = builder.HasIndex(e => new { e.TenantId, e.Name });
        }
    }
}
