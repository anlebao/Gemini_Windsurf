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
    public class JournalEntryConfiguration : IEntityTypeConfiguration<JournalEntry>, IEntityConfiguration
    {
        public void Configure(EntityTypeBuilder<JournalEntry> builder)
        {
            _ = builder.HasKey(e => e.Id);

            _ = builder.Property(e => e.JournalEntryId)
                .HasConversion<JournalEntryIdConverter>()
                .IsRequired();

            _ = builder.Property(e => e.JournalNo)
                .IsRequired()
                .HasMaxLength(50);

            // Wave 7: Map EntryDate — required for period-filtered queries in HKDBookGenerationService
            _ = builder.Property(e => e.EntryDate).IsRequired();

            _ = builder.Property(e => e.Description)
                .IsRequired()
                .HasMaxLength(500);

            _ = builder.Property(e => e.ReferenceType)
                .HasMaxLength(100);

            // VAS W1: Map ReferenceId — W0 writer sets this to order.Id, must be persisted
            _ = builder.Property(e => e.ReferenceId);

            // VAS W1: Map IsReversal — required for reversal entry identification
            _ = builder.Property(e => e.IsReversal);

            _ = builder.Property(e => e.ReversedJournalId)
                .HasConversion<JournalEntryIdConverter>();

            // OwnsMany: JournalEntryLine is a Value Object owned by JournalEntry
            _ = builder.OwnsMany(e => e.Lines, lineBuilder =>
            {
                _ = lineBuilder.HasKey(l => new { l.JournalEntryId, l.Id });
                _ = lineBuilder.Property(l => l.Id).ValueGeneratedNever(); // VAS W1: Id set by domain (sequential per entry)
                _ = lineBuilder.WithOwner().HasForeignKey(l => l.JournalEntryId);
                _ = lineBuilder.Property(l => l.AccountNumber).IsRequired().HasMaxLength(50);
                _ = lineBuilder.Property(l => l.DebitAmount).HasPrecision(18, 2);
                _ = lineBuilder.Property(l => l.CreditAmount).HasPrecision(18, 2);
                _ = lineBuilder.Property(l => l.Description).HasMaxLength(500);
            });

            // Indexes
            _ = builder.HasIndex(e => e.JournalEntryId).IsUnique();
            _ = builder.HasIndex(e => e.JournalNo).IsUnique();
            _ = builder.HasIndex(e => e.TenantId);
            // VAS W1: Index for period-filtered queries (EntryDate range scans)
            _ = builder.HasIndex(e => new { e.TenantId, e.EntryDate });
        }
    }
}
