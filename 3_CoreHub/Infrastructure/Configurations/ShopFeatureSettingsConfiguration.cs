using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VanAn.CoreHub.Infrastructure.Entities;

namespace VanAn.CoreHub.Infrastructure.Configurations;

public class ShopFeatureSettingsConfiguration : IEntityTypeConfiguration<ShopFeatureSettingsEntity>, IEntityConfiguration
{
    public void Configure(EntityTypeBuilder<ShopFeatureSettingsEntity> builder)
    {
        builder.ToTable("ShopFeatureSettings");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedOnAdd();

        // TenantId — inherited from BaseEntity, configured globally via TenantIdConfiguration
        // but we need to ensure it's mapped here for the standalone table
        builder.Property("TenantId").IsRequired();

        builder.Property(e => e.QR_TableNumber_Enabled).IsRequired().HasDefaultValue(false);
        builder.Property(e => e.Kitchen_Workflow_Enabled).IsRequired().HasDefaultValue(true);
        builder.Property(e => e.Voice_Note_Enabled).IsRequired().HasDefaultValue(false);
        builder.Property(e => e.Loyalty_Program_Enabled).IsRequired().HasDefaultValue(true);
        builder.Property(e => e.Accounting_Sync_Enabled).IsRequired().HasDefaultValue(true);
        builder.Property(e => e.EInvoice_Auto_Export_Enabled).IsRequired().HasDefaultValue(false);
        builder.Property(e => e.VAT_Display_Enabled).IsRequired().HasDefaultValue(true);
        builder.Property(e => e.Price_Validation_Enabled).IsRequired().HasDefaultValue(false);
        builder.Property(e => e.PollingIntervalSeconds).IsRequired().HasDefaultValue(15);

        // Loyalty-C WS-A: per-tenant loyalty formula (defaults preserve backward compat)
        builder.Property(e => e.Loyalty_PointsRate).IsRequired().HasDefaultValue(0m);
        builder.Property(e => e.Loyalty_MinPointsPerOrder).IsRequired().HasDefaultValue(0);
        builder.Property(e => e.Loyalty_MaxPointsPerOrder).IsRequired(false); // nullable
        builder.Property(e => e.Loyalty_AwardOnAllOrders).IsRequired().HasDefaultValue(true);

        // Loyalty-C WS-C: per-tenant notification rules
        builder.Property(e => e.Notify_MissionCompleted).IsRequired().HasDefaultValue(true);
        builder.Property(e => e.Notify_BirthdayBonus).IsRequired().HasDefaultValue(true);
        builder.Property(e => e.Notify_RedemptionFulfilled).IsRequired().HasDefaultValue(true);
        builder.Property(e => e.Notify_RedemptionCancelled).IsRequired().HasDefaultValue(true);
        builder.Property(e => e.Notify_VoucherExpiringSoon).IsRequired().HasDefaultValue(true);
        builder.Property(e => e.VoucherExpiryNotifyHours).IsRequired().HasDefaultValue(24);

        // One row per tenant — unique index on TenantId
        builder.HasIndex("TenantId").IsUnique();
    }
}
