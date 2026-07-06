using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace VanAn.Gateway.Controllers
{
    /// <summary>
    /// FIX-BATCH-2: Gateway forward for product QR code generation.
    /// KhachLink and admin UI call this endpoint to get a PNG QR code for a product.
    /// Forwards to ShopERP ProductsController.GetProductQrCode.
    /// </summary>
    [ApiController]
    [Route("api/products")]
    [AllowAnonymous]
    public class ProductsController(IHttpClientFactory httpClientFactory, ILogger<ProductsController> logger) : ControllerBase
    {
        private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;
        private readonly ILogger<ProductsController> _logger = logger;

        /// <summary>
        /// Forward GET /api/products/{id}/qr → ShopERP. Returns PNG image.
        /// </summary>
        [HttpGet("{id:guid}/qr")]
        public async Task<IActionResult> GetProductQrCode(Guid id, [FromQuery] Guid? tenantId)
        {
            try
            {
                var client = _httpClientFactory.CreateClient("shoperp");
                string url = tenantId.HasValue
                    ? $"/api/products/{id}/qr?tenantId={tenantId.Value}"
                    : $"/api/products/{id}/qr";

                var response = await client.GetAsync(url);
                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    return StatusCode((int)response.StatusCode, errorContent);
                }

                byte[] pngBytes = await response.Content.ReadAsByteArrayAsync();
                return File(pngBytes, "image/png", $"qr-{id}.png");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error forwarding GetProductQrCode to ShopERP for ProductId: {ProductId}", id);
                return StatusCode(500, new { error = "Internal server error" });
            }
        }
    }
}
