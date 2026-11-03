using VanAn.CoreHub.Services;
using VanAn.Shared.Domain;
using Xunit;

namespace VanAn.Core.Tests.Accounting;

/// <summary>Task PG: parser câu mô tả tự nhiên → intent/amount/date/đối tượng (rule-based, deterministic).</summary>
public class VoucherTextParseServiceTests
{
    private static readonly TenantId Tenant = new(Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd"));
    private static VoucherTextParseService Svc() => new(bankAccounts: null);

    private static Task<ParsedVoucher> Parse(string text) => Svc().ParseAsync(Tenant, text);

    // ---- T1: amount matrix ----
    [Theory]
    [InlineData("thu 100 triệu", 100_000_000)]
    [InlineData("thu một trăm triệu", 100_000_000)]
    [InlineData("thu 100tr", 100_000_000)]
    [InlineData("thu 100 củ", 100_000_000)]
    [InlineData("thu 500k", 500_000)]
    [InlineData("thu năm trăm nghìn", 500_000)]
    [InlineData("thu hai mươi lăm nghìn", 25_000)]
    [InlineData("thu 1 tỷ", 1_000_000_000)]
    [InlineData("thu một tỷ rưỡi", 1_500_000_000)]
    [InlineData("thu 1 tỷ 2", 1_200_000_000)]
    [InlineData("thu 2tr5", 2_500_000)]
    [InlineData("thu 100.000.000", 100_000_000)]
    [InlineData("thu mười lăm triệu", 15_000_000)]
    [InlineData("thu một trăm lẻ năm nghìn", 105_000)]
    [InlineData("thu 1,5 tỷ", 1_500_000_000)]
    public async Task Amount_VietnameseForms_Parsed(string text, decimal expected)
    {
        var r = await Parse(text);
        Assert.Equal(expected, r.Amount);
    }

    // ---- T2: intent matrix ----
    [Fact]
    public async Task Intent_WithdrawBankToCash_IsTransferOut()
    {
        var r = await Parse("hôm nay rút tiền gửi ngân hàng một trăm triệu nhập vào quỹ tiền mặt");
        Assert.Equal(VoucherIntent.TransferOut, r.Intent);
        Assert.Equal(100_000_000m, r.Amount);
        Assert.Equal("112", r.FromAccountCode);
        Assert.Equal("111", r.ToAccountCode);
        Assert.Equal("High", r.Confidence);
    }

    [Fact]
    public async Task Intent_DepositCashToBank_IsTransferIn()
    {
        var r = await Parse("nộp 50 triệu tiền mặt vào tài khoản ngân hàng");
        Assert.Equal(VoucherIntent.TransferIn, r.Intent);
        Assert.Equal("111", r.FromAccountCode);
        Assert.Equal("112", r.ToAccountCode);
    }

    [Fact]
    public async Task Intent_CustomerPaysDebt_IsReceivablePayment()
    {
        var r = await Parse("khách anh Hùng trả nợ 5 triệu bằng chuyển khoản");
        Assert.Equal(VoucherIntent.ReceivablePayment, r.Intent);
        Assert.Equal(5_000_000m, r.Amount);
        Assert.Equal("anh Hùng", r.DoiTuong);
        Assert.Equal("bank", r.PaymentMethod);
        Assert.Equal("High", r.Confidence);
    }

    [Fact]
    public async Task Intent_PaySupplier_IsPayablePayment()
    {
        var r = await Parse("trả nợ nhà cung cấp ABC 20 triệu");
        Assert.Equal(VoucherIntent.PayablePayment, r.Intent);
        Assert.Equal("nhà cung cấp ABC", r.DoiTuong);
    }

    [Fact]
    public async Task Intent_CreditSale_IsReceivable()
    {
        var r = await Parse("bán chịu cho khách chị Lan 8 triệu");
        Assert.Equal(VoucherIntent.Receivable, r.Intent);
        Assert.Contains("Lan", r.DoiTuong);
    }

    [Fact]
    public async Task Intent_RentExpense_IsExpenseWithCategory()
    {
        var r = await Parse("chi tiền thuê mặt bằng 15 triệu hôm qua");
        Assert.Equal(VoucherIntent.Expense, r.Intent);
        Assert.Equal(15_000_000m, r.Amount);
        Assert.Equal("rent", r.Category);
        Assert.Equal(DateTime.Today.AddDays(-1), r.Date);
    }

    [Fact]
    public async Task Intent_CapitalContribution_IsCapital()
    {
        var r = await Parse("chủ góp vốn thêm 200 triệu bằng tiền mặt");
        Assert.Equal(VoucherIntent.Capital, r.Intent);
        Assert.Equal(200_000_000m, r.Amount);
    }

    [Fact]
    public async Task Intent_Advance_IsNotRevenue()
    {
        var r = await Parse("khách đặt cọc trước 3 triệu");
        Assert.Equal(VoucherIntent.Advance, r.Intent);
        Assert.Contains(r.Warnings, w => w.Contains("trả trước"));
    }

    [Fact]
    public async Task Intent_PurchaseGoods_HintsPurchase()
    {
        var r = await Parse("mua hàng hóa nhập kho 30 triệu");
        Assert.Equal(VoucherIntent.Purchase, r.Intent);
    }

    [Fact]
    public async Task Intent_SaleReceipt_IsRevenue()
    {
        var r = await Parse("thu tiền bán hàng 2 triệu tiền mặt");
        Assert.Equal(VoucherIntent.Revenue, r.Intent);
        Assert.Equal("cash", r.PaymentMethod);
    }

    // ---- T3: dates ----
    [Fact]
    public async Task Date_ExplicitAndRelative()
    {
        Assert.Equal(DateTime.Today, (await Parse("thu 1 triệu hôm nay")).Date);
        Assert.Equal(DateTime.Today.AddDays(-1), (await Parse("chi 1 triệu hôm qua")).Date);
        Assert.Equal(DateTime.Today.AddDays(-2), (await Parse("chi 1 triệu hôm kia")).Date);
        var d = (await Parse("thu 1 triệu ngày 25/10")).Date;
        Assert.Equal(new DateTime(DateTime.Today.Year, 10, 25), d);
    }

    // ---- T4: đối tượng ----
    [Theory]
    [InlineData("khách trả nợ 5 triệu", null)]
    [InlineData("khách công ty TNHH ABC trả nợ 5 triệu", "công ty TNHH ABC")]
    public async Task DoiTuong_Extraction(string text, string? expected)
    {
        var r = await Parse(text);
        Assert.Equal(expected, r.DoiTuong);
    }

    // ---- T5: ambiguous → missing, KHÔNG đoán ----
    [Fact]
    public async Task Ambiguous_TransferWithoutDirection_StillTransferWithWarning()
    {
        var r = await Parse("chuyển tiền 2 triệu");
        Assert.True(r.Intent is VoucherIntent.TransferIn or VoucherIntent.TransferOut or VoucherIntent.Unknown);
        Assert.NotEmpty(r.Warnings.Concat(r.MissingFields));
    }

    [Fact]
    public async Task Missing_Amount_FlaggedNotGuessed()
    {
        var r = await Parse("khách anh Hùng trả nợ");
        Assert.Null(r.Amount);
        Assert.Contains("amount", r.MissingFields);
    }

    [Fact]
    public async Task Missing_DoiTuong_FlaggedForCongNo()
    {
        var r = await Parse("thu nợ 5 triệu");
        Assert.Contains("đối tượng", r.MissingFields);
    }

    [Fact]
    public async Task Description_KeepsOriginalText()
    {
        var r = await Parse("  Thu tiền Bán hàng 2 triệu  ");
        Assert.Equal("Thu tiền Bán hàng 2 triệu", r.Description);
    }

    // ---- phí chuyển ----
    [Fact]
    public async Task Fee_ExtractedForTransfer()
    {
        var r = await Parse("rút 50 triệu từ ngân hàng về quỹ phí 30 nghìn");
        Assert.Equal(VoucherIntent.TransferOut, r.Intent);
        Assert.Equal(50_000_000m, r.Amount);
        Assert.Equal(30_000m, r.FeeAmount);
    }

    // ---- Task G2 fixes (RV phát hiện) ----
    [Fact]
    public async Task Fix_NameNumberCollision_AmountAndDoiTuong()
    {
        // "ông Ba" — "ba"=3 từng bị quét nhầm thành amount đầu + cắt tên.
        var r = await Parse("khách ông Ba trả nợ 2tr5");
        Assert.Equal(VoucherIntent.ReceivablePayment, r.Intent);
        Assert.Equal(2_500_000m, r.Amount);
        Assert.Equal("ông Ba", r.DoiTuong);
    }

    [Fact]
    public async Task Fix_WordNumberName_KeepsFullName()
    {
        // "chú Tư" — "tư"=4 (từng làm amount=9tr + mất tên).
        var r = await Parse("thu nợ 2tr5 khách chú Tư trả góp");
        Assert.Equal(2_500_000m, r.Amount);
        Assert.Equal("chú Tư", r.DoiTuong);
    }

    [Fact]
    public async Task Fix_ThanhToanCho_KeepsDoiTuong()
    {
        // "thanh toán" — "anh" trong "thANH toán" từng match substring marker → mất đối tượng.
        var r = await Parse("thanh toán nợ 5tr ck cho nhà cung cấp Nam");
        Assert.Equal(VoucherIntent.PayablePayment, r.Intent);
        Assert.Equal(5_000_000m, r.Amount);
        Assert.Equal("bank", r.PaymentMethod);
        Assert.Equal("nhà cung cấp Nam", r.DoiTuong);
        Assert.DoesNotContain("đối tượng", r.MissingFields);
    }

    [Fact]
    public async Task Fix_ThanhToanCho_NguoiBan()
    {
        var r = await Parse("thanh toán nợ 5 triệu cho người bán Nam");
        Assert.Equal(VoucherIntent.PayablePayment, r.Intent);
        Assert.Equal("người bán Nam", r.DoiTuong);
    }

    // ---- PH2: Loan / LoanRepayment ----

    [Fact]
    public async Task Intent_VayNganHangNhapQuy_IsLoan_NotTransfer()
    {
        // Trước PH2: "vay ngân hàng 100 triệu nhập quỹ" (có cash + bank) bị transfer nuốt.
        var r = await Parse("vay ngân hàng 100 triệu nhập quỹ tiền mặt");
        Assert.Equal(VoucherIntent.Loan, r.Intent);
        Assert.Equal(100_000_000m, r.Amount);
    }

    [Fact]
    public async Task Intent_VayVon_IsLoan()
    {
        var r = await Parse("vay vốn kinh doanh 50 triệu");
        Assert.Equal(VoucherIntent.Loan, r.Intent);
        Assert.Equal(50_000_000m, r.Amount);
        Assert.Equal("High", r.Confidence);
    }

    [Fact]
    public async Task Intent_TraNoVay_IsLoanRepayment_NotPayablePayment()
    {
        // Trước PH2: "trả nợ vay" rơi vào công nợ PayablePayment (Nợ 331 — sai hướng).
        var r = await Parse("trả nợ vay 20 triệu");
        Assert.Equal(VoucherIntent.LoanRepayment, r.Intent);
        Assert.Equal(20_000_000m, r.Amount);
    }

    [Fact]
    public async Task Intent_TraTienVay_IsLoanRepayment()
    {
        var r = await Parse("trả tiền vay ngân hàng 15 triệu");
        Assert.Equal(VoucherIntent.LoanRepayment, r.Intent);
    }

    [Fact]
    public async Task Intent_TraLaiVay_IsExpense_NotLoan()
    {
        // Lãi vay = chi phí (635), không phải trả gốc.
        var r = await Parse("trả lãi vay 5 triệu");
        Assert.Equal(VoucherIntent.Expense, r.Intent);
        Assert.Equal(5_000_000m, r.Amount);
    }

    [Fact]
    public async Task Intent_ChoVay_IsUnknown_NotLoan()
    {
        // "cho vay" = xuất tiền cho vay (128/138) — chưa có phiếu → Unknown (chặn an toàn).
        var r = await Parse("cho công ty X vay 100 triệu");
        Assert.Equal(VoucherIntent.Unknown, r.Intent);
    }

    [Fact]
    public async Task Intent_Loan_NoDoiTuongRequired()
    {
        var r = await Parse("vay 30 triệu");
        Assert.Equal(VoucherIntent.Loan, r.Intent);
        Assert.DoesNotContain("đối tượng", r.MissingFields);
    }
}
