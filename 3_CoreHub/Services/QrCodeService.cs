using QRCoder;
using System.Drawing;
using System.Drawing.Imaging;
using Microsoft.Extensions.Configuration;
using VanAn.Shared.DTOs;

namespace VanAn.CoreHub.Services
{
    /// <summary>
    /// Server-side QR code generation service for products.
    /// R2-0d: Consolidated — replaces former IShopQrCodeService (ShopERP) with unified interface.
    /// </summary>
    public interface IQrCodeService
    {
        byte[] GenerateProductQRCode(Guid productId, Guid shopId);

        /// <summary>
        /// R2-0d: Generate QR code with optional table number (when QR_TableNumber_Enabled = ON).
        /// </summary>
        byte[] GenerateProductQRCode(Guid productId, Guid shopId, string? tableNumber);

        /// <summary>
        /// Phase 5: Generate QR code with price/VAT/name snapshot embedded in payload.
        /// Lets Scan.razor add to cart without an API call (fast offline scan).
        /// Use this overload when generating new QR codes for products with a known price.
        /// </summary>
        byte[] GenerateProductQRCode(Guid productId, Guid shopId, string? tableNumber,
            decimal unitPrice, decimal vatRate, string? productName);

        /// <summary>
        /// Phase 5: Full QR code with TenantId — required for multi-tenant cart grouping.
        /// Scan.razor can add to cart without ANY API call (price + tenant + name all in QR).
        /// </summary>
        byte[] GenerateProductQRCode(Guid productId, Guid shopId, string? tableNumber,
            decimal unitPrice, decimal vatRate, string? productName, Guid tenantId);

        /// <summary>
        /// Phase 5+: Full QR code with TenantId + ImageUrl — lets Scan.razor display product image
        /// in fast-path mode (no API call). Use this overload when the product has an image URL.
        /// </summary>
        byte[] GenerateProductQRCode(Guid productId, Guid shopId, string? tableNumber,
            decimal unitPrice, decimal vatRate, string? productName, Guid tenantId, string? imageUrl);
    }

    public class QrCodeService : IQrCodeService
    {
        /// <summary>
        /// #112 fix: KhachLink base URL for QR content — configurable via ExternalUrls:KhachLink.
        /// Falls back to legacy hardcoded domain for backward compat.
        /// Supports diemthuong2/diemthuong3/diemthuong4 scaling without code changes.
        /// </summary>
        private readonly string _khachLinkBaseUrl;

        public QrCodeService(IConfiguration? configuration = null)
        {
            _khachLinkBaseUrl = configuration?["ExternalUrls:KhachLink"]
                ?? "https://diemthuong.khachvip.online";
        }

        public byte[] GenerateProductQRCode(Guid productId, Guid shopId)
        {
            return GenerateProductQRCode(productId, shopId, tableNumber: null);
        }

        /// <summary>
        /// R2-0d: Generate QR code with optional table number.
        /// When tableNumber is null, QR payload excludes it (backward compat).
        /// Legacy overload — does NOT embed price/VAT/name. Use the 6-arg overload for new QR codes.
        /// </summary>
        public byte[] GenerateProductQRCode(Guid productId, Guid shopId, string? tableNumber)
        {
            return GenerateProductQRCode(productId, shopId, tableNumber, unitPrice: 0m, vatRate: 0m, productName: null);
        }

        /// <summary>
        /// Phase 5: Generate QR code with price/VAT/name snapshot embedded in payload.
        /// Routes to the full overload with TenantId=Guid.Empty (legacy — tenant not embedded).
        /// Prefer the 7-arg overload that includes TenantId for multi-tenant cart support.
        /// </summary>
        public byte[] GenerateProductQRCode(Guid productId, Guid shopId, string? tableNumber,
            decimal unitPrice, decimal vatRate, string? productName)
        {
            return GenerateProductQRCode(productId, shopId, tableNumber, unitPrice, vatRate, productName, Guid.Empty);
        }

        /// <summary>
        /// Phase 5: Full QR code with TenantId — required for multi-tenant cart grouping.
        /// </summary>
        public byte[] GenerateProductQRCode(Guid productId, Guid shopId, string? tableNumber,
            decimal unitPrice, decimal vatRate, string? productName, Guid tenantId)
        {
            return GenerateProductQRCode(productId, shopId, tableNumber, unitPrice, vatRate, productName, tenantId, imageUrl: null);
        }

        /// <summary>
        /// Phase 5+: Full QR code with TenantId + ImageUrl — lets Scan.razor display product image
        /// in fast-path mode (no API call).
        /// </summary>
        public byte[] GenerateProductQRCode(Guid productId, Guid shopId, string? tableNumber,
            decimal unitPrice, decimal vatRate, string? productName, Guid tenantId, string? imageUrl)
        {
            var qrPayload = new QRCodePayload(productId, shopId, tableNumber, unitPrice, vatRate, productName, tenantId, imageUrl);
            // Issue 9: Use URL format so external scanners (Zalo) can open the link
            // #112: Use configurable KhachLink base URL (ExternalUrls:KhachLink) instead of hardcoded domain
            var qrContent = qrPayload.ToQrContent(_khachLinkBaseUrl);

            using (QRCodeGenerator qrGenerator = new QRCodeGenerator())
            {
                QRCoder.QRCodeData qrCodeData = qrGenerator.CreateQrCode(qrContent, QRCodeGenerator.ECCLevel.Q);
                using (PngByteQRCode qrCode = new PngByteQRCode(qrCodeData))
                {
                    return qrCode.GetGraphic(20);
                }
            }
        }
    }
}