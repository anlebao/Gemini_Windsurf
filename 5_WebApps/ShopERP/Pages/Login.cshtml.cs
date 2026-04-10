using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Antiforgery;
using System.Globalization;
using System.Security.Claims;
using VanAn.Shared.Domain;

namespace VanAn.ShopERP.Pages;

[ValidateAntiForgeryToken]
public class LoginModel : PageModel
{
    private readonly IAntiforgery _antiforgery;

    public LoginModel(IAntiforgery antiforgery)
    {
        _antiforgery = antiforgery;
    }

    [BindProperty]
    public string Username { get; set; } = string.Empty;
    
    [BindProperty]
    public string Password { get; set; } = string.Empty;

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        // DEMO AUTHENTICATION - Multi-Role ShopERP Accounts
        var (role, isValid) = Username.ToUpperInvariant() switch
        {
            "ADMIN@VANAN.VN" when Password == "VanAn@2026" => (UserRole.Owner, true),
            "KHO@VANAN.VN" when Password == "VanAn@2026" => (UserRole.StoreKeeper, true),
            "BAOVE@VANAN.VN" when Password == "VanAn@2026" => (UserRole.Guard, true),
            "OWNER" when Password == "owner123" => (UserRole.Owner, true), // Legacy
            "KEEPER" when Password == "keeper123" => (UserRole.StoreKeeper, true), // Legacy
            "GUARD" when Password == "guard123" => (UserRole.Guard, true), // Legacy
            "STAFF" when Password == "staff123" => (UserRole.Staff, true), // Legacy
            _ => (UserRole.Staff, false)
        };

        if (!isValid)
        {
            ModelState.AddModelError(string.Empty, "Email hoặc password không đúng");
            return Page();
        }

        // Tạo Claims cho authentication
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.Name, Username),
            new Claim(ClaimTypes.Role, role.ToString()),
            new Claim("DisplayName", GetDisplayName(role))
        };

        var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var authProperties = new AuthenticationProperties
        {
            IsPersistent = true,
            ExpiresUtc = DateTimeOffset.UtcNow.AddHours(8)
        };

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(claimsIdentity),
            authProperties);

        // Redirect based on role
        return role switch
        {
            UserRole.Guard => RedirectToPage("/Guard/Scan"),
            _ => RedirectToPage("/Index")
        };
    }

    private static string GetDisplayName(UserRole role) => role switch
    {
        UserRole.Owner => "Chủ quán",
        UserRole.StoreKeeper => "Thủ kho",
        UserRole.Guard => "Bảo vệ",
        UserRole.Staff => "Phục vụ",
        _ => "Unknown"
    };
}
