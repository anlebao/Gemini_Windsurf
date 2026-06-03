using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace VanAn.ShopERP.Pages
{
    [Authorize(Roles = "Guard")]
    public class GuardRedirectModel : PageModel
    {
        public IActionResult OnGet()
        {
            // Auto-redirect Guard users to scanner page
            return RedirectToPage("/Guard/Scan");
        }
    }
}
