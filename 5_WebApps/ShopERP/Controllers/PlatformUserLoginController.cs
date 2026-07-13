using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using VanAn.CoreHub.Services;
using VanAn.Shared.Domain.Common;

namespace VanAn.ShopERP.Controllers;

[ApiController]
[Route("api/platform")]
public class PlatformUserLoginController : ControllerBase
{
    private readonly IPlatformUserLoginService _platformUserLoginService;

    public PlatformUserLoginController(IPlatformUserLoginService platformUserLoginService)
    {
        _platformUserLoginService = platformUserLoginService;
    }

    // F1: [AllowAnonymous] required — login endpoint must be reachable without auth,
    // otherwise [Authorize] on class creates a deadlock (need auth to login).
    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var result = await _platformUserLoginService.LoginAsync(request.Username, request.Password);

        if (result == null)
        {
            return Unauthorized(new { success = false, message = "Invalid credentials" });
        }

        // Issue Cookie auth for Blazor Server (same pattern as DevLoginController L66-69)
        var claims = new List<Claim>
        {
            new(ClaimTypes.Name, "System Admin"),
            new(ClaimTypes.NameIdentifier, result.UserId.ToString()),
            new(ClaimTypes.Email, result.Email),
            new(ClaimTypes.Role, result.Role),
            new("sub", result.Email),
            new("role", result.Role),
        };

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);

        var authProperties = new AuthenticationProperties
        {
            IsPersistent = true,
            ExpiresUtc = DateTimeOffset.UtcNow.AddHours(8),
            AllowRefresh = true,
        };

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            principal,
            authProperties);

        return Ok(new
        {
            success = true,
            email = result.Email,
            role = result.Role,
            token = result.Token,
            message = "Platform login successful"
        });
    }

    public record LoginRequest(string Username, string Password);
}
