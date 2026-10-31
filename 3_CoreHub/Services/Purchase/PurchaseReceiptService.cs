using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using VanAn.CoreHub.Infrastructure;
using VanAn.CoreHub.Infrastructure.Entities;
using VanAn.CoreHub.Repositories;
using VanAn.CoreHub.Services.CongNo;
using VanAn.CoreHub.Services.Stock;
using VanAn.CoreHub.Services.Journal;
using VanAn.Shared.Domain;
using VanAn.Shared.Domain.Audit;
using VanAn.Shared.DTOs;

namespace VanAn.CoreHub.Services.Purchase;

/// <summary>Dòng hàng đầu vào khi lập phiếu nhập mua — IngredientId link kho (nullable = dòng tự do).</summary>
public sealed record PurchaseReceiptLineInput(string ItemName, decimal Quantity, decimal UnitPrice,
    Guid? IngredientId = null);

/// <summary>Một dòng bút toán preview (trước khi ghi).</summary>
public sealed record JournalPreviewLine(string AccountCode, decimal DebitAmount, decimal CreditAmount, string Description);

/// <summary>Preview bút toán nhập mua — resolve account-map THẬT (không lưu DB).</summary>
public sealed record PurchaseJournalPreview(
    string DebitAccount, string CreditAccount, string MappingVersion,
    decimal NetAmount, decimal VatAmount, decimal TotalAmount,
    IReadOnlyList<JournalPreviewLine> Lines);

/// <summary>Option TK cho dropdown (code + tên).</summary>
public sealed record AccountOption(string Code, string Name);

/// <summary>
/// SRS v1.2 W6 tiếp (user chốt 2026-10-31): phiếu nhập mua — luồng 1 cấp duyệt
/// Draft → Approved → Posted → Returned (+ Cancelled). JE/công nợ/kho CHỈ ghi khi
/// Post (không phải lúc tạo) — phiếu Draft/Approved sửa/hủy được, Posted khóa sửa.
/// Stock ghi qua IStockMovementService optional (chỉ ShopERP host đăng ký — SQLite).
/// </summary>
public interface IPurchaseReceiptService
{
    /// <summary>Tạo phiếu ở trạng thái DRAFT (chưa JE/công nợ/kho — ghi khi Post).
    /// FR-PUR-007: sourceEventId lặp → trả phiếu đã có.</summary>
    Task<PurchaseReceiptEntity> CreateAsync(TenantId tenantId, string supplierName, string? supplierRef,
        DateTime receiptDate, IReadOnlyList<PurchaseReceiptLineInput> lines,
        PurchasePayMethod payMethod, string? cashAccountCode, string debitAccountCode,
        decimal vatAmount = 0m, string? reference = null, string? sourceEventId = null,
        DateTime? dueDate = null, CancellationToken ct = default);

    /// <summary>Sửa phiếu DRAFT — header + thay toàn bộ dòng. Posted khóa sửa (trả hàng/điều chỉnh).</summary>
    Task<PurchaseReceiptEntity> UpdateAsync(Guid receiptId, string supplierName, string? supplierRef,
        DateTime receiptDate, IReadOnlyList<PurchaseReceiptLineInput> lines,
        PurchasePayMethod payMethod, string? cashAccountCode, string debitAccountCode,
        decimal vatAmount = 0m, string? reference = null, DateTime? dueDate = null,
        CancellationToken ct = default);

    /// <summary>Duyệt phiếu (1 cấp) — Draft → Approved.</summary>
    Task<PurchaseReceiptEntity> ApproveAsync(Guid receiptId, string? approvedBy, CancellationToken ct = default);

    /// <summary>Ghi sổ — Approved → Posted: JE (purchase/purchase-cash) + công nợ 331 (credit)
    /// + StockMovement (best-effort, fail-safe — drift → ApplyStockAsync/R13).</summary>
    Task<PurchaseReceiptEntity> PostAsync(Guid receiptId, CancellationToken ct = default);

