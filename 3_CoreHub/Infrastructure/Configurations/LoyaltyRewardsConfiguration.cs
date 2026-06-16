using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Infrastructure.Configurations
{
    /// <summary>
    /// EF Core configuration for LoyaltyRewards entity
    /// </summary>
    public class LoyaltyRewardsConfiguration : IEntityTypeConfiguration<LoyaltyRewards>, IEntityConfiguration
    {
        public void Configure(EntityTypeBuilder<LoyaltyRewards> builder)
        {
            _ = builder.HasKey(e => e.Id);


            // Note: CustomerId is Guid (not value object)

            _ = builder.Property(e => e.PointBalance)
                .HasDefaultValue(0);

            _ = builder.Property(e => e.History)
                .HasMaxLength(2000);

            _ = builder.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP");

            // Indexes
            _ = builder.HasIndex(e => new { e.TenantId, e.CustomerId }).IsUnique();
        }
    }
}
