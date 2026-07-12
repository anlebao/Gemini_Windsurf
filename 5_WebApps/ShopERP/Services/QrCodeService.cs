using QRCoder;
using System.Drawing;
using System.Drawing.Imaging;
using VanAn.Shared.DTOs;

namespace VanAn.ShopERP.Services
{
    /// <summary>
    /// Server-side QR code generation service for ShopERP admin
    /// </summary>
    public interface IShopQrCodeService
    {
        byte[] GenerateProductQRCode(Guid productId, Guid shopId);
        /// <summary>
        /// W3-T8: Generate QR code with optional table number (when QR_TableNumber_Enabled = ON).
        /// </summary>
        byte[] GenerateProductQRCode(Guid productId, Guid shopId, string? tableNumber);
    }

    public class ShopQrCodeService : IShopQrCodeService
    {
        public byte[] GenerateProductQRCode(Guid productId, Guid shopId)
        {
            return GenerateProductQRCode(productId, shopId, tableNumber: null);
        }

        /// <summary>
        /// W3-T8: Generate QR code with optional table number.
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