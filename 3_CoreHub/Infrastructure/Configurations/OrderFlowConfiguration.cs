using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Infrastructure.Configurations
{
    /// <summary>
    /// Issue #195 (2026-10-19, approved Q1/Q4): EF Core configuration for OrderFlowProfile
    /// + OrderFlowStep. PG-only conceptually (Gateway source of truth — ShopERP đọc qua
    /// Gateway tenant API), config implements IEntityConfiguration theo pattern
    /// TenantNotification (DbSet contract trên cả 2 context — SQLite không query).
    /// Single-Identity: Ignore business key VO — chỉ map BaseEntity.Id.
    /// </summary>
    public class OrderFlowProfileConfiguration : IEntityTypeConfiguration<OrderFlowProfile>, IEntityConfiguration
    {
        public void Configure(EntityTypeBuilder<OrderFlowProfile> builder)
        {
            builder.ToTable("OrderFlowProfiles");

            builder.HasKey(e => e.Id);
            builder.Ignore(e => e.OrderFlowProfileId);

            builder.Property(e => e.Scope)
                .HasConversion<int>()
                .IsRequired();

            builder.Property(e => e.SourceType).HasMaxLength(20);
            builder.Property(e => e.CategoryKey).HasMaxLength(100);
            builder.Property(e => e.Name).IsRequired().HasMaxLength(200);
            builder.Property(e => e.IsActive).HasDefaultValue(true);

            builder.Property(e => e.TenantId).IsRequired();

            builder.HasMany(e => e.Steps)
                .WithOne()
                .HasForeignKey(s => s.ProfileId)
                .OnDelete(DeleteBehavior.Cascade);

            // Một tenant chỉ 1 active profile cho cùng (TenantId, SourceType) — enforce ở service;
            // index phi-unique để query nhanh (unique phức tạp vì SourceType nullable + cross-provider).
            builder.HasIndex(e => new { e.TenantId, e.Scope, e.SourceType, e.IsActive });

            builder.HasQueryFilter(e => !e.IsDeleted);

            builder.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            builder.Property(e => e.UpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            builder.Property(e => e.IsDeleted).HasDefaultValue(false);
        }
    }

    public class OrderFlowStepConfiguration : IEntityTypeConfiguration<OrderFlowStep>, IEntityConfiguration
    {
        public void Configure(EntityTypeBuilder<OrderFlowStep> builder)
        {
            builder.ToTable("OrderFlowSteps");

            builder.HasKey(e => e.Id);
            builder.Ignore(e => e.OrderFlowStepId);

            builder.Property(e => e.ProfileId).IsRequired();
            builder.Property(e => e.Code).IsRequired().HasMaxLength(50);
            builder.Property(e => e.DisplayName).IsRequired().HasMaxLength(200);
            builder.Property(e => e.SortOrder).IsRequired();
            builder.Property(e => e.IsTerminal).HasDefaultValue(false);

            // List<string> → JSON TEXT (cross-provider PG + SQLite — tránh bảng join thứ 3).
            builder.Property(e => e.AllowedNextCodes)
                .HasConversion(
                    v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                    v => JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions?)null) ?? new List<string>());

            builder.Property(e => e.TenantId).IsRequired();

            builder.HasIndex(e => e.ProfileId);
            builder.HasIndex(e => new { e.ProfileId, e.SortOrder });

            builder.HasQueryFilter(e => !e.IsDeleted);

            builder.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            builder.Property(e => e.UpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            builder.Property(e => e.IsDeleted).HasDefaultValue(false);
        }
    }
}
