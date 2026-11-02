using VanAn.CoreHub.Services;
using VanAn.CoreHub.Services.Vouchers;
using VanAn.Shared.Domain;
using Xunit;

namespace VanAn.Core.Tests.Accounting;

/// <summary>Task PH/G1 — Voucher Framework registry + posting strategy (TDD: red trước).</summary>
public class VoucherFrameworkTests
{
    private static readonly TenantId Tenant = new(Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd"));

    // ---------- L1: registry ----------

    [Fact]
    public void Registry_ResolveByCode_ReturnsDefinition()
    {
        var registry = new VoucherTypeRegistry();
        registry.Register(new VoucherTypeDefinition("advance", "Phiếu Tạm Ứng", "/vouchers/advance", "02-TT",
            [new VoucherField("amount", VoucherFieldType.Amount, "Số Tiền", true, "amount"),
             new VoucherField("date", VoucherFieldType.Date, "Ngày", true, "date"),
             new VoucherField("party", VoucherFieldType.Party, "Người Nhận", true, "doiTuong")],
            "advance"));

        var def = registry.ResolveByCode("advance");
        Assert.NotNull(def);
        Assert.Equal("Phiếu Tạm Ứng", def!.Name);
        Assert.Equal("/vouchers/advance", def.Route);
    }

    [Fact]
    public void Registry_ResolveByUnknownCode_ReturnsNull()
    {
        var registry = new VoucherTypeRegistry();
        Assert.Null(registry.ResolveByCode("khong-ton-tai"));
    }

    [Fact]
    public void Registry_ResolveByIntent_MapsSmartEntry()
    {
        var registry = new VoucherTypeRegistry();
        registry.Register(new VoucherTypeDefinition("advance", "Phiếu Tạm Ứng", "/vouchers/advance", "02-TT", [], "advance"), VoucherIntent.AdvanceOut, VoucherIntent.AdvanceSettle);
        Assert.Equal("advance", registry.ResolveByIntent(VoucherIntent.AdvanceOut)?.Code);
        Assert.Equal("advance", registry.ResolveByIntent(VoucherIntent.AdvanceSettle)?.Code);
    }

    [Fact]
    public void Registry_ResolveByUnknownIntent_ReturnsNull()
    {
        var registry = new VoucherTypeRegistry();
        Assert.Null(registry.ResolveByIntent(VoucherIntent.Unknown));
    }

    // ---------- L3: AdvanceStrategy ----------

    [Fact]
    public async Task Advance_Out_JournalsDebit141CreditCash()
    {
        var strategy = new AdvancePostingStrategy();
        var result = await strategy.PostAsync(new VoucherSubmission(
            Tenant, VoucherIntent.AdvanceOut, Amount: 5_000_000m, Date: new DateTime(2026, 11, 2),
            Party: "anh Nam", CashAccount: "111", AllocatedToEntryId: null), CancellationToken.None);

        Assert.NotNull(result.Entry);
        Assert.Equal("111", result.Entry.CashAccountCode);
        Assert.Contains(result.Entry.Lines, l => l.AccountNumber == "141" && l.DebitAmount == 5_000_000m);
        Assert.Contains(result.Entry.Lines, l => l.AccountNumber == "111" && l.CreditAmount == 5_000_000m);
    }

    [Fact]
    public async Task Advance_Settle_JournalsDebitCashCredit141()
    {
        var strategy = new AdvancePostingStrategy();
        var result = await strategy.PostAsync(new VoucherSubmission(
            Tenant, VoucherIntent.AdvanceSettle, Amount: 3_000_000m, Date: new DateTime(2026, 11, 2),
            Party: "anh Nam", CashAccount: "111", AllocatedToEntryId: Guid.NewGuid()), CancellationToken.None);

        Assert.NotNull(result.Entry);
        Assert.Contains(result.Entry.Lines, l => l.AccountNumber == "111" && l.DebitAmount == 3_000_000m);
        Assert.Contains(result.Entry.Lines, l => l.AccountNumber == "141" && l.CreditAmount == 3_000_000m);
    }

    [Fact]
    public async Task Advance_Out_WithoutParty_Rejected()
    {
        var strategy = new AdvancePostingStrategy();
        var result = await strategy.PostAsync(new VoucherSubmission(
            Tenant, VoucherIntent.AdvanceOut, Amount: 5_000_000m, Date: new DateTime(2026, 11, 2),
            Party: null, CashAccount: "111", AllocatedToEntryId: null), CancellationToken.None);

        Assert.Null(result.Entry);
        Assert.Contains(result.Errors, e => e.Contains("đối tượng", StringComparison.OrdinalIgnoreCase));
    }
}