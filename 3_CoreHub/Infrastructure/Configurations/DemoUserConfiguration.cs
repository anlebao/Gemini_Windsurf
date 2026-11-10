using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VanAn.CoreHub.Infrastructure.DataProtection;
using VanAn.CoreHub.Infrastructure.ValueConverters;
using DemoUser = VanAn.Shared.Domain.Aggregates.UserAggregate.DemoUser;
using UserRole = VanAn.Shared.Domain.Aggregates.UserAggregate.UserRole;

namespace VanAn.CoreHub.Infrastructure.Configurations
{
    /// <summary>
    /// EF Core configuration for DemoUser entity
    /// </summary>
    public class DemoUserConfiguration : IEntityTypeConfiguration<DemoUser>, IEntityConfiguration
    {
        public void Configure(EntityTypeBuilder<DemoUser> builder)
        {
            _ = builder.HasKey(e => e.Id);


            _ = builder.Property(e => e.Username)
                .IsRequired()
                .HasMaxLength(100);

            _ = builder.Property(e => e.PasswordHash)
                .IsRequired()
                .HasMaxLength(500);

            _ = builder.Property(e => e.DisplayName)
                .IsRequired()
                .HasMaxLength(200);

            // Wave 2: PII encryption for Email
            _ = builder.Property(e => e.Email)
                .HasMaxLength(500)
                .HasConversion(new EncryptedStringConverter(
                    DataProtectionProviderAccessor.CreateProtector("DemoUser.Email")));

            // ID-P1 (2026-11-10): PII encryption cho PhoneNumber — protector RIÊNG (không cross-decrypt)
            _ = builder.Property(e => e.PhoneNumber)
                .HasMaxLength(500)
                .HasConversion(new EncryptedStringConverter(
                    DataProtectionProviderAccessor.CreateProtector("DemoUser.PhoneNumber")));

            // ID-P1: trust fields — VerifiedChannel plain (provenance, không search nhu cầu → no index)
            _ = builder.Property(e => e.PhoneVerifiedAt);
            _ = builder.Property(e => e.VerifiedChannel).HasMaxLength(20);
            _ = builder.Property(e => e.TrustLevel).HasConversion<int>();

            // MFA-1 (2026-11-10): TOTP secret encrypted at rest — protector RIÊNG
            _ = builder.Property(e => e.MfaSecret)
                .HasMaxLength(500)
                .HasConversion(new EncryptedStringConverter(
                    DataProtectionProviderAccessor.CreateProtector("DemoUser.MfaSecret")));
            _ = builder.Property(e => e.MfaEnabled);
            _ = builder.Property(e => e.TrustedDeviceHash).HasMaxLength(128);

            // Enum conversion for UserRole
            _ = builder.Property(e => e.Role)
                .HasConversion<int>();

            _ = builder.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP");

            // Indexes
            _ = builder.HasIndex(e => new { e.TenantId, e.Username }).IsUnique();
        }
    }
}
