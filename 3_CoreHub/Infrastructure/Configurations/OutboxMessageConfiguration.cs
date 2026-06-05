using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace VanAn.CoreHub.Infrastructure.Configurations
{
    /// <summary>
    /// EF Core configuration for OutboxMessage - Week 1 implementation
    /// Implements Eventual Consistency pattern for reliable event processing
    /// </summary>
    public class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>, IEntityConfiguration
    {
        public void Configure(EntityTypeBuilder<OutboxMessage> builder)
        {
            // Primary key
            _ = builder.HasKey(e => e.Id);

            // Property configurations
            _ = builder.Property(e => e.EventType)
                .IsRequired()
                .HasMaxLength(200);

            _ = builder.Property(e => e.EventData)
                .IsRequired();

            _ = builder.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP");

            _ = builder.Property(e => e.ProcessedAt)
                .IsRequired(false);

            _ = builder.Property(e => e.Error)
                .HasMaxLength(1000)
                .IsRequired(false);

            _ = builder.Property(e => e.RetryCount)
                .HasDefaultValue(0);

            _ = builder.Property(e => e.NextRetryAt)
                .IsRequired(false);

            _ = builder.Property(e => e.TenantId)
                .IsRequired();

            _ = builder.Property(e => e.Status)
                .HasConversion<int>();

            // Indexes for performance
            _ = builder.HasIndex(e => e.Status);
            _ = builder.HasIndex(e => e.CreatedAt);
            _ = builder.HasIndex(e => e.NextRetryAt);
            _ = builder.HasIndex(e => e.TenantId);

            // Composite index for efficient querying
            _ = builder.HasIndex(e => new { e.Status, e.NextRetryAt, e.TenantId });

            // Multi-tenancy query filter - will be set dynamically by VanAnDbContext
            // builder.HasQueryFilter(e => e.TenantId == Guid.Empty);
        }
    }
}
