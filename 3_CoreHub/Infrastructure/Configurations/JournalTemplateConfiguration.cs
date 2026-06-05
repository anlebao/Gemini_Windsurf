using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VanAn.Shared.Domain;
using VanAn.CoreHub.Infrastructure.ValueConverters;

namespace VanAn.CoreHub.Infrastructure.Configurations
{
    /// <summary>
    /// EF Core Configuration for JournalTemplate entity
    /// JournalTemplateLine and TemplateValidationRule are Value Objects (OwnsMany)
    /// </summary>
    public class JournalTemplateConfiguration : IEntityTypeConfiguration<JournalTemplate>
    {
        public void Configure(EntityTypeBuilder<JournalTemplate> builder)
        {
            _ = builder.HasKey(e => e.Id);

            _ = builder.Property(e => e.Code)
                .IsRequired()
                .HasMaxLength(50);

            _ = builder.Property(e => e.Description)
                .IsRequired()
                .HasMaxLength(500);

            _ = builder.Property(e => e.TenantId)
                .HasConversion<TenantIdConverter>()
                .IsRequired();

            _ = builder.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP");

            // OwnsMany: JournalTemplateLine is a Value Object owned by JournalTemplate
            _ = builder.OwnsMany(e => e.Lines, lineBuilder =>
            {
                _ = lineBuilder.Property(l => l.AccountNumber).IsRequired().HasMaxLength(50);
                _ = lineBuilder.Property(l => l.AmountFormula).HasMaxLength(200);
                _ = lineBuilder.Property(l => l.DescriptionTemplate).HasMaxLength(500);
                _ = lineBuilder.Ignore(l => l.IsCredit);
            });

            // OwnsMany: TemplateValidationRule is a Value Object owned by JournalTemplate
            _ = builder.OwnsMany(e => e.ValidationRules, ruleBuilder =>
            {
                _ = ruleBuilder.Property(r => r.Rule).IsRequired().HasMaxLength(500);
                _ = ruleBuilder.Property(r => r.Message).HasMaxLength(500);
            });

            // BusinessRules is a primitive collection (List<string>)
            _ = builder.Ignore(e => e.BusinessRules);

            // Indexes
            _ = builder.HasIndex(e => new { e.TenantId, e.Code }).IsUnique();
        }
    }
}