    /// <summary>Hủy phiếu — chỉ Draft|Approved.</summary>
    Task<PurchaseReceiptEntity> CancelAsync(Guid receiptId, string reason, CancellationToken ct = default);

    /// <summary>Trả hàng/đảo phiếu — Posted → Returned: JE đảo (purchase-return, ReversedJournalId)
    /// + reversal phiếu công nợ 331 + movement PurchaseReturn.</summary>
    Task<PurchaseReceiptEntity> ReturnAsync(Guid receiptId, string reason, CancellationToken ct = default);

    /// <summary>Retry ghi kho cho phiếu Posted chưa StockApplied (sau lỗi cross-DB).</summary>
    Task<PurchaseReceiptEntity> ApplyStockAsync(Guid receiptId, CancellationToken ct = default);

    /// <summary>Preview bút toán — resolve account-map THẬT, không lưu (FR-RULE-006 tối giản).</summary>
    Task<PurchaseJournalPreview> PreviewJournalAsync(TenantId tenantId, PurchasePayMethod payMethod,
        string? cashAccountCode, string debitAccountCode, decimal netAmount, decimal vatAmount,
        CancellationToken ct = default);

    /// <summary>JE đã ghi của phiếu (gốc + đảo) — chi tiết chứng từ.</summary>
    Task<List<JournalEntry>> ListJournalsAsync(Guid receiptId, CancellationToken ct = default);

    /// <summary>Options TK ghi Nợ cho dropdown lập phiếu (15x + 6x global chart + tenant custom active).</summary>
    Task<IReadOnlyList<AccountOption>> GetDebitAccountOptionsAsync(TenantId tenantId, CancellationToken ct = default);

    /// <summary>Options TK tiền cho trả ngay (111 + BankAccounts active của tenant).</summary>
    Task<IReadOnlyList<AccountOption>> GetCashAccountOptionsAsync(TenantId tenantId, CancellationToken ct = default);

    Task<PurchaseReceiptEntity?> GetByIdAsync(Guid receiptId, CancellationToken ct = default);
    Task<List<PurchaseReceiptEntity>> ListAsync(TenantId tenantId, DateTime? from = null, DateTime? to = null,
        PurchaseReceiptStatus? status = null, string? supplierFilter = null, CancellationToken ct = default);
}

