// HKD Electronic Invoice Bounded Context
// File: Controllers/HKDElectronicInvoiceController.cs
// Responsibility: API layer for HKD Electronic Invoice operations
// ACID Scope: Invoice creation + Revenue recognition + Inventory deduction (Unit of Work)

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VanAn.CoreHub.Services.Orchestration;
using VanAn.Shared.Domain;
using VanAn.Shared.Domain.Common;
using VanAn.ShopERP.EInvoice.Dtos;

namespace VanAn.ShopERP.EInvoice.Controllers;

/// <summary>
/// HKD Electronic Invoice Controller
/// Bounded Context: EInvoice (isolated from other ShopERP modules)
/// Routes: /api/einvoice/*
/// Tenant isolation: Via ITenantProvider (JWT claim)
/// </summary>
[ApiController]
[Route("api/einvoice")]
[Authorize(Policy = "RequireTenantAccess")]
public class HKDElectronicInvoiceController : ControllerBase
{
    private readonly IEInvoiceOrchestrator _orchestrator;
    private readonly ITenantProvider _tenantProvider;
    private readonly ILogger<HKDElectronicInvoiceController> _logger;

    public HKDElectronicInvoiceController(
        IEInvoiceOrchestrator orchestrator,
        ITenantProvider tenantProvider,
        ILogger<HKDElectronicInvoiceController> logger)
    {
        _orchestrator = orchestrator;
        _tenantProvider = tenantProvider;
        _logger = logger;
    }

