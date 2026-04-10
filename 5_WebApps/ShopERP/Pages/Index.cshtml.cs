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
        private readonly IAccountingService _accountingService;

        public IndexModel(IOrderService orderService, IAccountingService accountingService)
        {
            _orderService = orderService;
            _accountingService = accountingService;
        }

        public decimal TodayRevenue { get; private set; }
        public int TodayOrderCount { get; private set; }

        public async Task OnGetAsync()
        {
            var tenantId = GetTenantId();
            
            // Real-time data from backend services
            TodayRevenue = await _accountingService.GetTodayRevenueAsync(tenantId);
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
