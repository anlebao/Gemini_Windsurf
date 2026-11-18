using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace VanAn.ShopERP.Controllers
{
    /// <summary>
    /// ATRS user guides (HTML) — SystemAdmin only. Files live in Content/Guides/atrs
    /// (outside wwwroot → không public); served through this authorized controller.
    /// </summary>
    [Authorize(Roles = "SystemAdmin")]
    [Route("admin/guides/atrs/{doc?}")]
    public class AdminGuidesController(IWebHostEnvironment env) : Controller
    {
        private static readonly HashSet<string> AllowedDocs = new(StringComparer.OrdinalIgnoreCase)
        {
            "index", "khach-hang", "nhan-vien", "faq-bao-mat", "atrs-guide.css"
        };

        [HttpGet]
        public IActionResult Get(string? doc)
        {
            var name = string.IsNullOrWhiteSpace(doc) ? "index" : doc;
            if (!AllowedDocs.Contains(name))
                return NotFound();

            var ext = name.EndsWith(".css", StringComparison.OrdinalIgnoreCase) ? "text/css" : "text/html; charset=utf-8";
            var path = Path.Combine(env.ContentRootPath, "Content", "Guides", "atrs", name);
            if (!System.IO.File.Exists(path))
                return NotFound();

            return PhysicalFile(path, ext);
        }
    }
}
