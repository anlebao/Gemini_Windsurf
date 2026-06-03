using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Antiforgery;
using System.Security.Claims;
using VanAn.Shared.Domain;

namespace VanAn.ShopERP.Pages
{
    [ValidateAntiForgeryToken]
    public class LoginModel(IAntiforgery antiforgery) : PageModel
    {
        private readonly IAntiforgery _antiforgery = antiforgery;

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
            (UserRole role, bool isValid) = Username.ToUpperInvariant() switch
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
            List<Claim> claims =
            [
                new Claim(ClaimTypes.Name, Username),
                new Claim(ClaimTypes.Role, role.ToString()),
                new Claim("DisplayName", GetDisplayName(role))
            ];

            ClaimsIdentity claimsIdentity = new(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            AuthenticationProperties authProperties = new()
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
                UserRole.None => throw new NotImplementedException(),
                UserRole.Owner => throw new NotImplementedException(),
                UserRole.StoreKeeper => throw new NotImplementedException(),
                UserRole.Staff => throw new NotImplementedException(),
                UserRole.Masterchef => throw new NotImplementedException(),
                _ => RedirectToPage("/Index")
            };
        }

        private static string GetDisplayName(UserRole role)
        {
            return role switch
            {
                UserRole.Owner => "Chủ quán",
                UserRole.StoreKeeper => "Thủ kho",
                UserRole.Guard => "Bảo vệ",
                UserRole.Staff => "Phục vụ",
                UserRole.None => throw new NotImplementedException(),
                UserRole.Masterchef => throw new NotImplementedException(),
                _ => "Unknown"
            };
        }
    }
}
