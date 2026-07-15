using QRCoder;
using System.Drawing;
using System.Drawing.Imaging;
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
    }

    public class QrCodeService : IQrCodeService
    {
        public byte[] GenerateProductQRCode(Guid productId, Guid shopId)
        {
            return GenerateProductQRCode(productId, shopId, tableNumber: null);
        }

        /// <summary>
        /// R2-0d: Generate QR code with optional table number.
        /// When tableNumber is null, QR payload excludes it (backward compat).
        /// </summary>
        public byte[] GenerateProductQRCode(Guid productId, Guid shopId, string? tableNumber)
        {
            var qrPayload = new QRCodePayload(productId, shopId, tableNumber);
            var qrJson = qrPayload.ToJson();

            using (QRCodeGenerator qrGenerator = new QRCodeGenerator())
            {
                QRCoder.QRCodeData qrCodeData = qrGenerator.CreateQrCode(qrJson, QRCodeGenerator.ECCLevel.Q);
                using (PngByteQRCode qrCode = new PngByteQRCode(qrCodeData))
                {
                    return qrCode.GetGraphic(20);
                }
            }
        }
    }
}