using Microsoft.AspNetCore.Mvc;
using VanAn.Atrs.Api.Auth;
using VanAn.Atrs.Infra;

namespace VanAn.Atrs.Api.Controllers;

/// <summary>
/// ATRS-13 — in-app notifications cho user (X-Customer-Token).
/// Ownership enforce ở service (không lộ notification người khác — anti-enumeration 404).
/// KHÔNG chứa secret/code/evidence (body do writer kiểm soát).
/// </summary>
[ApiController]
[Route("api/v1/account-trust/notifications")]
public class NotificationsController(
    IAtrsCustomerContextService customerContext,
    IAtrsNotificationQueryService notificationService,
    ILogger<NotificationsController> logger) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] int skip = 0, [FromQuery] int take = 50, CancellationToken ct = default)
    {
        var auth = await customerContext.AuthenticateAsync(Request, ct);
        if (auth.Status == AtrsCustomerTokenStatus.DownstreamUnavailable)
            return StatusCode(503, new { error = "Dịch vụ đang bảo trì, vui lòng thử lại sau." });
        if (auth.TrustSubjectId is null)
            return Unauthorized(new { error = "Token không hợp lệ hoặc đã hết hạn." });
        return Ok(await notificationService.ListAsync(auth.TrustSubjectId.Value, skip, take, ct));
    }

    [HttpPost("{id:guid}/read")]
    public async Task<IActionResult> MarkRead(Guid id, CancellationToken ct)
    {
        var auth = await customerContext.AuthenticateAsync(Request, ct);
        if (auth.Status == AtrsCustomerTokenStatus.DownstreamUnavailable)
            return StatusCode(503, new { error = "Dịch vụ đang bảo trì, vui lòng thử lại sau." });
        if (auth.TrustSubjectId is null)
            return Unauthorized(new { error = "Token không hợp lệ hoặc đã hết hạn." });
        var ok = await notificationService.MarkReadAsync(id, auth.TrustSubjectId.Value, ct);
        return ok ? Ok(new { success = true }) : NotFound();
    }
}