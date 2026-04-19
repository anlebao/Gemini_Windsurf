using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using VanAn.Shared.Domain;
using VanAn.CoreHub.Services;
using Microsoft.AspNetCore.Authorization;

namespace VanAn.ShopERP.Pages
{
    [Authorize]
    public class IndexModel : PageModel
    {
        private readonly IOrderService _orderService;

        public IndexModel(IOrderService orderService)
        {
            _orderService = orderService;
        }

        public decimal TodayRevenue { get; private set; } = 0; // MVP: Placeholder
        public int TodayOrderCount { get; private set; }

        public async Task OnGetAsync()
        {
            var tenantId = GetTenantId();
            
            // Real-time data from backend services
            // TodayRevenue - MVP: Placeholder until accounting service implemented
            TodayOrderCount = await _orderService.GetTodayOrderCountAsync(tenantId);
        }

        private Guid GetTenantId()
        {
            // Extract TenantId from user claims or session
            var tenantIdClaim = User.FindFirst("TenantId")?.Value;
            return Guid.TryParse(tenantIdClaim, out var parsedId) ? parsedId : Guid.Empty;
        }
    }
}
