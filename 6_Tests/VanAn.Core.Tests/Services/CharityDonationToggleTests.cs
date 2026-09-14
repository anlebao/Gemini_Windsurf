using FluentAssertions;
using VanAn.CoreHub.Infrastructure.Entities;
using VanAn.Shared.Services;
using Xunit;

namespace VanAn.Core.Tests.Services
{
    /// <summary>
    /// C3 (2026-09-14): Tests for the Charity_Donation_Enabled shop feature toggle.
    /// Verifies the toggle is wired correctly through DTO, entity, and UpdateToggles layers.
    /// </summary>
    [Trait("Category", "Unit")]
    public class CharityDonationToggleTests
    {
        [Fact(DisplayName = "ShopFeatureSettingsDto has Charity_Donation_Enabled with default true")]
        public void Dto_HasCharityDonationEnabled_DefaultTrue()
        {
            ShopFeatureSettingsDto dto = new();
            _ = dto.Charity_Donation_Enabled.Should().BeTrue();
        }

        [Fact(DisplayName = "ShopFeatureSettingsEntity has Charity_Donation_Enabled with default true")]
        public void Entity_HasCharityDonationEnabled_DefaultTrue()
        {
            var entity = new ShopFeatureSettingsEntity(new VanAn.Shared.Domain.TenantId(Guid.NewGuid()));
            _ = entity.Charity_Donation_Enabled.Should().BeTrue();
        }

        [Fact(DisplayName = "UpdateToggles sets Charity_Donation_Enabled correctly")]
        public void UpdateToggles_SetsCharityDonationEnabled()
        {
            var entity = new ShopFeatureSettingsEntity(new VanAn.Shared.Domain.TenantId(Guid.NewGuid()));

            entity.UpdateToggles(
                qrTableNumber: false,
                kitchenWorkflow: true,
                voiceNote: false,
                loyaltyProgram: true,
                accountingSync: true,
                einvoiceAutoExport: false,
                charityDonationEnabled: false);

            _ = entity.Charity_Donation_Enabled.Should().BeFalse();
        }

        [Fact(DisplayName = "UpdateToggles default charityDonationEnabled parameter is true")]
        public void UpdateToggles_DefaultCharityDonation_IsTrue()
        {
            var entity = new ShopFeatureSettingsEntity(new VanAn.Shared.Domain.TenantId(Guid.NewGuid()));

            entity.UpdateToggles(
                qrTableNumber: false,
                kitchenWorkflow: true,
                voiceNote: false,
                loyaltyProgram: true,
                accountingSync: true,
                einvoiceAutoExport: false);

            _ = entity.Charity_Donation_Enabled.Should().BeTrue();
        }
    }
}
