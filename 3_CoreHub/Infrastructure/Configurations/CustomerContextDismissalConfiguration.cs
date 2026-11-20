using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VanAn.CoreHub.Infrastructure.Entities;
using VanAn.CoreHub.Infrastructure.Configurations;

namespace VanAn.CoreHub.Infrastructure.Configurations;

/// <summary>
/// EF config cho <see cref="CustomerContextDismissalEntity"/> — picked up bởi
/// ShopERPDbContext.ApplyConfigurationsFromAssembly (tên kết thúc "Configuration"
/// + implements IEntityConfiguration).
/// </summary>
public class CustomerContextDismissalConfiguration : IEntityTypeConfiguration<CustomerContextDismissalEntity>, IEntityConfiguration
{
    public void Configure(EntityTypeBuilder<CustomerContextDismissalEntity> builder)
    {
        _ = builder.HasKey(e => e.Id);
        _ = builder.Property(e => e.CardId).IsRequired().HasMaxLength(64);
        _ = builder.Property(e => e.DismissedAt).IsRequired();
        _ = builder.Property(e => e.ExpiresAt).IsRequired();

        // Một dismissal active per (tenant, customer, card) — re-dismiss = update row này.
        _ = builder.HasIndex(e => new { e.TenantId, e.CustomerId, e.CardId }).IsUnique();
        _ = builder.HasIndex(e => new { e.CustomerId, e.ExpiresAt });
    }
}
