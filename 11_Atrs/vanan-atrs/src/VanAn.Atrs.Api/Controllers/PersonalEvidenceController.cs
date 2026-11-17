using Microsoft.AspNetCore.Mvc;
using VanAn.Atrs.Api.Auth;
using VanAn.Atrs.Contracts;
using VanAn.Atrs.Infra;

namespace VanAn.Atrs.Api.Controllers;

/// <summary>
/// ATRS-6 subset (Wave 4) — Personal evidence (U02, FR-006) + consent (SRS §11.2).
/// Customer endpoints, X-Customer-Token. Evidence values luôn USER_DECLARED/UNVERIFIED;
/// response mask giá trị nhạy cảm.
/// </summary>
[ApiController]
[Route("api/v1/account-trust")]
public class PersonalEvidenceController(
    IAtrsCustomerContextService customerContext,
    IPersonalEvidenceService evidenceService,
    ILogger<PersonalEvidenceController> logger) : ControllerBase
{
    [HttpGet("personal-evidence")]
    public async Task<IActionResult> GetEvidence(CancellationToken ct)
    {
        var (auth, error) = await AuthenticateAsync(ct);
        if (error is not null) return error;
        return Ok(await evidenceService.GetMineAsync(auth!.TrustSubjectId!.Value, ct));
    }

    [HttpPut("personal-evidence")]
    public async Task<IActionResult> UpsertEvidence([FromBody] UpsertPersonalEvidenceRequest request, CancellationToken ct)
    {
        var (auth, error) = await AuthenticateAsync(ct);
        if (error is not null) return error;
        var dto = await evidenceService.UpsertAsync(auth!.TrustSubjectId!.Value, request, ct);
        return dto is null
            ? BadRequest(new { error = "FieldKey không hợp lệ hoặc thiếu privacyNoticeVersion." })
            : Ok(dto);
    }

    [HttpGet("consents")]
    public async Task<IActionResult> ListConsents(CancellationToken ct)
    {
        var (auth, error) = await AuthenticateAsync(ct);
        if (error is not null) return error;
        return Ok(await evidenceService.ListConsentsAsync(auth!.TrustSubjectId!.Value, ct));
    }

    [HttpPost("consents")]
    public async Task<IActionResult> GrantConsent([FromBody] GrantConsentRequest request, CancellationToken ct)
    {
        var (auth, error) = await AuthenticateAsync(ct);
        if (error is not null) return error;
        try
        {
            return Ok(await evidenceService.GrantConsentAsync(auth!.TrustSubjectId!.Value, request, ct));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("consents/{id:guid}/withdraw")]
    public async Task<IActionResult> WithdrawConsent(Guid id, CancellationToken ct)
    {
        var (auth, error) = await AuthenticateAsync(ct);
        if (error is not null) return error;
        var ok = await evidenceService.WithdrawConsentAsync(auth!.TrustSubjectId!.Value, id, ct);
        return ok ? Ok(new { success = true }) : NotFound();
    }

    private async Task<(AtrsCustomerAuthResult? auth, IActionResult? error)> AuthenticateAsync(CancellationToken ct)
    {
        var auth = await customerContext.AuthenticateAsync(Request, ct);
        if (auth.Status == AtrsCustomerTokenStatus.DownstreamUnavailable)
            return (null, StatusCode(503, new { error = "Dịch vụ đang bảo trì, vui lòng thử lại sau." }));
        if (auth.TrustSubjectId is null)
            return (null, Unauthorized(new { error = "Token không hợp lệ hoặc đã hết hạn." }));
        return (auth, null);
    }
}
