using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using VanAn.CoreHub.Infrastructure;
using VanAn.CoreHub.Services.Onboarding;
using VanAn.CoreHub.Tests.TestInfrastructure;
using VanAn.Shared.Domain;
using VanAn.Shared.Domain.Aggregates.SystemSettingAggregate;
using Xunit;

namespace VanAn.Core.Tests.Services.Onboarding;

/// <summary>
/// Platform Onboarding Flags (2026-11-09 — SRS FR-22): SystemSetting-backed
/// toggles cho 3 entry-point self-serve + master switch. Default true.
/// </summary>
public class PlatformOnboardingFlagsServiceTests
{
    private static PlatformOnboardingFlagsService CreateSut(IVanAnDbContext db)
        => new(db, NullLogger<PlatformOnboardingFlagsService>.Instance);

    [Fact(DisplayName = "GetFlags_NoRows_DefaultsAllTrue")]
    public async Task GetFlags_NoRows_DefaultsAllTrue()
    {
        using var scope = VanAnDbContextTestFactory.Create();
        var sut = CreateSut(scope.Context);

        var flags = await sut.GetFlagsAsync();

        flags.SelfServeEnabled.Should().BeTrue();
        flags.DirectoryCta.Should().BeTrue();
        flags.QrEntry.Should().BeTrue();
        flags.DeeplinkPrefill.Should().BeTrue();
    }

    [Fact(DisplayName = "SetFlags_Persists_AndReadsBack")]
    public async Task SetFlags_Persists_AndReadsBack()
    {
        using var scope = VanAnDbContextTestFactory.Create();
        var sut = CreateSut(scope.Context);
        var adminId = Guid.NewGuid();

        await sut.SetFlagsAsync(
            new UpdateOnboardingFlagsRequest(false, true, false, true), adminId);

        var flags = await sut.GetFlagsAsync();
        flags.SelfServeEnabled.Should().BeFalse();
        flags.DirectoryCta.Should().BeTrue();
        flags.QrEntry.Should().BeFalse();
        flags.DeeplinkPrefill.Should().BeTrue();

        // Verify SystemSetting rows + UpdatedBy audit
        var rows = await scope.Context.SystemSettings.IgnoreQueryFilters()
            .Where(s => s.Key.StartsWith("Onboarding."))
            .ToListAsync();
        rows.Should().HaveCount(4);
        rows.Should().OnlyContain(r => r.UpdatedBy == adminId);
        rows.Should().Contain(r => r.Key == "Onboarding.SelfServeEnabled" && r.Value == "false");
    }

    [Fact(DisplayName = "SetFlags_Twice_Upserts_NotDuplicates")]
    public async Task SetFlags_Twice_Upserts_NotDuplicates()
    {
        using var scope = VanAnDbContextTestFactory.Create();
        var sut = CreateSut(scope.Context);

        await sut.SetFlagsAsync(new UpdateOnboardingFlagsRequest(false, false, false, false), Guid.NewGuid());
        await sut.SetFlagsAsync(new UpdateOnboardingFlagsRequest(true, true, true, true), Guid.NewGuid());

        var rows = await scope.Context.SystemSettings.IgnoreQueryFilters()
            .Where(s => s.Key.StartsWith("Onboarding."))
            .ToListAsync();
        rows.Should().HaveCount(4);
        (await sut.GetFlagsAsync()).SelfServeEnabled.Should().BeTrue();
    }

    [Fact(DisplayName = "IsSelfServeEnabled_ReflectsMasterToggle")]
    public async Task IsSelfServeEnabled_ReflectsMasterToggle()
    {
        using var scope = VanAnDbContextTestFactory.Create();
        var sut = CreateSut(scope.Context);

        (await sut.IsSelfServeEnabledAsync()).Should().BeTrue();
        await sut.SetFlagsAsync(new UpdateOnboardingFlagsRequest(false, true, true, true), Guid.NewGuid());
        (await sut.IsSelfServeEnabledAsync()).Should().BeFalse();
    }

    [Fact(DisplayName = "Toggles_AreIndependent")]
    public async Task Toggles_AreIndependent()
    {
        using var scope = VanAnDbContextTestFactory.Create();
        var sut = CreateSut(scope.Context);

        // Chỉ tắt directory CTA — 3 cái kia vẫn true
        await sut.SetFlagsAsync(new UpdateOnboardingFlagsRequest(true, false, true, true), Guid.NewGuid());

        var flags = await sut.GetFlagsAsync();
        flags.SelfServeEnabled.Should().BeTrue();
        flags.DirectoryCta.Should().BeFalse();
        flags.QrEntry.Should().BeTrue();
        flags.DeeplinkPrefill.Should().BeTrue();
    }
}
