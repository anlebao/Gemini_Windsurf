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

            // SINGLE-IDENTITY: OrderItemId is synced to Id in OrderItem.Create (Id = OrderItemId.Value).
            // Ignore — no separate DB column. Code reads entity.Id, not entity.OrderItemId.Value.
            _ = builder.Ignore(e => e.OrderItemId);


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

            // Phase 3 (Option C): Product navigation removed — Gateway PG no longer stores Products.
            // OrderItem.ProductId is now a plain Guid column (snapshot from client at checkout time).
            // Products live in ShopERP SQLite. Referential integrity is enforced at ShopERP level.
            // FK constraint dropped via migration AddOutboxRoutingKey (Phase 3).
            // builder.HasOne(e => e.Product).WithMany().HasForeignKey(e => e.ProductId).OnDelete(DeleteBehavior.Restrict);

            // Indexes
            _ = builder.HasIndex(e => new { e.TenantId, e.OrderId });
            _ = builder.HasIndex(e => e.ProductId);
            _ = builder.HasIndex(e => e.KitchenStatus);
        }
    }
}
