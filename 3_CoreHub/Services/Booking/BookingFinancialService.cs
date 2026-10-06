using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using VanAn.CoreHub.Infrastructure;
using VanAn.CoreHub.Infrastructure.Messaging;
using VanAn.Shared.Domain;
using BookingEntity = VanAn.Shared.Domain.Booking;

namespace VanAn.CoreHub.Services.Booking;

/// <summary>
/// BookingFinancialService — deposit/payment/invoice facts (SRS §16). Booking = source of facts:
/// PaymentTransaction lưu bản chất khoản tiền (§16.2), InvoiceIntegrationRecord snapshot trigger (§16.4),
/// outbox events cho Accounting/E-Invoice consumer (§16.6). KHÔNG hard-code thời điểm hóa đơn (§16.2 NĐ 70/2025).
/// </summary>
public sealed class BookingFinancialService(
    VanAnDbContext context,
    IOutboxRepository? outboxRepository,
    ILogger<BookingFinancialService> logger) : IBookingFinancialService
{
    private readonly VanAnDbContext _context = context;
    private readonly IOutboxRepository? _outboxRepository = outboxRepository;
    private readonly ILogger<BookingFinancialService> _logger = logger;

    /// <summary>§16.2 — phân loại theo tax profile (EinvoiceMode của tenant). Default SecurityDeposit (behavior P2 giữ nguyên).</summary>
    public MoneyNatureType ResolveDepositNature(BookingTenantConfig config)
    {
        // EinvoiceMode là snapshot tax profile từ Accounting module (§16.5). Tenant cấu hình deposit là
        // "tiền trả trước cho dịch vụ" → PREPAYMENT_FOR_SERVICE (hóa đơn tại thời điểm thu — NĐ 70/2025).
        string mode = config.EinvoiceMode?.Trim().ToUpperInvariant() ?? string.Empty;
        if (mode.Contains("PREPAY", StringComparison.OrdinalIgnoreCase) || mode.Contains("DEPOSIT_IS_PREPAYMENT", StringComparison.OrdinalIgnoreCase))
            return MoneyNatureType.PrepaymentForService;
        return MoneyNatureType.SecurityDeposit;
    }

    /// <summary>§16.4 — trigger snapshot: trả trước → hóa đơn tại thu tiền (OnPayment); cọc bảo đảm → ExternalRule (ngoại lệ riêng); thanh toán cuối → OnCompletion.</summary>
    public InvoiceTrigger ResolveInvoiceTrigger(MoneyNatureType nature, BookingTenantConfig config)
    {
        if (config.EinvoiceMode?.Contains("NO_INVOICE", StringComparison.OrdinalIgnoreCase) == true)
            return InvoiceTrigger.NotApplicable;
        return nature switch
        {
            MoneyNatureType.PrepaymentForService => InvoiceTrigger.OnPayment,
            MoneyNatureType.SecurityDeposit => InvoiceTrigger.ExternalRule,
            _ => InvoiceTrigger.OnCompletion
        };
    }

    public async Task<PaymentTransaction> CaptureDepositAsync(
        TenantId tenantId, Guid bookingId, decimal amount, string method = "VIETQR",
        string? providerRef = null, CancellationToken ct = default)
    {
        BookingEntity booking = await GetBookingOrThrowAsync(tenantId, bookingId, ct);
        BookingTenantConfig config = await GetConfigAsync(tenantId, ct);
        MoneyNatureType nature = ResolveDepositNature(config);

        var tx = new PaymentTransaction(tenantId, bookingId, nature, amount, method);
        _context.PaymentTransactions.Add(tx);

        if (string.IsNullOrWhiteSpace(providerRef))
        {
            _ = await _context.SaveChangesAsync(ct);
            _logger.LogInformation("Deposit PENDING: booking={BookingId} tx={TxId} amount={Amount} nature={Nature}", bookingId, tx.Id, amount, nature);
            return tx;
        }

        tx.MarkPaid(providerRef);
        booking.RecordDepositPaid();
        await EmitAsync(tenantId, "DepositPaid", new { bookingId, tx.Id, amount, nature, providerRef }, ct);
        if (ResolveInvoiceTrigger(nature, config) == InvoiceTrigger.OnPayment)
            await EmitInvoiceRequiredAsync(tenantId, bookingId, ResolveInvoiceTrigger(nature, config), ct);

        _ = await _context.SaveChangesAsync(ct);
        _logger.LogInformation("Deposit PAID: booking={BookingId} tx={TxId} amount={Amount} nature={Nature}", bookingId, tx.Id, amount, nature);
        return tx;
    }

    public async Task<PaymentTransaction> ConfirmDepositAsync(
        TenantId tenantId, Guid bookingId, Guid paymentTransactionId, string? providerRef = null, CancellationToken ct = default)
    {
        PaymentTransaction tx = await _context.PaymentTransactions.IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.TenantId == tenantId && t.BookingId == bookingId && t.Id == paymentTransactionId, ct)
            ?? throw new NotFoundException("Payment transaction không tồn tại trong booking này.");
        if (tx.Status == PaymentStatus.Paid)
            return tx;   // idempotent

        BookingEntity booking = await GetBookingOrThrowAsync(tenantId, bookingId, ct);
        BookingTenantConfig config = await GetConfigAsync(tenantId, ct);

        tx.MarkPaid(providerRef);
        booking.RecordDepositPaid();
        await EmitAsync(tenantId, "DepositPaid", new { bookingId, tx.Id, tx.Amount, tx.Type, providerRef }, ct);
        if (ResolveInvoiceTrigger(tx.Type, config) == InvoiceTrigger.OnPayment)
            await EmitInvoiceRequiredAsync(tenantId, bookingId, ResolveInvoiceTrigger(tx.Type, config), ct);

        _ = await _context.SaveChangesAsync(ct);
        _logger.LogInformation("Deposit confirmed: booking={BookingId} tx={TxId}", bookingId, tx.Id);
        return tx;
    }

    public async Task<PaymentTransaction> CaptureFinalPaymentAsync(
        TenantId tenantId, Guid bookingId, decimal amount, string method = "CASH",
        string? providerRef = null, CancellationToken ct = default)
    {
        BookingEntity booking = await GetBookingOrThrowAsync(tenantId, bookingId, ct);
        BookingTenantConfig config = await GetConfigAsync(tenantId, ct);

        var tx = new PaymentTransaction(tenantId, bookingId, MoneyNatureType.FinalPayment, amount, method);
        _context.PaymentTransactions.Add(tx);
        tx.MarkPaid(providerRef);
        booking.UpdatePaymentStatus(PaymentStatus.Paid);

        await EmitAsync(tenantId, "PaymentCaptured", new { bookingId, tx.Id, amount, providerRef }, ct);
        if (ResolveInvoiceTrigger(MoneyNatureType.FinalPayment, config) == InvoiceTrigger.OnPayment)
            await EmitInvoiceRequiredAsync(tenantId, bookingId, ResolveInvoiceTrigger(MoneyNatureType.FinalPayment, config), ct);

        _ = await _context.SaveChangesAsync(ct);
        _logger.LogInformation("Final payment captured: booking={BookingId} tx={TxId} amount={Amount}", bookingId, tx.Id, amount);
        return tx;
    }

    public async Task<PaymentTransaction> RefundDepositAsync(TenantId tenantId, Guid bookingId, string? providerRef = null, CancellationToken ct = default)
    {
        PaymentTransaction? deposit = await _context.PaymentTransactions.IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.TenantId == tenantId && t.BookingId == bookingId
                                      && (t.Type == MoneyNatureType.SecurityDeposit || t.Type == MoneyNatureType.PrepaymentForService)
                                      && t.Status == PaymentStatus.Paid, ct);

        if (deposit is null)
            throw new ValidationException("Không có deposit đã thanh toán để hoàn.");

        deposit.MarkRefunded();
        await EmitAsync(tenantId, "RefundCompleted", new { bookingId, deposit.Id, deposit.Amount, providerRef }, ct);
        _ = await _context.SaveChangesAsync(ct);
        _logger.LogInformation("Deposit refunded: booking={BookingId} tx={TxId}", bookingId, deposit.Id);
        return deposit;
    }

    public async Task RecordServiceCompletedAsync(TenantId tenantId, Guid bookingId, CancellationToken ct = default)
    {
        BookingEntity booking = await GetBookingOrThrowAsync(tenantId, bookingId, ct);
        BookingTenantConfig config = await GetConfigAsync(tenantId, ct);
        InvoiceIntegrationRecord record = await EnsureInvoiceRecordAsync(tenantId, bookingId, ct);

        // Invoice facts theo trigger snapshot (§16.4) — NĐ 70/2025: dịch vụ hoàn thành → hóa đơn (OnCompletion).
        InvoiceTrigger trigger = record.InvoiceTrigger == InvoiceTrigger.NotApplicable
            ? ResolveInvoiceTrigger(MoneyNatureType.FinalPayment, config)
            : record.InvoiceTrigger;
        if (trigger == InvoiceTrigger.OnCompletion && record.InvoiceStatus == BookingInvoiceStatus.NotRequired)
        {
            record.UpdateStatus(BookingInvoiceStatus.Pending);
            booking.UpdateInvoiceFacts(BookingInvoiceStatus.Pending, InvoiceTrigger.OnCompletion);
            await EmitInvoiceRequiredAsync(tenantId, bookingId, InvoiceTrigger.OnCompletion, ct);
        }
        else
        {
            booking.UpdateInvoiceFacts(record.InvoiceStatus, trigger);
        }

        await EmitAsync(tenantId, "ServiceCompleted", new { bookingId, booking.ActualTotal, booking.EstimatedTotal }, ct);
        _ = await _context.SaveChangesAsync(ct);
        _logger.LogInformation("ServiceCompleted facts recorded: booking={BookingId} trigger={Trigger}", bookingId, trigger);
    }

    public async Task<InvoiceIntegrationRecord> EnsureInvoiceRecordAsync(TenantId tenantId, Guid bookingId, CancellationToken ct = default)
        => await EnsureInvoiceRecordAsync(tenantId, bookingId, trigger: null, ct);

    private async Task<InvoiceIntegrationRecord> EnsureInvoiceRecordAsync(TenantId tenantId, Guid bookingId, InvoiceTrigger? trigger, CancellationToken ct)
    {
        InvoiceIntegrationRecord? record = await _context.InvoiceIntegrationRecords.IgnoreQueryFilters()
            .FirstOrDefaultAsync(r => r.TenantId == tenantId && r.BookingId == bookingId, ct);
        if (record is not null)
            return record;

        // §16.4 — trigger là SNAPSHOT theo bản chất khoản tiền đầu tiên (KHÔNG cho frontend chọn).
        BookingTenantConfig config = await GetConfigAsync(tenantId, ct);
        InvoiceTrigger resolved = trigger ?? ResolveInvoiceTrigger(MoneyNatureType.FinalPayment, config);
        record = new InvoiceIntegrationRecord(tenantId, bookingId, resolved);
        _context.InvoiceIntegrationRecords.Add(record);
        _ = await _context.SaveChangesAsync(ct);
        return record;
    }

    public async Task<InvoiceIntegrationRecord> UpdateInvoiceStatusAsync(
        TenantId tenantId, Guid bookingId, BookingInvoiceStatus status,
        string? provider = null, string? reference = null, string? errorCode = null, CancellationToken ct = default)
    {
        BookingEntity booking = await GetBookingOrThrowAsync(tenantId, bookingId, ct);
        InvoiceIntegrationRecord record = await EnsureInvoiceRecordAsync(tenantId, bookingId, ct);

        record.UpdateStatus(status, provider, reference, errorCode);
        booking.UpdateInvoiceFacts(status, record.InvoiceTrigger);

        string eventType = status switch
        {
            BookingInvoiceStatus.Issued => "InvoiceIssued",
            BookingInvoiceStatus.Failed => "InvoiceFailed",
            _ => "InvoiceStatusUpdated"
        };
        await EmitAsync(tenantId, eventType, new { bookingId, record.Id, status, provider, reference, errorCode }, ct);
        _ = await _context.SaveChangesAsync(ct);
        _logger.LogInformation("Invoice status updated: booking={BookingId} status={Status}", bookingId, status);
        return record;
    }

    public async Task<PaymentTransaction?> GetDepositAsync(TenantId tenantId, Guid bookingId, CancellationToken ct = default)
        => await _context.PaymentTransactions.IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.TenantId == tenantId && t.BookingId == bookingId
                                      && (t.Type == MoneyNatureType.SecurityDeposit || t.Type == MoneyNatureType.PrepaymentForService), ct);

    // ── Helpers ─────────────────────────────────────────────────────────

    private async Task<BookingEntity> GetBookingOrThrowAsync(TenantId tenantId, Guid bookingId, CancellationToken ct)
        => await _context.Bookings.IgnoreQueryFilters()
            .FirstOrDefaultAsync(b => b.TenantId == tenantId && b.Id == bookingId, ct)
           ?? throw new NotFoundException("Booking không tồn tại trong tenant này.");

    private async Task<BookingTenantConfig> GetConfigAsync(TenantId tenantId, CancellationToken ct)
    {
        BookingTenantConfig? config = await _context.BookingTenantConfigs.IgnoreQueryFilters()
            .FirstOrDefaultAsync(c => c.TenantId == tenantId, ct);
        return config ?? new BookingTenantConfig(tenantId);
    }

    private async Task EmitInvoiceRequiredAsync(TenantId tenantId, Guid bookingId, InvoiceTrigger trigger, CancellationToken ct)
    {
        BookingEntity booking = await GetBookingOrThrowAsync(tenantId, bookingId, ct);
        InvoiceIntegrationRecord record = await EnsureInvoiceRecordAsync(tenantId, bookingId, trigger, ct);
        if (record.InvoiceStatus == BookingInvoiceStatus.NotRequired)
            record.UpdateStatus(BookingInvoiceStatus.Pending);
        booking.UpdateInvoiceFacts(BookingInvoiceStatus.Pending, record.InvoiceTrigger);
        await EmitAsync(tenantId, "InvoiceRequired", new { bookingId, record.Id, trigger = trigger.ToString(), amount = booking.ActualTotal ?? booking.EstimatedTotal }, ct);
    }

    private async Task EmitAsync(TenantId tenantId, string eventType, object payload, CancellationToken ct)
    {
        if (_outboxRepository is null)
            return;   // tests không wire outbox — facts vẫn persist (PaymentTransaction/InvoiceRecord)
        var outboxEvent = new OutboxEvent(
            tenantId, new ElectronicInvoiceId(Guid.Empty), eventType,
            JsonSerializer.Serialize(payload));
        await _outboxRepository.EnqueueAsync(outboxEvent, ct);
    }
}