    /// <summary>
    /// Create new HKD Electronic Invoice
    /// POST /api/einvoice
    /// ACID: Invoice entity + Outbox event in same transaction
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<InvoiceDto>> CreateInvoice(
        [FromBody] CreateInvoiceRequest request,
        CancellationToken cancellationToken)
    {
        if (!_tenantProvider.HasTenant)
        {
            _logger.LogWarning("CreateInvoice: Tenant not found in JWT claims");
            return Unauthorized(new { Error = "Tenant access required" });
        }

        var tenantId = new TenantId(_tenantProvider.TenantId);
        var orderId = new OrderId(request.OrderId);
        var idempotencyKey = new InvoiceIdempotencyKey(Guid.NewGuid().ToString("N"));
        var invoiceType = request.InvoiceType.ToLowerInvariant() switch
        {
            "service" or "services" => InvoiceType.Services,
            "mixed" => InvoiceType.Mixed,
            "hkd" => InvoiceType.HKD,
            _ => InvoiceType.Goods
        };

        _logger.LogInformation(
            "Creating HKD Electronic Invoice for tenant={Tenant}, order={Order}",
            tenantId.Value,
            orderId.Value);

        try
        {
            var invoiceId = await _orchestrator.CreateInvoiceAsync(
                tenantId,
                orderId,
                idempotencyKey,
                invoiceType,
                request.Amount,
                request.VatAmount,
                request.TotalAmount,
                request.CustomerName,
                request.CustomerTaxCode,
                request.CustomerAddress,
                cancellationToken);

            _logger.LogInformation(
                "HKD Electronic Invoice created: {InvoiceId}",
                invoiceId.Value);

            // Return created invoice details
            var invoice = await _orchestrator.GetInvoiceAsync(invoiceId, cancellationToken);
            if (invoice is null)
            {
                return StatusCode(500, new { Error = "Invoice created but could not be retrieved" });
            }

            var dto = MapToDto(invoice, request.Items);
            return Created($"/api/einvoice/{invoiceId.Value}", dto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create HKD Electronic Invoice");
            return StatusCode(500, new { Error = "Invoice creation failed", Detail = ex.Message });
        }
    }

    /// <summary>
    /// Get invoice by ID
    /// GET /api/einvoice/{id}
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<InvoiceDto>> GetInvoice(
        Guid id,
        CancellationToken cancellationToken)
    {
        if (!_tenantProvider.HasTenant)
        {
            return Unauthorized(new { Error = "Tenant access required" });
        }

        var invoiceId = new ElectronicInvoiceId(id);
        var invoice = await _orchestrator.GetInvoiceAsync(invoiceId, cancellationToken);

        if (invoice is null)
        {
            return NotFound(new { Error = $"Invoice {id} not found" });
        }

        // Verify tenant isolation
        if (invoice.TenantId.Value != _tenantProvider.TenantId)
        {
            _logger.LogWarning(
                "Tenant isolation violation: User from tenant {UserTenant} attempted to access invoice {Invoice} belonging to tenant {InvoiceTenant}",
                _tenantProvider.TenantId,
                id,
                invoice.TenantId.Value);
            return NotFound(new { Error = $"Invoice {id} not found" });
        }

        var dto = MapToDto(invoice, new List<InvoiceItemDto>());
        return Ok(dto);
    }

    /// <summary>
    /// Submit invoice to tax provider
    /// POST /api/einvoice/{id}/submit
    /// State transition: Draft → PendingSend → SentToProvider
    /// </summary>
    [HttpPost("{id:guid}/submit")]
    public async Task<ActionResult<SubmitInvoiceResponse>> SubmitInvoice(
        Guid id,
        CancellationToken cancellationToken)
    {
        if (!_tenantProvider.HasTenant)
        {
            return Unauthorized(new { Error = "Tenant access required" });
        }

        var invoiceId = new ElectronicInvoiceId(id);

        // Verify invoice exists and belongs to tenant
        var invoice = await _orchestrator.GetInvoiceAsync(invoiceId, cancellationToken);
        if (invoice is null)
        {
            return NotFound(new { Error = $"Invoice {id} not found" });
        }

        if (invoice.TenantId.Value != _tenantProvider.TenantId)
        {
            return NotFound(new { Error = $"Invoice {id} not found" });
        }

        // Verify invoice can be submitted (must be Draft)
        if (invoice.Status != InvoiceStatus.Draft)
        {
            return BadRequest(new
            {
                Error = $"Invoice cannot be submitted in status {invoice.Status}. Expected: Draft"
            });
        }

        _logger.LogInformation(
            "Submitting HKD Electronic Invoice {InvoiceId} for tenant {Tenant}",
            id,
            _tenantProvider.TenantId);

        try
        {
            await _orchestrator.SubmitInvoiceAsync(invoiceId, cancellationToken);

            return Ok(new SubmitInvoiceResponse(
                Success: true,
                Message: "Invoice submitted successfully",
                SubmittedAt: DateTime.UtcNow));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to submit invoice {InvoiceId}", id);
            return StatusCode(500, new SubmitInvoiceResponse(
                Success: false,
                Message: $"Submit failed: {ex.Message}"));
        }
    }

    /// <summary>
    /// Get invoice status
    /// GET /api/einvoice/{id}/status
    /// </summary>
    [HttpGet("{id:guid}/status")]
    public async Task<ActionResult<InvoiceStatusResponse>> GetInvoiceStatus(
        Guid id,
        CancellationToken cancellationToken)
    {
        if (!_tenantProvider.HasTenant)
        {
            return Unauthorized(new { Error = "Tenant access required" });
        }

        var invoiceId = new ElectronicInvoiceId(id);
        var invoice = await _orchestrator.GetInvoiceAsync(invoiceId, cancellationToken);

        if (invoice is null)
        {
            return NotFound(new { Error = $"Invoice {id} not found" });
        }

        // Verify tenant isolation
        if (invoice.TenantId.Value != _tenantProvider.TenantId)
        {
            return NotFound(new { Error = $"Invoice {id} not found" });
        }

        var dto = new InvoiceStatusResponse(
            InvoiceId: id,
            Status: invoice.Status.ToString(),
            CurrentProvider: invoice.CurrentProvider?.ToString(),
            SubmittedAt: invoice.SubmittedAt,
            ApprovedAt: invoice.ApprovedAt,
            ProviderInvoiceNumber: invoice.ProviderInvoiceNumber,
            FailureReason: invoice.FailureReason);

        return Ok(dto);
    }

    /// <summary>
    /// Map domain entity to DTO
    /// </summary>
    private static InvoiceDto MapToDto(ElectronicInvoice invoice, List<InvoiceItemDto> items)
    {
        return new InvoiceDto(
            InvoiceId: invoice.InvoiceId.Value,
            OrderId: invoice.OrderId.Value,
            InvoiceType: invoice.InvoiceType.ToString(),
            Amount: invoice.Amount,
            VatAmount: invoice.VatAmount,
            TotalAmount: invoice.TotalAmount,
            CustomerName: invoice.CustomerName,
            CustomerTaxCode: invoice.CustomerTaxCode,
            CustomerAddress: invoice.CustomerAddress,
            Status: invoice.Status.ToString(),
            CurrentProvider: invoice.CurrentProvider?.ToString(),
            SubmittedAt: invoice.SubmittedAt,
            ApprovedAt: invoice.ApprovedAt,
            ProviderInvoiceNumber: invoice.ProviderInvoiceNumber,
            CreatedAt: invoice.CreatedAt,
            Items: items);
    }
}
