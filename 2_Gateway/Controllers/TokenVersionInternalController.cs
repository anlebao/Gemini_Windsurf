using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VanAn.CoreHub.Infrastructure;
using VanAn.Gateway.Filters;
using VanAn.Gateway.Services;

namespace VanAn.Gateway.Controllers;

/// <summary>
/// ATRS-3 (D3): internal endpoints cho token version — ATRS/ShopERP gọi sau khi
/// recovery COMPLETED / security event để revoke mọi token cũ của user/customer.
/// Auth: [InternalApiKey] (X-Internal-Api-Key) — service-to-service, không phải user JWT.
/// </summary>
[ApiController]
[Route("api/internal/auth/token-version")]
[AllowAnonymous]
[InternalApiKey]
public class TokenVersionInternalController(
    IVanAnDbContext db,
    ITokenVersionService tokenVersionService,
    ILogger<TokenVersionInternalController> logger) : ControllerBase
{
    /// <summary>
    /// GET /api/internal/auth/token-version/{userId} — version hiện tại cho login side
    /// (ShopERP đọc để đưa claim tv vào JWT). 404 khi không phải DemoUser (version 0).
    /// </summary>
    [HttpGet("{userId:guid}")]
    public async Task<IActionResult> GetVersion(Guid userId, CancellationToken ct)
    {
        var version = await tokenVersionService.GetVersionAsync(userId, ct);
        return version.HasValue
            ? Ok(new { userId, version = version.Value })
            : NotFound(new { userId, version = 0 });
    }

    /// <summary>
    /// POST /api/internal/auth/token-version/bump — bump version (revoke mọi token cũ).
    /// Body: { userId, identityType: "DemoUser" | "Customer" }.
    /// </summary>
    [HttpPost("bump")]
    public async Task<IActionResult> Bump([FromBody] BumpTokenVersionRequest request, CancellationToken ct)
    {
        if (request.UserId == Guid.Empty)
            return BadRequest(new { error = "UserId is required." });

        if (request.IdentityType == "Customer")
        {
            var customer = await db.Customers.IgnoreQueryFilters()
                .FirstOrDefaultAsync(c => c.Id == request.UserId, ct);
            if (customer is null)
                return NotFound(new { error = $"Customer {request.UserId} not found." });
            customer.BumpTokenVersion();
        }
        else
        {
            var user = await db.Users.IgnoreQueryFilters()
                .FirstOrDefaultAsync(u => u.Id == request.UserId, ct);
            if (user is null)
                return NotFound(new { error = $"User {request.UserId} not found." });
            user.BumpTokenVersion();
        }

        await db.SaveChangesAsync(ct);
        await tokenVersionService.InvalidateAsync(request.UserId);
        logger.LogInformation("Token version bumped for {IdentityType} {UserId}", request.IdentityType, request.UserId);
        return Ok(new { message = "Token version bumped — mọi token cũ bị từ chối." });
    }

    public sealed record BumpTokenVersionRequest(Guid UserId, string IdentityType);
}