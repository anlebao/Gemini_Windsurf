using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Infrastructure.Configurations
{
    /// <summary>
    /// EF Core configuration for AssistantLink (UNIVERSAL AI ASSISTANT W1 — hosted AI Link).
    /// PG-only (Gateway VanAnDbContext). Tenant-scoped via BaseEntity/IMustHaveTenant.
    /// Slug unique toàn hệ (public resolution không biết tenant trước).
    /// </summary>
    public class AssistantLinkConfiguration : IEntityTypeConfiguration<AssistantLink>, IEntityConfiguration
    {
        public void Configure(EntityTypeBuilder<AssistantLink> builder)
        {
            _ = builder.HasKey(e => e.Id);

            _ = builder.Property(e => e.Slug).IsRequired().HasMaxLength(32);
            _ = builder.Property(e => e.Label).HasMaxLength(128);
            _ = builder.Property(e => e.IsActive).IsRequired();
            _ = builder.Property(e => e.RevokedBy).HasMaxLength(256);

            _ = builder.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP");

            _ = builder.HasIndex(e => e.Slug).IsUnique();
            _ = builder.HasIndex(e => e.TenantId);
        }
    }
}
