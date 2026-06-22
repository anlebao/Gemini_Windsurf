using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Antiforgery;
using System.Security.Claims;
using VanAn.Shared.Domain;
using VanAn.CoreHub.Interfaces;
using VanAn.CoreHub.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace VanAn.ShopERP.Pages
{
    [ValidateAntiForgeryToken]
    public class LoginModel(
        IAntiforgery antiforgery,
        IVanAnDbContext dbContext) : PageModel
    {
        private readonly IAntiforgery _antiforgery = antiforgery;
        private readonly IVanAnDbContext _dbContext = dbContext;

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
            UserRole role;
            Guid? userId = null;
            bool isValid = true;

            switch (Username.ToUpperInvariant())
            {
                case "ADMIN@VANAN.VN" when Password == "VanAn@2026":
                    role = UserRole.Owner;
                    userId = Guid.Parse("11111111-1111-1111-1111-111111111111");
                    break;
                case "KHO@VANAN.VN" when Password == "VanAn@2026":
                    role = UserRole.StoreKeeper;
                    userId = Guid.Parse("22222222-2222-2222-2222-222222222222");
                    break;
                case "BAOVE@VANAN.VN" when Password == "VanAn@2026":
                    role = UserRole.Guard;
                    userId = Guid.Parse("33333333-3333-3333-3333-333333333333");
                    break;
                case "OWNER" when Password == "owner123":
                    role = UserRole.Owner;
                    userId = Guid.Parse("44444444-4444-4444-4444-444444444444");
                    break;
                case "KEEPER" when Password == "keeper123":
                    role = UserRole.StoreKeeper;
                    userId = Guid.Parse("55555555-5555-5555-5555-555555555555");
                    break;
                case "GUARD" when Password == "guard123":
                    role = UserRole.Guard;
                    userId = Guid.Parse("66666666-6666-6666-6666-666666666666");
                    break;
                case "STAFF" when Password == "staff123":
                    role = UserRole.Staff;
                    userId = Guid.Parse("77777777-7777-7777-7777-777777777777");
                    break;
                default:
                    role = UserRole.Staff;
                    isValid = false;
                    break;
            }

            if (!isValid || userId == null)
            {
                ModelState.AddModelError(string.Empty, "Email hoặc password không đúng");
                return Page();
            }

            // Wave 1 Phase 2: Lookup tenant from UserTenant table
            var userTenant = await _dbContext.UserTenants
                .AsNoTracking()
                .FirstOrDefaultAsync(ut => ut.UserId == userId.Value && ut.IsActive);

            // Fallback to default tenant if no mapping exists (E2E testing)
            Guid tenantId = userTenant?.TenantId ?? Guid.Parse("00000000-0000-0000-0000-000000000001");
            string tenantRole = userTenant?.Role ?? role.ToString();

            // Tạo Claims cho authentication
            // Wave 1 Phase 2: Standardized claim name "tenant_id" (snake_case, OIDC standard)
            List<Claim> claims =
            [
                new Claim(ClaimTypes.Name, Username),
                new Claim(ClaimTypes.Role, role.ToString()),
                new Claim("DisplayName", GetDisplayName(role)),
                new Claim("tenant_id", tenantId.ToString())
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
                UserRole.Owner => RedirectToPage("/Index"),
                UserRole.StoreKeeper => RedirectToPage("/Index"),
                UserRole.Staff => RedirectToPage("/Index"),
                UserRole.Masterchef => RedirectToPage("/Index"),
                UserRole.None => throw new NotImplementedException(),
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
