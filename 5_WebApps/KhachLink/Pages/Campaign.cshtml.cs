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

            Guid resolvedShopId = ShopId ?? Campaign.ShopId ?? Guid.Empty;
            Products = await _productService.GetProductsAsync(resolvedShopId);

            return Page();
        }
    }
}
