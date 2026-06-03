using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VanAn.Shared.Domain;
using VanAn.CoreHub.Infrastructure.ValueConverters;

namespace VanAn.CoreHub.Infrastructure.Configurations
{
    /// <summary>
    /// EF Core Configuration for JournalEntry entity
    /// JournalEntryLine is a Value Object owned by JournalEntry (OwnsMany)
    /// </summary>
    public class JournalEntryConfiguration : IEntityTypeConfiguration<JournalEntry>
    {
        public void Configure(EntityTypeBuilder<JournalEntry> builder)
        {
            builder.HasKey(e => e.Id);

            builder.Property(e => e.JournalEntryId)
                .HasConversion<JournalEntryIdConverter>()
                .IsRequired();

            builder.Property(e => e.JournalNo)
                .IsRequired()
                .HasMaxLength(50);

            builder.Property(e => e.Description)
                .IsRequired()
                .HasMaxLength(500);

            builder.Property(e => e.ReferenceType)
                .HasMaxLength(100);

            builder.Property(e => e.TenantId)
                .HasConversion<TenantIdConverter>()
                .IsRequired();

            builder.Property(e => e.ReversedJournalId)
                .HasConversion<JournalEntryIdConverter>();

            // OwnsMany: JournalEntryLine is a Value Object owned by JournalEntry
            builder.OwnsMany(e => e.Lines, lineBuilder =>
            {
                lineBuilder.WithOwner().HasForeignKey(l => l.JournalEntryId);
                lineBuilder.Property(l => l.AccountNumber).IsRequired().HasMaxLength(50);
                lineBuilder.Property(l => l.DebitAmount).HasPrecision(18, 2);
                lineBuilder.Property(l => l.CreditAmount).HasPrecision(18, 2);
                lineBuilder.Property(l => l.Description).HasMaxLength(500);
            });

            // Indexes
            builder.HasIndex(e => e.JournalEntryId).IsUnique();
            builder.HasIndex(e => e.JournalNo).IsUnique();
            builder.HasIndex(e => e.TenantId);
        }
    }
}
