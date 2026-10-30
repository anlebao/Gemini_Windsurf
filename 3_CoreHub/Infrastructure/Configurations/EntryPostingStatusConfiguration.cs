using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VanAn.CoreHub.Infrastructure.Entities;

namespace VanAn.CoreHub.Infrastructure.Configurations
{
    /// <summary>SRS Phase A: EntryPostingStatusEntity — 1 row per AccountingEntry (PK = EntryId).</summary>
    public class EntryPostingStatusConfiguration : IEntityTypeConfiguration<EntryPostingStatusEntity>, IEntityConfiguration
    {
        public void Configure(EntityTypeBuilder<EntryPostingStatusEntity> builder)
        {
            _ = builder.HasKey(e => e.EntryId);
            _ = builder.Property(e => e.EntryId).ValueGeneratedNever();
            _ = builder.Property(e => e.TenantId).IsRequired();
            _ = builder.Property(e => e.Status).IsRequired();
            _ = builder.Property(e => e.Reason).HasMaxLength(500);
            _ = builder.Property(e => e.Attempts).IsRequired();
            _ = builder.Property(e => e.CreatedAt).IsRequired();
            _ = builder.Property(e => e.UpdatedAt).IsRequired();
            _ = builder.HasIndex(e => new { e.Status, e.UpdatedAt });
            _ = builder.HasIndex(e => e.TenantId);
        }
    }
}
