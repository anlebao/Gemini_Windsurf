using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Infrastructure.Configurations
{
    /// <summary>
    /// EF Core configuration for OrderItem entity
    /// </summary>
    public class OrderItemConfiguration : IEntityTypeConfiguration<OrderItem>, IEntityConfiguration
    {
        public void Configure(EntityTypeBuilder<OrderItem> builder)
        {
            _ = builder.HasKey(e => e.Id);

            // OrderItemId value object converter
            _ = builder.Property(e => e.OrderItemId)
                .HasConversion(id => id.Value, value => new OrderItemId(value))
                .IsRequired();


            // Note: OrderId and ProductId are Guid (not value objects) per Domain.cs

            _ = builder.Property(e => e.UnitPrice)
                .HasPrecision(18, 2);

            _ = builder.Property(e => e.VatRate)
                .HasPrecision(5, 4);

            _ = builder.Property(e => e.Notes)
                .HasMaxLength(500);

            _ = builder.Property(e => e.ProductName)
                .HasMaxLength(200);

            _ = builder.Property(e => e.ItemNoteText)
                .HasMaxLength(500);

            // Enum conversion for KitchenStatus
            _ = builder.Property(e => e.KitchenStatus)
                .HasConversion<int>();

            _ = builder.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP");

            // Navigation properties
            _ = builder.HasOne(e => e.Order)
                .WithMany(o => o.Items)
                .HasForeignKey(e => e.OrderId)
                .OnDelete(DeleteBehavior.Cascade);

            // Product navigation uses Guid (not value object)
            _ = builder.HasOne(e => e.Product)
                .WithMany()
                .HasForeignKey(e => e.ProductId)
                .OnDelete(DeleteBehavior.Restrict);

            // Indexes
            _ = builder.HasIndex(e => e.OrderItemId);
            _ = builder.HasIndex(e => new { e.TenantId, e.OrderId });
            _ = builder.HasIndex(e => e.ProductId);
            _ = builder.HasIndex(e => e.KitchenStatus);
        }
    }
}
