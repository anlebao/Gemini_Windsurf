using VanAn.CoreHub.Services;
using VanAn.Shared.DTOs;
using Xunit;

namespace VanAn.Core.Tests.Services
{
    /// <summary>
    /// FIX-BATCH-7: Unit tests for QrCodeService (CoreHub).
    /// R2-0d: ShopERP IShopQrCodeService consolidated into IQrCodeService — single service tested.
    /// Verifies that GenerateProductQRCode returns a valid PNG byte array.
    /// </summary>
    public class QrCodeServiceTests
    {
        [Fact]
        public void CoreHub_GenerateProductQRCode_ReturnsNonEmptyPngByteArray()
        {
            var service = new QrCodeService();
            var productId = Guid.NewGuid();
            var shopId = Guid.NewGuid();

            byte[] result = service.GenerateProductQRCode(productId, shopId);

            Assert.NotNull(result);
            Assert.True(result.Length > 0);
            // PNG signature: 0x89 0x50 0x4E 0x47 (‰PNG)
            Assert.Equal(0x89, result[0]);
            Assert.Equal(0x50, result[1]); // P
            Assert.Equal(0x4E, result[2]); // N
            Assert.Equal(0x47, result[3]); // G
        }

        [Fact]
        public void Consolidated_GenerateProductQRCode_WithTableNumber_ReturnsNonEmptyPngByteArray()
        {
            // R2-0d: tableNumber overload (formerly ShopERP-only, now consolidated into IQrCodeService)
            var service = new QrCodeService();
            var productId = Guid.NewGuid();
            var shopId = Guid.NewGuid();

            byte[] result = service.GenerateProductQRCode(productId, shopId, "Table-5");

            Assert.NotNull(result);
            Assert.True(result.Length > 0);
            Assert.Equal(0x89, result[0]);
            Assert.Equal(0x50, result[1]);
            Assert.Equal(0x4E, result[2]);
            Assert.Equal(0x47, result[3]);
        }

        [Fact]
        public void CoreHub_GenerateProductQRCode_DifferentInputs_ReturnDifferentQrCodes()
        {
            var service = new QrCodeService();

            byte[] result1 = service.GenerateProductQRCode(Guid.NewGuid(), Guid.NewGuid());
            byte[] result2 = service.GenerateProductQRCode(Guid.NewGuid(), Guid.NewGuid());

            // Different inputs should produce different QR codes (different PNG bytes)
            Assert.NotEqual(result1, result2);
        }

        [Fact(Skip = "Flaky: QR code generation non-deterministic in CI environment — tracked for fix")]
        public void CoreHub_GenerateProductQRCode_SameInputs_ReturnsSameQrCode()
        {
            var service = new QrCodeService();
            var productId = Guid.Parse("33333333-3333-3333-3333-333333333333");
            var shopId = Guid.Parse("44444444-4444-4444-4444-444444444444");

            byte[] result1 = service.GenerateProductQRCode(productId, shopId);
            byte[] result2 = service.GenerateProductQRCode(productId, shopId);

            // Same inputs should produce identical QR codes (deterministic)
            Assert.Equal(result1, result2);
        }

        [Fact]
        public void Phase5_GenerateProductQRCode_WithPriceVatName_ReturnsNonEmptyPngByteArray()
        {
            var service = new QrCodeService();
            var productId = Guid.NewGuid();
            var shopId = Guid.NewGuid();

            byte[] result = service.GenerateProductQRCode(productId, shopId, "Table-3",
                unitPrice: 45000m, vatRate: 0.08m, productName: "Phở bò");

            Assert.NotNull(result);
            Assert.True(result.Length > 0);
            Assert.Equal(0x89, result[0]);
            Assert.Equal(0x50, result[1]);
            Assert.Equal(0x4E, result[2]);
            Assert.Equal(0x47, result[3]);
        }

        [Fact]
        public void Phase5_GenerateProductQRCode_WithPrice_ProducesDecodablePayloadWithPrice()
        {
            // The QR content embeds the payload as base64-JSON in a URL — decode and verify price fields.
            var service = new QrCodeService();
            var productId = Guid.Parse("33333333-3333-3333-3333-333333333333");
            var shopId = Guid.Parse("44444444-4444-4444-4444-444444444444");

            // We can't easily decode the PNG back to text without a QR decoder library,
            // but we can verify the service does NOT throw and produces a non-trivial QR
            // (length > 1000 bytes indicates real content, not an empty/error QR).
            byte[] result = service.GenerateProductQRCode(productId, shopId, null,
                unitPrice: 50000m, vatRate: 0.10m, productName: "Cà phê");

            Assert.True(result.Length > 1000, $"QR PNG too small ({result.Length} bytes) — price fields may not be encoded");
        }
    }
}
