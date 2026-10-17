using Microsoft.EntityFrameworkCore;
using VanAn.CoreHub.Infrastructure;
using VanAn.CoreHub.Infrastructure.Messaging;
using VanAn.Shared.Domain;
using VanAn.Shared.Domain.Aggregates.SystemSettingAggregate;

namespace VanAn.CoreHub.Services.Orchestration;

/// <summary>
/// EInvoiceOrchestrator - Orchestrator implementation (ONLY coordination)
/// Delegates to focused services: Policy, Retry, Fallback, Compliance, Webhook
/// </summary>
public class EInvoiceOrchestrator : IEInvoiceOrchestrator
{
    private readonly IInvoicePolicyService _policyService;
    private readonly IRetryPolicyService _retryService;
    private readonly IFallbackService _fallbackService;
    private readonly IComplianceService _complianceService;
    private readonly IWebhookService _webhookService;
    private readonly IOutboxRepository _outboxRepository;
    private readonly VanAnDbContext _dbContext;
    private readonly IAuditTrailService? _auditTrailService;

    public EInvoiceOrchestrator(
        IInvoicePolicyService policyService,
        IRetryPolicyService retryService,
        IFallbackService fallbackService,
        IComplianceService complianceService,
        IWebhookService webhookService,
        IOutboxRepository outboxRepository,
        VanAnDbContext dbContext,
        IAuditTrailService? auditTrailService = null)
    {
        _policyService = policyService;
        _retryService = retryService;
        _fallbackService = fallbackService;
        _complianceService = complianceService;
        _webhookService = webhookService;
        _outboxRepository = outboxRepository;
        _dbContext = dbContext;
        _auditTrailService = auditTrailService;
    }

    public async Task<ElectronicInvoiceId> CreateInvoiceAsync(
        TenantId tenantId,
        OrderId orderId,
        InvoiceIdempotencyKey idempotencyKey,
        InvoiceType invoiceType,
        decimal amount,
        decimal vatAmount,
        decimal totalAmount,
        string customerName,
        string customerTaxCode,
        string customerAddress,
        CancellationToken cancellationToken = default)
    {
        var invoice = new ElectronicInvoice(
            tenantId, orderId, idempotencyKey, invoiceType,
            amount, vatAmount, totalAmount,
            customerName, customerTaxCode, customerAddress);

        var aggregate = new InvoiceAggregate(invoice);
        aggregate.Submit();

        var outboxEvent = new OutboxEvent(
            tenantId,
            invoice.InvoiceId,
            "InvoiceCreated",
            System.Text.Json.JsonSerializer.Serialize(new { invoiceId = invoice.InvoiceId.Value }));

        using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            await _dbContext.ElectronicInvoices.AddAsync(invoice, cancellationToken);
            await _outboxRepository.EnqueueAsync(outboxEvent, cancellationToken);
            await _dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }

