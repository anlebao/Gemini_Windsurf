using QRCoder;
using System.Drawing;
using System.Drawing.Imaging;
using VanAn.Shared.DTOs;

namespace VanAn.CoreHub.Services
{
    /// <summary>
    /// Server-side QR code generation service for products
    /// </summary>
    public interface IQrCodeService
    {
        byte[] GenerateProductQRCode(Guid productId, Guid shopId);
    }

    public class QrCodeService : IQrCodeService
    {
        public byte[] GenerateProductQRCode(Guid productId, Guid shopId)
        {
            var qrPayload = new QRCodePayload(productId, shopId);
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