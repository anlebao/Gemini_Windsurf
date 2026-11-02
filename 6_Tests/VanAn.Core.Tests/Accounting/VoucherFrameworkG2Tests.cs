using VanAn.CoreHub.Services;
using VanAn.CoreHub.Services.Vouchers;
using VanAn.Shared.Domain;
using Xunit;

namespace VanAn.Core.Tests.Accounting;

/// <summary>Task PH/G2 — Xuất Kho + Thanh Lý TSCĐ strategies (TDD: red trước).</summary>
public class VoucherFrameworkG2Tests
{
    private static readonly TenantId Tenant = new(Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd"));

    [Fact]
    public async Task StockIssue_Production_Journals154Credit152()
    {
        var r = await new StockIssuePostingStrategy().PostAsync(new VoucherSubmission(
            Tenant, VoucherIntent.StockOut, Amount: 5_000_000m, Date: new DateTime(2026, 11, 2),
            Party: null, CashAccount: null, AllocatedToEntryId: null, Mode: "production"), CancellationToken.None);
        Assert.NotNull(r.Entry);
        Assert.Contains(r.Entry!.Lines, l => l.AccountNumber == "154" && l.DebitAmount == 5_000_000m);
        Assert.Contains(r.Entry.Lines, l => l.AccountNumber == "152" && l.CreditAmount == 5_000_000m);
    }

    [Fact]
    public async Task StockIssue_Sale_Journals632Credit152()
    {
        var r = await new StockIssuePostingStrategy().PostAsync(new VoucherSubmission(
            Tenant, VoucherIntent.StockOut, Amount: 3_000_000m, Date: new DateTime(2026, 11, 2),
            Party: null, CashAccount: null, AllocatedToEntryId: null, Mode: "sale"), CancellationToken.None);
        Assert.Contains(r.Entry!.Lines, l => l.AccountNumber == "632" && l.DebitAmount == 3_000_000m);
        Assert.Contains(r.Entry.Lines, l => l.AccountNumber == "152" && l.CreditAmount == 3_000_000m);
    }

    [Fact]
    public async Task AssetDisposal_Loss_JournalsBalanced()
    {
        // Nguyên giá 100tr, hao mòn 40tr, thanh lý 50tr → lỗ 10tr
        var r = await new AssetDisposalPostingStrategy().PostAsync(new VoucherSubmission(
            Tenant, VoucherIntent.AssetDisposal, Amount: 50_000_000m, Date: new DateTime(2026, 11, 2),
            Party: null, CashAccount: "111", AllocatedToEntryId: null,
            OriginalCost: 100_000_000m, AccumulatedDepreciation: 40_000_000m, LiquidationPrice: 50_000_000m), CancellationToken.None);
        Assert.NotNull(r.Entry);
        Assert.Contains(r.Entry!.Lines, l => l.AccountNumber == "214" && l.DebitAmount == 40_000_000m);
        Assert.Contains(r.Entry.Lines, l => l.AccountNumber == "811" && l.DebitAmount == 10_000_000m);
        Assert.Contains(r.Entry.Lines, l => l.AccountNumber == "111" && l.DebitAmount == 50_000_000m);
        Assert.Contains(r.Entry.Lines, l => l.AccountNumber == "211" && l.CreditAmount == 100_000_000m);
        Assert.True(Balanced(r.Entry.Lines));
    }

    [Fact]
    public async Task AssetDisposal_Gain_Journals711Credit()
    {
        // Nguyên giá 100tr, hao mòn 60tr, thanh lý 50tr → lãi 10tr
        var r = await new AssetDisposalPostingStrategy().PostAsync(new VoucherSubmission(
            Tenant, VoucherIntent.AssetDisposal, Amount: 50_000_000m, Date: new DateTime(2026, 11, 2),
            Party: null, CashAccount: "111", AllocatedToEntryId: null,
            OriginalCost: 100_000_000m, AccumulatedDepreciation: 60_000_000m, LiquidationPrice: 50_000_000m), CancellationToken.None);
        Assert.Contains(r.Entry!.Lines, l => l.AccountNumber == "711" && l.CreditAmount == 10_000_000m);
        Assert.True(Balanced(r.Entry.Lines));
    }

    private static bool Balanced(IReadOnlyList<VoucherLine> lines)
        => lines.Sum(l => l.DebitAmount) == lines.Sum(l => l.CreditAmount);
}