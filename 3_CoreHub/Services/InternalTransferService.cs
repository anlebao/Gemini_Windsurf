using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using VanAn.CoreHub.Infrastructure;
using VanAn.CoreHub.Infrastructure.Entities;
using VanAn.CoreHub.Repositories;
using VanAn.CoreHub.Services.Journal;
using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Services;

/// <summary>SRS v1.2 W3/D: phiếu chuyển tiền nội bộ 111↔112x + số dư tiền.</summary>
public interface IInternalTransferService
{
    /// <summary>Tạo phiếu + post JE out (idempotent theo transferId). inTransit=false → nhận ngay (JE to/from, Received);
    /// true → qua 113 (JE 113/from, InTransit — chờ ReceiveAsync).</summary>
    Task<InternalTransferEntity> CreateAsync(TenantId tenantId, string fromAccountCode, string toAccountCode,
        decimal amount, decimal? feeAmount = null, string? feeAccountCode = null, bool inTransit = false,
        string? description = null, string? bankReference = null, DateTime? transferDate = null,
        CancellationToken ct = default);

    /// <summary>InTransit → Received: JE Nợ {to}/Có 113.</summary>
    Task<InternalTransferEntity> ReceiveAsync(Guid transferId, DateTime? receivedDate = null, CancellationToken ct = default);

    /// <summary>InTransit → Cancelled: JE đảo Nợ {from}/Có 113 (tiền đang chuyển bị hủy).</summary>
    Task<InternalTransferEntity> CancelInTransitAsync(Guid transferId, CancellationToken ct = default);

    /// <summary>Discrepancy — xử lý manual (AT-011).</summary>
    Task<InternalTransferEntity> MarkDiscrepancyAsync(Guid transferId, CancellationToken ct = default);

    /// <summary>Đảo transfer đã Received — tạo phiếu ngược (to→from) + ReversedById link.</summary>
    Task<InternalTransferEntity> ReverseAsync(Guid transferId, string? description = null, CancellationToken ct = default);

    Task<InternalTransferEntity?> GetByIdAsync(Guid transferId, CancellationToken ct = default);
    Task<List<InternalTransferEntity>> ListAsync(TenantId tenantId, InternalTransferStatus? status = null, CancellationToken ct = default);

    /// <summary>Số dư TK tiền = ΣNợ − ΣCó trên JournalEntryLine (entries-based — FR-PAY-006 approximation).</summary>
    Task<decimal> GetCashBalanceAsync(TenantId tenantId, string accountCode, DateTime? upTo = null, CancellationToken ct = default);
}

