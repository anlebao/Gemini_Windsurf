using Microsoft.AspNetCore.Mvc;
using VanAn.Atrs.Api.Auth;
using VanAn.Atrs.Contracts;
using VanAn.Atrs.Domain;
using VanAn.Atrs.Infra;

namespace VanAn.Atrs.Api.Controllers;

/// <summary>
/// Disputes (FR-012, U11-U13 backend) — customer endpoints, X-Customer-Token auth.
/// User chỉ thấy data USER_VISIBLE; anti-enumeration: case người khác → 404.
/// </summary>
[ApiController]
[Route("api/v1/account-trust/disputes")]
public class DisputesController(
    IAtrsCustomerContextService customerContext,
    IDisputeCaseManager disputeManager,
    ILogger<DisputesController> logger) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Submit([FromBody] SubmitDisputeRequest request, CancellationToken ct)
    {
        var auth = await AuthenticateAsync(ct);
        if (auth.Error is not null) return auth.Error;

        try
        {
            var result = await disputeManager.SubmitAsync(auth.TrustSubjectId!.Value, request, ct);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var auth = await AuthenticateAsync(ct);
        if (auth.Error is not null) return auth.Error;
        var items = await disputeManager.ListMineAsync(auth.TrustSubjectId!.Value, ct);
        return Ok(items);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var auth = await AuthenticateAsync(ct);
        if (auth.Error is not null) return auth.Error;
        var dto = await disputeManager.GetMineAsync(id, auth.TrustSubjectId!.Value, ct);
        return dto is null ? NotFound() : Ok(dto);
    }

    [HttpPost("{id:guid}/evidence")]
    public async Task<IActionResult> AddEvidence(Guid id, [FromBody] AddDisputeEvidenceRequest request, CancellationToken ct)
    {
        var auth = await AuthenticateAsync(ct);
        if (auth.Error is not null) return auth.Error;
        try
        {
            var dto = await disputeManager.AddEvidenceAsync(id, auth.TrustSubjectId!.Value, request, ct);
            return dto is null ? NotFound() : Ok(dto);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("{id:guid}/withdraw")]
    public async Task<IActionResult> Withdraw(Guid id, CancellationToken ct)
    {
        var auth = await AuthenticateAsync(ct);
        if (auth.Error is not null) return auth.Error;
        var ok = await disputeManager.WithdrawAsync(id, auth.TrustSubjectId!.Value, ct);
        return ok ? Ok(new { success = true }) : NotFound();
    }

    private async Task<(IActionResult? Error, Guid? TrustSubjectId)> AuthenticateAsync(CancellationToken ct)
    {
        var auth = await customerContext.AuthenticateAsync(Request, ct);
        if (auth.Status == AtrsCustomerTokenStatus.DownstreamUnavailable)
            return (StatusCode(503, new { error = "Dịch vụ đang bảo trì, vui lòng thử lại sau." }), null);
        if (auth.TrustSubjectId is null)
            return (Unauthorized(new { error = "Token không hợp lệ hoặc đã hết hạn." }), null);
        return (null, auth.TrustSubjectId);
    }
}

/// <summary>
/// Transaction history (FR-013) — customer đọc ledger entries của mình tại 1 tenant.
/// </summary>
[ApiController]
[Route("api/v1/account-trust/transactions")]
public class TransactionsController(
    IAtrsCustomerContextService customerContext,
    IDisputeCaseManager disputeManager) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] Guid tenantId, CancellationToken ct)
    {
        var auth = await customerContext.AuthenticateAsync(Request, ct);
        if (auth.Status == AtrsCustomerTokenStatus.DownstreamUnavailable)
            return StatusCode(503, new { error = "Dịch vụ đang bảo trì, vui lòng thử lại sau." });
        if (auth.TrustSubjectId is null)
            return Unauthorized(new { error = "Token không hợp lệ hoặc đã hết hạn." });
        if (tenantId == Guid.Empty)
            return BadRequest(new { error = "tenantId là bắt buộc." });

        var entries = await disputeManager.GetTransactionsAsync(auth.TrustSubjectId.Value, tenantId, ct);
        return Ok(entries);
    }
}
