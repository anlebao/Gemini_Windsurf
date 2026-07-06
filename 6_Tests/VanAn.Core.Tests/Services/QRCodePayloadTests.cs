using VanAn.Shared.DTOs;
using Xunit;

namespace VanAn.Core.Tests.Services
{
    /// <summary>
    /// FIX-BATCH-7: Unit tests for QRCodePayload DTO.
    /// Tests JSON round-trip, null/invalid handling, and timestamp generation.
    /// </summary>
    public class QRCodePayloadTests
    {
        [Fact]
        public void Constructor_SetsProductIdAndShopId()
        {
            var productId = Guid.NewGuid();
            var shopId = Guid.NewGuid();

            var payload = new QRCodePayload(productId, shopId);

            Assert.Equal(productId, payload.ProductId);
            Assert.Equal(shopId, payload.ShopId);
            Assert.True(payload.Timestamp > 0);
        }

        [Fact]
        public void ToJson_ReturnValidJson_WithAllFields()
        {
            var payload = new QRCodePayload(Guid.Parse("11111111-1111-1111-1111-111111111111"), Guid.Parse("22222222-2222-2222-2222-222222222222"));

            var json = payload.ToJson();

            Assert.Contains("\"ProductId\":\"11111111-1111-1111-1111-111111111111\"", json);
            Assert.Contains("\"ShopId\":\"22222222-2222-2222-2222-222222222222\"", json);
            Assert.Contains("\"Timestamp\"", json);
        }

        [Fact]
        public void FromJson_RoundTrip_PreservesAllFields()
        {
            var original = new QRCodePayload(Guid.NewGuid(), Guid.NewGuid());
            var json = original.ToJson();

            var parsed = QRCodePayload.FromJson(json);

            Assert.NotNull(parsed);
            Assert.Equal(original.ProductId, parsed!.ProductId);
            Assert.Equal(original.ShopId, parsed.ShopId);
            Assert.Equal(original.Timestamp, parsed.Timestamp);
        }

        [Fact]
        public void FromJson_InvalidJson_ReturnsNull()
        {
            var result = QRCodePayload.FromJson("not valid json {{{");
            Assert.Null(result);
        }

        [Fact]
        public void FromJson_EmptyString_ReturnsNull()
        {
            var result = QRCodePayload.FromJson("");
            Assert.Null(result);
        }

        [Fact]
        public void FromJson_NullString_ReturnsNull()
        {
            var result = QRCodePayload.FromJson(null!);
            Assert.Null(result);
        }

        [Fact]
        public void FromJson_JsonMissingFields_ReturnsObject_WithDefaultValues()
        {
            // JSON without ProductId/ShopId — deserialization should still work (defaults)
            var result = QRCodePayload.FromJson("{\"Timestamp\":12345}");
            Assert.NotNull(result);
            Assert.Equal(12345, result!.Timestamp);
            Assert.Equal(Guid.Empty, result.ProductId);
        }
    }
}
