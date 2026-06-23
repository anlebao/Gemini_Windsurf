using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VanAn.CoreHub.Domain;
using VanAn.CoreHub.Infrastructure.DataProtection;
using VanAn.CoreHub.Infrastructure.ValueConverters;

namespace VanAn.CoreHub.Infrastructure.Configurations
{
    /// <summary>
    /// EF Core configuration for FacebookLead entity
    /// Inherits from Lead - uses TPH (Table Per Hierarchy) mapping
    /// </summary>
    public class FacebookLeadConfiguration : IEntityTypeConfiguration<FacebookLead>, IEntityConfiguration
    {
        public void Configure(EntityTypeBuilder<FacebookLead> builder)
        {
            // FacebookLead inherits from Lead
            // LeadId is inherited from Lead base class - already configured in LeadConfiguration
            // EF Core TPH will handle the hierarchy mapping automatically

            // Wave 2: PII encryption for inherited PhoneNumber and Email
            _ = builder.Property(e => e.PhoneNumber)
                .IsRequired()
                .HasMaxLength(500)
                .HasConversion(new EncryptedStringConverter(
                    DataProtectionProviderAccessor.CreateProtector("Lead.PhoneNumber")));

            _ = builder.Property(e => e.Email)
                .HasMaxLength(500)
                .HasConversion(new EncryptedStringConverter(
                    DataProtectionProviderAccessor.CreateProtector("Lead.Email")));

            // Facebook-specific properties
            _ = builder.Property(e => e.FacebookLeadId)
                .IsRequired()
                .HasMaxLength(100);

            _ = builder.Property(e => e.FacebookAdId)
                .IsRequired()
                .HasMaxLength(100);

            _ = builder.Property(e => e.FacebookPageId)
                .IsRequired()
                .HasMaxLength(100);

            _ = builder.Property(e => e.FacebookCampaignId)
                .IsRequired()
                .HasMaxLength(100);

            _ = builder.Property(e => e.FacebookFormData)
                .HasMaxLength(4000); // JSON data can be large

            // Indexes for Facebook-specific lookups
            _ = builder.HasIndex(e => e.FacebookLeadId);
            _ = builder.HasIndex(e => e.FacebookCampaignId);
            _ = builder.HasIndex(e => e.IsFacebookProcessed);
        }
    }
}
