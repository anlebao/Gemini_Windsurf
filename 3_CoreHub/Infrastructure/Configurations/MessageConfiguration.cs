using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Infrastructure.Configurations
{
    /// <summary>
    /// EF Core configuration for Message entity (Community Commerce Sprint 0).
    /// </summary>
    public class MessageConfiguration : IEntityTypeConfiguration<Message>
    {
        public void Configure(EntityTypeBuilder<Message> builder)
        {
            _ = builder.HasKey(e => e.Id);
            _ = builder.Property(e => e.ConversationId).IsRequired();
            _ = builder.Property(e => e.SenderId).IsRequired();
            _ = builder.Property(e => e.Content).IsRequired().HasMaxLength(2000);
            _ = builder.Property(e => e.SentAt).IsRequired();
            _ = builder.HasIndex(e => e.ConversationId);
            _ = builder.Property(e => e.TenantId).IsRequired();
        }
    }
}
