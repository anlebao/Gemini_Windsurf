using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Infrastructure.Configurations
{
    /// <summary>
    /// EF Core configuration for VaIIeTenantConfig (VA-IIE Phase 3-4 — per-tenant config forecast + bot alert).
    /// 1 row / tenant (unique TenantId index). Applied to cả ShopERP SQLite + PG (tables exist, PG stays empty).
    /// </summary>
    public class VaIIeTenantConfigConfiguration : IEntityTypeConfiguration<VaIIeTenantConfig>, IEntityConfiguration
    {
        public void Configure(EntityTypeBuilder<VaIIeTenantConfig> builder)
        {
            _ = builder.HasKey(e => e.Id);

            // SINGLE-IDENTITY: VaIIeTenantConfigId is synced to Id in constructor (Id = VaIIeTenantConfigId.Value).
            // Ignore — no separate DB column. Code reads entity.Id, not entity.VaIIeTenantConfigId.Value.
            _ = builder.Ignore(e => e.VaIIeTenantConfigId);

            _ = builder.Property(e => e.AvgDailyConsumptionWindowDays)
                .HasDefaultValue(14);
            _ = builder.Property(e => e.LeadTimeDays)
                .HasDefaultValue(2);
            _ = builder.Property(e => e.SafetyDays)
                .HasDefaultValue(1);

            _ = builder.Property(e => e.TelegramBotToken)
                .HasMaxLength(512);
            _ = builder.Property(e => e.TelegramChatId)
                .HasMaxLength(128);
            _ = builder.Property(e => e.ZaloAccessToken)
                .HasMaxLength(512);
            _ = builder.Property(e => e.ZaloRecipientId)
                .HasMaxLength(128);

            _ = builder.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP");

            // 1 row / tenant
            _ = builder.HasIndex(e => e.TenantId)
                .IsUnique();
        }
    }
}
