using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VanAn.Shared.Domain.Aggregates.ApiKeyAggregate;

namespace VanAn.CoreHub.Infrastructure.Configurations
{
    /// <summary>
    /// Wave 14: EF Core configuration for ApiKey entity.
    /// Per-tenant, no global query filter (ApiKey is looked up by raw Id before TenantId is known).
    /// </summary>
    public class ApiKeyConfiguration : IEntityTypeConfiguration<ApiKey>, IEntityConfiguration
    {
        public void Configure(EntityTypeBuilder<ApiKey> builder)
        {
            builder.ToTable("ApiKeys");
            builder.HasKey(k => k.Id);
            builder.Property(k => k.TenantId).IsRequired();
            builder.Property(k => k.Name).IsRequired().HasMaxLength(100);
            builder.Property(k => k.SecretHash).IsRequired().HasMaxLength(200);
            builder.Property(k => k.IsActive).IsRequired();
            builder.Property(k => k.CreatedAt).IsRequired();
            builder.Property(k => k.ExpiresAt).IsRequired();
            builder.Property(k => k.LastUsedAt);
            builder.Property(k => k.RevokedAt);

            // Index for fast lookup by TenantId
            builder.HasIndex(k => k.TenantId);
            // Unique name per tenant
            builder.HasIndex(k => new { k.TenantId, k.Name }).IsUnique();
        }
    }
}
