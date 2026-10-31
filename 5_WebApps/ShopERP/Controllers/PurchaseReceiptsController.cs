using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VanAn.CoreHub.Infrastructure.Entities;
using VanAn.CoreHub.Services.Purchase;
using VanAn.Shared.Domain;

namespace VanAn.ShopERP.Controllers;

/// <summary>
/// SRS v1.2 W6 tiếp (GĐ1 — user chốt 2026-10-31): chứng từ nhập mua với luồng
/// 1 cấp duyệt Draft → Approved → Posted → Returned (+ Cancelled).
/// JE/công nợ/kho ghi khi Post — phiếu Draft/Approved sửa/hủy được, Posted khóa sửa.
///
/// Phân quyền: đọc + lập/sửa draft = AccountingAccess (kế toán công nợ được lập);
/// duyệt/ghi sổ/hủy/trả hàng/ghi kho = AccountingFullAccess (Owner/KTT/Admin).
/// </summary>
[ApiController]
[Route("api/shop/accounting/purchase-receipts")]
[Authorize]
public class PurchaseReceiptsController(IPurchaseReceiptService purchaseService) : ControllerBase
{
    public sealed record LineInput(string ItemName, decimal Quantity, decimal UnitPrice, Guid? IngredientId);

    public sealed record CreateReceiptRequest(string SupplierName, string? SupplierRef,
        DateTime? ReceiptDate, string PayMethod, string? CashAccountCode, string DebitAccountCode,
        decimal VatAmount, string? Reference, string? SourceEventId, DateTime? DueDate,
        IReadOnlyList<LineInput> Lines);

    public sealed record UpdateReceiptRequest(string SupplierName, string? SupplierRef,
        DateTime? ReceiptDate, string PayMethod, string? CashAccountCode, string DebitAccountCode,
        decimal VatAmount, string? Reference, DateTime? DueDate,
        IReadOnlyList<LineInput> Lines);

    public sealed record ReasonRequest(string Reason);

    public sealed record PreviewRequest(string PayMethod, string? CashAccountCode, string DebitAccountCode,
        decimal NetAmount, decimal VatAmount);

    public sealed record ReceiptLineDto(string ItemName, decimal Quantity, decimal UnitPrice,
        decimal LineTotal, Guid? IngredientId);

    public sealed record ReceiptDto(Guid Id, string VoucherNumber, string SupplierName, string? SupplierRef,
        DateTime ReceiptDate, decimal NetAmount, decimal VatAmount, decimal TotalAmount,
        string PayMethod, string? CashAccountCode, string DebitAccountCode, string Status,
        string? Reference, string? SourceEventId, Guid? PayableEntryId, DateTime? DueDate,
        DateTime? ApprovedAt, string? ApprovedBy, DateTime? PostedAt, DateTime? StockAppliedAt,
        bool HasStockLines,
        IReadOnlyList<ReceiptLineDto> Lines);

