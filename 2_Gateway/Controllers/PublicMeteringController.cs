using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using VanAn.Gateway.Services;
using VanAn.Shared.Domain.Common;

namespace VanAn.Gateway.Controllers;

/// <summary>
/// Public (anonymous) usage ingest for client-rendered apps — currently only `store.visit`
/// fired by KhachLink Store.razor via sendBeacon (task_p4 hook a).
/// Server-side whitelist because anonymous input is untrusted: tenantId must be a valid Guid,
/// serviceCode must be whitelisted, quantity must be exactly 1. Suspicious/bot traffic still
/// gets HTTP 200 (never reveals validation outcome) but is NOT forwarded to metering.
/// Rate-limited by the dedicated "public-metering" policy (60 req/min/IP).
/// </summary>
[ApiController]
[Route("api/public/metering")]
[EnableRateLimiting("public-metering")]
public sealed class PublicMeteringController(IMeteringClient metering, ILogger<PublicMeteringController> logger) : ControllerBase
{
    private static readonly HashSet<string> AllowedServiceCodes = new(StringComparer.Ordinal) { "store.visit" };

    private static readonly string[] BotUserAgentTokens = ["bot", "crawler", "spider", "curl", "wget", "python"];

    public sealed record PublicMeteringEventRequest(
        string? TenantId,
        string? ServiceCode,
        decimal Quantity,
        string? Unit,
        DateTime OccurredAtUtc,
        string? IdempotencyKey);

    [AllowAnonymous]
    [HttpPost("event")]
    public IActionResult RecordEvent([FromBody] PublicMeteringEventRequest? req)
    {
        // Hard validation → 400 (malformed request, not a bot probe concern).
        if (req is null || !Guid.TryParse(req.TenantId, out var tenantId) || tenantId == Guid.Empty)
            return BadRequest(new { error = "invalid_tenantId" });
        if (string.IsNullOrWhiteSpace(req.ServiceCode) || !AllowedServiceCodes.Contains(req.ServiceCode))
            return BadRequest(new { error = "invalid_serviceCode" });
        if (req.Quantity != 1)
            return BadRequest(new { error = "invalid_quantity" });

        // Soft filter — return success shape without forwarding (bots must not learn filters).
        var ua = Request.Headers.UserAgent.ToString();
        var isBot = string.IsNullOrEmpty(ua)
            || BotUserAgentTokens.Any(t => ua.Contains(t, StringComparison.OrdinalIgnoreCase));
        if (isBot) return Ok(new { accepted = false });

        var hour = DateTime.UtcNow.ToString("yyyyMMddHH");
        var key = string.IsNullOrWhiteSpace(req.IdempotencyKey)
            ? $"visit:{tenantId}:anon:{hour}"
            : req.IdempotencyKey!;

        _ = metering.RecordUsageAsync(new MeteringUsageEvent(
            tenantId,
            req.ServiceCode,
            1,
            string.IsNullOrWhiteSpace(req.Unit) ? "visit" : req.Unit!,
            req.OccurredAtUtc == default ? DateTime.UtcNow : req.OccurredAtUtc,
            key,
            MetadataJson: "{\"source\":\"public-gateway\"}"));

        return Ok(new { accepted = true });
    }
}
