using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using VanAn.CoreHub.Infrastructure.Entities;
using VanAn.CoreHub.Infrastructure.Seed;
using VanAn.CoreHub.Repositories;
using VanAn.CoreHub.Services;
using VanAn.CoreHub.Services.CongNo;
using VanAn.CoreHub.Services.Journal;
using VanAn.CoreHub.Services.Purchase;
using VanAn.CoreHub.Services.Stock;
using VanAn.CoreHub.Tests.TestInfrastructure;
using VanAn.Shared.Domain;
using VanAn.Shared.Domain.Audit;
using Xunit;
using CoreAccountingEntry = VanAn.Shared.Domain.AccountingEntry;

namespace VanAn.Core.Tests.Accounting;

/// <summary>
/// SRS v1.2 W6 tiếp (GĐ1/GĐ2 — luồng 1 cấp duyệt, user chốt 2026-10-31):
/// Create → Draft (KHÔNG JE/công nợ/kho) → Approve → Post (JE + công nợ + gọi stock).
/// Trả hàng → JE đảo + reversal công nợ + stock return. Update/Cancel chỉ Draft|Approved.
/// </summary>
public class PurchaseReceiptServiceTests
{
    private static readonly TenantId Tenant = new(Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee"));
    private static readonly DateTime ReceiptDate = new(2026, 10, 15);

    private static async Task<(PurchaseReceiptService svc, TestContextScope scope, Mock<IStockMovementService> stock)> BuildAsync()
    {
        var scope = VanAnDbContextTestFactory.Create();
        scope.TenantProvider!.SetTenant(Tenant.Value);
        var db = scope.Context;
        _ = await AccountChartSeeder.SeedAsync(db, NullLogger.Instance); // validate TK cần chart
        var hkd = new HKDBookRepository(db, NullLogger<HKDBookRepository>.Instance);
        var entryRepo = new AccountingEntryRepository(db, NullLogger<AccountingEntryRepository>.Instance);
        var periodClosing = new Mock<IPeriodClosingService>();
        periodClosing.Setup(p => p.GetPeriodStatusAsync(It.IsAny<AccountingPeriod>(), It.IsAny<TenantId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(PeriodClosingStatus.Open);
        var audit = new Mock<IAuditTrailService>();
        var congNo = new CongNoService(entryRepo, periodClosing.Object, audit.Object, NullLogger<CongNoService>.Instance);
        var reversal = new ReversalService(entryRepo, NullLogger<ReversalService>.Instance);
        var voucher = new VoucherNumberService(db, NullLogger<VoucherNumberService>.Instance);
        var stock = new Mock<IStockMovementService>();
        var svc = new PurchaseReceiptService(db, hkd, NullLogger<PurchaseReceiptService>.Instance,
            congNo, reversal, voucher, stockMovementService: stock.Object);
        return (svc, scope, stock);
    }

    private static readonly IReadOnlyList<PurchaseReceiptLineInput> Lines =
    [
        new PurchaseReceiptLineInput("Gạo ST25", 10m, 50_000m),
        new PurchaseReceiptLineInput("Sữa tươi", 5m, 100_000m)
    ]; // net = 1.000.000

    private static async Task<PurchaseReceiptEntity> CreateAndPostAsync(PurchaseReceiptService svc,
        PurchasePayMethod payMethod = PurchasePayMethod.Credit, string? cash = null,
        decimal vat = 80_000m, string? sourceEventId = null,
        IReadOnlyList<PurchaseReceiptLineInput>? lines = null)
    {
        var receipt = await svc.CreateAsync(Tenant, "NCC Gạo Miền Tây", "0312345678",
            ReceiptDate, lines ?? Lines, payMethod, cash, "156", vat, sourceEventId: sourceEventId);
        await svc.ApproveAsync(receipt.Id, "ketoantruong");
        return await svc.PostAsync(receipt.Id);
    }

    // ── T0: create → DRAFT, chưa JE/công nợ; post từ Draft bị chặn ──────────
    [Fact]
    public async Task Create_StaysDraft_NoJeNoPayable_PostBlocked()
    {
        var (svc, scope, _) = await BuildAsync();
        using var _ = scope;
        var db = scope.Context;

        var receipt = await svc.CreateAsync(Tenant, "NCC Gạo Miền Tây", "0312345678",
            ReceiptDate, Lines, PurchasePayMethod.Credit, null, "156", 80_000m);

        Assert.Equal(PurchaseReceiptStatus.Draft, receipt.Status);
        Assert.Equal(1_000_000m, receipt.NetAmount);
        Assert.Equal(1_080_000m, receipt.TotalAmount);
        Assert.StartsWith("NM-", receipt.VoucherNumber);
        Assert.Null(receipt.PayableEntryId);
        Assert.Empty(db.JournalEntries.Where(j => j.TenantId == Tenant));
        Assert.Empty(db.AccountingEntries.Where(e => e.TenantId == Tenant));

        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.PostAsync(receipt.Id));
    }

    // ── T1: mua chịu approve+post → JE Nợ 156/Có 331 (+Nợ 133) + công nợ 331+ ──
    [Fact]
    public async Task CreditPurchase_ApprovePost_PostsJe156To331_AndCreatesPayable()
    {
        var (svc, scope, _) = await BuildAsync();
        using var _ = scope;
        var db = scope.Context;

        var receipt = await CreateAndPostAsync(svc);

        Assert.Equal(PurchaseReceiptStatus.Posted, receipt.Status);
        Assert.NotNull(receipt.ApprovedAt);
        Assert.Equal("ketoantruong", receipt.ApprovedBy);
        Assert.NotNull(receipt.PostedAt);
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
        Assert.Equal("133", je.Lines.ElementAt(1).AccountNumber);
        Assert.Equal(80_000m, je.Lines.ElementAt(1).DebitAmount);
        Assert.Equal("331", je.Lines.ElementAt(2).AccountNumber);
        Assert.Equal(1_080_000m, je.Lines.ElementAt(2).CreditAmount);

        var payable = db.AccountingEntries.Single(e => e.TenantId == Tenant);
        Assert.Equal("331", payable.AccountCode);
        Assert.Equal(1_080_000m, payable.Amount);
        Assert.Equal(receipt.PayableEntryId, payable.Id);
    }

    // ── T2: trả ngay + VAT → 3-line JE / Có 111 — không công nợ ────────────
    [Fact]
    public async Task ImmediatePurchase_WithVat_Posts3LineJe_NoPayable()
    {
        var (svc, scope, _) = await BuildAsync();
        using var _ = scope;
        var db = scope.Context;

        var receipt = await CreateAndPostAsync(svc, PurchasePayMethod.Immediate, "111");

        Assert.Null(receipt.PayableEntryId);
        var je = db.JournalEntries.Single(j => j.TenantId == Tenant);
        Assert.Equal(AccountingPostingPurposes.PurchaseCash, je.PostingPurpose);
        Assert.Equal("156", je.Lines.ElementAt(0).AccountNumber);
        Assert.Equal("133", je.Lines.ElementAt(1).AccountNumber);
        Assert.Equal("111", je.Lines.ElementAt(2).AccountNumber);
        Assert.Equal(1_080_000m, je.Lines.ElementAt(2).CreditAmount);
        Assert.Empty(db.AccountingEntries.Where(e => e.TenantId == Tenant));
    }

    // ── T3: trả hàng → JE đảo (purchase-return) + reversal công nợ + stock return ──
    [Fact]
    public async Task Return_PostsReversalJe_AndReversesPayable_AndStockReturn()
    {
        var (svc, scope, stock) = await BuildAsync();
        using var _ = scope;
        var db = scope.Context;

        var receipt = await CreateAndPostAsync(svc);

        var returned = await svc.ReturnAsync(receipt.Id, "Hàng kém chất lượng");

        Assert.Equal(PurchaseReceiptStatus.Returned, returned.Status);

        var journals = db.JournalEntries.Where(j => j.TenantId == Tenant).OrderBy(j => j.CreatedAt).ToList();
        Assert.Equal(2, journals.Count);
        var reversal = journals[1];
        Assert.True(reversal.IsReversal);
        Assert.Equal(AccountingPostingPurposes.PurchaseReturn, reversal.PostingPurpose);
        Assert.Equal(journals[0].JournalEntryId, reversal.ReversedJournalId);
        Assert.Equal(3, reversal.Lines.Count);
        Assert.Equal(1_000_000m, reversal.Lines.ElementAt(0).CreditAmount); // đảo chiều
        Assert.Equal(1_080_000m, reversal.Lines.ElementAt(2).DebitAmount);

        var entries = db.AccountingEntries.Where(e => e.TenantId == Tenant).ToList();
        Assert.Equal(2, entries.Count);
        var rev = entries.Single(e => e.ReversalEntryId == receipt.PayableEntryId);
        Assert.Equal(-1_080_000m, rev.Amount);

        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.ReturnAsync(receipt.Id, "again"));
    }

    // ── T4: duplicate sourceEventId → trả phiếu đã có (idempotent draft) ──────
    [Fact]
    public async Task DuplicateSourceEvent_ReturnsExisting_NoDoublePosting()
    {
        var (svc, scope, _) = await BuildAsync();
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

        await svc.ApproveAsync(first.Id, "ktt");
        await svc.PostAsync(first.Id);
        Assert.Single(db.JournalEntries.Where(j => j.TenantId == Tenant));
        Assert.Single(db.AccountingEntries.Where(e => e.TenantId == Tenant));
    }

    // ── T5: sửa Draft — header + thay dòng; Posted khóa sửa (immutability) ────
    [Fact]
    public async Task UpdateDraft_ReplacesHeaderAndLines_PostedImmutable()
    {
        var (svc, scope, _) = await BuildAsync();
        using var _ = scope;

        var receipt = await svc.CreateAsync(Tenant, "NCC A", null,
            ReceiptDate, Lines, PurchasePayMethod.Credit, null, "156", 0m);

        var updated = await svc.UpdateAsync(receipt.Id, "NCC B mới", "0999",
            ReceiptDate.AddDays(1), [new PurchaseReceiptLineInput("Đường", 2m, 25_000m)],
            PurchasePayMethod.Immediate, "111", "641", 0m, "ref-1", null);

        Assert.Equal("NCC B mới", updated.SupplierName);
        Assert.Equal(PurchasePayMethod.Immediate, updated.PayMethod);
        Assert.Equal("111", updated.CashAccountCode);
        Assert.Single(updated.Lines);
        Assert.Equal(50_000m, updated.NetAmount);

        await svc.ApproveAsync(receipt.Id, null);
        await svc.PostAsync(receipt.Id);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.UpdateAsync(receipt.Id, "X", null, ReceiptDate, Lines,
                PurchasePayMethod.Credit, null, "156", 0m, null, null));
    }

