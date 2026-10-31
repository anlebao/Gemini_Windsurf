using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VanAn.CoreHub.Infrastructure.Entities;
using VanAn.CoreHub.Services;
using VanAn.Shared.Domain;

namespace VanAn.ShopERP.Controllers;

/// <summary>
/// SRS v1.2 W3/D (M9): quản lý TK tiền per tenant — quỹ 111 + ngân hàng 112x.
/// Tenant admin (kế toán) thêm/sửa/vô hiệu TK ngân hàng phục vụ sổ TGNH + phiếu thu/chi.
/// </summary>
[ApiController]
[Route("api/shop/accounting/bank-accounts")]
[Authorize]
public class BankAccountsController(IBankAccountService bankAccountService) : ControllerBase
{
    public sealed record BankAccountDto(Guid Id, string AccountCode, string? BankName,
        string? AccountNumber, string DisplayName, bool IsActive, DateTime? EffectiveFrom);

    public sealed record CreateBankAccountRequest(string AccountCode, string DisplayName,
        string? BankName, string? AccountNumber);

    public sealed record UpdateBankAccountRequest(string DisplayName,
        string? BankName, string? AccountNumber);

    /// <summary>Danh sách TK tiền của tenant (lazy-seed quỹ 111 nếu chưa có).</summary>
    [HttpGet]
    [Authorize(Policy = "AccountingAccess")]
    public async Task<ActionResult<IReadOnlyList<BankAccountDto>>> List(
        [FromQuery] Guid? tenantId, [FromQuery] bool includeInactive = false, CancellationToken ct = default)
    {
        Guid tid = tenantId ?? GetTenantIdFromContext();
        if (tid == Guid.Empty)
            return BadRequest("TenantId is required.");

        var accounts = await bankAccountService.ListAsync(new TenantId(tid), includeInactive, ct);
        return Ok(accounts.Select(ToDto).ToList());
    }

    /// <summary>Tạo TK ngân hàng/quỹ phụ — code bắt đầu "11", unique per tenant.</summary>
    [HttpPost]
    [Authorize(Policy = "AccountingFullAccess")]
    public async Task<ActionResult<BankAccountDto>> Create(
        [FromBody] CreateBankAccountRequest request, [FromQuery] Guid? tenantId, CancellationToken ct = default)
    {
        Guid tid = tenantId ?? GetTenantIdFromContext();
        if (tid == Guid.Empty)
            return BadRequest("TenantId is required.");

        try
        {
            var account = await bankAccountService.CreateAsync(new TenantId(tid),
                request.AccountCode, request.DisplayName, request.BankName, request.AccountNumber, ct);
            return Ok(ToDto(account));
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

    /// <summary>Sửa tên/thông tin ngân hàng (không đổi AccountCode — giữ lịch sử trỏ về).</summary>
    [HttpPut("{id:guid}")]
    [Authorize(Policy = "AccountingFullAccess")]
    public async Task<ActionResult<BankAccountDto>> Update(Guid id,
        [FromBody] UpdateBankAccountRequest request, CancellationToken ct = default)
    {
        try
        {
            var account = await bankAccountService.UpdateAsync(id,
                request.DisplayName, request.BankName, request.AccountNumber, ct);
            return Ok(ToDto(account));
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    /// <summary>Vô hiệu TK (deactivate — không xóa, giữ lịch sử). Không áp dụng cho quỹ 111.</summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Policy = "AccountingFullAccess")]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken ct = default)
    {
        try
        {
            await bankAccountService.DeactivateAsync(id, ct);
            return NoContent();
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    private static BankAccountDto ToDto(BankAccountEntity b) => new(
        b.Id, b.AccountCode, b.BankName, b.AccountNumber, b.DisplayName, b.IsActive, b.EffectiveFrom);

    private Guid GetTenantIdFromContext()
    {
        string? tenantIdStr = User.FindFirst("TenantId")?.Value;
        if (Guid.TryParse(tenantIdStr, out Guid tid))
            return tid;
        return Guid.Empty;
    }
}
