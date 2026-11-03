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
            "advance"), new AdvancePostingStrategy());

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
        registry.Register(new VoucherTypeDefinition("advance", "Phiếu Tạm Ứng", "/vouchers/advance", "02-TT", [], "advance"), new AdvancePostingStrategy(), VoucherIntent.AdvanceOut, VoucherIntent.AdvanceSettle);
        Assert.Equal("advance", registry.ResolveByIntent(VoucherIntent.AdvanceOut)?.Code);
        Assert.Equal("advance", registry.ResolveByIntent(VoucherIntent.AdvanceSettle)?.Code);
    }

    [Fact]
    public void Registry_ResolveByUnknownIntent_ReturnsNull()
    {
        var registry = new VoucherTypeRegistry();
        Assert.Null(registry.ResolveByIntent(VoucherIntent.Unknown));
    }

    // ---------- PH2: hosted types + dropdown options ----------

    [Fact]
    public void Registry_Register_NullStrategy_HostedType_Works()
    {
        var registry = new VoucherTypeRegistry();
        registry.Register(
            new VoucherTypeDefinition("revenue", "Thu doanh thu", "/accounting/revenue", null, [], "", "/accounting/revenue"),
            null, VoucherIntent.Revenue);
        var def = registry.ResolveByCode("revenue");
        Assert.NotNull(def);
        Assert.Equal("/accounting/revenue", def!.HostPage);
        Assert.Null(registry.ResolveStrategy("revenue")); // hosted — không strategy
        Assert.Equal("revenue", registry.ResolveByIntent(VoucherIntent.Revenue)?.Code);
    }

    [Fact]
    public void Registry_GetOptionsForHost_ReturnsRegisteredOrder()
    {
        var registry = new VoucherTypeRegistry();
        registry.Register(new VoucherTypeDefinition("revenue", "Thu doanh thu", "/accounting/revenue", null, [], "", "/accounting/revenue"), null, VoucherIntent.Revenue);
        registry.Register(new VoucherTypeDefinition("receivable", "Ghi nhận phải thu", "/accounting/revenue", null, [], "", "/accounting/revenue"), null, VoucherIntent.Receivable);
        registry.Register(new VoucherTypeDefinition("receivable-payment", "Thu tiền khách trả nợ", "/accounting/revenue", null, [], "", "/accounting/revenue"), null, VoucherIntent.ReceivablePayment, VoucherIntent.Advance);
        registry.Register(new VoucherTypeDefinition("expense", "Chi phí", "/accounting/expenses", null, [], "", "/accounting/expenses"), null, VoucherIntent.Expense);

        var revenue = registry.GetOptionsForHost("/accounting/revenue");
        Assert.Equal(3, revenue.Count);
        Assert.Equal(["revenue", "receivable", "receivable-payment"], revenue.Select(d => d.Code));
        Assert.Equal(1, registry.GetOptionsForHost("/accounting/expenses").Count);
        Assert.Empty(registry.GetOptionsForHost("/accounting/transfers"));
    }

    [Fact]
    public void Registry_ResolveByIntent_LoanAndLoanRepayment_Maps()
    {
        var registry = new VoucherTypeRegistry();
        registry.Register(new VoucherTypeDefinition("loan-in", "Phiếu Vay Nhận Tiền", "/vouchers/loan-in", "01-TT", [], "loan"), new LoanPostingStrategy(), VoucherIntent.Loan);
        registry.Register(new VoucherTypeDefinition("loan-repayment", "Phiếu Trả Nợ Vay", "/vouchers/loan-repayment", "02-TT", [], "loan"), new LoanPostingStrategy(), VoucherIntent.LoanRepayment);

        Assert.Equal("loan-in", registry.ResolveByIntent(VoucherIntent.Loan)?.Code);
        Assert.Equal("loan-repayment", registry.ResolveByIntent(VoucherIntent.LoanRepayment)?.Code);
        Assert.NotNull(registry.ResolveStrategy("loan-in"));
        Assert.NotNull(registry.ResolveStrategy("loan-repayment"));
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

    // ---------- PH2: LoanPostingStrategy ----------

    [Fact]
    public async Task Loan_In_JournalsDebitCashCreditLoanAccount()
    {
        var strategy = new LoanPostingStrategy();
        var result = await strategy.PostAsync(new VoucherSubmission(
            Tenant, VoucherIntent.Loan, Amount: 100_000_000m, Date: new DateTime(2026, 11, 3),
            Party: null, CashAccount: "111", AllocatedToEntryId: null,
            LoanAccountCode: "341"), CancellationToken.None);

        Assert.NotNull(result.Entry);
        Assert.Equal("111", result.Entry.CashAccountCode);
        Assert.Contains(result.Entry.Lines, l => l.AccountNumber == "111" && l.DebitAmount == 100_000_000m);
        Assert.Contains(result.Entry.Lines, l => l.AccountNumber == "341" && l.CreditAmount == 100_000_000m);
    }

    [Fact]
    public async Task Loan_Repayment_JournalsDebitLoanAccountCreditCash()
    {
        var strategy = new LoanPostingStrategy();
        var result = await strategy.PostAsync(new VoucherSubmission(
            Tenant, VoucherIntent.LoanRepayment, Amount: 20_000_000m, Date: new DateTime(2026, 11, 3),
            Party: null, CashAccount: "112", AllocatedToEntryId: null,
            LoanAccountCode: "311"), CancellationToken.None);

        Assert.NotNull(result.Entry);
        Assert.Contains(result.Entry.Lines, l => l.AccountNumber == "311" && l.DebitAmount == 20_000_000m);
        Assert.Contains(result.Entry.Lines, l => l.AccountNumber == "112" && l.CreditAmount == 20_000_000m);
    }

    [Fact]
    public async Task Loan_WithoutAccount_Rejected()
    {
        var strategy = new LoanPostingStrategy();
        var result = await strategy.PostAsync(new VoucherSubmission(
            Tenant, VoucherIntent.Loan, Amount: 100_000_000m, Date: new DateTime(2026, 11, 3),
            Party: null, CashAccount: "111", AllocatedToEntryId: null,
            LoanAccountCode: null), CancellationToken.None);

        Assert.Null(result.Entry);
        Assert.Contains(result.Errors, e => e.Contains("tài khoản vay", StringComparison.OrdinalIgnoreCase));
    }

    // ---------- PH3: Depreciation / Deposit strategies + registry ----------

    [Fact]
    public async Task Depreciation_JournalsDebitExpenseCredit214()
    {
        var strategy = new DepreciationPostingStrategy();
        var result = await strategy.PostAsync(new VoucherSubmission(
            Tenant, VoucherIntent.Depreciation, Amount: 10_000_000m, Date: new DateTime(2026, 11, 3),
            Party: null, CashAccount: "111", AllocatedToEntryId: null,
            LoanAccountCode: "642"), CancellationToken.None);

        Assert.NotNull(result.Entry);
        Assert.Contains(result.Entry.Lines, l => l.AccountNumber == "642" && l.DebitAmount == 10_000_000m);
        Assert.Contains(result.Entry.Lines, l => l.AccountNumber == "214" && l.CreditAmount == 10_000_000m);
    }

    [Fact]
    public async Task Deposit_Receive_JournalsDebitCashCredit338()
    {
        var strategy = new DepositPostingStrategy();
        var result = await strategy.PostAsync(new VoucherSubmission(
            Tenant, VoucherIntent.Deposit, Amount: 5_000_000m, Date: new DateTime(2026, 11, 3),
            Party: null, CashAccount: "111", AllocatedToEntryId: null,
            Mode: "receive"), CancellationToken.None);

        Assert.NotNull(result.Entry);
        Assert.Contains(result.Entry.Lines, l => l.AccountNumber == "111" && l.DebitAmount == 5_000_000m);
        Assert.Contains(result.Entry.Lines, l => l.AccountNumber == "338" && l.CreditAmount == 5_000_000m);
    }

    [Fact]
    public async Task Deposit_Return_JournalsDebit338CreditCash()
    {
        var strategy = new DepositPostingStrategy();
        var result = await strategy.PostAsync(new VoucherSubmission(
            Tenant, VoucherIntent.Deposit, Amount: 2_000_000m, Date: new DateTime(2026, 11, 3),
            Party: null, CashAccount: "112", AllocatedToEntryId: null,
            Mode: "return"), CancellationToken.None);

        Assert.NotNull(result.Entry);
        Assert.Contains(result.Entry.Lines, l => l.AccountNumber == "338" && l.DebitAmount == 2_000_000m);
        Assert.Contains(result.Entry.Lines, l => l.AccountNumber == "112" && l.CreditAmount == 2_000_000m);
    }

    [Fact]
    public void Registry_ResolveByIntent_Ph3Intents_Maps()
    {
        var registry = new VoucherTypeRegistry();
        registry.Register(new VoucherTypeDefinition("journal-adjustment", "Phiếu Hạch Toán (CTGS)", "/accounting/journal-voucher", null, [], "", "/accounting/journal-voucher"), null, VoucherIntent.JournalAdjustment);
        registry.Register(new VoucherTypeDefinition("depreciation", "Phiếu Khấu Hao TSCĐ", "/vouchers/depreciation", null, [], "depreciation"), new DepreciationPostingStrategy(), VoucherIntent.Depreciation);
        registry.Register(new VoucherTypeDefinition("deposit", "Phiếu Ký Quỹ / Ký Cược", "/vouchers/deposit", null, [], "deposit"), new DepositPostingStrategy(), VoucherIntent.Deposit);

        Assert.Equal("journal-adjustment", registry.ResolveByIntent(VoucherIntent.JournalAdjustment)?.Code);
        Assert.Equal("/accounting/journal-voucher", registry.ResolveByIntent(VoucherIntent.JournalAdjustment)?.Route);
        Assert.Equal("depreciation", registry.ResolveByIntent(VoucherIntent.Depreciation)?.Code);
        Assert.Equal("deposit", registry.ResolveByIntent(VoucherIntent.Deposit)?.Code);
    }
}