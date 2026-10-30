using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VanAn.CoreHub.Infrastructure.Entities;

namespace VanAn.CoreHub.Infrastructure.Configurations;

/// <summary>SRS v1.2 W2/C1: Attachments — đính kèm chứng từ link tới entity kế toán.</summary>
public class AttachmentConfiguration : IEntityTypeConfiguration<AttachmentEntity>, IEntityConfiguration
{
    public void Configure(EntityTypeBuilder<AttachmentEntity> builder)
    {
        _ = builder.ToTable("Attachments");
        _ = builder.HasKey(e => e.Id);
        _ = builder.Property(e => e.Id).ValueGeneratedOnAdd();
        _ = builder.Property(e => e.TenantId).IsRequired();
        _ = builder.Property(e => e.EntityType).IsRequired().HasMaxLength(50);
        _ = builder.Property(e => e.EntityId).IsRequired();
        _ = builder.Property(e => e.FileUrl).IsRequired().HasMaxLength(1000);
        _ = builder.Property(e => e.FileName).HasMaxLength(300);
        _ = builder.Property(e => e.MimeType).HasMaxLength(100);
        _ = builder.Property(e => e.UploadedBy).HasMaxLength(200);
        _ = builder.Property(e => e.CreatedAt).IsRequired().HasDefaultValueSql("CURRENT_TIMESTAMP");

        _ = builder.HasIndex(e => new { e.TenantId, e.EntityType, e.EntityId });
    }
}
