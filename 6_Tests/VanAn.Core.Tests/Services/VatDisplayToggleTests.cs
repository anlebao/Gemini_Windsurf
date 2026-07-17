using FluentAssertions;
using VanAn.CoreHub.Infrastructure.Entities;
using VanAn.CoreHub.Services;
using Xunit;

namespace VanAn.Core.Tests.Services
{
    /// <summary>
    /// RC-7: Tests for the VAT_Display_Enabled shop feature toggle.
    /// Verifies the toggle is wired correctly through DTO, entity, and service layers.
    /// </summary>
    [Trait("Category", "Unit")]
    public class VatDisplayToggleTests
    {
        [Fact(DisplayName = "ShopFeatureSettingsDto has VAT_Display_Enabled with default true")]
        public void Dto_HasVatDisplayEnabled_DefaultTrue()
        {
            ShopFeatureSettingsDto dto = new();
            _ = dto.VAT_Display_Enabled.Should().BeTrue();
        }

        [Fact(DisplayName = "ShopFeatureSettingsEntity has VAT_Display_Enabled with default true")]
        public void Entity_HasVatDisplayEnabled_DefaultTrue()
        {
            var entity = new ShopFeatureSettingsEntity(new VanAn.Shared.Domain.TenantId(Guid.NewGuid()));
            _ = entity.VAT_Display_Enabled.Should().BeTrue();
        }

        [Fact(DisplayName = "UpdateToggles sets VAT_Display_Enabled correctly")]
        public void UpdateToggles_SetsVatDisplayEnabled()
        {
            var entity = new ShopFeatureSettingsEntity(new VanAn.Shared.Domain.TenantId(Guid.NewGuid()));

            entity.UpdateToggles(
                qrTableNumber: false,
                kitchenWorkflow: true,
                voiceNote: false,
                loyaltyProgram: true,
                accountingSync: true,
                einvoiceAutoExport: false,
                pollingIntervalSeconds: 15,
                vatDisplay: false);

            _ = entity.VAT_Display_Enabled.Should().BeFalse();
        }

        [Fact(DisplayName = "UpdateToggles default vatDisplay parameter is true")]
        public void UpdateToggles_DefaultVatDisplay_IsTrue()
        {
            var entity = new ShopFeatureSettingsEntity(new VanAn.Shared.Domain.TenantId(Guid.NewGuid()));

            entity.UpdateToggles(
                qrTableNumber: false,
                kitchenWorkflow: true,
                voiceNote: false,
                loyaltyProgram: true,
                accountingSync: true,
                einvoiceAutoExport: false,
                pollingIntervalSeconds: 15);

            _ = entity.VAT_Display_Enabled.Should().BeTrue();
        }
    }
}
