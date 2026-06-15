using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Infrastructure.Configurations
{
    /// <summary>
    /// EF Core configuration for PendingInvoiceQueue entity
    /// </summary>
    public class PendingInvoiceQueueConfiguration : IEntityTypeConfiguration<PendingInvoiceQueue>, IEntityConfiguration
    {
        public void Configure(EntityTypeBuilder<PendingInvoiceQueue> builder)
        {
            _ = builder.HasKey(e => e.QueueId);

            _ = builder.Property(e => e.OrderId)
                .HasConversion(id => id.Value, value => new OrderId(value))
                .IsRequired();

            _ = builder.Property(e => e.TenantId)
                .HasConversion(id => id.Value, value => new TenantId(value))
                .IsRequired();

            _ = builder.Property(e => e.TotalAmount)
                .HasPrecision(18, 2);

            _ = builder.Property(e => e.VatAmount)
                .HasPrecision(18, 2);

            _ = builder.Property(e => e.Status)
                .HasConversion<int>()
                .IsRequired();

            _ = builder.Property(e => e.RetryCount)
                .IsRequired();

            _ = builder.Property(e => e.ErrorMessage)
                .HasMaxLength(1000);

            _ = builder.Property(e => e.ProcessedAt);

            _ = builder.Property(e => e.CreatedAt)
                .IsRequired()
                .HasDefaultValueSql("CURRENT_TIMESTAMP");

            // Index for querying pending invoices by status
            _ = builder.HasIndex(e => e.Status);

            // Composite index for tenant + status queries
            _ = builder.HasIndex(e => new { e.TenantId, e.Status });
        }
    }
}