    /// <summary>Danh sách phiếu nhập mua của tenant (mới nhất trước).
    /// Filters: from/to (ReceiptDate), status (Draft/Approved/Posted/Returned/Cancelled), q (NCC hoặc số phiếu).</summary>
    [HttpGet]
    [Authorize(Policy = "AccountingAccess")]
    public async Task<ActionResult<IReadOnlyList<ReceiptDto>>> List(
        [FromQuery] Guid? tenantId, [FromQuery] DateTime? from, [FromQuery] DateTime? to,
        [FromQuery] string? status, [FromQuery] string? q, CancellationToken ct)
    {
        Guid tid = tenantId ?? GetTenantIdFromContext();
        if (tid == Guid.Empty)
            return BadRequest("TenantId is required.");

        PurchaseReceiptStatus? statusFilter = null;
        if (!string.IsNullOrWhiteSpace(status))
        {
            if (!Enum.TryParse<PurchaseReceiptStatus>(status, ignoreCase: true, out PurchaseReceiptStatus parsed))
                return BadRequest("Status không hợp lệ (Draft/Approved/Posted/Returned/Cancelled).");
            statusFilter = parsed;
        }

        var receipts = await purchaseService.ListAsync(new TenantId(tid), from, to, statusFilter, q, ct);
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

    /// <summary>JE đã ghi của phiếu (gốc + đảo — hiển thị bút toán trên chi tiết).</summary>
    [HttpGet("{id:guid}/journals")]
    [Authorize(Policy = "AccountingAccess")]
    public async Task<IActionResult> Journals(Guid id, CancellationToken ct)
    {
        try
        {
            var journals = await purchaseService.ListJournalsAsync(id, ct);
            return Ok(journals.Select(j => new
            {
                j.Id,
                j.EntryDate,
                j.Description,
                j.IsReversal,
                PostingPurpose = j.PostingPurpose,
                Lines = j.Lines.Select(l => new
                {
                    l.AccountNumber, l.DebitAmount, l.CreditAmount, l.Description
                }).ToList()
            }).ToList());
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    /// <summary>Preview bút toán — resolve account-map THẬT, không lưu (xem trước khi ghi).</summary>
    [HttpPost("preview")]
    [Authorize(Policy = "AccountingAccess")]
    public async Task<IActionResult> Preview([FromBody] PreviewRequest request, CancellationToken ct)
    {
        Guid tid = GetTenantIdFromContext();
        if (tid == Guid.Empty)
            return BadRequest("TenantId is required.");
        if (!Enum.TryParse<PurchasePayMethod>(request.PayMethod, ignoreCase: true, out PurchasePayMethod payMethod))
            return BadRequest("PayMethod phải là 'credit' hoặc 'immediate'.");

        try
        {
            var preview = await purchaseService.PreviewJournalAsync(new TenantId(tid), payMethod,
                request.CashAccountCode, request.DebitAccountCode, request.NetAmount, request.VatAmount, ct);
            return Ok(preview);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    /// <summary>Options TK ghi Nợ (dropdown) + TK tiền (trả ngay).</summary>
    [HttpGet("account-options")]
    [Authorize(Policy = "AccountingAccess")]
    public async Task<IActionResult> AccountOptions([FromQuery] Guid? tenantId, CancellationToken ct)
    {
        Guid tid = tenantId ?? GetTenantIdFromContext();
        if (tid == Guid.Empty)
            return BadRequest("TenantId is required.");
        var tenant = new TenantId(tid);
        return Ok(new
        {
            debitAccounts = await purchaseService.GetDebitAccountOptionsAsync(tenant, ct),
            cashAccounts = await purchaseService.GetCashAccountOptionsAsync(tenant, ct)
        });
    }

    /// <summary>Lập phiếu nháp (Draft — chưa JE/công nợ/kho). PayMethod: "credit"|"immediate".</summary>
    [HttpPost]
    [Authorize(Policy = "AccountingAccess")]
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
                request.Lines.Select(l => new PurchaseReceiptLineInput(l.ItemName, l.Quantity, l.UnitPrice, l.IngredientId)).ToList(),
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

    /// <summary>Sửa phiếu Draft — header + thay toàn bộ dòng.</summary>
    [HttpPut("{id:guid}")]
    [Authorize(Policy = "AccountingAccess")]
    public async Task<ActionResult<ReceiptDto>> Update(Guid id,
        [FromBody] UpdateReceiptRequest request, CancellationToken ct)
    {
        if (!Enum.TryParse<PurchasePayMethod>(request.PayMethod, ignoreCase: true, out PurchasePayMethod payMethod))
            return BadRequest("PayMethod phải là 'credit' hoặc 'immediate'.");

        try
        {
            var receipt = await purchaseService.UpdateAsync(id,
                request.SupplierName, request.SupplierRef,
                request.ReceiptDate ?? DateTime.UtcNow,
                request.Lines.Select(l => new PurchaseReceiptLineInput(l.ItemName, l.Quantity, l.UnitPrice, l.IngredientId)).ToList(),
                payMethod, request.CashAccountCode, request.DebitAccountCode,
                request.VatAmount, request.Reference, request.DueDate, ct);
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

    /// <summary>Duyệt phiếu (1 cấp) — Draft → Approved.</summary>
    [HttpPost("{id:guid}/approve")]
    [Authorize(Policy = "AccountingFullAccess")]
    public async Task<ActionResult<ReceiptDto>> Approve(Guid id, CancellationToken ct)
    {
        try
        {
            string? approver = User.Identity?.Name;
            var receipt = await purchaseService.ApproveAsync(id, approver, ct);
            return Ok(ToDto(receipt));
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(ex.Message);
        }
    }

    /// <summary>Ghi sổ — Approved → Posted (JE + công nợ + kho).</summary>
    [HttpPost("{id:guid}/post")]
    [Authorize(Policy = "AccountingFullAccess")]
    public async Task<ActionResult<ReceiptDto>> Post(Guid id, CancellationToken ct)
    {
        try
        {
            var receipt = await purchaseService.PostAsync(id, ct);
            return Ok(ToDto(receipt));
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(ex.Message);
        }
    }

    /// <summary>Hủy phiếu — chỉ Draft|Approved.</summary>
    [HttpPost("{id:guid}/cancel")]
    [Authorize(Policy = "AccountingFullAccess")]
    public async Task<ActionResult<ReceiptDto>> Cancel(Guid id,
        [FromBody] ReasonRequest request, CancellationToken ct)
    {
        try
        {
            var receipt = await purchaseService.CancelAsync(id, request.Reason, ct);
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

    /// <summary>Trả hàng/đảo phiếu — JE đảo (purchase-return) + đảo công nợ + xuất kho.</summary>
    [HttpPost("{id:guid}/return")]
    [Authorize(Policy = "AccountingFullAccess")]
    public async Task<ActionResult<ReceiptDto>> Return(Guid id,
        [FromBody] ReasonRequest request, CancellationToken ct)
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

    /// <summary>Retry ghi kho cho phiếu Posted chưa StockApplied (sau lỗi cross-DB).</summary>
    [HttpPost("{id:guid}/apply-stock")]
    [Authorize(Policy = "AccountingFullAccess")]
    public async Task<ActionResult<ReceiptDto>> ApplyStock(Guid id, CancellationToken ct)
    {
        try
        {
            var receipt = await purchaseService.ApplyStockAsync(id, ct);
            return Ok(ToDto(receipt));
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
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
        r.ApprovedAt, r.ApprovedBy, r.PostedAt, r.StockAppliedAt,
        r.Lines.Any(l => l.IngredientId.HasValue),
        r.Lines.Select(l => new ReceiptLineDto(l.ItemName, l.Quantity, l.UnitPrice, l.LineTotal, l.IngredientId)).ToList());

    private Guid GetTenantIdFromContext()
    {
        string? tenantIdStr = User.FindFirst("TenantId")?.Value;
        if (Guid.TryParse(tenantIdStr, out Guid tid))
            return tid;
        return Guid.Empty;
    }
}
