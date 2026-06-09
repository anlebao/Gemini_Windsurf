using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VanAn.CoreHub.Infrastructure.Messaging;

namespace VanAn.CoreHub.Infrastructure.Configurations;

/// <summary>
/// EF Core configuration for ProcessedWebhookKey entity.
/// Durable idempotency store for webhook deduplication (Finding #5 fix).
/// </summary>
public class ProcessedWebhookKeyConfiguration
    : IEntityTypeConfiguration<ProcessedWebhookKey>, IEntityConfiguration
{
    public void Configure(EntityTypeBuilder<ProcessedWebhookKey> builder)
    {
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .IsRequired();

        builder.Property(e => e.IdempotencyKey)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(e => e.ProcessedAt)
            .IsRequired();

        builder.Property(e => e.CreatedAt)
            .IsRequired()
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        // Clustered unique index on IdempotencyKey — primary lookup path
        builder.HasIndex(e => e.IdempotencyKey)
            .IsUnique();
    }
}