/// <summary>
/// Posting qua account-map (event purchase → ({src},331) · purchase-cash → ({src},{cash});
/// {src}=DebitAccountCode do tenant chọn: 156 DN · 611 HTX · 6xx chi phí HKD — card §2.1).
/// VAT đầu vào → dòng Nợ 133 riêng (hardcode — TK thuế cố định). Resolver miss → builtin inline.
/// JE: ReferenceType "PurchaseReceipt" + ReferenceId=receipt.Id + purpose — idempotent qua
/// ExistsByReferenceAsync + unique index Phase A (Tenant,RefType,RefId,Purpose).
/// Stock: cross-DB (PG ↔ SQLite) — fail-safe + StockAppliedAt flag cho retry (GĐ2).
/// </summary>
public sealed class PurchaseReceiptService(
    IAccountingDbContext dbContext,
    IHKDBookRepository hkdBookRepository,
    ILogger<PurchaseReceiptService> logger,
    ICongNoService? congNoService = null,
    IReversalService? reversalService = null,
    IVoucherNumberService? voucherNumberService = null,
    IAccountMapResolver? accountMapResolver = null,
    IAuditTrailService? auditTrailService = null,
    IStockMovementService? stockMovementService = null) : IPurchaseReceiptService
{
    public const string ReferenceType = "PurchaseReceipt";
    public const string InputVatAccount = "133";
    private const string VoucherType = VoucherNumberService.TypePurchaseReceipt;

    public async Task<PurchaseReceiptEntity> CreateAsync(TenantId tenantId, string supplierName, string? supplierRef,
        DateTime receiptDate, IReadOnlyList<PurchaseReceiptLineInput> lines,
        PurchasePayMethod payMethod, string? cashAccountCode, string debitAccountCode,
        decimal vatAmount = 0m, string? reference = null, string? sourceEventId = null,
        DateTime? dueDate = null, CancellationToken ct = default)
    {
        await ValidateInputsAsync(tenantId, lines, payMethod, cashAccountCode, debitAccountCode, ct);

        // FR-PUR-007: sync/retry lặp cùng source event → trả phiếu đã tạo (không phiếu trùng).
        if (!string.IsNullOrWhiteSpace(sourceEventId))
        {
            PurchaseReceiptEntity? existing = await dbContext.PurchaseReceipts.IgnoreQueryFilters()
                .Include(r => r.Lines)
                .FirstOrDefaultAsync(r => r.TenantId == tenantId && r.SourceEventId == sourceEventId, ct);
            if (existing != null)
            {
                logger.LogInformation("PurchaseReceipt: SourceEventId {SourceEventId} đã tồn tại (receipt {Id}) — skip (idempotent)",
                    sourceEventId, existing.Id);
                return existing;
            }
        }

        string voucher = voucherNumberService != null
            ? await voucherNumberService.NextNumberAsync(tenantId, VoucherType, receiptDate.Year, ct)
            : $"{VoucherType}-{receiptDate:yyyy}-{Guid.NewGuid().ToString("N")[..5].ToUpperInvariant()}";

        var receipt = new PurchaseReceiptEntity(tenantId, voucher, supplierName, supplierRef,
            receiptDate, payMethod, payMethod == PurchasePayMethod.Immediate ? cashAccountCode : null,
            debitAccountCode, vatAmount, reference, sourceEventId, dueDate);
        foreach (PurchaseReceiptLineInput input in lines)
        {
            receipt.AddLine(input.ItemName, input.Quantity, input.UnitPrice, input.IngredientId);
        }
        dbContext.PurchaseReceipts.Add(receipt);
        await dbContext.SaveChangesAsync(ct);
        await AuditSafeAsync(tenantId, receipt, "create", ct);
        return receipt;
    }

    public async Task<PurchaseReceiptEntity> UpdateAsync(Guid receiptId, string supplierName, string? supplierRef,
        DateTime receiptDate, IReadOnlyList<PurchaseReceiptLineInput> lines,
        PurchasePayMethod payMethod, string? cashAccountCode, string debitAccountCode,
        decimal vatAmount = 0m, string? reference = null, DateTime? dueDate = null,
        CancellationToken ct = default)
    {
        PurchaseReceiptEntity receipt = await LoadAsync(receiptId, ct);
        await ValidateInputsAsync(receipt.TenantId, lines, payMethod, cashAccountCode, debitAccountCode, ct);

        receipt.UpdateDraft(supplierName, supplierRef, receiptDate, payMethod, cashAccountCode,
            debitAccountCode, vatAmount, reference, dueDate);
        // Thay dòng: tracked RemoveRange + DbSet Add (KHÔNG chạm nav — EF auto-fixup populates
        // receipt.Lines với tracked entities; orphan-fixup + delete đụng row → concurrency ex).
        List<PurchaseReceiptLineEntity> oldLines = receipt.Lines.ToList();
        dbContext.PurchaseReceiptLines.RemoveRange(oldLines);
        List<PurchaseReceiptLineEntity> newLines = lines.Select(i =>
            new PurchaseReceiptLineEntity(receipt.TenantId, receipt.Id,
                i.ItemName, i.Quantity, i.UnitPrice, i.IngredientId)).ToList();
        dbContext.PurchaseReceiptLines.AddRange(newLines);
        receipt.SetTotals(newLines.Sum(l => l.LineTotal));
        await dbContext.SaveChangesAsync(ct);
        // Nav in-memory: bỏ entity đã xóa (đã detach post-save); dòng mới EF fixup
        // có thể đã add vào nav — chỉ add nếu chưa có (tránh trùng).
        foreach (PurchaseReceiptLineEntity old in oldLines)
        {
            receipt.Lines.Remove(old);
        }
        foreach (PurchaseReceiptLineEntity nl in newLines)
        {
            if (!receipt.Lines.Contains(nl))
            {
                receipt.Lines.Add(nl);
            }
        }
        await AuditSafeAsync(receipt.TenantId, receipt, "update", ct);
        return receipt;
    }

    public async Task<PurchaseReceiptEntity> ApproveAsync(Guid receiptId, string? approvedBy, CancellationToken ct = default)
    {
        PurchaseReceiptEntity receipt = await LoadAsync(receiptId, ct);
        receipt.Approve(approvedBy);
        await dbContext.SaveChangesAsync(ct);
        await AuditSafeAsync(receipt.TenantId, receipt, "approve", ct);
        return receipt;
    }

    public async Task<PurchaseReceiptEntity> PostAsync(Guid receiptId, CancellationToken ct = default)
    {
        PurchaseReceiptEntity receipt = await LoadAsync(receiptId, ct);
        if (receipt.Status != PurchaseReceiptStatus.Approved)
            throw new InvalidOperationException($"Chỉ ghi sổ phiếu đã duyệt (hiện {receipt.Status}).");

        // JE: credit → Nợ {debit} + Nợ 133 (nếu VAT) / Có 331; cash → Có {cashAccount}.
        string eventCode = receipt.PayMethod == PurchasePayMethod.Credit
            ? AccountMapEventCodes.Purchase
            : AccountMapEventCodes.PurchaseCash;
        string purpose = receipt.PayMethod == PurchasePayMethod.Credit
            ? AccountingPostingPurposes.Purchase
            : AccountingPostingPurposes.PurchaseCash;
        string creditFallback = receipt.PayMethod == PurchasePayMethod.Credit ? "331" : receipt.CashAccountCode!;
        string desc = $"{(receipt.PayMethod == PurchasePayMethod.Credit ? "Nhập mua chịu" : "Nhập mua trả ngay")} {receipt.VoucherNumber} — {receipt.SupplierName}";

        await PostJournalAsync(receipt, eventCode, purpose, receipt.DebitAccountCode, creditFallback,
            receipt.NetAmount, receipt.VatAmount, receipt.TotalAmount, receipt.ReceiptDate, desc, ct);

        // Mua chịu → phiếu công nợ 331+ (CongNo ledger + FIFO aging + thanh toán sau qua payable-payment).
        // Fail-safe: lỗi công nợ không chặn post (drift → reconcile R-checks bắt).
        if (receipt.PayMethod == PurchasePayMethod.Credit && congNoService != null)
        {
            try
            {
                AccountingEntryDto payable = await congNoService.CreatePayableAsync(
                    receipt.TenantId, receipt.TotalAmount, receipt.SupplierName,
                    $"{desc} (VAT {receipt.VatAmount:N0})", receipt.ReceiptDate,
                    isPayment: false, mst: receipt.SupplierRef, reference: receipt.Id.ToString(), cancellationToken: ct);
                receipt.SetPayableEntry(payable.Id);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "PurchaseReceipt {Id}: tạo công nợ 331 lỗi — phiếu vẫn post (drift → R-checks)", receipt.Id);
            }
        }

        receipt.MarkPosted();
        await dbContext.SaveChangesAsync(ct);
        await TryApplyStockAsync(receipt, ct);
        await AuditSafeAsync(receipt.TenantId, receipt, "post", ct);
        return receipt;
    }

    public async Task<PurchaseReceiptEntity> CancelAsync(Guid receiptId, string reason, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("Reason is required.", nameof(reason));
        PurchaseReceiptEntity receipt = await LoadAsync(receiptId, ct);
        receipt.Cancel(reason);
        await dbContext.SaveChangesAsync(ct);
        await AuditSafeAsync(receipt.TenantId, receipt, "cancel", ct);
        return receipt;
    }

    public async Task<PurchaseReceiptEntity> ReturnAsync(Guid receiptId, string reason, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("Reason is required.", nameof(reason));

        PurchaseReceiptEntity receipt = await LoadAsync(receiptId, ct);

        // JE đảo: tìm JE gốc (purchase|purchase-cash, !IsReversal) → reversal purpose purchase-return.
        JournalEntry? original = await dbContext.JournalEntries.IgnoreQueryFilters()
            .Include(j => j.Lines)
            .FirstOrDefaultAsync(j => j.TenantId == receipt.TenantId
                && j.ReferenceType == ReferenceType && j.ReferenceId == receiptId && !j.IsReversal, ct);
        if (original != null)
        {
            bool alreadyReturned = await dbContext.JournalEntries.IgnoreQueryFilters()
                .AnyAsync(j => j.TenantId == receipt.TenantId && j.ReferenceType == ReferenceType
                    && j.ReferenceId == receiptId && j.IsReversal, ct);
            if (!alreadyReturned)
            {
                JournalEntry reversal = new(receipt.TenantId, DateTime.UtcNow,
                    $"Trả hàng {receipt.VoucherNumber} — {reason}",
                    ReferenceType, receiptId,
                    isReversal: true, reversedJournalId: original.JournalEntryId,
                    postingPurpose: AccountingPostingPurposes.PurchaseReturn);
                foreach (JournalEntryLine line in original.Lines)
                {
                    reversal.AddLine(line.AccountNumber, line.CreditAmount, line.DebitAmount,
                        $"Reversal of: {line.Description}");
                }
                await hkdBookRepository.AddToBookAsync(reversal, AccountingBookType.S2b_HKD, ct);
            }
        }
        else
        {
            logger.LogWarning("PurchaseReceipt {Id}: không tìm thấy JE gốc — chỉ đảo công nợ/status", receiptId);
        }

        // Đảo phiếu công nợ 331 (mua chịu) — ReversalEntryId pattern → nợ net về 0 trong FIFO stream.
        // Fail-safe: đã đảo tay trước đó / không tìm thấy → log + tiếp tục (không chặn trả hàng).
        if (receipt.PayableEntryId is Guid payableId && reversalService != null)
        {
            try
            {
                bool alreadyReversed = await dbContext.AccountingEntries.IgnoreQueryFilters()
                    .AnyAsync(e => e.TenantId == receipt.TenantId && e.ReversalEntryId == payableId, ct);
                if (!alreadyReversed)
                {
                    _ = await reversalService.CreateReversalEntryAsync(
                        new AccountingEntryId(payableId), receipt.TenantId, $"Trả hàng {receipt.VoucherNumber} — {reason}", ct);
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "PurchaseReceipt {Id}: đảo công nợ {PayableId} lỗi — tiếp tục (drift → R-checks)", receiptId, payableId);
            }
        }

        receipt.MarkReturned(reason);
        await dbContext.SaveChangesAsync(ct);

        // Stock out — chỉ khi phiếu đã ghi kho trước đó (fail-safe; drift → R13).
        if (stockMovementService != null && receipt.StockAppliedAt != null)
        {
            try
            {
                int written = await stockMovementService.ApplyReturnStockAsync(receipt, ct);
                logger.LogInformation("PurchaseReceipt {Id}: trả hàng — {Count} movement PurchaseReturn", receiptId, written);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "PurchaseReceipt {Id}: movement trả hàng lỗi — drift → R13", receiptId);
            }
        }

        await AuditSafeAsync(receipt.TenantId, receipt, "return", ct);
        return receipt;
    }

    public async Task<PurchaseReceiptEntity> ApplyStockAsync(Guid receiptId, CancellationToken ct = default)
    {
        PurchaseReceiptEntity receipt = await LoadAsync(receiptId, ct);
        if (receipt.Status != PurchaseReceiptStatus.Posted)
            throw new InvalidOperationException($"Chỉ ghi kho cho phiếu đã Posted (hiện {receipt.Status}).");
        await TryApplyStockAsync(receipt, ct);
        return receipt;
    }

    public async Task<PurchaseJournalPreview> PreviewJournalAsync(TenantId tenantId, PurchasePayMethod payMethod,
        string? cashAccountCode, string debitAccountCode, decimal netAmount, decimal vatAmount,
        CancellationToken ct = default)
    {
        if (payMethod == PurchasePayMethod.Immediate && string.IsNullOrWhiteSpace(cashAccountCode))
            throw new ArgumentException("CashAccountCode (111/112x) bắt buộc khi trả ngay.", nameof(cashAccountCode));

        string eventCode = payMethod == PurchasePayMethod.Credit
            ? AccountMapEventCodes.Purchase
            : AccountMapEventCodes.PurchaseCash;
        string creditFallback = payMethod == PurchasePayMethod.Credit ? "331" : cashAccountCode!;

        AccountMapResult? map = accountMapResolver != null
            ? await accountMapResolver.ResolveAsync(eventCode, tenantId, debitAccountCode, cashAccountCode, ct)
            : null;
        (string debit, string credit, string version) = map != null
            ? (map.Debit, map.Credit, map.MappingVersion ?? "rule")
            : (debitAccountCode, creditFallback, "builtin");

        decimal total = netAmount + vatAmount;
        var lines = new List<JournalPreviewLine>
        {
            new(debit, netAmount, 0m, payMethod == PurchasePayMethod.Credit ? "Nhập mua chịu — tiền hàng" : "Nhập mua trả ngay — tiền hàng")
        };
        if (vatAmount > 0)
        {
            lines.Add(new JournalPreviewLine(InputVatAccount, vatAmount, 0m, "VAT đầu vào"));
        }
        lines.Add(new JournalPreviewLine(credit, 0m, total,
            payMethod == PurchasePayMethod.Credit ? "Phải trả nhà cung cấp" : "Thanh toán"));

        return new PurchaseJournalPreview(debit, credit, version, netAmount, vatAmount, total, lines);
    }

    public async Task<List<JournalEntry>> ListJournalsAsync(Guid receiptId, CancellationToken ct = default)
    {
        PurchaseReceiptEntity receipt = await LoadAsync(receiptId, ct);
        return await dbContext.JournalEntries.IgnoreQueryFilters()
            .AsNoTracking()
            .Include(j => j.Lines)
            .Where(j => j.TenantId == receipt.TenantId && j.ReferenceType == ReferenceType && j.ReferenceId == receiptId)
            .OrderBy(j => j.EntryDate)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<AccountOption>> GetDebitAccountOptionsAsync(TenantId tenantId, CancellationToken ct = default)
    {
        // TK phổ biến ghi Nợ nhập mua: 15x hàng tồn kho + 6x chi phí/mua hàng (mọi chuẩn)
        // + TK custom active của tenant. Distinct theo code — nhiều chuẩn trùng code.
        List<(string Code, string Name)> global = await dbContext.AccountCharts.AsNoTracking()
            .Where(c => c.AccountCode.StartsWith("15") || c.AccountCode.StartsWith("6"))
            .Select(c => new ValueTuple<string, string>(c.AccountCode, c.AccountName))
            .ToListAsync(ct);
        List<(string Code, string Name)> custom = await dbContext.TenantAccountCharts.AsNoTracking()
            .Where(t => t.TenantId == tenantId.Value && t.IsActive && t.Source == TenantAccountChartSources.Custom)
            .Select(t => new ValueTuple<string, string>(t.AccountCode, t.AccountName))
            .ToListAsync(ct);

        var options = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach ((string code, string name) in global)
        {
            options.TryAdd(code, name);
        }
        foreach ((string code, string name) in custom)
        {
            options[code] = name; // custom override tên
        }
        return options.Select(kv => new AccountOption(kv.Key, kv.Value)).ToList();
    }

    public async Task<IReadOnlyList<AccountOption>> GetCashAccountOptionsAsync(TenantId tenantId, CancellationToken ct = default)
    {
        var options = new List<AccountOption> { new("111", "Tiền mặt") };
        List<BankAccountEntity> banks = await dbContext.BankAccounts.AsNoTracking()
            .Where(b => b.TenantId == tenantId && b.IsActive)
            .OrderBy(b => b.AccountCode)
            .ToListAsync(ct);
        foreach (BankAccountEntity bank in banks)
        {
            if (options.All(o => o.Code != bank.AccountCode))
            {
                options.Add(new AccountOption(bank.AccountCode, bank.DisplayName));
            }
        }
        return options;
    }

    public async Task<PurchaseReceiptEntity?> GetByIdAsync(Guid receiptId, CancellationToken ct = default)
        => await dbContext.PurchaseReceipts.IgnoreQueryFilters()
            .Include(r => r.Lines)
            .FirstOrDefaultAsync(r => r.Id == receiptId, ct);

    public async Task<List<PurchaseReceiptEntity>> ListAsync(TenantId tenantId, DateTime? from = null, DateTime? to = null,
        PurchaseReceiptStatus? status = null, string? supplierFilter = null, CancellationToken ct = default)
    {
        IQueryable<PurchaseReceiptEntity> query = dbContext.PurchaseReceipts
            .Where(r => r.TenantId == tenantId && !r.IsDeleted
                && (from == null || r.ReceiptDate >= from) && (to == null || r.ReceiptDate < to));
        if (status.HasValue)
        {
            query = query.Where(r => r.Status == status.Value);
        }
        if (!string.IsNullOrWhiteSpace(supplierFilter))
        {
            query = query.Where(r => r.SupplierName.Contains(supplierFilter) || r.VoucherNumber.Contains(supplierFilter));
        }
        return await query.OrderByDescending(r => r.ReceiptDate).ToListAsync(ct);
    }

    /// <summary>Validation chung create/update: lines + hình thức thanh toán + tài khoản hợp lệ.</summary>
    private async Task ValidateInputsAsync(TenantId tenantId, IReadOnlyList<PurchaseReceiptLineInput> lines,
        PurchasePayMethod payMethod, string? cashAccountCode, string debitAccountCode, CancellationToken ct)
    {
        if (lines is null || lines.Count == 0)
            throw new ArgumentException("Phiếu nhập mua phải có ít nhất 1 dòng hàng.", nameof(lines));
        if (payMethod == PurchasePayMethod.Immediate)
        {
            if (string.IsNullOrWhiteSpace(cashAccountCode))
                throw new ArgumentException("CashAccountCode (111/112x) bắt buộc khi trả ngay.", nameof(cashAccountCode));
            if (!cashAccountCode.StartsWith("111", StringComparison.Ordinal)
                && !cashAccountCode.StartsWith("112", StringComparison.Ordinal))
                throw new ArgumentException($"Tài khoản tiền '{cashAccountCode}' không hợp lệ — phải thuộc nhóm 111/112.", nameof(cashAccountCode));
        }
        // TK ghi Nợ phải tồn tại trong danh mục (global chart hoặc tenant custom) + không bị ngừng dùng.
        await ValidateDebitAccountAsync(tenantId, debitAccountCode, ct);
    }

    private async Task ValidateDebitAccountAsync(TenantId tenantId, string accountCode, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(accountCode))
            throw new ArgumentException("DebitAccountCode là bắt buộc.", nameof(accountCode));
        TenantAccountChartEntity? tenantOverride = await dbContext.TenantAccountCharts
            .FirstOrDefaultAsync(t => t.TenantId == tenantId.Value && t.AccountCode == accountCode, ct);
        if (tenantOverride != null)
        {
            if (!tenantOverride.IsActive)
                throw new ArgumentException($"Tài khoản '{accountCode}' đã ngừng sử dụng.", nameof(accountCode));
            return; // Override/Custom active → hợp lệ
        }
        bool exists = await dbContext.AccountCharts.AnyAsync(c => c.AccountCode == accountCode, ct);
        if (!exists)
            throw new ArgumentException($"Tài khoản '{accountCode}' không tồn tại trong danh mục tài khoản.", nameof(accountCode));
    }

    /// <summary>Ghi StockMovement sau khi post — fail-safe (cross-DB SQLite; drift → R13 + retry ApplyStock).</summary>
    private async Task TryApplyStockAsync(PurchaseReceiptEntity receipt, CancellationToken ct)
    {
        if (stockMovementService == null || receipt.StockAppliedAt != null || !receipt.Lines.Any(l => l.IngredientId.HasValue))
        {
            return;
        }
        try
        {
            int written = await stockMovementService.ApplyReceiptStockAsync(receipt, ct);
            receipt.MarkStockApplied();
            await dbContext.SaveChangesAsync(ct);
            logger.LogInformation("PurchaseReceipt {Id}: ghi kho {Count} movement PurchaseIn", receipt.Id, written);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "PurchaseReceipt {Id}: ghi kho lỗi — StockApplied chưa set, retry qua ApplyStock (drift → R13)", receipt.Id);
        }
    }

    private async Task<PurchaseReceiptEntity> LoadAsync(Guid receiptId, CancellationToken ct)
        => await dbContext.PurchaseReceipts.IgnoreQueryFilters()
            .Include(r => r.Lines)
            .FirstOrDefaultAsync(r => r.Id == receiptId, ct)
            ?? throw new KeyNotFoundException($"PurchaseReceipt {receiptId} not found");

    /// <summary>Post JE nhiều dòng qua resolver (fallback builtin). Idempotent theo (RefType, receiptId).</summary>
    private async Task PostJournalAsync(PurchaseReceiptEntity receipt, string eventCode, string purpose,
        string srcHint, string creditFallback, decimal netAmount, decimal vatAmount, decimal totalAmount,
        DateTime date, string description, CancellationToken ct)
    {
        if (await hkdBookRepository.ExistsByReferenceAsync(receipt.TenantId, ReferenceType, receipt.Id, ct))
        {
            logger.LogInformation("PurchaseReceipt {Id}: JE đã tồn tại — skip (idempotent)", receipt.Id);
            return;
        }

        AccountMapResult? map = accountMapResolver != null
            ? await accountMapResolver.ResolveAsync(eventCode, receipt.TenantId, srcHint, receipt.CashAccountCode, ct)
            : null;
        (string debit, string credit, string version) = map != null
            ? (map.Debit, map.Credit, map.MappingVersion ?? "rule")
            : (srcHint, creditFallback, "builtin");

        JournalEntry je = new(receipt.TenantId, date, description,
            ReferenceType, receipt.Id, postingPurpose: purpose, mappingVersion: version);
        je.AddLine(debit, netAmount, 0, description);
        if (vatAmount > 0)
        {
            je.AddLine(InputVatAccount, vatAmount, 0, $"VAT đầu vào {receipt.VoucherNumber}");
        }
        je.AddLine(credit, 0, totalAmount, description);
        await hkdBookRepository.AddToBookAsync(je, AccountingBookType.S2b_HKD, ct);
        logger.LogInformation("PurchaseReceipt {Id}: JE {Event} {Debit}/{Credit} net {Net} vat {Vat} total {Total} ({Version})",
            receipt.Id, eventCode, debit, credit, netAmount, vatAmount, totalAmount, version);
    }

    private async Task AuditSafeAsync(TenantId tenantId, PurchaseReceiptEntity receipt, string action, CancellationToken ct)
    {
        if (auditTrailService == null)
        {
            return;
        }
        try
        {
            await auditTrailService.LogCreateAsync(AuditableEntityType.AccountingEntry, receipt.Id,
                System.Text.Json.JsonSerializer.Serialize(new
                {
                    action, receipt.VoucherNumber, receipt.SupplierName, receipt.TotalAmount,
                    payMethod = receipt.PayMethod.ToString(), status = receipt.Status.ToString()
                }),
                correlationId: $"{tenantId.Value}:purchase:{receipt.Id}");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "PurchaseReceipt {Id}: audit log lỗi (non-blocking)", receipt.Id);
        }
    }
}
