using Microsoft.EntityFrameworkCore;
using VanAn.CoreHub.Infrastructure;
using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Services.Orchestration;

/// <summary>
/// InvoicePolicyService - Invoice business rules implementation
/// Focused service: ONLY invoice policy logic per HKD regulations
/// </summary>
public class InvoicePolicyService : IInvoicePolicyService
{
    private readonly VanAnDbContext? _dbContext;

    // HKD Business Policy Constants per TT152-2025
    private const decimal MaxInvoiceAmount = 100_000_000_000m; // 100 billion VND
    private const decimal MinInvoiceAmount = 1_000m; // 1,000 VND minimum

    public InvoicePolicyService() : this(null!) { }

    public InvoicePolicyService(VanAnDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task ValidateInvoiceAsync(
        ElectronicInvoiceId invoiceId,
        CancellationToken cancellationToken = default)
    {
        if (invoiceId is null || invoiceId.Value == Guid.Empty)
            throw new ArgumentException("InvoiceId must be a valid non-empty GUID.", nameof(invoiceId));

        if (_dbContext is null)
            throw new InvalidOperationException("InvoicePolicyService requires a database context.");

        var invoice = await _dbContext.ElectronicInvoices
            .AsNoTracking()
            .FirstOrDefaultAsync(i => i.InvoiceId == invoiceId, cancellationToken);

        if (invoice is null)
            throw new InvalidOperationException($"Invoice {invoiceId.Value} not found.");

        ValidateBusinessPolicy(invoice);
    }

    public async Task<bool> CanSubmitAsync(
        ElectronicInvoiceId invoiceId,
        CancellationToken cancellationToken = default)
    {
        if (invoiceId is null || invoiceId.Value == Guid.Empty)
            return false;

        if (_dbContext is null)
            return false;

        var invoice = await _dbContext.ElectronicInvoices
            .AsNoTracking()
            .FirstOrDefaultAsync(i => i.InvoiceId == invoiceId, cancellationToken);

        if (invoice is null)
            return false;

        try
        {
            ValidateBusinessPolicy(invoice);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static void ValidateBusinessPolicy(ElectronicInvoice invoice)
    {
        var errors = new List<string>();

        // 1. Amount range validation
        if (invoice.TotalAmount < MinInvoiceAmount)
            errors.Add($"TotalAmount {invoice.TotalAmount:N0} VND is below minimum threshold of {MinInvoiceAmount:N0} VND.");

        if (invoice.TotalAmount > MaxInvoiceAmount)
            errors.Add($"TotalAmount {invoice.TotalAmount:N0} VND exceeds maximum allowed of {MaxInvoiceAmount:N0} VND.");

        // 2. Status validation — only Draft or PendingSend can be submitted
        if (invoice.Status != InvoiceStatus.Draft && invoice.Status != InvoiceStatus.PendingSend)
            errors.Add($"Invoice status '{invoice.Status}' does not allow submission. Expected: Draft or PendingSend.");

        // 3. Tax amount validation (VAT calculation check)
        var expectedVat = CalculateExpectedVat(invoice.Amount, invoice.InvoiceType);
        var vatTolerance = 0.01m;
        if (Math.Abs(invoice.VatAmount - expectedVat) > vatTolerance)
            errors.Add($"VatAmount {invoice.VatAmount:N2} does not match calculated VAT {expectedVat:N2} for invoice type {invoice.InvoiceType}.");

        // 4. Customer information completeness
        if (string.IsNullOrWhiteSpace(invoice.CustomerName))
            errors.Add("CustomerName is required for invoice submission.");

        if (string.IsNullOrWhiteSpace(invoice.CustomerTaxCode))
            errors.Add("CustomerTaxCode is required for invoice submission.");

        if (errors.Count > 0)
            throw new InvalidOperationException(
                $"Invoice policy validation failed for invoice {invoice.InvoiceId.Value}: {string.Join("; ", errors)}");
    }

    private static decimal CalculateExpectedVat(decimal amount, InvoiceType invoiceType)
    {
        // Standard VAT rates per TT152-2025
        var vatRate = invoiceType switch
        {
            InvoiceType.Goods => 0.10m,    // 10% for goods
            InvoiceType.Services => 0.08m, // 8% for services
            InvoiceType.Mixed => 0.10m,   // 10% default for mixed
            _ => 0.10m
        };

        return Math.Round(amount * vatRate, 2, MidpointRounding.AwayFromZero);
    }
}
