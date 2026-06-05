using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace VanAn.ShopERP.Pages.Kitchen
{
    [Authorize(Roles = "Masterchef,Staff,Manager")]
    public class IndexModel : PageModel
    {
        public Guid ShopId { get; set; }

        public void OnGet()
        {
            // Get shop ID from user claims or session
            string? shopIdClaim = User.FindFirst("ShopId")?.Value;
            if (Guid.TryParse(shopIdClaim, out Guid parsedShopId))
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