        return invoice.InvoiceId;
    }

    public async Task<ElectronicInvoice?> GetInvoiceAsync(
        ElectronicInvoiceId invoiceId,
        CancellationToken cancellationToken = default)
    {
        if (invoiceId is null || invoiceId.Value == Guid.Empty)
            return null;

        return await _dbContext.ElectronicInvoices
            .AsNoTracking()
            .FirstOrDefaultAsync(i => i.InvoiceId == invoiceId, cancellationToken);
    }

    public async Task<InvoiceStatus?> GetInvoiceStatusAsync(
        ElectronicInvoiceId invoiceId,
        CancellationToken cancellationToken = default)
    {
        if (invoiceId is null || invoiceId.Value == Guid.Empty)
            return null;

        var invoice = await _dbContext.ElectronicInvoices
            .AsNoTracking()
            .Select(i => new { i.InvoiceId, i.Status })
            .FirstOrDefaultAsync(i => i.InvoiceId == invoiceId, cancellationToken);

        return invoice?.Status;
    }

    /// <summary>
    /// W3 (P0.3): list invoices của tenant — mới nhất trước, kèm line items.
    /// </summary>
    public async Task<IReadOnlyList<ElectronicInvoice>> ListInvoicesAsync(
        TenantId tenantId,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.ElectronicInvoices
            .AsNoTracking()
            .Include(i => i.Items)
            .Where(i => i.TenantId == tenantId)
            .OrderByDescending(i => i.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// W3 (P0.3 — Điều 10 TT 91/2026): tạo hóa đơn điều chỉnh/thay thế.
    /// Guard: chỉ hóa đơn TaxApproved có ProviderInvoiceNumber mới được xử lý sai sót
    /// (hóa đơn chưa phát hành sửa trực tiếp; hóa đơn hủy theo quy trình riêng — DEFERRED).
    /// </summary>
    public async Task<ElectronicInvoiceId> AdjustInvoiceAsync(
        ElectronicInvoiceId originalInvoiceId,
        InvoiceAdjustmentType adjustmentType,
        string reason,
        InvoiceAdjustmentData? newData = null,
        CancellationToken cancellationToken = default)
    {
        if (originalInvoiceId is null || originalInvoiceId.Value == Guid.Empty)
            throw new ArgumentException("Original invoice id is required.", nameof(originalInvoiceId));
        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("Adjustment reason is required (Điều 10 TT 91/2026).", nameof(reason));

        var original = await _dbContext.ElectronicInvoices
            .Include(i => i.Items)
            .FirstOrDefaultAsync(i => i.InvoiceId == originalInvoiceId, cancellationToken)
            ?? throw new InvalidOperationException($"Invoice {originalInvoiceId.Value} not found.");

        if (original.Status != InvoiceStatus.TaxApproved)
            throw new InvalidOperationException(
                $"Only issued (TaxApproved) invoices can be adjusted/replaced. Current status: {original.Status}.");
        if (original.ProviderInvoiceNumber is null)
            throw new InvalidOperationException(
                "Original invoice has no provider invoice number — cannot adjust an unissued invoice.");

        // Hóa đơn mới kế thừa dữ liệu gốc; field nào có newData thì override.
        var invoice = new ElectronicInvoice(
            original.TenantId,
            original.OrderId,
            new InvoiceIdempotencyKey(Guid.NewGuid().ToString("N")),
            original.InvoiceType,
            newData?.Amount ?? original.Amount,
            newData?.VatAmount ?? original.VatAmount,
            newData?.TotalAmount ?? original.TotalAmount,
            newData?.CustomerName ?? original.CustomerName,
            newData?.CustomerTaxCode ?? original.CustomerTaxCode,
            newData?.CustomerAddress ?? original.CustomerAddress);

        invoice.MarkAsAdjustmentOf(original, adjustmentType, reason);
        invoice.Submit(); // Draft → PendingSend — đi pipeline outbox như hóa đơn thường

        // Copy line items từ hóa đơn gốc (điều chỉnh tiền ở tổng — MVP scope).
        foreach (var item in original.Items)
        {
            var copy = new InvoiceItem(
                original.TenantId,
                invoice.InvoiceId,
                item.ItemCode,
                item.ItemName,
                item.Unit,
                item.Quantity,
                item.UnitPrice,
                item.VatRate);
            invoice.Items.Add(copy);
        }

        if (adjustmentType == InvoiceAdjustmentType.Replace)
            original.MarkAsReplaced();
        else
            original.MarkAsAdjusted();

        var outboxEvent = new OutboxEvent(
            original.TenantId,
            invoice.InvoiceId,
            "InvoiceCreated",
            System.Text.Json.JsonSerializer.Serialize(new { invoiceId = invoice.InvoiceId.Value }));

        using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            await _dbContext.ElectronicInvoices.AddAsync(invoice, cancellationToken);
            _dbContext.ElectronicInvoices.Update(original);
            await _outboxRepository.EnqueueAsync(outboxEvent, cancellationToken);
            await _dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }

        // Audit Correction — async queue (fire-and-forget), Pattern #31 writer SetTenant per log.
        if (_auditTrailService is not null)
        {
            await _auditTrailService.LogInvoiceCorrectionAsync(
                original.InvoiceId.Value,
                invoice.InvoiceId.Value,
                adjustmentType.ToString(),
                reason,
                cancellationToken: cancellationToken);
        }

        return invoice.InvoiceId;
    }

    public async Task SubmitInvoiceAsync(
        ElectronicInvoiceId invoiceId,
        CancellationToken cancellationToken = default)
    {
        await _policyService.ValidateInvoiceAsync(invoiceId, cancellationToken);
        await _complianceService.ValidateComplianceAsync(invoiceId, cancellationToken);
        await _retryService.SubmitWithRetryAsync(invoiceId, cancellationToken);
    }

    public async Task ProcessWebhookAsync(
        string providerId,
        string providerInvoiceNumber,
        string callbackData,
        CancellationToken cancellationToken = default)
    {
        await _webhookService.ProcessWebhookAsync(providerId, providerInvoiceNumber, callbackData, cancellationToken);
    }

    /// <summary>
    /// P0.4 (W2, 2026-10-17 — Điều 8 TT 91/2026): upsert SystemSetting theo tenant.
    /// IgnoreQueryFilters + filter TenantId tường minh (ambient tenant có thể khác trong worker context).
    /// </summary>
    public async Task SetEInvoiceUsageSuspendedAsync(
        TenantId tenantId,
        bool suspended,
        string? reason = null,
        Guid? updatedBy = null,
        CancellationToken cancellationToken = default)
    {
        var flag = await _dbContext.SystemSettings
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(
                s => s.TenantId == tenantId
                    && s.Key == ComplianceService.EInvoiceUsageSuspendedKey,
                cancellationToken);

        if (flag is null)
        {
            flag = new SystemSetting(
                tenantId,
                ComplianceService.EInvoiceUsageSuspendedKey,
                suspended ? "true" : "false",
                updatedBy);
            await _dbContext.SystemSettings.AddAsync(flag, cancellationToken);
        }
        else
        {
            flag.Update(suspended ? "true" : "false", updatedBy);
        }

        if (reason is not null)
        {
            var reasonSetting = await _dbContext.SystemSettings
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(
                    s => s.TenantId == tenantId
                        && s.Key == ComplianceService.EInvoiceUsageSuspendedReasonKey,
                    cancellationToken);

            if (reasonSetting is null)
            {
                await _dbContext.SystemSettings.AddAsync(
                    new SystemSetting(tenantId, ComplianceService.EInvoiceUsageSuspendedReasonKey, reason, updatedBy),
                    cancellationToken);
            }
            else
            {
                reasonSetting.Update(reason, updatedBy);
            }
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
