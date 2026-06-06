using Microsoft.AspNetCore.Mvc;
using VanAn.CoreHub.Services.Orchestration;
using VanAn.Shared.Domain;

namespace VanAn.Gateway.Controllers;

/// <summary>
/// HKDElectronicInvoiceController - REST API for E-Invoice operations
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class HKDElectronicInvoiceController : ControllerBase
{
    private readonly IEInvoiceOrchestrator _orchestrator;

    public HKDElectronicInvoiceController(IEInvoiceOrchestrator orchestrator)
    {
        _orchestrator = orchestrator;
    }

    /// <summary>
    /// Create new electronic invoice
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> CreateInvoice([FromBody] CreateInvoiceRequest request, CancellationToken cancellationToken)
    {
        // Stub: Create invoice
        // TODO: Implement with actual invoice creation
        return Ok(new { InvoiceId = Guid.NewGuid() });
    }

    /// <summary>
    /// Get invoice by ID
    /// </summary>
    [HttpGet("{id}")]
    public async Task<IActionResult> GetInvoice(Guid id, CancellationToken cancellationToken)
    {
        // Stub: Get invoice
        // TODO: Implement with actual invoice retrieval
        return Ok(new { InvoiceId = id });
    }

    /// <summary>
    /// Submit invoice to provider
    /// </summary>
    [HttpPost("{id}/submit")]
    public async Task<IActionResult> SubmitInvoice(Guid id, CancellationToken cancellationToken)
    {
        var invoiceId = new ElectronicInvoiceId(id);
        await _orchestrator.SubmitInvoiceAsync(invoiceId, cancellationToken);
        return Ok(new { Message = "Invoice submitted successfully" });
    }

    /// <summary>
    /// Get invoice status
    /// </summary>
    [HttpGet("{id}/status")]
    public async Task<IActionResult> GetInvoiceStatus(Guid id, CancellationToken cancellationToken)
    {
        // Stub: Get invoice status
        // TODO: Implement with actual status retrieval
        return Ok(new { InvoiceId = id, Status = "Draft" });
    }
}

/// <summary>
/// Create Invoice Request DTO
/// </summary>
public record CreateInvoiceRequest(
    Guid OrderId,
    InvoiceType InvoiceType,
    decimal Amount,
    decimal VatAmount,
    decimal TotalAmount,
    string CustomerName,
    string CustomerTaxCode,
    string CustomerAddress
);
