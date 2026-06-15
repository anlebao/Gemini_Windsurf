using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Infrastructure.Configurations
{
    /// <summary>
    /// EF Core configuration for InvoiceItem entity
    /// </summary>
    public class InvoiceItemConfiguration : IEntityTypeConfiguration<InvoiceItem>, IEntityConfiguration
    {
        public void Configure(EntityTypeBuilder<InvoiceItem> builder)
        {
            _ = builder.HasKey(e => e.Id);

            _ = builder.Property(e => e.Id)
                .HasConversion(id => id.Value, value => new InvoiceItemId(value))
                .IsRequired();

            _ = builder.Property(e => e.InvoiceId)
                .HasConversion(id => id.Value, value => new ElectronicInvoiceId(value))
                .IsRequired();

            _ = builder.Property(e => e.ItemCode)
                .IsRequired()
                .HasMaxLength(50);

            _ = builder.Property(e => e.ItemName)
                .IsRequired()
                .HasMaxLength(200);

            _ = builder.Property(e => e.Unit)
                .IsRequired()
                .HasMaxLength(20);

            _ = builder.Property(e => e.Quantity)
                .HasPrecision(18, 4);

            _ = builder.Property(e => e.UnitPrice)
                .HasPrecision(18, 2);

            _ = builder.Property(e => e.VatRate)
                .HasPrecision(5, 4);

            _ = builder.Property(e => e.Amount)
                .HasPrecision(18, 2);

            _ = builder.Property(e => e.VatAmount)
                .HasPrecision(18, 2);

            // Navigation property
            _ = builder.HasOne(e => e.Invoice)
                  .WithMany(i => i.Items)
                  .HasForeignKey(e => e.InvoiceId)
                  .HasPrincipalKey(i => i.InvoiceId)
                  .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
