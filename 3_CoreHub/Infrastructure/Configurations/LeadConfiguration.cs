using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VanAn.CoreHub.Domain;
using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Infrastructure.Configurations
{
    /// <summary>
    /// EF Core configuration for Lead entity
    /// </summary>
    public class LeadConfiguration : IEntityTypeConfiguration<Lead>, IEntityConfiguration
    {
        public void Configure(EntityTypeBuilder<Lead> builder)
        {
            _ = builder.HasKey(e => e.Id);

            // LeadId value object converter
            _ = builder.Property(e => e.LeadId)
                .HasConversion(id => id.Value, value => new LeadId(value))
                .IsRequired();

            // TenantId is Guid (not TenantId value object) in this entity
            _ = builder.Property(e => e.TenantId)
                .IsRequired();

            _ = builder.Property(e => e.FullName)
                .IsRequired()
                .HasMaxLength(200);

            _ = builder.Property(e => e.PhoneNumber)
                .IsRequired()
                .HasMaxLength(20);

            _ = builder.Property(e => e.Email)
                .HasMaxLength(100);

            _ = builder.Property(e => e.CompanyName)
                .HasMaxLength(200);

            _ = builder.Property(e => e.JobTitle)
                .HasMaxLength(100);

            _ = builder.Property(e => e.LeadNotes)
                .HasMaxLength(1000);

            _ = builder.Property(e => e.SourceReference)
                .HasMaxLength(200);

            _ = builder.Property(e => e.ConversionReason)
                .HasMaxLength(500);

            _ = builder.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP");

            _ = builder.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP");

            // Enum conversions
            _ = builder.Property(e => e.Source)
                .HasConversion<int>();

            _ = builder.Property(e => e.Status)
                .HasConversion<int>();

            // Indexes
            _ = builder.HasIndex(e => e.LeadId);
            _ = builder.HasIndex(e => new { e.TenantId, e.Status });
            _ = builder.HasIndex(e => e.PhoneNumber);
            _ = builder.HasIndex(e => e.AssignedStaffId);

            // Soft delete filter
            _ = builder.HasQueryFilter(e => !e.IsDeleted);
        }
    }
}
