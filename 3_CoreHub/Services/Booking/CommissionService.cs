using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using VanAn.CoreHub.Infrastructure;
using VanAn.CoreHub.Services;
using VanAn.Shared.Domain;
using BookingEntity = VanAn.Shared.Domain.Booking;

namespace VanAn.CoreHub.Services.Booking;

/// <summary>
/// CommissionService — qualification §17.1 (COMPLETED + payment qualified + attribution valid) →
/// CommissionLedgerEntry snapshot (rule + tax) immutable §17.5 → payout WalletTransaction.
/// Ledger hợp nhất BOOKING|ORDER (D3) — SalesReferral legacy giữ nguyên.
/// </summary>
public sealed class CommissionService(
    VanAnDbContext context,
    ITaxWithholdingPolicyAdapter taxAdapter,
    IWalletService walletService,
    ILogger<CommissionService> logger) : ICommissionService
{
    private readonly VanAnDbContext _context = context;
    private readonly ITaxWithholdingPolicyAdapter _taxAdapter = taxAdapter;
    private readonly IWalletService _walletService = walletService;
    private readonly ILogger<CommissionService> _logger = logger;

    // MVP: salesman tax profile chưa có bảng riêng (§17.2 fields tối thiểu defer) → snapshot với defaults
    // IndividualContractor + cư trú VN + không HĐLĐ. Accounting/Tax module dùng facts để xử lý đúng.
    private const TaxPayeeType DefaultPayeeType = TaxPayeeType.IndividualContractor;
    private const string DefaultResidency = "VN";
    private const string DefaultContractType = "UNDER_3_MONTHS";

    public async Task<CommissionLedgerEntry?> FinalizeCommissionForBookingAsync(TenantId tenantId, Guid bookingId, CancellationToken ct = default)
    {
        // Idempotent: booking đã có ledger entry (source Booking) → không double-create.
        CommissionLedgerEntry? existing = await _context.CommissionLedgerEntries.IgnoreQueryFilters()
            .FirstOrDefaultAsync(e => e.TenantId == tenantId && e.SourceType == CommissionSourceType.Booking && e.BookingId == bookingId, ct);
        if (existing is not null)
        {
            _logger.LogInformation("Commission ledger đã tồn tại cho booking {BookingId} — entry {EntryId}", bookingId, existing.Id);
            return existing;
        }

        BookingEntity? booking = await _context.Bookings.IgnoreQueryFilters()
            .FirstOrDefaultAsync(b => b.TenantId == tenantId && b.Id == bookingId, ct);
        if (booking is null)
            throw new NotFoundException("Booking không tồn tại trong tenant này.");

        // §17.1 — COMPLETED
        if (booking.Status != BookingStatus.Completed)
        {
            _logger.LogInformation("Commission skipped — booking {BookingId} chưa COMPLETED (status={Status})", bookingId, booking.Status);
            return null;
        }

        // §17.1 — payment qualified: không yêu cầu cọc → NotRequired là qualified; có cọc → phải Paid.
        bool paymentQualified = !booking.DepositRequired || booking.PaymentStatus == PaymentStatus.Paid;
        if (!paymentQualified)
        {
            _logger.LogInformation("Commission skipped — booking {BookingId} payment chưa qualified (status={Status})", bookingId, booking.PaymentStatus);
            return null;
        }

        // §17.1 — attribution valid: booking.AttributionId != null ∧ session qualified ∧ chưa hết hạn.
        if (booking.AttributionId is null)
        {
            _logger.LogInformation("Commission skipped — booking {BookingId} không có attribution", bookingId);
            return null;
        }
        AttributionSession? session = await _context.AttributionSessions.IgnoreQueryFilters()
            .FirstOrDefaultAsync(s => s.TenantId == tenantId && s.Id == booking.AttributionId.Value, ct);
        if (session is null || !session.IsQualified || (session.AttributionExpiryAt is not null && session.AttributionExpiryAt <= DateTime.UtcNow))
        {
            _logger.LogInformation("Commission skipped — booking {BookingId} attribution không hợp lệ", bookingId);
            return null;
        }
        if (session.SalesmanId is null)
        {
            _logger.LogInformation("Commission skipped — booking {BookingId} không có salesman trong attribution", bookingId);
            return null;
        }

        // Rule matching: active ∧ offering (null = mọi offering) ∧ salesman (null = mọi salesman) — ưu tiên specificity.
        List<CommissionRule> rules = await _context.CommissionRules.IgnoreQueryFilters()
            .Where(r => r.TenantId == tenantId && r.IsActive
                        && (r.OfferingId == null || r.OfferingId == booking.OfferingId)
                        && (r.SalesmanId == null || r.SalesmanId == session.SalesmanId))
            .ToListAsync(ct);

        CommissionRule? rule = rules
            .OrderByDescending(r => r.OfferingId is not null)
            .ThenByDescending(r => r.SalesmanId is not null)
            .FirstOrDefault();
        if (rule is null)
        {
            _logger.LogInformation("Commission skipped — booking {BookingId} không có CommissionRule active", bookingId);
            return null;
        }

        decimal baseAmount = booking.ActualTotal ?? booking.EstimatedTotal;
        decimal gross = rule.CommissionType switch
        {
            CommissionType.FixedAmount => rule.CommissionValue,
            _ => Math.Round(baseAmount * rule.CommissionValue / 100m, 0, MidpointRounding.AwayFromZero)
        };

        // Tax snapshot (§17.3) — versioned adapter; KHÔNG hard-code 10%.
        TaxWithholdingResult tax = _taxAdapter.Calculate(new TaxWithholdingInput(
            DefaultPayeeType, DefaultResidency, DefaultContractType, gross, _taxAdapter.CurrentVersion));

        string ruleSnapshot = JsonSerializer.Serialize(new
        {
            rule.Id,
            rule.CommissionType,
            rule.CommissionValue,
            rule.OfferingId,
            rule.SalesmanId,
            rule.QualificationStatus
        });

        var entry = new CommissionLedgerEntry(
            tenantId, CommissionSourceType.Booking, session.SalesmanId.Value,
            ruleSnapshot, baseAmount, gross, tax.TaxWithheldAmount, tax.NetPayable,
            bookingId: bookingId, qrId: session.QrId,
            taxRuleVersion: tax.TaxRuleVersion, withholdingReasonCode: tax.WithholdingReasonCode);
        entry.MarkEarned();   // finalized — snapshot immutable từ đây (§17.5)

        _context.CommissionLedgerEntries.Add(entry);
        _context.BookingEvents.Add(new BookingEvent(
            tenantId, bookingId, "CommissionEarned", "SYSTEM", null,
            JsonSerializer.Serialize(new { entry.Id, entry.GrossCommissionAmount, entry.NetCommissionAmount })));
        _ = await _context.SaveChangesAsync(ct);

        _logger.LogInformation("Commission finalized: booking={BookingId} entry={EntryId} gross={Gross} net={Net} tax={Tax}",
            bookingId, entry.Id, gross, tax.NetPayable, tax.TaxWithheldAmount);
        return entry;
    }

    public async Task<CommissionLedgerEntry> ReverseCommissionAsync(TenantId tenantId, Guid ledgerEntryId, string? reason = null, CancellationToken ct = default)
    {
        CommissionLedgerEntry? original = await _context.CommissionLedgerEntries.IgnoreQueryFilters()
            .FirstOrDefaultAsync(e => e.TenantId == tenantId && e.Id == ledgerEntryId, ct)
            ?? throw new NotFoundException("Ledger entry không tồn tại trong tenant này.");

        // Idempotent: đã reverse → trả reversal entry hiện có.
        CommissionLedgerEntry? existingReversal = await _context.CommissionLedgerEntries.IgnoreQueryFilters()
            .FirstOrDefaultAsync(e => e.TenantId == tenantId && e.RelatedEntryId == ledgerEntryId, ct);
        if (existingReversal is not null)
            return existingReversal;

        if (original.State is not (CommissionLedgerState.Earned or CommissionLedgerState.Paid))
            throw new ValidationException($"Không thể reverse entry ở trạng thái {original.State}.");

        // Bắt trạng thái paid TRƯỚC khi MarkReversed (MarkReversed đổi State → Reversed).
        bool wasPaid = original.State == CommissionLedgerState.Paid && original.WalletTransactionId is not null;

        // §17.5 — reversal entry mới (RelatedEntryId = original), snapshot giữ nguyên; original → Reversed.
        var reversal = new CommissionLedgerEntry(
            tenantId, original.SourceType, original.SalesmanId,
            original.RuleSnapshotJson, original.BaseAmount, original.GrossCommissionAmount,
            original.TaxWithheldAmount, original.NetCommissionAmount,
            bookingId: original.BookingId, orderId: original.OrderId, qrId: original.QrId,
            taxRuleVersion: original.TaxRuleVersion, withholdingReasonCode: original.WithholdingReasonCode,
            currency: original.Currency, relatedEntryId: original.Id);
        reversal.MarkReversed();
        original.MarkReversed();

        _context.CommissionLedgerEntries.Add(reversal);
        if (original.BookingId is not null)
        {
            _context.BookingEvents.Add(new BookingEvent(
                tenantId, original.BookingId.Value, "CommissionReversed", "SYSTEM", null,
                JsonSerializer.Serialize(new { originalId = original.Id, reversalId = reversal.Id, reason })));
        }

        // §26.6 — original đã PAID → hoàn wallet (WalletTransaction Reversal, -net).
        if (wasPaid && original.WalletTransactionId is not null)
        {
            var walletTx = await _walletService.CreateTransactionAsync(
                original.SalesmanId, WalletTransactionType.Reversal, -original.NetCommissionAmount,
                $"Hoàn hoa hồng booking (reversal #{original.Id:N})",
                relatedTransactionId: original.WalletTransactionId, tenantIdOverride: tenantId);
            reversal.RecordReversalWalletTransaction(walletTx.Id);   // link audit (State giữ Reversed)
        }

        _ = await _context.SaveChangesAsync(ct);
        _logger.LogInformation("Commission reversed: original={OriginalId} reversal={ReversalId}", original.Id, reversal.Id);
        return reversal;
    }

    public async Task<CommissionLedgerEntry> PayCommissionAsync(TenantId tenantId, Guid ledgerEntryId, CancellationToken ct = default)
    {
        CommissionLedgerEntry? entry = await _context.CommissionLedgerEntries.IgnoreQueryFilters()
            .FirstOrDefaultAsync(e => e.TenantId == tenantId && e.Id == ledgerEntryId, ct)
            ?? throw new NotFoundException("Ledger entry không tồn tại trong tenant này.");
        if (entry.State != CommissionLedgerState.Earned)
            throw new ValidationException($"Chỉ EARNED mới payout được (hiện {entry.State}).");

        if (entry.NetCommissionAmount <= 0)
            throw new ValidationException("Net commission <= 0 — không có khoản payout.");

        // Reuse WalletService (precedent SalesReferral commission) — WalletTransaction(Commission, +net).
        WalletTransaction walletTx = await _walletService.CreateTransactionAsync(
            entry.SalesmanId, WalletTransactionType.Commission, entry.NetCommissionAmount,
            $"Hoa hồng booking {entry.BookingId:N}",
            relatedOrderId: entry.OrderId, tenantIdOverride: tenantId);

        entry.MarkPaid(walletTx.Id);
        _ = await _context.SaveChangesAsync(ct);
        _logger.LogInformation("Commission paid: entry={EntryId} walletTx={WalletTxId} net={Net}", entry.Id, walletTx.Id, entry.NetCommissionAmount);
        return entry;
    }

    public async Task<IReadOnlyList<CommissionLedgerEntry>> GetLedgerAsync(
        TenantId tenantId, Guid? salesmanId = null, Guid? bookingId = null, CancellationToken ct = default)
    {
        IQueryable<CommissionLedgerEntry> query = _context.CommissionLedgerEntries.IgnoreQueryFilters()
            .Where(e => e.TenantId == tenantId);
        if (salesmanId is not null)
            query = query.Where(e => e.SalesmanId == salesmanId);
        if (bookingId is not null)
            query = query.Where(e => e.BookingId == bookingId);
        return await query.OrderByDescending(e => e.CreatedAt).ToListAsync(ct);
    }
}
