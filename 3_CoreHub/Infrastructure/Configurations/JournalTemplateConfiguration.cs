using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VanAn.Shared.Domain;
using VanAn.CoreHub.Infrastructure.ValueConverters;

namespace VanAn.CoreHub.Infrastructure.Configurations;

/// <summary>
/// EF Core Configuration for JournalTemplate entity
/// JournalTemplateLine and TemplateValidationRule are Value Objects (OwnsMany)
/// </summary>
public class JournalTemplateConfiguration : IEntityTypeConfiguration<JournalTemplate>
{
    public void Configure(EntityTypeBuilder<JournalTemplate> builder)
    {
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Code)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(e => e.Description)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(e => e.TenantId)
            .HasConversion<TenantIdConverter>()
            .IsRequired();

        builder.Property(e => e.CreatedAt)
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        // OwnsMany: JournalTemplateLine is a Value Object owned by JournalTemplate
        builder.OwnsMany(e => e.Lines, lineBuilder =>
        {
            lineBuilder.Property(l => l.AccountNumber).IsRequired().HasMaxLength(50);
            lineBuilder.Property(l => l.AmountFormula).HasMaxLength(200);
            lineBuilder.Property(l => l.DescriptionTemplate).HasMaxLength(500);
            lineBuilder.Ignore(l => l.IsCredit);
        });

        // OwnsMany: TemplateValidationRule is a Value Object owned by JournalTemplate
        builder.OwnsMany(e => e.ValidationRules, ruleBuilder =>
        {
            ruleBuilder.Property(r => r.Rule).IsRequired().HasMaxLength(500);
            ruleBuilder.Property(r => r.Message).HasMaxLength(500);
        });

        // BusinessRules is a primitive collection (List<string>)
        builder.Ignore(e => e.BusinessRules);

        // Indexes
        builder.HasIndex(e => new { e.TenantId, e.Code }).IsUnique();
    }
}
