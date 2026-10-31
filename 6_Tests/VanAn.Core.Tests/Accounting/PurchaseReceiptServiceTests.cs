using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using VanAn.CoreHub.Infrastructure.Entities;
using VanAn.CoreHub.Repositories;
using VanAn.CoreHub.Services;
using VanAn.CoreHub.Services.CongNo;
using VanAn.CoreHub.Services.Journal;
using VanAn.CoreHub.Services.Purchase;
using VanAn.CoreHub.Tests.TestInfrastructure;
using VanAn.Shared.Domain;
using VanAn.Shared.Domain.Audit;
using Xunit;
using CoreAccountingEntry = VanAn.Shared.Domain.AccountingEntry;

namespace VanAn.Core.Tests.Accounting;

/// <summary>
/// SRS v1.2 W6/E — phiếu nhập mua (task card E §5):
/// T1 credit JE theo map (156 DN) + công nợ 331 · T2 cash + VAT 3-line JE ·
/// T3 trả hàng → JE đảo + reversal công nợ · T4 duplicate sourceEvent → single posting.
/// </summary>
public class PurchaseReceiptServiceTests
{
    private static readonly TenantId Tenant = new(Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee"));
    private static readonly DateTime ReceiptDate = new(2026, 10, 15);

    private static (PurchaseReceiptService svc, TestContextScope scope) Build()
    {
        var scope = VanAnDbContextTestFactory.Create();
        scope.TenantProvider!.SetTenant(Tenant.Value);
        var db = scope.Context;
        var hkd = new HKDBookRepository(db, NullLogger<HKDBookRepository>.Instance);
        var entryRepo = new AccountingEntryRepository(db, NullLogger<AccountingEntryRepository>.Instance);
        var periodClosing = new Mock<IPeriodClosingService>();
        periodClosing.Setup(p => p.GetPeriodStatusAsync(It.IsAny<AccountingPeriod>(), It.IsAny<TenantId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(PeriodClosingStatus.Open);
        var audit = new Mock<IAuditTrailService>();
        var congNo = new CongNoService(entryRepo, periodClosing.Object, audit.Object, NullLogger<CongNoService>.Instance);
        var reversal = new ReversalService(entryRepo, NullLogger<ReversalService>.Instance);
        var voucher = new VoucherNumberService(db, NullLogger<VoucherNumberService>.Instance);
        var svc = new PurchaseReceiptService(db, hkd, NullLogger<PurchaseReceiptService>.Instance,
            congNo, reversal, voucher);
        return (svc, scope);
    }

    private static readonly IReadOnlyList<PurchaseReceiptLineInput> Lines =
    [
        new PurchaseReceiptLineInput("Gạo ST25", 10m, 50_000m),
        new PurchaseReceiptLineInput("Sữa tươi", 5m, 100_000m)
    ]; // net = 1.000.000

    // ── T1: mua chịu → JE Nợ 156/Có 331 + phiếu công nợ 331+ ────────────────
    [Fact]
    public async Task CreditPurchase_PostsJe156To331_AndCreatesPayable()
    {
        var (svc, scope) = Build();
        using var _ = scope;
        var db = scope.Context;

        var receipt = await svc.CreateAsync(Tenant, "NCC Gạo Miền Tây", "0312345678",
            ReceiptDate, Lines, PurchasePayMethod.Credit, cashAccountCode: null,
            debitAccountCode: "156", vatAmount: 80_000m);

        Assert.Equal(1_000_000m, receipt.NetAmount);
        Assert.Equal(1_080_000m, receipt.TotalAmount);
        Assert.StartsWith("NM-", receipt.VoucherNumber);
        Assert.NotNull(receipt.PayableEntryId);

        var journals = db.JournalEntries.Where(j => j.TenantId == Tenant).ToList();
        Assert.Single(journals);
        var je = journals[0];
        Assert.Equal(AccountingPostingPurposes.Purchase, je.PostingPurpose);
        Assert.Equal(PurchaseReceiptService.ReferenceType, je.ReferenceType);
        Assert.Equal(receipt.Id, je.ReferenceId);
        Assert.Equal(3, je.Lines.Count);
        Assert.Equal("156", je.Lines.ElementAt(0).AccountNumber);
        Assert.Equal(1_000_000m, je.Lines.ElementAt(0).DebitAmount);
        Assert.Equal("133", je.Lines.ElementAt(1).AccountNumber); // VAT đầu vào
        Assert.Equal(80_000m, je.Lines.ElementAt(1).DebitAmount);
        Assert.Equal("331", je.Lines.ElementAt(2).AccountNumber);
        Assert.Equal(1_080_000m, je.Lines.ElementAt(2).CreditAmount);

        // Phiếu công nợ 331+ (CongNo ledger — thanh toán sau qua payable-payment).
        var payable = db.AccountingEntries.Single(e => e.TenantId == Tenant);
        Assert.Equal("331", payable.AccountCode);
        Assert.Equal(1_080_000m, payable.Amount);
        Assert.Equal("NCC Gạo Miền Tây", payable.Vendor);
        Assert.Equal(receipt.PayableEntryId, payable.Id);
    }

    // ── T2: trả ngay + VAT → 3-line JE Nợ 156 + Nợ 133 / Có 111 — không công nợ ──
    [Fact]
    public async Task ImmediatePurchase_WithVat_Posts3LineJe_NoPayable()
    {
        var (svc, scope) = Build();
        using var _ = scope;
        var db = scope.Context;

        var receipt = await svc.CreateAsync(Tenant, "Tạp hóa An Bình", null,
            ReceiptDate, Lines, PurchasePayMethod.Immediate, cashAccountCode: "111",
            debitAccountCode: "156", vatAmount: 80_000m);

        Assert.Null(receipt.PayableEntryId);
        var je = db.JournalEntries.Single(j => j.TenantId == Tenant);
        Assert.Equal(AccountingPostingPurposes.PurchaseCash, je.PostingPurpose);
        Assert.Equal("156", je.Lines.ElementAt(0).AccountNumber);
        Assert.Equal("133", je.Lines.ElementAt(1).AccountNumber);
        Assert.Equal("111", je.Lines.ElementAt(2).AccountNumber);
        Assert.Equal(1_080_000m, je.Lines.ElementAt(2).CreditAmount);
        Assert.Empty(db.AccountingEntries.Where(e => e.TenantId == Tenant));
    }

    // ── T3: trả hàng → JE đảo (purchase-return, ReversedJournalId) + reversal công nợ ──
    [Fact]
    public async Task Return_PostsReversalJe_AndReversesPayable()
    {
        var (svc, scope) = Build();
        using var _ = scope;
        var db = scope.Context;

        var receipt = await svc.CreateAsync(Tenant, "NCC Gạo", null,
            ReceiptDate, Lines, PurchasePayMethod.Credit, null, "156", 80_000m);

        var returned = await svc.ReturnAsync(receipt.Id, "Hàng kém chất lượng");

        Assert.Equal(PurchaseReceiptStatus.Returned, returned.Status);

        // JE đảo: purpose purchase-return, IsReversal, link ReversedJournalId, lines swapped.
        var journals = db.JournalEntries.Where(j => j.TenantId == Tenant).OrderBy(j => j.CreatedAt).ToList();
        Assert.Equal(2, journals.Count);
        var reversal = journals[1];
        Assert.True(reversal.IsReversal);
        Assert.Equal(AccountingPostingPurposes.PurchaseReturn, reversal.PostingPurpose);
        Assert.Equal(journals[0].JournalEntryId, reversal.ReversedJournalId);
        Assert.Equal(3, reversal.Lines.Count);
        Assert.Equal("156", reversal.Lines.ElementAt(0).AccountNumber);
        Assert.Equal(1_000_000m, reversal.Lines.ElementAt(0).CreditAmount); // đảo chiều
        Assert.Equal("331", reversal.Lines.ElementAt(2).AccountNumber);
        Assert.Equal(1_080_000m, reversal.Lines.ElementAt(2).DebitAmount);

        // Công nợ: reversal entry âm link ReversalEntryId → nợ net về 0.
        var entries = db.AccountingEntries.Where(e => e.TenantId == Tenant).ToList();
        Assert.Equal(2, entries.Count);
        var rev = entries.Single(e => e.ReversalEntryId == receipt.PayableEntryId);
        Assert.Equal(-1_080_000m, rev.Amount);

        // Trả hàng lần 2 → chặn.
        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.ReturnAsync(receipt.Id, "again"));
    }

    // ── T4: duplicate sourceEventId → trả phiếu đã có, không JE/entry trùng ──
    [Fact]
    public async Task DuplicateSourceEvent_ReturnsExisting_NoDoublePosting()
    {
        var (svc, scope) = Build();
        using var _ = scope;
        var db = scope.Context;

        var first = await svc.CreateAsync(Tenant, "NCC A", null,
            ReceiptDate, Lines, PurchasePayMethod.Credit, null, "156", 0m,
            sourceEventId: "GRN-001");
        var second = await svc.CreateAsync(Tenant, "NCC A", null,
            ReceiptDate, Lines, PurchasePayMethod.Credit, null, "156", 0m,
            sourceEventId: "GRN-001");

        Assert.Equal(first.Id, second.Id);
        Assert.Single(db.PurchaseReceipts.Where(r => r.TenantId == Tenant));
        Assert.Single(db.JournalEntries.Where(j => j.TenantId == Tenant));
        Assert.Single(db.AccountingEntries.Where(e => e.TenantId == Tenant));
    }
}
