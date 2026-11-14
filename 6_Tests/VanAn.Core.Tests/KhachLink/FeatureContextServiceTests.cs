using VanAn.KhachLink.Services;
using Xunit;

namespace VanAn.Core.Tests.KhachLink;

/// <summary>
/// W2-5 (FR-V6): FeatureContextService chain-order unit tests.
/// Chain: explicit → instance OwnerTenantId → LastInteraction → null.
/// </summary>
public class FeatureContextServiceTests
{
    private static readonly Guid ExplicitId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid OwnerId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid LastId = Guid.Parse("33333333-3333-3333-3333-333333333333");

    private static FeatureContextService Create(
        Guid? ownerTenantId = null,
        Guid lastTenantId = default,
        bool ownerThrows = false,
        bool lastThrows = false)
    {
        return new FeatureContextService(
            getOwnerTenantIdAsync: () =>
            {
                if (ownerThrows) throw new InvalidOperationException("owner provider failed");
                return Task.FromResult(ownerTenantId);
            },
            getLastTenantIdAsync: () =>
            {
                if (lastThrows) throw new InvalidOperationException("last provider failed");
                return Task.FromResult(lastTenantId);
            });
    }

    [Fact]
    public async Task ExplicitTenantId_Wins_Over_Owner_And_Last()
    {
        var svc = Create(ownerTenantId: OwnerId, lastTenantId: LastId);
        var result = await svc.GetEffectiveTenantIdAsync(ExplicitId);
        Assert.Equal(ExplicitId, result);
    }

    [Fact]
    public async Task ExplicitEmptyGuid_FallsThrough_To_Owner()
    {
        var svc = Create(ownerTenantId: OwnerId, lastTenantId: LastId);
        var result = await svc.GetEffectiveTenantIdAsync(Guid.Empty);
        Assert.Equal(OwnerId, result);
    }

    [Fact]
    public async Task NullExplicit_Uses_OwnerTenantId_When_Present()
    {
        var svc = Create(ownerTenantId: OwnerId, lastTenantId: LastId);
        var result = await svc.GetEffectiveTenantIdAsync(null);
        Assert.Equal(OwnerId, result);
    }

    [Fact]
    public async Task NoOwner_FallsBack_To_LastInteraction()
    {
        var svc = Create(ownerTenantId: null, lastTenantId: LastId);
        var result = await svc.GetEffectiveTenantIdAsync(null);
        Assert.Equal(LastId, result);
    }

    [Fact]
    public async Task OwnerEmptyGuid_Treated_As_NoContext_FallsBack_To_Last()
    {
        var svc = Create(ownerTenantId: Guid.Empty, lastTenantId: LastId);
        var result = await svc.GetEffectiveTenantIdAsync(null);
        Assert.Equal(LastId, result);
    }

    [Fact]
    public async Task LastEmptyGuid_Returns_Null()
    {
        var svc = Create(ownerTenantId: null, lastTenantId: Guid.Empty);
        var result = await svc.GetEffectiveTenantIdAsync(null);
        Assert.Null(result);
    }

    [Fact]
    public async Task NoContextAtAll_Returns_Null()
    {
        var svc = Create(ownerTenantId: null, lastTenantId: Guid.Empty);
        var result = await svc.GetEffectiveTenantIdAsync();
        Assert.Null(result);
    }

    [Fact]
    public async Task HasTenantContext_True_When_Resolved_False_When_Null()
    {
        var withContext = Create(ownerTenantId: OwnerId);
        var without = Create(ownerTenantId: null, lastTenantId: Guid.Empty);
        Assert.True(await withContext.HasTenantContextAsync());
        Assert.False(await without.HasTenantContextAsync());
    }

    // ── FR-V7: ShouldRenderFeature render rule ──────────────────────────────

    [Theory]
    [InlineData(true, true, true)]
    [InlineData(true, false, false)]   // toggle ON nhưng không context → KHÔNG render (không nút chết)
    [InlineData(false, true, false)]   // toggle OFF → không render kể cả có context
    [InlineData(false, false, false)]
    public void ShouldRenderFeature_TruthTable(bool toggle, bool hasContext, bool expected)
    {
        Assert.Equal(expected, FeatureContextService.ShouldRenderFeature(toggle, hasContext));
    }
}
