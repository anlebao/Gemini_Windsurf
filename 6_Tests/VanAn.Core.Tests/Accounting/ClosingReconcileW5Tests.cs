using Moq;
using VanAn.CoreHub.Infrastructure.Entities;
using VanAn.CoreHub.Repositories;
using VanAn.CoreHub.Services;
using VanAn.CoreHub.Services.Reconcile;
using VanAn.CoreHub.Tests.TestInfrastructure;
using VanAn.Shared.Domain;
using Xunit;

namespace VanAn.Core.Tests.Accounting;

/// <summary>SRS v1.2 W5/F: closing JEs 911 (regime-gated) + ReconciliationItems + bank match.</summary>
public class ClosingReconcileW5Tests
{
    private static readonly TenantId Tenant = new(Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee"));

    private static TestContextScope NewScope()
    {
        var scope = VanAnDbContextTestFactory.Create();
        scope.TenantProvider!.SetTenant(Tenant.Value);
        return scope;
    }

    private static (PeriodClosingService svc, List<JournalEntry> posted) NewClosingSvc(
        TestContextScope scope, TenantType? tenantType)
    {
        var posted = new List<JournalEntry>();
        var hkd = new Mock<IHKDBookRepository>();
        hkd.Setup(h => h.AddToBookAsync(It.IsAny<JournalEntry>(), It.IsAny<AccountingBookType>(), It.IsAny<CancellationToken>()))
            .Callback<JournalEntry, AccountingBookType, CancellationToken>((je, _, _) => posted.Add(je))
            .Returns(Task.CompletedTask);
        var flags = new Mock<IVasFeatureFlagService>();
        flags.Setup(f => f.GetTenantTypeAsync(It.IsAny<TenantId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(tenantType);
        var svc = new PeriodClosingService(
            new Mock<IAccountingEntryRepository>().Object,
            new Mock<IReversalService>().Object,
            new Mock<IAuditTrailService>().Object,
            scope.Context,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<PeriodClosingService>.Instance,
            hkdBookRepository: hkd.Object,
            vasFeatureFlagService: flags.Object);
        return (svc, posted);
    }

    private static JournalEntry SeedJe(TestContextScope scope, DateTime date, params (string Acc, decimal Dr, decimal Cr)[] lines)
    {
        JournalEntry je = new(Tenant, date, "seed", postingPurpose: AccountingPostingPurposes.Original);
        foreach (var (acc, dr, cr) in lines) je.AddLine(acc, dr, cr, null);
        scope.Context.JournalEntries.Add(je);
        return je;
    }

    [Fact]
    public async Task Closing_DN_Rev100Exp40_Lai60_ThreeJes()
    {
        using var scope = NewScope();
        var (svc, posted) = NewClosingSvc(scope, TenantType.Enterprise_SME);
        // Doanh thu 511: Có 100 (net −100); Chi phí 642: Nợ 40.
        SeedJe(scope, new DateTime(2026, 10, 10), ("511", 0, 100m), ("642", 40m, 0m));
        await scope.Context.SaveChangesAsync();

        int count = await svc.GenerateClosingEntriesAsync(
            AccountingPeriod.Create(2026, 10), Tenant, Guid.NewGuid());

        Assert.Equal(3, count);
        // JE1: Nợ 511 100 / Có 911 100
        var jeRev = posted[0];
        Assert.Contains(jeRev.Lines, l => l.AccountNumber == "511" && l.DebitAmount == 100m);
        Assert.Contains(jeRev.Lines, l => l.AccountNumber == "911" && l.CreditAmount == 100m);
        // JE2: Nợ 911 40 / Có 642 40
        var jeExp = posted[1];
        Assert.Contains(jeExp.Lines, l => l.AccountNumber == "911" && l.DebitAmount == 40m);
        Assert.Contains(jeExp.Lines, l => l.AccountNumber == "642" && l.CreditAmount == 40m);
        // JE3: lãi 60 → Nợ 911 60 / Có 421 60
        var jeRes = posted[2];
        Assert.Contains(jeRes.Lines, l => l.AccountNumber == "911" && l.DebitAmount == 60m);
        Assert.Contains(jeRes.Lines, l => l.AccountNumber == "421" && l.CreditAmount == 60m);
        Assert.All(posted, je => Assert.Equal(AccountingPostingPurposes.Closing, je.PostingPurpose));
    }

    [Fact]
    public async Task Closing_HkdAndDnsn_Skip()
    {
        using var scope = NewScope();
        var (svc, posted) = NewClosingSvc(scope, TenantType.HKD);
        SeedJe(scope, new DateTime(2026, 10, 10), ("511", 0, 100m));
        await scope.Context.SaveChangesAsync();

        Assert.Equal(0, await svc.GenerateClosingEntriesAsync(AccountingPeriod.Create(2026, 10), Tenant, Guid.NewGuid()));
        Assert.Empty(posted);
    }

    [Fact]
    public async Task Closing_Idempotent_RerunSkips()
    {
        using var scope = NewScope();
        var (svc, posted) = NewClosingSvc(scope, TenantType.Enterprise_SME);
        // JE closing đã tồn tại → skip.
        JournalEntry existing = new(Tenant, new DateTime(2026, 10, 31), "closing cũ",
            postingPurpose: AccountingPostingPurposes.Closing);
        existing.AddLine("511", 100, 0, null);
        existing.AddLine("911", 0, 100, null);
        scope.Context.JournalEntries.Add(existing);
        await scope.Context.SaveChangesAsync();

        Assert.Equal(0, await svc.GenerateClosingEntriesAsync(AccountingPeriod.Create(2026, 10), Tenant, Guid.NewGuid()));
        Assert.Empty(posted);
    }

    [Fact]
    public async Task Items_EmitDedup_AutoResolve()
    {
        using var scope = NewScope();
        var svc = new ReconciliationItemService(scope.Context,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<ReconciliationItemService>.Instance);
        var warn = new ReconcileResult("2026-10", Tenant.Value, "t", DateTime.UtcNow,
            new ReconcileSummary(1, 0, 1, 0, 2),
            [new ReconcileCheck("R1", "JE balance", ReconcileLevel.Warning, "1 JE lệch", ""),
             new ReconcileCheck("R2", "Post status", ReconcileLevel.Pass, "ok", "")],
            []);

        Assert.Equal(1, await svc.EmitFromReconcileAsync(warn));
        Assert.Equal(0, await svc.EmitFromReconcileAsync(warn)); // dedup

        var items = await svc.ListAsync(Tenant, "Open");
        Assert.Single(items);
        Assert.Equal("R1", items[0].CheckCode);

        // Check R1 pass lần sau → auto-resolve.
        var pass = warn with { Checks = [new ReconcileCheck("R1", "JE balance", ReconcileLevel.Pass, "ok", "")] };
        await svc.EmitFromReconcileAsync(pass);
        var after = await svc.ListAsync(Tenant);
        Assert.Equal("Resolved", after[0].Status);
    }

    [Fact]
    public async Task BankStatement_ImportAndMatch()
    {
        using var scope = NewScope();
        var bank = new BankAccountEntity(Tenant, "1121", "TK VCB", "Vietcombank", "001122");
        scope.Context.BankAccounts.Add(bank);
        // JE trên 1121: Nợ 1121 500k (tiền vào) + Có 1121 100k (tiền ra).
        SeedJe(scope, new DateTime(2026, 10, 10), ("1121", 500000m, 0m));
        SeedJe(scope, new DateTime(2026, 10, 11), ("1121", 0m, 100000m));
        await scope.Context.SaveChangesAsync();

        var svc = new BankStatementService(scope.Context,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<BankStatementService>.Instance);
        int imported = await svc.ImportCsvAsync(Tenant, bank.Id,
            "10/10/2026,500000,FT001,Nop tien\n12/10/2026,-100000,FT002,Rut tien\n15/10/2026,-99999,FT999,Khong khop");
        Assert.Equal(3, imported);

        var report = await svc.ReconcileAsync(Tenant, bank.Id);
        Assert.Equal(3, report.TotalStatementLines);
        Assert.Equal(2, report.MatchedCount);
        Assert.Single(report.UnmatchedStatementLines);
        Assert.Equal("FT999", report.UnmatchedStatementLines[0].Reference);
    }
}
