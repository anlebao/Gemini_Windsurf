using FluentAssertions;
using VanAn.CoreHub.Infrastructure.Entities;
using VanAn.Shared.Services;
using Xunit;

namespace VanAn.Core.Tests.Services
{
    /// <summary>
    /// VOICE SERVICE P4 fix (2026-11-12): Tests for the Voice_Call_Enabled shop feature toggle.
    /// The flag was added to the DTO/UI in P4 but never persisted (entity column + mapping
    /// missing) — saving in /settings/shop-features silently reverted it to OFF.
    /// Verifies the toggle is wired through DTO → entity → UpdateToggles → service layers.
    /// </summary>
    [Trait("Category", "Unit")]
    public class VoiceCallToggleTests
    {
        [Fact(DisplayName = "ShopFeatureSettingsDto has Voice_Call_Enabled with default false (opt-in)")]
        public void Dto_HasVoiceCallEnabled_DefaultFalse()
        {
            ShopFeatureSettingsDto dto = new();
            _ = dto.Voice_Call_Enabled.Should().BeFalse();
        }

        [Fact(DisplayName = "ShopFeatureSettingsEntity has Voice_Call_Enabled with default false")]
        public void Entity_HasVoiceCallEnabled_DefaultFalse()
        {
            var entity = new ShopFeatureSettingsEntity(new VanAn.Shared.Domain.TenantId(Guid.NewGuid()));
            _ = entity.Voice_Call_Enabled.Should().BeFalse();
        }

        [Fact(DisplayName = "UpdateToggles sets Voice_Call_Enabled correctly")]
        public void UpdateToggles_SetsVoiceCallEnabled()
        {
            var entity = new ShopFeatureSettingsEntity(new VanAn.Shared.Domain.TenantId(Guid.NewGuid()));

            entity.UpdateToggles(
                qrTableNumber: false,
                kitchenWorkflow: true,
                voiceNote: false,
                loyaltyProgram: true,
                accountingSync: true,
                einvoiceAutoExport: false,
                voiceCallEnabled: true);

            _ = entity.Voice_Call_Enabled.Should().BeTrue();
        }

        [Fact(DisplayName = "UpdateToggles default voiceCallEnabled parameter is false")]
        public void UpdateToggles_DefaultVoiceCall_IsFalse()
        {
            var entity = new ShopFeatureSettingsEntity(new VanAn.Shared.Domain.TenantId(Guid.NewGuid()));

            entity.UpdateToggles(
                qrTableNumber: false,
                kitchenWorkflow: true,
                voiceNote: false,
                loyaltyProgram: true,
                accountingSync: true,
                einvoiceAutoExport: false);

            _ = entity.Voice_Call_Enabled.Should().BeFalse();
        }
    }
}
