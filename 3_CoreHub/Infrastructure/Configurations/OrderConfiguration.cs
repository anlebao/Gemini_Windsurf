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
    public class OrderConfiguration : IEntityTypeConfiguration<Order>, IEntityConfiguration
    {
        public void Configure(EntityTypeBuilder<Order> builder)
        {
            // STEP 1.2: Use OwnsOne for Value Objects
            // CustomerInfo is a Value Object that should be saved as columns in Order table
            _ = builder.OwnsOne(o => o.CustomerInfo, customerInfoBuilder =>
            {
                _ = customerInfoBuilder.Property(ci => ci.FullName).HasMaxLength(200);
                _ = customerInfoBuilder.Property(ci => ci.PhoneNumber).HasMaxLength(50);
                _ = customerInfoBuilder.Property(ci => ci.Email).HasMaxLength(200);
                _ = customerInfoBuilder.Property(ci => ci.Address).HasMaxLength(500);
                _ = customerInfoBuilder.Property(ci => ci.Notes).HasMaxLength(1000);
            });

            // Use BaseEntity.Id as primary key (Guid) - OrderItem.OrderId FK is Guid
            _ = builder.HasKey(o => o.Id);

            // Order.OrderId property is synced to Order.Id in Order.Create (single identity).
            // Explicitly ignore — no separate DB column (UUIDv7 refactor).
            _ = builder.Ignore(o => o.OrderId);

            // Configure index for CustomerId for faster queries
            _ = builder.HasIndex(o => o.CustomerId);

            // Configure index for OrderDate for sorting
            _ = builder.HasIndex(o => o.OrderDate);

            // Configure Status as required with value converter for OrderStatusId
            _ = builder.Property(o => o.Status)
                .HasConversion(id => id.Value, value => new OrderStatusId(value))
                .IsRequired();

            // Configure OrderType with default value
            _ = builder.Property(o => o.OrderType)
                .HasDefaultValue("DINEIN")
                .IsRequired();

            // Configure financial properties
            _ = builder.Property(o => o.SubTotal).HasPrecision(18, 2);
            _ = builder.Property(o => o.TotalVatAmount).HasPrecision(18, 2);
            _ = builder.Property(o => o.ShippingFee).HasPrecision(18, 2);
            _ = builder.Property(o => o.DiscountAmount).HasPrecision(18, 2);
            _ = builder.Property(o => o.TotalAmount).HasPrecision(18, 2);

            // String property constraints
            _ = builder.Property(o => o.TextCommand).HasMaxLength(500);
            _ = builder.Property(o => o.VoiceCommandUrl).HasMaxLength(500);
            _ = builder.Property(o => o.PaymentMethod).HasMaxLength(20);
            _ = builder.Property(o => o.PaymentStatus).HasMaxLength(20);
            _ = builder.Property(o => o.VietQR_TransactionId).HasMaxLength(100);
            _ = builder.Property(o => o.CustomerNotes).HasMaxLength(1000);
            _ = builder.Property(o => o.StaffNotes).HasMaxLength(1000);
            _ = builder.Property(o => o.TrackingCode).HasMaxLength(50);
            _ = builder.Property(o => o.OrderDate).HasDefaultValueSql("CURRENT_TIMESTAMP");

            // Wave 5 (approved 2026-07-03): Per-order industry sector override (TT 152 S2a/S2b).
            // Nullable — existing orders get NULL, falls back to Tenant.DefaultIndustrySector.
            _ = builder.Property(o => o.IndustrySector)
                .HasConversion<int?>();

            // Community Commerce Sprint 0 — 8 new nullable fields (v1.1: +ReferralProductId)
            _ = builder.Property(o => o.ShipperId);
            _ = builder.Property(o => o.SalesmanId);
            _ = builder.Property(o => o.ReferralCode).HasMaxLength(30); // composite "{salesmanCode}|{productShortCode}"
            _ = builder.Property(o => o.ReferralProductId); // v1.1 NEW
            _ = builder.Property(o => o.DeliveryLat);
            _ = builder.Property(o => o.DeliveryLng);
            _ = builder.Property(o => o.CodAmount).HasPrecision(18, 2);
            _ = builder.Property(o => o.CodCollectedAt);
            _ = builder.HasIndex(o => o.ShipperId);
            _ = builder.HasIndex(o => o.SalesmanId);
            _ = builder.HasIndex(o => o.ReferralProductId); // v1.1 NEW

            // Sprint 7 — Commerce Mode Toggle (additive, nullable except CommerceMode)
            _ = builder.Property(o => o.CommerceMode)
                .HasConversion<int>()
                .HasDefaultValue(CommerceMode.Marketplace)
                .IsRequired();
            _ = builder.Property(o => o.CostPrice).HasPrecision(18, 2);
            _ = builder.Property(o => o.SellPrice).HasPrecision(18, 2);
            _ = builder.Property(o => o.PlatformMargin).HasPrecision(18, 2);
            _ = builder.Property(o => o.DeliveryFee).HasPrecision(18, 2);
            _ = builder.Property(o => o.PlatformFeeRate).HasPrecision(18, 4);
            _ = builder.Property(o => o.CommunityFundRate).HasPrecision(18, 4);
            _ = builder.HasIndex(o => o.CommerceMode);

            // Navigation properties
            _ = builder.HasOne(o => o.Customer)
                  .WithMany(c => c.Orders)
                  .HasForeignKey(o => o.CustomerId)
                  .OnDelete(DeleteBehavior.SetNull);

            // TenantId value object converter
            _ = builder.Property(o => o.TenantId)
                .IsRequired()
                .HasConversion(id => id.Value, value => new TenantId(value));
        }
    }
}
