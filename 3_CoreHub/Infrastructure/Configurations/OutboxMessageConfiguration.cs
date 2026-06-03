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
            builder.HasKey(e => e.Id);

            // Property configurations
            builder.Property(e => e.EventType)
                .IsRequired()
                .HasMaxLength(200);

            builder.Property(e => e.EventData)
                .IsRequired();

            builder.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP");

            builder.Property(e => e.ProcessedAt)
                .IsRequired(false);

            builder.Property(e => e.Error)
                .HasMaxLength(1000)
                .IsRequired(false);

            builder.Property(e => e.RetryCount)
                .HasDefaultValue(0);

            builder.Property(e => e.NextRetryAt)
                .IsRequired(false);

            builder.Property(e => e.TenantId)
                .IsRequired();

            builder.Property(e => e.Status)
                .HasConversion<int>();

            // Indexes for performance
            builder.HasIndex(e => e.Status);
            builder.HasIndex(e => e.CreatedAt);
            builder.HasIndex(e => e.NextRetryAt);
            builder.HasIndex(e => e.TenantId);

            // Composite index for efficient querying
            builder.HasIndex(e => new { e.Status, e.NextRetryAt, e.TenantId });

            // Multi-tenancy query filter - will be set dynamically by VanAnDbContext
            // builder.HasQueryFilter(e => e.TenantId == Guid.Empty);
        }
    }
}
