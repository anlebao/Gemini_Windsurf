using VanAn.Shared.DTOs;
using Xunit;

namespace VanAn.Core.Tests.Dto
{
    /// <summary>
    /// Phase 5: QRCodePayload must carry UnitPrice + VatRate + ProductName so Scan.razor
    /// can add to cart without an API call (fast offline scan). Backward compat: old QR
    /// codes without these fields deserialize to 0/null — Scan.razor falls back to API.
    /// </summary>
    public class QRCodePayloadPriceTests
    {
        private static readonly Guid ProductId = Guid.Parse("33333333-3333-3333-3333-333333333333");
        private static readonly Guid ShopId = Guid.Parse("44444444-4444-4444-4444-444444444444");

        [Fact]
        public void QRCodePayload_Default_NewInstance_HasZeroPriceAndNullName()
        {
            var payload = new QRCodePayload(ProductId, ShopId);

            Assert.Equal(0m, payload.UnitPrice);
            Assert.Equal(0m, payload.VatRate);
            Assert.Null(payload.ProductName);
        }

        [Fact]
        public void QRCodePayload_PriceConstructor_SetsPriceVatName()
        {
            var payload = new QRCodePayload(ProductId, ShopId, tableNumber: null,
                unitPrice: 45000m, vatRate: 0.08m, productName: "Phở bò");

            Assert.Equal(ProductId, payload.ProductId);
            Assert.Equal(ShopId, payload.ShopId);
            Assert.Equal(45000m, payload.UnitPrice);
            Assert.Equal(0.08m, payload.VatRate);
            Assert.Equal("Phở bò", payload.ProductName);
        }

        [Fact]
        public void QRCodePayload_ToJson_RoundTrip_PreservesPriceVatName()
        {
            var payload = new QRCodePayload(ProductId, ShopId, "Table-5",
                unitPrice: 50000m, vatRate: 0.10m, productName: "Cà phê sữa");

            string json = payload.ToJson();
            var parsed = System.Text.Json.JsonSerializer.Deserialize<QRCodePayload>(json);

            Assert.NotNull(parsed);
            Assert.Equal(ProductId, parsed!.ProductId);
            Assert.Equal(ShopId, parsed.ShopId);
            Assert.Equal("Table-5", parsed.TableNumber);
            Assert.Equal(50000m, parsed.UnitPrice);
            Assert.Equal(0.10m, parsed.VatRate);
            Assert.Equal("Cà phê sữa", parsed.ProductName);
        }

        [Fact]
        public void QRCodePayload_FromJson_LegacyQrWithoutPriceFields_DefaultsToZero()
        {
            // Simulate an old QR code printed before Phase 5 — no UnitPrice/VatRate/ProductName fields.
            string legacyJson = "{\"ProductId\":\"33333333-3333-3333-3333-333333333333\",\"ShopId\":\"44444444-4444-4444-4444-444444444444\",\"Timestamp\":1718000000,\"TableNumber\":null}";

            var parsed = System.Text.Json.JsonSerializer.Deserialize<QRCodePayload>(legacyJson);

            Assert.NotNull(parsed);
            Assert.Equal(ProductId, parsed!.ProductId);
            Assert.Equal(0m, parsed.UnitPrice);
            Assert.Equal(0m, parsed.VatRate);
            Assert.Null(parsed.ProductName);
        }

        [Fact]
        public void QRCodePayload_ToQrContent_UrlFormat_RoundTrip_PreservesPrice()
        {
            var payload = new QRCodePayload(ProductId, ShopId, null,
                unitPrice: 120000m, vatRate: 0.08m, productName: "Bún chả Hà Nội");

            string qrContent = payload.ToQrContent();
            var parsed = QRCodePayload.FromJson(qrContent);

            Assert.NotNull(parsed);
            Assert.Equal(120000m, parsed!.UnitPrice);
            Assert.Equal(0.08m, parsed.VatRate);
            Assert.Equal("Bún chả Hà Nội", parsed.ProductName);
        }
    }
}
