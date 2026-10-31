using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using VanAn.CoreHub.Infrastructure;
using VanAn.CoreHub.Infrastructure.Entities;
using VanAn.CoreHub.Repositories;
using VanAn.CoreHub.Services.CongNo;
using VanAn.CoreHub.Services.Journal;
using VanAn.Shared.Domain;
using VanAn.Shared.Domain.Audit;
using VanAn.Shared.DTOs;

namespace VanAn.CoreHub.Services.Purchase;

/// <summary>Dòng hàng đầu vào khi lập phiếu nhập mua.</summary>
public sealed record PurchaseReceiptLineInput(string ItemName, decimal Quantity, decimal UnitPrice);

/// <summary>SRS v1.2 W6/E: phiếu nhập mua — JE + công nợ 331 + trả hàng đảo bút toán.</summary>
public interface IPurchaseReceiptService
{
    /// <summary>Tạo phiếu nhập mua + post JE (purchase/purchase-cash qua account-map).
    /// Credit → thêm phiếu công nợ 331+ (CongNoService). FR-PUR-007: sourceEventId lặp → trả phiếu đã có.</summary>
    Task<PurchaseReceiptEntity> CreateAsync(TenantId tenantId, string supplierName, string? supplierRef,
        DateTime receiptDate, IReadOnlyList<PurchaseReceiptLineInput> lines,
        PurchasePayMethod payMethod, string? cashAccountCode, string debitAccountCode,
        decimal vatAmount = 0m, string? reference = null, string? sourceEventId = null,
        DateTime? dueDate = null, CancellationToken ct = default);

    /// <summary>Trả hàng/đảo phiếu — JE đảo (purpose purchase-return, ReversedJournalId link)
    /// + reversal phiếu công nợ 331 (ReversalEntryId pattern → nợ net về 0).</summary>
    Task<PurchaseReceiptEntity> ReturnAsync(Guid receiptId, string reason, CancellationToken ct = default);

    Task<PurchaseReceiptEntity?> GetByIdAsync(Guid receiptId, CancellationToken ct = default);
    Task<List<PurchaseReceiptEntity>> ListAsync(TenantId tenantId, DateTime? from = null, DateTime? to = null, CancellationToken ct = default);
}

/// <summary>
/// Posting qua account-map (event purchase → ({src},331) · purchase-cash → ({src},{cash});
/// {src}=DebitAccountCode do tenant chọn: 156 DN · 611 HTX · 6xx chi phí HKD — card §2.1
/// "fallback Skipped" KHÔNG áp vì counter-account do phiếu mang theo, không cần tenant config).
/// VAT đầu vào → dòng Nợ 133 riêng (hardcode — TK thuế cố định). Resolver miss → builtin inline.
/// JE: ReferenceType "PurchaseReceipt" + ReferenceId=receipt.Id + purpose — idempotent qua
/// ExistsByReferenceAsync + unique index Phase A (Tenant,RefType,RefId,Purpose).
/// </summary>
public sealed class PurchaseReceiptService(
    IAccountingDbContext dbContext,
    IHKDBookRepository hkdBookRepository,
    ILogger<PurchaseReceiptService> logger,
    ICongNoService? congNoService = null,
    IReversalService? reversalService = null,
    IVoucherNumberService? voucherNumberService = null,
    IAccountMapResolver? accountMapResolver = null,
    IAuditTrailService? auditTrailService = null) : IPurchaseReceiptService
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
        if (lines is null || lines.Count == 0)
            throw new ArgumentException("Phiếu nhập mua phải có ít nhất 1 dòng hàng.", nameof(lines));
        if (payMethod == PurchasePayMethod.Immediate && string.IsNullOrWhiteSpace(cashAccountCode))
            throw new ArgumentException("CashAccountCode (111/112x) bắt buộc khi trả ngay.", nameof(cashAccountCode));

        // FR-PUR-007: sync/retry lặp cùng source event → trả phiếu đã tạo (không JE/entry trùng).
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
            receipt.AddLine(input.ItemName, input.Quantity, input.UnitPrice);
        }
        dbContext.PurchaseReceipts.Add(receipt);

        // JE: credit → Nợ {debit} + Nợ 133 (nếu VAT) / Có 331; cash → Có {cashAccount}.
        string eventCode = payMethod == PurchasePayMethod.Credit
            ? AccountMapEventCodes.Purchase
            : AccountMapEventCodes.PurchaseCash;
        string purpose = payMethod == PurchasePayMethod.Credit
            ? AccountingPostingPurposes.Purchase
            : AccountingPostingPurposes.PurchaseCash;
        string creditFallback = payMethod == PurchasePayMethod.Credit ? "331" : cashAccountCode!;
        string desc = $"{(payMethod == PurchasePayMethod.Credit ? "Nhập mua chịu" : "Nhập mua trả ngay")} {voucher} — {supplierName}";

        await PostJournalAsync(receipt, eventCode, purpose, debitAccountCode, creditFallback,
            receipt.NetAmount, receipt.VatAmount, receipt.TotalAmount, receiptDate, desc, ct);

        // Mua chịu → phiếu công nợ 331+ (CongNo ledger + FIFO aging + thanh toán sau qua payable-payment).
        // Fail-safe: lỗi công nợ không chặn phiếu (JE đã post — drift → reconcile R-checks bắt).
        if (payMethod == PurchasePayMethod.Credit && congNoService != null)
        {
            try
            {
                AccountingEntryDto payable = await congNoService.CreatePayableAsync(
                    tenantId, receipt.TotalAmount, supplierName,
                    $"{desc} (VAT {receipt.VatAmount:N0})", receiptDate,
                    isPayment: false, mst: supplierRef, reference: receipt.Id.ToString(), cancellationToken: ct);
                receipt.SetPayableEntry(payable.Id);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "PurchaseReceipt {Id}: tạo công nợ 331 lỗi — phiếu vẫn lưu (drift → R-checks)", receipt.Id);
            }
        }

        await dbContext.SaveChangesAsync(ct);
        await AuditSafeAsync(tenantId, receipt, "create", ct);
        return receipt;
    }

    public async Task<PurchaseReceiptEntity> ReturnAsync(Guid receiptId, string reason, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("Reason is required.", nameof(reason));

        PurchaseReceiptEntity receipt = await dbContext.PurchaseReceipts.IgnoreQueryFilters()
            .Include(r => r.Lines)
            .FirstOrDefaultAsync(r => r.Id == receiptId, ct)
            ?? throw new KeyNotFoundException($"PurchaseReceipt {receiptId} not found");

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
        await AuditSafeAsync(receipt.TenantId, receipt, "return", ct);
        return receipt;
    }

    public async Task<PurchaseReceiptEntity?> GetByIdAsync(Guid receiptId, CancellationToken ct = default)
        => await dbContext.PurchaseReceipts.IgnoreQueryFilters()
            .Include(r => r.Lines)
            .FirstOrDefaultAsync(r => r.Id == receiptId, ct);

    public async Task<List<PurchaseReceiptEntity>> ListAsync(TenantId tenantId, DateTime? from = null, DateTime? to = null, CancellationToken ct = default)
        => await dbContext.PurchaseReceipts
            .Where(r => r.TenantId == tenantId && !r.IsDeleted
                && (from == null || r.ReceiptDate >= from) && (to == null || r.ReceiptDate < to))
            .OrderByDescending(r => r.ReceiptDate)
            .ToListAsync(ct);

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
            ? (map.Debit, map.Credit, map.MappingVersion)
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