    // ── T6: hủy Draft → Cancelled; hủy Posted bị chặn ───────────────────────
    [Fact]
    public async Task Cancel_OnlyDraftOrApproved_PostedBlocked()
    {
        var (svc, scope, _) = await BuildAsync();
        using var _ = scope;

        var draft = await svc.CreateAsync(Tenant, "NCC A", null,
            ReceiptDate, Lines, PurchasePayMethod.Credit, null, "156", 0m);
        var cancelled = await svc.CancelAsync(draft.Id, "Nhập nhầm NCC");
        Assert.Equal(PurchaseReceiptStatus.Cancelled, cancelled.Status);
        Assert.Equal("Nhập nhầm NCC", cancelled.CancelReason);

        var posted = await CreateAndPostAsync(svc);
        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.CancelAsync(posted.Id, "x"));
    }

    // ── T7: preview — resolve builtin giống JE thật, không lưu DB ──────────
    [Fact]
    public async Task Preview_ResolvesAccounts_NoWrites()
    {
        var (svc, scope, _) = await BuildAsync();
        using var _ = scope;
        var db = scope.Context;

        var preview = await svc.PreviewJournalAsync(Tenant, PurchasePayMethod.Credit, null,
            "156", 1_000_000m, 80_000m);

        Assert.Equal("156", preview.DebitAccount);
        Assert.Equal("331", preview.CreditAccount);
        Assert.Equal(3, preview.Lines.Count);
        Assert.Equal(1_080_000m, preview.TotalAmount);
        Assert.Empty(db.JournalEntries.Where(j => j.TenantId == Tenant));
    }

    // ── T8: validation — TK không tồn tại / cash sai nhóm / thiếu cash ─────
    [Fact]
    public async Task Validation_RejectsInvalidAccounts()
    {
        var (svc, scope, _) = await BuildAsync();
        using var _ = scope;

        await Assert.ThrowsAsync<ArgumentException>(() => svc.CreateAsync(Tenant, "NCC A", null,
            ReceiptDate, Lines, PurchasePayMethod.Credit, null, "999", 0m)); // TK không có trong chart
        await Assert.ThrowsAsync<ArgumentException>(() => svc.CreateAsync(Tenant, "NCC A", null,
            ReceiptDate, Lines, PurchasePayMethod.Immediate, "156", "156", 0m)); // cash không phải 111/112
        await Assert.ThrowsAsync<ArgumentException>(() => svc.CreateAsync(Tenant, "NCC A", null,
            ReceiptDate, Lines, PurchasePayMethod.Immediate, null, "156", 0m)); // thiếu cash
        await Assert.ThrowsAsync<ArgumentException>(() => svc.CreateAsync(Tenant, "NCC A", null,
            ReceiptDate, [], PurchasePayMethod.Credit, null, "156", 0m)); // không dòng hàng
    }

    // ── T9: post gọi stock service cho dòng link Ingredient; ApplyStock idempotent ──
    [Fact]
    public async Task Post_CallsStockService_ForLinkedLines()
    {
        var (svc, scope, stock) = await BuildAsync();
        using var _ = scope;

        var ingredientId = Guid.NewGuid();
        var lines = new List<PurchaseReceiptLineInput>
        {
            new("Gạo ST25", 10m, 50_000m, ingredientId),
            new("Chi phí vận chuyển", 1m, 100_000m) // dòng tự do — không kho
        };

        var receipt = await CreateAndPostAsync(svc, lines: lines);

        stock.Verify(s => s.ApplyReceiptStockAsync(
            It.Is<PurchaseReceiptEntity>(r => r.Id == receipt.Id), It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── T10: return gọi stock return khi đã ghi kho ─────────────────────────
    [Fact]
    public async Task Return_CallsStockReturn_WhenStockApplied()
    {
        var (svc, scope, stock) = await BuildAsync();
        using var _ = scope;
        var db = scope.Context;

        var lines = new List<PurchaseReceiptLineInput> { new("Gạo", 10m, 50_000m, Guid.NewGuid()) };
        var receipt = await CreateAndPostAsync(svc, lines: lines);

        // Giả lập stock đã ghi (service thật ở ShopERP/SQLite — mock trả movement count).
        stock.Setup(s => s.ApplyReceiptStockAsync(It.IsAny<PurchaseReceiptEntity>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);
        var reloaded = await svc.GetByIdAsync(receipt.Id);
        Assert.NotNull(reloaded!.StockAppliedAt); // MarkStockApplied chạy sau ApplyReceiptStockAsync

        await svc.ReturnAsync(receipt.Id, "Trả hàng");
        stock.Verify(s => s.ApplyReturnStockAsync(
            It.Is<PurchaseReceiptEntity>(r => r.Id == receipt.Id), It.IsAny<CancellationToken>()), Times.Once);
    }
}
