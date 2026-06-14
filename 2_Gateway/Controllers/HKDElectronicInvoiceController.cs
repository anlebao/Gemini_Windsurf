using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using VanAn.CoreHub.Services.Orchestration;
using VanAn.Gateway.DTOs;
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
    public async Task<IActionResult> CreateInvoice(
        [FromBody] CreateInvoiceRequest request,
        [FromHeader(Name = "X-Idempotency-Key")] string? idempotencyKey,
        CancellationToken cancellationToken)
    {
        // Validate request
        if (request == null)
        {
            return BadRequest(new { Error = "Request cannot be null" });
        }

        // Extract TenantId from HttpContext.User.Claims (Gateway boundary compliant)
        var tenantIdClaim = User.FindFirst("tenant_id")?.Value;
        if (string.IsNullOrEmpty(tenantIdClaim))
        {
            return Unauthorized(new { Error = "Tenant ID not found in claims" });
        }
        var tenantId = new TenantId(Guid.Parse(tenantIdClaim));
        var orderId = new OrderId(request.OrderId);

        // Generate or use provided idempotency key
        var idempotencyKeyValue = !string.IsNullOrEmpty(idempotencyKey)
            ? idempotencyKey
            : Guid.NewGuid().ToString();
        var idempotencyKeyObj = InvoiceIdempotencyKey.FromString(idempotencyKeyValue);

        // Call orchestrator
        var invoiceId = await _orchestrator.CreateInvoiceAsync(
            tenantId,
            orderId,
            idempotencyKeyObj,
            request.InvoiceType,
            request.Amount,
            request.VatAmount,
            request.TotalAmount,
            request.CustomerName,
            request.CustomerTaxCode,
            request.CustomerAddress,
            cancellationToken);

        // Return 201 Created with response DTO
        var response = new CreateInvoiceResponseDto(
            invoiceId.Value,
            $"HD{DateTime.Now:yyyyMMddHHmmss}",
            InvoiceStatus.Draft);

        return CreatedAtAction(
            nameof(GetInvoice),
            new { id = invoiceId.Value },
            response);
    }

    /// <summary>
    /// Get invoice by ID
    /// </summary>
    [HttpGet("{id}")]
    public async Task<IActionResult> GetInvoice(Guid id, CancellationToken cancellationToken)
    {
        var invoiceId = new ElectronicInvoiceId(id);
        var invoice = await _orchestrator.GetInvoiceAsync(invoiceId, cancellationToken);

        if (invoice == null)
        {
            return NotFound(new { Error = $"Invoice with ID {id} not found" });
        }

        // Map to DTO
        var response = new InvoiceDto(
            invoice.InvoiceId.Value,
            invoice.OrderId.Value.ToString(),
            invoice.CustomerName,
            invoice.CustomerTaxCode,
            invoice.Items.Select(i => new InvoiceItemDto(
                i.ItemCode,
                i.ItemName,
                i.Unit,
                i.Quantity,
                i.UnitPrice,
                i.VatRate,
                i.Amount,
                i.VatAmount)).ToList(),
            invoice.TotalAmount,
            invoice.Status);

        return Ok(response);
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
        var invoiceId = new ElectronicInvoiceId(id);
        var status = await _orchestrator.GetInvoiceStatusAsync(invoiceId, cancellationToken);

        if (status == null)
        {
            return NotFound(new { Error = $"Invoice with ID {id} not found" });
        }

        // Map status to message
        var message = GetStatusMessage(status.Value);

        var response = new InvoiceStatusDto(id, status.Value, message);
        return Ok(response);
    }

    private static string GetStatusMessage(InvoiceStatus status)
    {
        return status switch
        {
            InvoiceStatus.Draft => "Hóa đơn nháp",
            InvoiceStatus.PendingSend => "Chờ gửi nhà cung cấp",
            InvoiceStatus.SentToProvider => "Đã gửi, chờ phản hồi",
            InvoiceStatus.TaxApproved => "CQT đã chấp nhận",
            InvoiceStatus.Failed => "Gửi thất bại",
            InvoiceStatus.Rejected => "CQT từ chối",
            _ => "Unknown"
        };
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
    string CustomerAddress,
    string? IdempotencyKey = null,
    List<InvoiceItemDto>? Items = null
);
