using Microsoft.AspNetCore.Mvc.RazorPages;
using VanAn.CoreHub.Services;
using Microsoft.AspNetCore.Authorization;

namespace VanAn.ShopERP.Pages
{
    [Authorize]
    public class IndexModel(IOrderService orderService) : PageModel
    {
        private readonly IOrderService _orderService = orderService;

        public decimal TodayRevenue { get; private set; } = 0; // MVP: Placeholder
        public int TodayOrderCount { get; private set; }

        public async Task OnGetAsync()
        {
            Guid tenantId = GetTenantId();

            // Real-time data from backend services
            // TodayRevenue - MVP: Placeholder until accounting service implemented
            TodayOrderCount = await _orderService.GetTodayOrderCountAsync(tenantId);
        }

        private Guid GetTenantId()
        {
            // Extract TenantId from user claims or session
            string? tenantIdClaim = User.FindFirst("TenantId")?.Value;
            return Guid.TryParse(tenantIdClaim, out Guid parsedId) ? parsedId : Guid.Empty;
        }
    }
}
