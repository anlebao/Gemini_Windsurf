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

            // FK relationship to Customer — 1:N (Batch 2): a customer holds one Silo row per
            // awarding tenant. Was 1:1 → EF treated a second row for the same customer as an orphan
            // and DELETED the previous row (cross-tenant point merging, RC2.4).
            _ = builder.HasOne(e => e.Customer)
                .WithMany(c => c.LoyaltyRewards)
                .HasForeignKey(e => e.CustomerId)
                .OnDelete(DeleteBehavior.Cascade);

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
