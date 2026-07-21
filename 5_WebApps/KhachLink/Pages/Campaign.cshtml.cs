using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using VanAn.KhachLink.Models;
using VanAn.KhachLink.Services.Http;
using VanAn.Shared.Domain;

namespace VanAn.KhachLink.Pages
{
    public class CampaignModel(IHttpClientFactory httpClientFactory, ProductHttpService productService) : PageModel
    {
        private readonly HttpClient _httpClient = httpClientFactory.CreateClient("gateway");
        private readonly ProductHttpService _productService = productService;

        public SocialCampaign? Campaign { get; set; }
        public string Code { get; set; } = string.Empty;
        public string TrackingCode { get; set; } = string.Empty;
        public string Keyframes { get; set; } = "fade-in";
        public List<ProductDto> Products { get; set; } = [];

        // shopId query param kept for backward compat with old campaign URLs — now interpreted as tenantId.
        [FromQuery(Name = "shopId")]
        public Guid? ShopId { get; set; }

        public async Task<IActionResult> OnGetAsync(string trackingCode)
        {
            TrackingCode = trackingCode ?? string.Empty;
            Code = TrackingCode;

            if (string.IsNullOrEmpty(TrackingCode))
            {
                return NotFound();
            }

            Campaign = await _httpClient.GetFromJsonAsync<SocialCampaign>($"api/campaigns/{Uri.EscapeDataString(TrackingCode)}");

            if (Campaign == null)
            {
                return NotFound();
            }

            // Shop entity removed 2026-07-21 — use Campaign.TenantId (or legacy shopId param as fallback).
            Guid resolvedTenantId = ShopId ?? Campaign.TenantId.Value;
            Products = await _productService.GetProductsAsync(resolvedTenantId);

            return Page();
        }
    }
}
