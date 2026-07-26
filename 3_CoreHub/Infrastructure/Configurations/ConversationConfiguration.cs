using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Infrastructure.Configurations
{
    /// <summary>
    /// EF Core configuration for Conversation entity (Community Commerce Sprint 0).
    /// 1 conversation per Order (unique index on OrderId).
    /// </summary>
    public class ConversationConfiguration : IEntityTypeConfiguration<Conversation>
    {
        public void Configure(EntityTypeBuilder<Conversation> builder)
        {
            _ = builder.HasKey(e => e.Id);
            _ = builder.Property(e => e.OrderId).IsRequired();
            _ = builder.Property(e => e.ShipperId).IsRequired();
            _ = builder.Property(e => e.CustomerId).IsRequired();
            _ = builder.HasIndex(e => e.OrderId).IsUnique(); // 1 conversation per order
            _ = builder.Property(e => e.TenantId).IsRequired();
        }
    }
}
