using Moq;
using VanAn.CoreHub.Infrastructure.Entities;
using VanAn.CoreHub.Services;
using VanAn.CoreHub.Services.Journal;
using VanAn.CoreHub.Tests.TestInfrastructure;
using VanAn.Shared.Domain;
using Xunit;

namespace VanAn.Core.Tests.Accounting;

/// <summary>W2/B follow-up: quản trị account-map — effective merge + tenant override + version bump + deactivate.</summary>
public class AccountMapRuleServiceTests
{
    private static readonly TenantId Tenant = new(Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd"));

    private static (AccountMapRuleService svc, TestContextScope scope) NewSvc()
    {
        var scope = VanAnDbContextTestFactory.Create();
        scope.TenantProvider!.SetTenant(Tenant.Value);
        var flags = new Mock<IVasFeatureFlagService>();
        flags.Setup(f => f.GetTenantTypeAsync(It.IsAny<TenantId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(TenantType.Enterprise_SME); // → TT133
        var svc = new AccountMapRuleService(scope.Context,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<AccountMapRuleService>.Instance, flags.Object);
        return (svc, scope);
    }

    [Fact]
    public async Task EffectiveRules_RegimeSeeded_ReturnsRegimeSource()
    {
        var (svc, scope) = NewSvc();
        scope.Context.AccountMappingRules.Add(new AccountMappingRuleEntity(
            "manual-receipt", "{cash}", "{src}", tenantId: null, regime: "TT133", version: 1));
        await scope.Context.SaveChangesAsync();

        var rows = await svc.GetEffectiveRulesAsync(Tenant);

        Assert.Equal(13, rows.Count);
        var receipt = rows.Single(r => r.EventCode == "manual-receipt");
        Assert.Equal("regime", receipt.Source);
        Assert.Equal("{cash}", receipt.DebitAccount);
        // event không có rule → builtin (manual-expense có builtin)
        Assert.Equal("builtin", rows.Single(r => r.EventCode == "manual-expense").Source);
        // payable không builtin → none
        Assert.Equal("none", rows.Single(r => r.EventCode == "payable").Source);
    }

    [Fact]
    public async Task Upsert_CreateThenUpdate_TenantSourceAndVersionBump()
    {
        var (svc, scope) = NewSvc();
        scope.Context.AccountMappingRules.Add(new AccountMappingRuleEntity(
            "manual-expense", "{src}", "{cash}", tenantId: null, regime: "TT133"));
        await scope.Context.SaveChangesAsync();

        var r1 = await svc.UpsertTenantRuleAsync(Tenant, "manual-expense", "6421", "111");
        Assert.Equal(1, r1.Version);

        var rows = await svc.GetEffectiveRulesAsync(Tenant);
        var row = rows.Single(r => r.EventCode == "manual-expense");
        Assert.Equal("tenant", row.Source);
        Assert.Equal("6421", row.DebitAccount);

        var r2 = await svc.UpsertTenantRuleAsync(Tenant, "manual-expense", "6422", "1121");
        Assert.Equal(2, r2.Version);
        Assert.Equal(r1.Id, r2.Id);
        Assert.Equal("6422", (await svc.GetEffectiveRulesAsync(Tenant)).Single(r => r.EventCode == "manual-expense").DebitAccount);
    }

    [Fact]
    public async Task Deactivate_FallsBackToRegime()
    {
        var (svc, scope) = NewSvc();
        scope.Context.AccountMappingRules.Add(new AccountMappingRuleEntity(
            "manual-expense", "{src}", "{cash}", tenantId: null, regime: "TT133"));
        await scope.Context.SaveChangesAsync();
        await svc.UpsertTenantRuleAsync(Tenant, "manual-expense", "6421", "111");

        await svc.DeactivateTenantRuleAsync(Tenant, "manual-expense");

        var row = (await svc.GetEffectiveRulesAsync(Tenant)).Single(r => r.EventCode == "manual-expense");
        Assert.Equal("regime", row.Source);
        Assert.Equal("{src}", row.DebitAccount);
    }

    [Fact]
    public async Task Upsert_RequiresBothAccounts()
    {
        var (svc, _) = NewSvc();
        await Assert.ThrowsAsync<ArgumentException>(() =>
            svc.UpsertTenantRuleAsync(Tenant, "manual-receipt", "111", null));
    }
}
