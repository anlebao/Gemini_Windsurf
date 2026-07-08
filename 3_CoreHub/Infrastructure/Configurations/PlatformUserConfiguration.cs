using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VanAn.CoreHub.Infrastructure.Entities;
using VanAn.Shared.Domain.Common;

namespace VanAn.CoreHub.Infrastructure.Configurations;

public class PlatformUserConfiguration : IEntityTypeConfiguration<PlatformUser>, IEntityConfiguration
{
    public void Configure(EntityTypeBuilder<PlatformUser> builder)
    {
        builder.ToTable("PlatformUsers");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedOnAdd();

        builder.Property(e => e.Username)
            .IsRequired()
            .HasMaxLength(100);
        builder.HasIndex(e => e.Username).IsUnique();

        builder.Property(e => e.PasswordHash)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(e => e.DisplayName)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(e => e.Email)
            .HasMaxLength(500);

        builder.Property(e => e.Role)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(e => e.IsActive)
            .IsRequired();

        builder.Property(e => e.CreatedAt)
            .IsRequired()
            .HasDefaultValueSql("CURRENT_TIMESTAMP");
    }
}
