using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Infrastructure.Configurations
{
    /// <summary>
    /// EF Core configuration for ConversationParticipant (Realtime Platform P2, 2026-09-17).
    /// N participants per conversation with a role code — the generic counterpart to the fixed
    /// ShipperId/CustomerId pair on Conversation. PG-only, like the other community entities.
    /// </summary>
    public class ConversationParticipantConfiguration : IEntityTypeConfiguration<ConversationParticipant>
    {
        public void Configure(EntityTypeBuilder<ConversationParticipant> builder)
        {
            _ = builder.HasKey(e => e.Id);
            _ = builder.Property(e => e.ConversationId).IsRequired();
            _ = builder.Property(e => e.ParticipantId).IsRequired();
            _ = builder.Property(e => e.RoleCode).IsRequired().HasMaxLength(32);
            _ = builder.Property(e => e.JoinedAt).IsRequired();
            _ = builder.Property(e => e.IsActive).IsRequired();
            // One row per (conversation, participant) — a participant rejoining reactivates the row.
            _ = builder.HasIndex(e => new { e.ConversationId, e.ParticipantId }).IsUnique();
            _ = builder.HasIndex(e => e.ParticipantId); // "my conversations" lookup
            _ = builder.Property(e => e.TenantId).IsRequired();
        }
    }
}
