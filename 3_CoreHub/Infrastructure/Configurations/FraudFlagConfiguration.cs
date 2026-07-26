using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Infrastructure.Configurations
{
    /// <summary>
    /// EF Core configuration for FraudFlag entity (Community Commerce Sprint 0 v1.2 NEW).
    /// Admin review queue. 3-strike ban logic in Sprint 6.
    /// </summary>
    public class FraudFlagConfiguration : IEntityTypeConfiguration<FraudFlag>
    {
        public void Configure(EntityTypeBuilder<FraudFlag> builder)
        {
            _ = builder.HasKey(e => e.Id);
            _ = builder.Property(e => e.EntityType).HasConversion<int>().IsRequired();
            _ = builder.Property(e => e.EntityId).IsRequired();
            _ = builder.Property(e => e.FlagType).HasConversion<int>().IsRequired();
            _ = builder.Property(e => e.RiskFactors).IsRequired(); // JSON
            _ = builder.Property(e => e.Description).IsRequired().HasMaxLength(500);
            _ = builder.Property(e => e.Status).HasConversion<int>().IsRequired();
            _ = builder.Property(e => e.ReviewNote).HasMaxLength(500);
            _ = builder.Property(e => e.TenantId).IsRequired();
            _ = builder.HasIndex(e => new { e.Status, e.CreatedAt }); // admin dashboard pending flags sort by date
            _ = builder.HasIndex(e => new { e.EntityType, e.EntityId }); // query flags per entity
            _ = builder.HasIndex(e => e.CustomerId); // 3-strike check
        }
    }
}
