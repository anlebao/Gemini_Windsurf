using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VanAn.Shared.Domain;
using VanAn.CoreHub.Infrastructure.ValueConverters;
using CoreAccountingEntry = VanAn.Shared.Domain.AccountingEntry;

namespace VanAn.CoreHub.Infrastructure.Configurations;

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
        
        // Property configurations with correct converters
        builder.Property(e => e.Id)
            .HasConversion<AccountingEntryIdConverter>();
        
        builder.Property(e => e.AccountingBookType)
            .HasConversion<AccountingBookTypeConverter>();
        
        builder.Property(e => e.PeriodYear);
        builder.Property(e => e.PeriodMonth);
        
        builder.Property(e => e.Amount)
            .HasConversion<MoneyConverter>()
            .HasPrecision(18, 2); // Financial precision
        
        builder.Property(e => e.Description)
            .IsRequired()
            .HasMaxLength(500);
        
        builder.Property(e => e.TenantId)
            .HasConversion<TenantIdConverter>()
            .IsRequired();
        
        builder.Property(e => e.CreatedAt)
            .HasDefaultValueSql("CURRENT_TIMESTAMP");
        
        builder.Property(e => e.ReversalEntryId)
            .HasConversion<AccountingEntryIdConverter>();
        
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
