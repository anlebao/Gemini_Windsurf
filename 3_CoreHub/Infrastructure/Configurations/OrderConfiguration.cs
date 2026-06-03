using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Infrastructure.Configurations
{
    /// <summary>
    /// EF Core Configuration for Order entity
    /// STEP 1: Fix EF Core Model - Use OwnsOne for Value Objects
    /// CustomerInfo is a Value Object that MUST be saved as columns within Order table
    /// </summary>
    public class OrderConfiguration : IEntityTypeConfiguration<Order>
    {
        public void Configure(EntityTypeBuilder<Order> builder)
        {
            // STEP 1.2: Use OwnsOne for Value Objects
            // CustomerInfo is a Value Object that should be saved as columns in Order table
            builder.OwnsOne(o => o.CustomerInfo, customerInfoBuilder =>
            {
                customerInfoBuilder.Property(ci => ci.FullName).HasMaxLength(200);
                customerInfoBuilder.Property(ci => ci.PhoneNumber).HasMaxLength(50);
                customerInfoBuilder.Property(ci => ci.Email).HasMaxLength(200);
                customerInfoBuilder.Property(ci => ci.Address).HasMaxLength(500);
                customerInfoBuilder.Property(ci => ci.Notes).HasMaxLength(1000);
            });

            // Use BaseEntity.Id as primary key (Guid) - OrderItem.OrderId FK is Guid
            builder.HasKey(o => o.Id);

            // Configure index for CustomerId for faster queries
            builder.HasIndex(o => o.CustomerId);

            // Configure index for OrderDate for sorting
            builder.HasIndex(o => o.OrderDate);

            // Configure Status as required
            builder.Property(o => o.Status).IsRequired();

            // Configure OrderType with default value
            builder.Property(o => o.OrderType)
                .HasDefaultValue("DINEIN")
                .IsRequired();

            // Configure financial properties
            builder.Property(o => o.SubTotal).HasPrecision(18, 2);
            builder.Property(o => o.TotalVatAmount).HasPrecision(18, 2);
            builder.Property(o => o.ShippingFee).HasPrecision(18, 2);
            builder.Property(o => o.DiscountAmount).HasPrecision(18, 2);
            builder.Property(o => o.TotalAmount).HasPrecision(18, 2);

            // String property constraints
            builder.Property(o => o.TextCommand).HasMaxLength(500);
            builder.Property(o => o.VoiceCommandUrl).HasMaxLength(500);
            builder.Property(o => o.PaymentMethod).HasMaxLength(20);
            builder.Property(o => o.PaymentStatus).HasMaxLength(20);
            builder.Property(o => o.VietQR_TransactionId).HasMaxLength(100);
            builder.Property(o => o.CustomerNotes).HasMaxLength(1000);
            builder.Property(o => o.StaffNotes).HasMaxLength(1000);
            builder.Property(o => o.TrackingCode).HasMaxLength(50);
            builder.Property(o => o.OrderDate).HasDefaultValueSql("CURRENT_TIMESTAMP");

            // Navigation properties
            builder.HasOne(o => o.Customer)
                  .WithMany(c => c.Orders)
                  .HasForeignKey(o => o.CustomerId)
                  .OnDelete(DeleteBehavior.SetNull);
        }
    }
}
