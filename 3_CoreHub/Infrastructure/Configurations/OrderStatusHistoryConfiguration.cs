using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Infrastructure.Configurations
{
    /// <summary>
    /// Issue #195 (2026-10-19, approved Q2): EF Core configuration for OrderStatusHistory.
    /// Tồn tại CẢ PG (VanAnDbContext) và ShopERP SQLite (ShopERPDbContext — ghi tại
    /// OrderWorkflowService khi transition ở POS/kitchen/UI). Append-only.
    /// Single-Identity: Ignore business key VO — chỉ map BaseEntity.Id.
    /// </summary>
    public class OrderStatusHistoryConfiguration : IEntityTypeConfiguration<OrderStatusHistory>, IEntityConfiguration
    {
        public void Configure(EntityTypeBuilder<OrderStatusHistory> builder)
        {
            builder.ToTable("OrderStatusHistories");

            builder.HasKey(e => e.Id);
            builder.Ignore(e => e.OrderStatusHistoryId);

            builder.Property(e => e.OrderId).IsRequired();
            builder.Property(e => e.FromStatus).HasMaxLength(50);
            builder.Property(e => e.ToStatus).IsRequired().HasMaxLength(50);
            builder.Property(e => e.ChangedBy).HasMaxLength(200);
            builder.Property(e => e.Reason).HasMaxLength(500);

            builder.Property(e => e.TenantId).IsRequired();

            builder.HasIndex(e => new { e.TenantId, e.OrderId, e.CreatedAt });

            builder.HasQueryFilter(e => !e.IsDeleted);

            builder.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            builder.Property(e => e.UpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            builder.Property(e => e.IsDeleted).HasDefaultValue(false);
        }
    }
}
