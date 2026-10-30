using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VanAn.CoreHub.Infrastructure.Entities;

namespace VanAn.CoreHub.Infrastructure.Configurations;

/// <summary>SRS v1.2 W2/B: AccountMappingRules — data-driven event→TK mapping (tenant &gt; regime &gt; builtin).</summary>
public class AccountMappingRuleConfiguration : IEntityTypeConfiguration<AccountMappingRuleEntity>, IEntityConfiguration
{
    public void Configure(EntityTypeBuilder<AccountMappingRuleEntity> builder)
    {
        _ = builder.ToTable("AccountMappingRules");
        _ = builder.HasKey(e => e.Id);
        _ = builder.Property(e => e.Id).ValueGeneratedOnAdd();
        _ = builder.Property(e => e.TenantId);
        _ = builder.Property(e => e.Regime).HasMaxLength(10);
        _ = builder.Property(e => e.EventCode).IsRequired().HasMaxLength(50);
        _ = builder.Property(e => e.DebitAccount).HasMaxLength(20);
        _ = builder.Property(e => e.CreditAccount).HasMaxLength(20);
        _ = builder.Property(e => e.AmountKind).HasMaxLength(20);
        _ = builder.Property(e => e.EffectiveFrom);
        _ = builder.Property(e => e.Version).IsRequired();
        _ = builder.Property(e => e.IsActive).IsRequired();
        _ = builder.Property(e => e.CreatedAt).IsRequired().HasDefaultValueSql("CURRENT_TIMESTAMP");
        _ = builder.Property(e => e.UpdatedAt).IsRequired();

        _ = builder.HasIndex(e => new { e.TenantId, e.EventCode });
        _ = builder.HasIndex(e => new { e.Regime, e.EventCode });
    }
}
