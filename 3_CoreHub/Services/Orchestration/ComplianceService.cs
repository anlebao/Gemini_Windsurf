using Microsoft.EntityFrameworkCore;
using VanAn.CoreHub.Infrastructure;
using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Services.Orchestration;

/// <summary>
/// ComplianceService - TT152-2025 compliance validation implementation
/// Focused service: ONLY compliance validation
/// </summary>
public class ComplianceService : IComplianceService
{
    private readonly VanAnDbContext? _dbContext;

    public ComplianceService() : this(null!) { }

    public ComplianceService(VanAnDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task ValidateComplianceAsync(
        ElectronicInvoiceId invoiceId,
        CancellationToken cancellationToken = default)
    {
        if (invoiceId is null || invoiceId.Value == Guid.Empty)
            throw new ArgumentException(
                "TT152-2025: InvoiceId must be a valid non-empty GUID.",
                nameof(invoiceId));

        if (_dbContext is null)
            throw new InvalidOperationException("ComplianceService requires a database context.");

        var invoice = await _dbContext.ElectronicInvoices
            .AsNoTracking()
            .FirstOrDefaultAsync(i => i.InvoiceId == invoiceId, cancellationToken);

        if (invoice is null)
            throw new InvalidOperationException($"Invoice {invoiceId.Value} not found.");

        ValidateTT152Compliance(invoice);
    }

    public async Task<bool> IsCompliantAsync(
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
            ValidateTT152Compliance(invoice);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static void ValidateTT152Compliance(ElectronicInvoice invoice)
    {
        var errors = new List<string>();

        // TT152-2025 mandatory fields
        if (string.IsNullOrWhiteSpace(invoice.CustomerName))
            errors.Add("CustomerName is required per TT152-2025.");

        if (string.IsNullOrWhiteSpace(invoice.CustomerTaxCode) || invoice.CustomerTaxCode.Length < 10)
            errors.Add("CustomerTaxCode must be at least 10 characters (tax authority requirement).");

        if (invoice.TotalAmount <= 0)
            errors.Add("TotalAmount must be greater than zero.");

        if (invoice.VatAmount < 0)
            errors.Add("VatAmount cannot be negative.");

        if (string.IsNullOrWhiteSpace(invoice.CustomerAddress))
            errors.Add("CustomerAddress is required per TT152-2025.");

        if (errors.Count > 0)
            throw new InvalidOperationException(
                $"TT152-2025 compliance validation failed for invoice {invoice.InvoiceId.Value}: {string.Join("; ", errors)}");
    }
}
