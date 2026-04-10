using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System;
using System.Security.Claims;

namespace VanAn.ShopERP.Pages.Kitchen
{
    [Authorize(Roles = "Masterchef,Staff,Manager")]
    public class IndexModel : PageModel
    {
        public Guid ShopId { get; set; }

        public void OnGet()
        {
            // Get shop ID from user claims or session
            var shopIdClaim = User.FindFirst("ShopId")?.Value;
            if (Guid.TryParse(shopIdClaim, out var parsedShopId))
            {
                ShopId = parsedShopId;
            }
            else
            {
                // Fallback to first shop or default
                ShopId = Guid.NewGuid(); // Default shop for demo
            }
        }
    }
}
