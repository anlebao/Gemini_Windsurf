using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VanAn.Shared.Domain.Audit;
using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Infrastructure.Configurations
{
    /// <summary>
    /// EF Core configuration for AuditLog - Immutable append-only table
    /// Compliance requirement: Audit logs must never be modified or deleted
    /// </summary>
    public class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
    {
        public void Configure(EntityTypeBuilder<AuditLog> builder)
        {
            // Primary key
            _ = builder.HasKey(e => e.Id);

            // Property configurations
            _ = builder.Property(e => e.Id)
                .ValueGeneratedOnAdd();

            // Action type as int (enum)
            _ = builder.Property(e => e.Action)
                .HasConversion<int>();

            // Entity type as int (enum)
            _ = builder.Property(e => e.EntityType)
                .HasConversion<int>();

            // EntityId as Guid
            _ = builder.Property(e => e.EntityId)
                .IsRequired();

            // OldValues and NewValues as JSON strings (nullable)
            _ = builder.Property(e => e.OldValues)
                .HasMaxLength(4000)
                .IsRequired(false);

            _ = builder.Property(e => e.NewValues)
                .HasMaxLength(4000)
                .IsRequired(false);

            // Reason for period close/reopen/correction
            _ = builder.Property(e => e.Reason)
                .HasMaxLength(1000)
                .IsRequired(false);

            // Correlation ID for tracking related operations
            _ = builder.Property(e => e.CorrelationId)
                .HasMaxLength(100)
                .IsRequired(false);

            // User information
            _ = builder.Property(e => e.UserId)
                .HasMaxLength(100)
                .IsRequired(false);

            _ = builder.Property(e => e.UserName)
                .HasMaxLength(200)
                .IsRequired(false);

            // IP Address and User Agent
            _ = builder.Property(e => e.IpAddress)
                .HasMaxLength(50)
                .IsRequired(false);

            _ = builder.Property(e => e.UserAgent)
                .HasMaxLength(500)
                .IsRequired(false);

            // TenantId with converter for SQLite compatibility
            builder.Property(e => e.TenantId)
                .IsRequired()
                .HasConversion(
                    id => id.Value,
                    value => new TenantId(value))
                .Metadata.SetValueComparer(new Microsoft.EntityFrameworkCore.ChangeTracking.ValueComparer<TenantId>(
                    (c1, c2) => c1.Value == c2.Value,
                    c => c.Value.GetHashCode(),
                    c => new TenantId(c.Value)));

            // Timestamp from BaseEntity.CreatedAt
            _ = builder.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP");

            // Indexes for query performance
            // Primary query: by tenant + date range
            _ = builder.HasIndex(e => new { e.TenantId, e.CreatedAt });

            // Query by entity (for entity history)
            _ = builder.HasIndex(e => new { e.TenantId, e.EntityType, e.EntityId });

            // Query by action type
            _ = builder.HasIndex(e => new { e.TenantId, e.Action, e.CreatedAt });

            // Query by user
            _ = builder.HasIndex(e => new { e.TenantId, e.UserId, e.CreatedAt });

            // Query by correlation ID (for tracking related operations)
            _ = builder.HasIndex(e => e.CorrelationId)
                .IsUnique(false);

            // Table name
            _ = builder.ToTable("AuditLogs");
        }
    }
}
