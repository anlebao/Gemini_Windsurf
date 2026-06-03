using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VanAn.Shared.Domain;
using CoreAccountingEntry = VanAn.Shared.Domain.AccountingEntry;

namespace VanAn.CoreHub.Infrastructure.Configurations
{
    /// <summary>
    /// EF Core configuration for AccountingEntry - Week 1 implementation
    /// Implements 5-layer protection: Domain, EF Core, Repository, Service, API
    /// </summary>
    public class AccountingEntryConfiguration : IEntityTypeConfiguration<CoreAccountingEntry>
    {
        public void Configure(EntityTypeBuilder<CoreAccountingEntry> builder)
        {
            // Primary key
            builder.HasKey(e => e.Id);

            // Property configurations - NO CONVERTERS for SQLite compatibility
            // Store as primitive types, handle conversion in repository/service layer

            builder.Property(e => e.Id)
                .ValueGeneratedOnAdd();

            builder.Property(e => e.AccountingBookType)
                .HasConversion<int>();

            builder.Property(e => e.EntryType)
                .HasConversion<int>();

            builder.Property(e => e.VatRate)
                .HasConversion<int>();

            builder.Property(e => e.PeriodYear)
                .HasConversion<int>();

            builder.Property(e => e.PeriodMonth)
                .HasConversion<int>();

            builder.Property(e => e.Amount)
                .HasPrecision(18, 2);

            builder.Property(e => e.Description)
                .IsRequired()
                .HasMaxLength(500);

            // Use inline TenantId conversion for SQLite compatibility
            builder.Property(e => e.TenantId)
                .IsRequired()
                .HasConversion(
                    id => id.Value,
                    value => new TenantId(value))
                .Metadata.SetValueComparer(new Microsoft.EntityFrameworkCore.ChangeTracking.ValueComparer<TenantId>(
                    (c1, c2) => c1.Value == c2.Value,
                    c => c.Value.GetHashCode(),
                    c => new TenantId(c.Value)));

            builder.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP");

            builder.Property(e => e.ReversalEntryId);
            // Note: ReversalEntryId is already Guid?, no converter needed

            // Indexes for performance with 4 HKD Books
            builder.HasIndex(e => new { e.TenantId, e.AccountingBookType });
            builder.HasIndex(e => new { e.TenantId, e.PeriodYear, e.PeriodMonth });
            builder.HasIndex(e => e.ReversalEntryId);

            // Multi-tenancy query filter - will be set dynamically by VanAnDbContext
            // builder.HasQueryFilter(e => e.TenantId == Guid.Empty);

            // Configure ReversalEntry relationship
            builder.HasOne<CoreAccountingEntry>()
                .WithMany()
                .HasForeignKey(e => e.ReversalEntryId)
                .OnDelete(DeleteBehavior.Restrict); // No cascade delete for immutability
        }
    }
}
