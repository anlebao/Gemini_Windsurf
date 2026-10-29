using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using VanAn.CoreHub.Infrastructure;
using VanAn.CoreHub.Repositories;
using VanAn.Shared.Domain;
using VanAn.Shared.Domain.Aggregates.MembershipAggregate;
using VanAn.Shared.Domain.Audit;
using CoreAccountingEntry = VanAn.Shared.Domain.AccountingEntry;
// Namespace "Booking" xung đột type Booking (lesson P1 — CS0118) → alias cho domain entity.
using BookingEntity = VanAn.Shared.Domain.Booking;

namespace VanAn.CoreHub.Services.Booking;

/// <summary>
/// Fix 2b (2026-10-14): BookingAccountingService — consumer DepositPaid/PaymentCaptured/ServiceCompleted
/// → AccountingEntry (511/3331; cọc = 3387 bản chất riêng). Pattern:
///   - Revenue: IAccountingService.CreateRevenueEntryAsync (period guard + duplicate-check + JournalEntry
///     HKD bridge + audit) — đúng luồng OrderService.GenerateAccountingEntriesAsync (net = tổng tiền,
///     VAT = 10% snapshot khớp order hook).
///   - Deposit: CoreAccountingEntry.CreateDebt (Adjustment + CashBankBook — pattern THU CHI & CÔNG NỢ)
///     accountCode 3387 "Tiền nhận cọc/trả trước" — KHÔNG lọt doanh thu/chi phí (BookType filter [G6]).
/// HTX (TT 71): 512 (nội bộ) / 511 (ngoài) — mirror OrderService.ApplyHtxInternalTagAsync.
/// Idempotent theo Reference + fail-safe (không fail luồng booking — retry lần gọi sau).
/// </summary>
public sealed class BookingAccountingService(
    VanAnDbContext context,
    IAccountingService accountingService,
    IAccountingEntryRepository entryRepository,
    IPeriodClosingService periodClosingService,
    IAuditTrailService auditTrailService,
    ILogger<BookingAccountingService> logger) : IBookingAccountingService
{
    /// <summary>TK 3387 — Phải trả, phải nộp khác: tiền nhận cọc/trả trước của khách (liability — hoàn/đối trừ sau).</summary>
    private const string DepositAccount = "3387";

    private readonly VanAnDbContext _context = context;
    private readonly IAccountingService _accountingService = accountingService;
    private readonly IAccountingEntryRepository _entryRepository = entryRepository;
    private readonly IPeriodClosingService _periodClosingService = periodClosingService;
    private readonly IAuditTrailService _auditTrailService = auditTrailService;
    private readonly ILogger<BookingAccountingService> _logger = logger;

    private static string BookingRef(Guid bookingId) => $"Booking:{bookingId}";

    public async Task RecordDepositPaidAsync(
        TenantId tenantId, Guid bookingId, Guid txId, decimal amount,
        MoneyNatureType nature, DateTime paidAt, CancellationToken ct = default)
    {
        if (amount <= 0)
            return;   // fail-safe: không có tiền → không entry
        // Bản chất riêng (user approve): cọc/trả trước KHÔNG phải doanh thu — 1 entry per transaction.
        string reference = $"{BookingRef(bookingId)}:Deposit:{txId}";
        if (await _entryRepository.ExistsByReferenceAsync(tenantId, reference, ct))
            return;   // idempotent

        var period = new AccountingPeriod(paidAt.Year, paidAt.Month);
        if (!await CanPostAsync(tenantId, period, ct))
            return;

        string desc = nature == MoneyNatureType.PrepaymentForService
            ? $"Khách trả trước dịch vụ — đặt lịch {bookingId}"
            : $"Nhận cọc bảo đảm — đặt lịch {bookingId}";

        try
        {
            CoreAccountingEntry entry = CoreAccountingEntry.CreateDebt(
                tenantId, period, new Money(amount), desc,
                accountCode: DepositAccount, reference: reference, transactionDate: paidAt);
            await _entryRepository.AddAsync(entry, ct);
            await AuditAsync(entry, desc);
            _logger.LogInformation("Booking deposit accounted: booking={BookingId} tx={TxId} amount={Amount} nature={Nature}",
                bookingId, txId, amount, nature);
        }
        catch (Exception ex)
        {
            // Fail-safe: không fail luồng thu cọc — retry lần gọi sau (idempotent theo reference).
            _logger.LogError(ex, "RecordDepositPaidAsync failed (non-blocking): booking={BookingId} tx={TxId}", bookingId, txId);
        }
    }

    public async Task RecordFinalPaymentAsync(
        TenantId tenantId, Guid bookingId, Guid txId, decimal amount, DateTime paidAt, CancellationToken ct = default)
    {
        // Issue #195 follow-up (2026-10-29, user approve A): booking đã có Order gắn kèm →
        // doanh thu chuyển hẳn sang ORDER (ghi tại order payment — single source, 2 store không
        // sync chéo: PG booking vs SQLite order). Skip ở booking path để chống double count.
        if (await HasLinkedOrderAsync(tenantId, bookingId, ct))
        {
            _logger.LogInformation("Booking {BookingId} có Order gắn kèm — doanh thu ghi tại order payment (skip booking final payment)", bookingId);
            return;
        }
        // Cash-basis: PaymentCaptured trước ServiceCompleted → ghi nhận doanh thu NGAY tại thời điểm thu.
        // Nếu booking đã có revenue (ServiceCompleted trước) → skip — KHÔNG double count.
        if (await _entryRepository.ExistsByReferenceAsync(tenantId, BookingRef(bookingId), ct))
        {
            _logger.LogDebug("Booking revenue already recorded — skip final payment accounting: booking={BookingId}", bookingId);
            return;
        }
        await CreateRevenueEntriesAsync(tenantId, bookingId, amount, paidAt, "Doanh thu dịch vụ đặt lịch (thanh toán cuối)", ct);
        _logger.LogInformation("Booking final payment accounted: booking={BookingId} tx={TxId} amount={Amount}", bookingId, txId, amount);
    }

    public async Task RecordServiceCompletedAsync(
        TenantId tenantId, Guid bookingId, decimal total, DateTime? completedAt, CancellationToken ct = default)
    {
        // Issue #195 follow-up (2026-10-29, user approve A): booking có Order → doanh thu ghi tại
        // order payment (single source). Order hook đã chạy TRƯỚC trong EnsureOrderAndFactsAsync.
        if (await HasLinkedOrderAsync(tenantId, bookingId, ct))
        {
            _logger.LogInformation("Booking {BookingId} có Order gắn kèm — doanh thu ghi tại order payment (skip booking service completed)", bookingId);
            return;
        }
        // Anchor SRS: hoàn tất lịch → doanh thu về đúng luồng kế toán hiện có.
        // Nếu PaymentCaptured đã ghi nhận trước → skip (idempotent).
        if (await _entryRepository.ExistsByReferenceAsync(tenantId, BookingRef(bookingId), ct))
        {
            _logger.LogDebug("Booking revenue already recorded — skip service completed accounting: booking={BookingId}", bookingId);
            return;
        }
        DateTime txDate = completedAt ?? DateTime.UtcNow;
        await CreateRevenueEntriesAsync(tenantId, bookingId, total, txDate, "Doanh thu dịch vụ đặt lịch", ct);
        _logger.LogInformation("Booking service completed accounted: booking={BookingId} total={Total}", bookingId, total);
    }

    /// <summary>
    /// Issue #195 follow-up (2026-10-29, user approve A): booking có Order gắn kèm (D2) →
    /// doanh thu thuộc trách nhiệm ORDER (ghi tại order payment). Fail-open: không đọc được
    /// booking → vẫn ghi doanh thu (không mất số liệu — double guard tầng order đã idempotent).
    /// </summary>
    private async Task<bool> HasLinkedOrderAsync(TenantId tenantId, Guid bookingId, CancellationToken ct)
    {
        try
        {
            BookingEntity? booking = await _context.Bookings
                .IgnoreQueryFilters()
                .AsNoTracking()
                .FirstOrDefaultAsync(b => b.Id == bookingId && b.TenantId == new TenantId(tenantId.Value), ct);
            return booking?.OrderId is not null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "HasLinkedOrderAsync failed (fail-open → ghi doanh thu booking): booking={BookingId}", bookingId);
            return false;
        }
    }

    // ── Revenue (511/512 + 3331) — mirror OrderService.GenerateAccountingEntriesAsync ──

    private async Task CreateRevenueEntriesAsync(
        TenantId tenantId, Guid bookingId, decimal total, DateTime transactionDate, string descriptionPrefix, CancellationToken ct)
    {
        if (total <= 0)
            return;
        var period = new AccountingPeriod(transactionDate.Year, transactionDate.Month);
        if (!await CanPostAsync(tenantId, period, ct))
            return;

        string reference = BookingRef(bookingId);
        IndustrySector? sector = await ResolveIndustrySectorAsync(tenantId, ct);
        string revenueAccount = await ResolveRevenueAccountAsync(tenantId, bookingId, ct);   // 512 (HTX nội) / 511
        decimal vat = Math.Round(total * 0.10m, 0, MidpointRounding.AwayFromZero);            // VAT 10% — khớp order hook (VatRate 0.10)

        try
        {
            // Revenue (net) — W0-T8 (H2): credit 511 = tổng tiền (gross price snapshot).
            _ = await _accountingService.CreateRevenueEntryAsync(
                tenantId, period, total,
                $"{descriptionPrefix} — mã đặt lịch {bookingId}",
                accountCode: revenueAccount,
                reference: reference,
                industrySector: sector,
                transactionDate: transactionDate);

            // VAT output liability — account 3331 (pattern OrderService).
            if (vat > 0)
            {
                _ = await _accountingService.CreateRevenueEntryAsync(
                    tenantId, period, vat,
                    $"Thuế GTGT đầu ra — đặt lịch {bookingId}",
                    accountCode: "3331",
                    reference: reference,
                    industrySector: sector,
                    transactionDate: transactionDate);
            }
        }
        catch (Exception ex)
        {
            // Fail-safe: không fail luồng booking — retry lần gọi sau (idempotent theo reference).
            _logger.LogError(ex, "CreateRevenueEntriesAsync failed (non-blocking): booking={BookingId}", bookingId);
        }
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    /// <summary>Period-closing guard (P1 decision #2: giữ guard) — kỳ đã đóng sổ → skip + log (fail-safe, không fail booking).</summary>
    private async Task<bool> CanPostAsync(TenantId tenantId, AccountingPeriod period, CancellationToken ct)
    {
        try
        {
            PeriodClosingStatus status = await _periodClosingService.GetPeriodStatusAsync(period, tenantId);
            if (status == PeriodClosingStatus.Closed)
            {
                _logger.LogWarning("Booking accounting skipped — kỳ {Period} đã đóng sổ (tenant {TenantId}).", period, tenantId.Value);
                return false;
            }
            return true;
        }
        catch (Exception ex)
        {
            // Fail-safe: lỗi period service không chặn luồng booking; entry sẽ bị chặn bởi guard kỳ đóng nếu có.
            _logger.LogWarning(ex, "CanPostAsync failed for tenant {TenantId} — allow post.", tenantId.Value);
            return true;
        }
    }

    /// <summary>Wave 5: IndustrySector cho bút toán (TT 152 S2a/S2b industry-group split).</summary>
    private async Task<IndustrySector?> ResolveIndustrySectorAsync(TenantId tenantId, CancellationToken ct)
        => await _context.Tenants
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(t => t.Id == tenantId)
            .Select(t => t.DefaultIndustrySector)
            .FirstOrDefaultAsync(ct);

    /// <summary>TT 71 (2026-10-02): tenant bán HTX + buyer là member Active → 512 (nội bộ); ngoài → 511 (pattern OrderService).</summary>
    private async Task<string> ResolveRevenueAccountAsync(TenantId tenantId, Guid bookingId, CancellationToken ct)
    {
        try
        {
            bool isHtx = await _context.Tenants
                .IgnoreQueryFilters()
                .AsNoTracking()
                .AnyAsync(t => t.Id == tenantId && t.AccountingStandard == AccountingStandard.TT71_2024, ct);
            if (!isHtx)
                return "511";

            // Buyer = customer sở hữu tenant là member Active của HTX bán → nội bộ.
            Guid? customerId = await _context.Bookings
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(b => b.Id == bookingId)
                .Select(b => b.CustomerId)
                .FirstOrDefaultAsync(ct);
            if (customerId is null || customerId == Guid.Empty)
                return "511";

            var buyerTenant = await _context.Tenants
                .IgnoreQueryFilters()
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.OwnerCustomerId == customerId, ct);
            if (buyerTenant is null)
                return "511";

            bool isMember = await _context.Members
                .IgnoreQueryFilters()
                .AsNoTracking()
                .AnyAsync(m => m.TenantId == tenantId && m.MemberTenantId == buyerTenant.Id && m.Status == MemberStatus.Active, ct);
            return isMember ? "512" : "511";
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "ResolveRevenueAccountAsync failed for tenant {TenantId} — fallback 511.", tenantId.Value);
            return "511";
        }
    }

    private async Task AuditAsync(CoreAccountingEntry entry, string description)
    {
        try
        {
            var newValues = JsonSerializer.Serialize(new
            {
                entry.Amount,
                Description = description,
                entry.AccountCode,
                entry.Reference,
                entry.TransactionDate
            });
            await _auditTrailService.LogCreateAsync(
                AuditableEntityType.AccountingEntry,
                entry.Id,
                newValues,
                correlationId: entry.Id.ToString());
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Booking accounting audit log failed (non-blocking): entry={EntryId}", entry.Id);
        }
    }
}
