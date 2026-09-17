using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Infrastructure.Configurations
{
    /// <summary>
    /// EF Core configuration for Conversation entity (Community Commerce Sprint 0).
    /// Realtime Platform P2 (2026-09-17): uniqueness moved from OrderId to
    /// (TenantId, SubjectType, SubjectId) so a conversation can be keyed by any subject
    /// (Shop, Ticket, Shipment...) — an OrderId-only unique index would collide on the second
    /// non-order conversation in the whole database (all of them carry OrderId = Guid.Empty).
    /// OrderId keeps a plain index: legacy queries still filter by OrderId
    /// (ChatService.GetOrCreateConversationAsync).
    /// </summary>
    public class ConversationConfiguration : IEntityTypeConfiguration<Conversation>
    {
        public void Configure(EntityTypeBuilder<Conversation> builder)
        {
            _ = builder.HasKey(e => e.Id);
            _ = builder.Property(e => e.OrderId).IsRequired();
            _ = builder.Property(e => e.ShipperId).IsRequired();
            _ = builder.Property(e => e.CustomerId).IsRequired();
            _ = builder.Property(e => e.SubjectType).IsRequired().HasMaxLength(32)
                .HasDefaultValue(RealtimeSubjectType.Order.ToString());
            _ = builder.Property(e => e.SubjectId).IsRequired();
            _ = builder.HasIndex(e => e.OrderId); // legacy lookup path — no longer unique
            _ = builder.HasIndex(e => new { e.TenantId, e.SubjectType, e.SubjectId }).IsUnique(); // 1 conversation per subject
            _ = builder.Property(e => e.TenantId).IsRequired();
        }
    }
}