/// <summary>
/// Posting qua account-map (event transfer-out/transfer-in/transfer-fee — hints:
/// out {src}=đích|113 {cash}=nguồn · in {src}=đích {cash}=113 · fee {src}=642 {cash}=nguồn).
/// FR-TRF-004: phí = JE riêng. FR-TRF-005: Nợ/Có 11x — tổng tài sản tiền không đổi (trừ phí).
/// Resolver null/miss → builtin inline (parity GetBuiltin — không phụ thuộc flag).
/// </summary>
public sealed class InternalTransferService(
    IAccountingDbContext dbContext,
    IHKDBookRepository hkdBookRepository,
    IVoucherNumberService? voucherNumberService = null,
    IAccountMapResolver? accountMapResolver = null,
    ILogger<InternalTransferService>? logger = null) : IInternalTransferService
{
    public const string ReferenceType = "InternalTransfer";
    public const string InTransitAccount = "113";
    public const string DefaultFeeAccount = "642";
    private const string VoucherType = "CT";

    public async Task<InternalTransferEntity> CreateAsync(TenantId tenantId, string fromAccountCode, string toAccountCode,
        decimal amount, decimal? feeAmount = null, string? feeAccountCode = null, bool inTransit = false,
        string? description = null, string? bankReference = null, DateTime? transferDate = null,
        CancellationToken ct = default)
    {
        if (amount <= 0) throw new ArgumentOutOfRangeException(nameof(amount), "Amount must be positive");
        if (fromAccountCode == toAccountCode) throw new ArgumentException("From/To account must differ", nameof(toAccountCode));

        DateTime date = transferDate ?? DateTime.UtcNow;
        string voucher = voucherNumberService != null
            ? await voucherNumberService.NextNumberAsync(tenantId, VoucherType, date.Year, ct)
            : $"{VoucherType}-{date:yyyy}-{Guid.NewGuid().ToString("N")[..5].ToUpperInvariant()}";

        var transfer = new InternalTransferEntity(tenantId, voucher, fromAccountCode, toAccountCode, amount,
            feeAmount, feeAmount > 0 ? (feeAccountCode ?? DefaultFeeAccount) : null,
            inTransit, date, bankReference, description);
        dbContext.InternalTransfers.Add(transfer);

        // JE out: InTransit → Nợ 113/Có {from}; immediate → Nợ {to}/Có {from}.
        string debitAccount = inTransit ? InTransitAccount : toAccountCode;
        await PostJournalAsync(transfer, AccountMapEventCodes.TransferOut, debitAccount, fromAccountCode,
            amount, date, description, ct);

        // FR-TRF-004: phí = JE riêng Nợ {642}/Có {from}.
        if (feeAmount is > 0)
        {
            await PostJournalAsync(transfer, AccountMapEventCodes.TransferFee,
                transfer.FeeAccountCode ?? DefaultFeeAccount, fromAccountCode,
                feeAmount.Value, date, $"Phí chuyển tiền {voucher}", ct, journalNoSuffix: "-FEE");
        }

        if (!inTransit)
        {
            transfer.ForceReceived(date); // immediate: đã post thẳng Nợ đích/Có nguồn
        }

        await dbContext.SaveChangesAsync(ct);
        return transfer;
    }

    public async Task<InternalTransferEntity> ReceiveAsync(Guid transferId, DateTime? receivedDate = null, CancellationToken ct = default)
    {
        InternalTransferEntity transfer = await GetRequiredAsync(transferId, ct);
        DateTime date = receivedDate ?? DateTime.UtcNow;
        await PostJournalAsync(transfer, AccountMapEventCodes.TransferIn, transfer.ToAccountCode, InTransitAccount,
            transfer.Amount, date, $"Nhận tiền chuyển {transfer.VoucherNumber}", ct, journalNoSuffix: "-IN");
        transfer.MarkReceived(date);
        await dbContext.SaveChangesAsync(ct);
        return transfer;
    }

    public async Task<InternalTransferEntity> CancelInTransitAsync(Guid transferId, CancellationToken ct = default)
    {
        InternalTransferEntity transfer = await GetRequiredAsync(transferId, ct);
        if (transfer.Status != InternalTransferStatus.InTransit)
            throw new InvalidOperationException($"Cannot cancel transfer in status {transfer.Status}");
        // Đảo JE out: Nợ {from}/Có 113 — tiền quay về nguồn.
        await PostJournalAsync(transfer, AccountMapEventCodes.TransferIn, transfer.FromAccountCode, InTransitAccount,
            transfer.Amount, DateTime.UtcNow, $"Hủy chuyển tiền {transfer.VoucherNumber}", ct,
            journalNoSuffix: "-CXL");
        transfer.MarkCancelledFromTransit();
        await dbContext.SaveChangesAsync(ct);
        return transfer;
    }

    public async Task<InternalTransferEntity> MarkDiscrepancyAsync(Guid transferId, CancellationToken ct = default)
    {
        InternalTransferEntity transfer = await GetRequiredAsync(transferId, ct);
        transfer.MarkDiscrepancy();
        await dbContext.SaveChangesAsync(ct);
        return transfer;
    }

    public async Task<InternalTransferEntity> ReverseAsync(Guid transferId, string? description = null, CancellationToken ct = default)
    {
        InternalTransferEntity original = await GetRequiredAsync(transferId, ct);
        if (original.Status != InternalTransferStatus.Received)
            throw new InvalidOperationException($"Cannot reverse transfer in status {original.Status}");
        if (original.ReversedById.HasValue)
            throw new InvalidOperationException("Transfer already reversed");

        // Phiếu đảo: to→from cùng Amount (không đảo phí — phí là chi phí riêng).
        InternalTransferEntity reversal = await CreateAsync(original.TenantId,
            original.ToAccountCode, original.FromAccountCode, original.Amount,
            description: description ?? $"Đảo phiếu chuyển {original.VoucherNumber}", ct: ct);
        original.MarkReversed(reversal.Id);
        await dbContext.SaveChangesAsync(ct);
        return reversal;
    }

    public async Task<InternalTransferEntity?> GetByIdAsync(Guid transferId, CancellationToken ct = default)
        => await dbContext.InternalTransfers.IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.Id == transferId, ct);

    public async Task<List<InternalTransferEntity>> ListAsync(TenantId tenantId, InternalTransferStatus? status = null, CancellationToken ct = default)
        => await dbContext.InternalTransfers
            .Where(t => t.TenantId == tenantId && (status == null || t.Status == status))
            .OrderByDescending(t => t.TransferDate)
            .ToListAsync(ct);

    /// <inheritdoc />
    public async Task<decimal> GetCashBalanceAsync(TenantId tenantId, string accountCode, DateTime? upTo = null, CancellationToken ct = default)
    {
        var lines = dbContext.JournalEntries
            .Where(j => j.TenantId == tenantId && !j.IsDeleted && (upTo == null || j.EntryDate <= upTo))
            .SelectMany(j => j.Lines)
            .Where(l => l.AccountNumber == accountCode);
        return await lines.SumAsync(l => l.DebitAmount - l.CreditAmount, ct);
    }

    private async Task<InternalTransferEntity> GetRequiredAsync(Guid transferId, CancellationToken ct)
        => await GetByIdAsync(transferId, ct)
            ?? throw new KeyNotFoundException($"InternalTransfer {transferId} not found");

    /// <summary>Post JE 2 dòng qua resolver (fallback builtin). Idempotent theo (ReferenceType, transferId, suffix).</summary>
    private async Task PostJournalAsync(InternalTransferEntity transfer, string eventCode,
        string srcHint, string cashHint, decimal amount, DateTime date, string? description,
        CancellationToken ct, string journalNoSuffix = "")
    {
        AccountMapResult? map = accountMapResolver != null
            ? await accountMapResolver.ResolveAsync(eventCode, transfer.TenantId, srcHint, cashHint, ct)
            : null;
        (string debit, string credit, string version) = map != null
            ? (map.Debit, map.Credit, map.MappingVersion)
            : (srcHint, cashHint, "builtin");

        JournalEntry je = new(transfer.TenantId, date,
            description ?? $"Chuyển tiền {transfer.VoucherNumber}{journalNoSuffix}",
            ReferenceType, transfer.Id,
            postingPurpose: AccountingPostingPurposes.Original, mappingVersion: version);
        je.AddLine(debit, amount, 0, description);
        je.AddLine(credit, 0, amount, description);
        await hkdBookRepository.AddToBookAsync(je, AccountingBookType.S2b_HKD, ct);
        logger?.LogInformation("InternalTransfer {TransferId}: JE {Event} {Debit}/{Credit} amount {Amount} ({Version})",
            transfer.Id, eventCode, debit, credit, amount, version);
    }
}
