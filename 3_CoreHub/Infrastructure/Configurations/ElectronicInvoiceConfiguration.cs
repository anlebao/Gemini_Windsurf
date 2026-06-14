using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Infrastructure.Configurations
{
    /// <summary>
    /// EF Core configuration for ElectronicInvoice entity
    /// Supports atomic transaction with OutboxMessage (Finding #3 fix)
    /// </summary>
    public class ElectronicInvoiceConfiguration : IEntityTypeConfiguration<ElectronicInvoice>, IEntityConfiguration
    {
        public void Configure(EntityTypeBuilder<ElectronicInvoice> builder)
        {
            _ = builder.HasKey(e => e.InvoiceId);

            _ = builder.Property(e => e.InvoiceId)
                .HasConversion(id => id.Value, value => new ElectronicInvoiceId(value))
                .IsRequired();

            _ = builder.Property(e => e.OrderId)
                .IsRequired();

            _ = builder.Property(e => e.IdempotencyKey)
                .HasConversion(k => k.Value, value => new InvoiceIdempotencyKey(value))
                .IsRequired()
                .HasMaxLength(200);

            _ = builder.Property(e => e.InvoiceType)
                .HasConversion<int>()
                .IsRequired();

            _ = builder.Property(e => e.Amount)
                .HasPrecision(18, 2);

            _ = builder.Property(e => e.VatAmount)
                .HasPrecision(18, 2);

            _ = builder.Property(e => e.TotalAmount)
                .HasPrecision(18, 2);

            _ = builder.Property(e => e.CustomerName)
                .IsRequired()
                .HasMaxLength(200);

            _ = builder.Property(e => e.CustomerTaxCode)
                .IsRequired()
                .HasMaxLength(20);

            _ = builder.Property(e => e.CustomerAddress)
                .HasMaxLength(500);

            _ = builder.Property(e => e.Status)
                .HasConversion<int>()
                .IsRequired();

            _ = builder.Property(e => e.CurrentProvider)
                .HasConversion(
                    p => p == null ? null : p.Value,
                    value => value == null ? null : new ProviderId(value))
                .HasMaxLength(100);

            _ = builder.Property(e => e.SubmittedAt);
            _ = builder.Property(e => e.ApprovedAt);

            _ = builder.Property(e => e.ProviderInvoiceNumber)
                .HasMaxLength(100);

            _ = builder.Property(e => e.FailureReason)
                .HasMaxLength(1000);

            // Unique index on IdempotencyKey (legal compliance — prevent duplicate invoices)
            _ = builder.HasIndex(e => e.IdempotencyKey)
                .IsUnique();

            // Composite index for tenant + status queries
            _ = builder.HasIndex(e => new { e.TenantId, e.Status });

            // Ignore navigation properties not mapped as separate tables here
            _ = builder.Ignore(e => e.SubmitAttempts);
            _ = builder.Ignore(e => e.OutboxEvent);
        }
    }
}
