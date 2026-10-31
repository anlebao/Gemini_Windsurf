using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VanAn.CoreHub.Infrastructure.Entities;
using VanAn.CoreHub.Services.Purchase;
using VanAn.Shared.Domain;

namespace VanAn.ShopERP.Controllers;

/// <summary>
/// SRS v1.2 W6/E (backend-first — task card E): chứng từ nhập mua per tenant.
/// POST create → phiếu + JE (purchase/purchase-cash) + công nợ 331 khi mua chịu.
/// POST {id}/return → JE đảo + đảo phiếu công nợ (trả hàng — AT-008).
/// </summary>
[ApiController]
[Route("api/shop/accounting/purchase-receipts")]
[Authorize]
public class PurchaseReceiptsController(IPurchaseReceiptService purchaseService) : ControllerBase
{
    public sealed record LineInput(string ItemName, decimal Quantity, decimal UnitPrice);

    public sealed record CreateReceiptRequest(string SupplierName, string? SupplierRef,
        DateTime? ReceiptDate, string PayMethod, string? CashAccountCode, string DebitAccountCode,
        decimal VatAmount, string? Reference, string? SourceEventId, DateTime? DueDate,
        IReadOnlyList<LineInput> Lines);

    public sealed record ReturnReceiptRequest(string Reason);

    public sealed record ReceiptLineDto(string ItemName, decimal Quantity, decimal UnitPrice, decimal LineTotal);

    public sealed record ReceiptDto(Guid Id, string VoucherNumber, string SupplierName, string? SupplierRef,
        DateTime ReceiptDate, decimal NetAmount, decimal VatAmount, decimal TotalAmount,
        string PayMethod, string? CashAccountCode, string DebitAccountCode, string Status,
        string? Reference, string? SourceEventId, Guid? PayableEntryId, DateTime? DueDate,
        IReadOnlyList<ReceiptLineDto> Lines);

    /// <summary>Danh sách phiếu nhập mua của tenant (mới nhất trước).</summary>
    [HttpGet]
    [Authorize(Policy = "AccountingAccess")]
    public async Task<ActionResult<IReadOnlyList<ReceiptDto>>> List(
        [FromQuery] Guid? tenantId, [FromQuery] DateTime? from, [FromQuery] DateTime? to, CancellationToken ct)
    {
        Guid tid = tenantId ?? GetTenantIdFromContext();
        if (tid == Guid.Empty)
            return BadRequest("TenantId is required.");

        var receipts = await purchaseService.ListAsync(new TenantId(tid), from, to, ct);
        return Ok(receipts.Select(ToDto).ToList());
    }

    /// <summary>Chi tiết phiếu (kèm lines).</summary>
    [HttpGet("{id:guid}")]
    [Authorize(Policy = "AccountingAccess")]
    public async Task<ActionResult<ReceiptDto>> Get(Guid id, CancellationToken ct)
    {
        var receipt = await purchaseService.GetByIdAsync(id, ct);
        return receipt == null ? NotFound() : Ok(ToDto(receipt));
    }

    /// <summary>Lập phiếu nhập mua. PayMethod: "credit" (mua chịu — JE 156/331 + công nợ)
    /// hoặc "immediate" (trả ngay — JE 156/{cash}).</summary>
    [HttpPost]
    [Authorize(Policy = "AccountingFullAccess")]
    public async Task<ActionResult<ReceiptDto>> Create(
        [FromBody] CreateReceiptRequest request, [FromQuery] Guid? tenantId, CancellationToken ct)
    {
        Guid tid = tenantId ?? GetTenantIdFromContext();
        if (tid == Guid.Empty)
            return BadRequest("TenantId is required.");
        if (!Enum.TryParse<PurchasePayMethod>(request.PayMethod, ignoreCase: true, out PurchasePayMethod payMethod))
            return BadRequest("PayMethod phải là 'credit' hoặc 'immediate'.");

        try
        {
            var receipt = await purchaseService.CreateAsync(new TenantId(tid),
                request.SupplierName, request.SupplierRef,
                request.ReceiptDate ?? DateTime.UtcNow,
                request.Lines.Select(l => new PurchaseReceiptLineInput(l.ItemName, l.Quantity, l.UnitPrice)).ToList(),
                payMethod, request.CashAccountCode, request.DebitAccountCode,
                request.VatAmount, request.Reference, request.SourceEventId, request.DueDate, ct);
            return Ok(ToDto(receipt));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(ex.Message);
        }
    }

    /// <summary>Trả hàng/đảo phiếu — JE đảo (purchase-return) + đảo công nợ 331.</summary>
    [HttpPost("{id:guid}/return")]
    [Authorize(Policy = "AccountingFullAccess")]
    public async Task<ActionResult<ReceiptDto>> Return(Guid id,
        [FromBody] ReturnReceiptRequest request, CancellationToken ct)
    {
        try
        {
            var receipt = await purchaseService.ReturnAsync(id, request.Reason, ct);
            return Ok(ToDto(receipt));
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(ex.Message);
        }
    }

    private static ReceiptDto ToDto(PurchaseReceiptEntity r) => new(
        r.Id, r.VoucherNumber, r.SupplierName, r.SupplierRef, r.ReceiptDate,
        r.NetAmount, r.VatAmount, r.TotalAmount,
        r.PayMethod.ToString(), r.CashAccountCode, r.DebitAccountCode, r.Status.ToString(),
        r.Reference, r.SourceEventId, r.PayableEntryId, r.DueDate,
        r.Lines.Select(l => new ReceiptLineDto(l.ItemName, l.Quantity, l.UnitPrice, l.LineTotal)).ToList());

    private Guid GetTenantIdFromContext()
    {
        string? tenantIdStr = User.FindFirst("TenantId")?.Value;
        if (Guid.TryParse(tenantIdStr, out Guid tid))
            return tid;
        return Guid.Empty;
    }
}
