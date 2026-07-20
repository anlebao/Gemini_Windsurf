using FluentAssertions;
using VanAn.Shared.Domain;
using VanAn.Shared.Domain.Common;
using Xunit;

namespace VanAn.Core.Tests.Domain
{
    /// <summary>
    /// Phase 6: Unit tests for FeaturedProduct entity.
    /// Verifies Single-Identity pattern, factory, update methods, validation.
    /// </summary>
    public class FeaturedProductTests
    {
        private static readonly TenantId TestTenantId = new(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"));
        private static readonly Guid TestProductId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

        [Fact(DisplayName = "Phase6: FeaturedProduct.Create sets Id = FeaturedProductId.Value (Single-Identity)")]
        public void Create_SetsId_EqualsFeaturedProductIdValue()
        {
            var id = Guid.NewGuid();
            var fp = FeaturedProduct.Create(id, TestTenantId, TestProductId, "Cà phê sữa", 25000m);

            fp.Id.Should().Be(id);
            fp.FeaturedProductId.Value.Should().Be(id);
        }

        [Fact(DisplayName = "Phase6: FeaturedProduct constructor sets Id = FeaturedProductId.Value (Single-Identity)")]
        public void Constructor_SetsId_EqualsFeaturedProductIdValue()
        {
            var fp = new FeaturedProduct(TestTenantId, TestProductId, "Cà phê sữa", 25000m);

            fp.Id.Should().Be(fp.FeaturedProductId.Value);
            fp.Id.Should().NotBe(Guid.Empty);
        }

        [Fact(DisplayName = "Phase6: FeaturedProduct.Create sets all properties correctly")]
        public void Create_SetsAllProperties()
        {
            var id = Guid.NewGuid();
            var fp = FeaturedProduct.Create(id, TestTenantId, TestProductId, "Cà phê sữa", 25000m,
                "Cà phê sữa đá ngon", "https://example.com/img.jpg", sortOrder: 5);

            fp.ProductId.Should().Be(TestProductId);
            fp.DisplayName.Should().Be("Cà phê sữa");
            fp.DisplayPrice.Should().Be(25000m);
            fp.DisplayDescription.Should().Be("Cà phê sữa đá ngon");
            fp.ImageUrl.Should().Be("https://example.com/img.jpg");
            fp.SortOrder.Should().Be(5);
            fp.IsActive.Should().BeTrue();
            fp.FeaturedAt.Should().NotBe(default);
            fp.TenantId.Should().Be(TestTenantId);
        }

        [Fact(DisplayName = "Phase6: FeaturedProduct.Create throws on empty DisplayName")]
        public void Create_ThrowsOnEmptyDisplayName()
        {
            var act = () => FeaturedProduct.Create(Guid.NewGuid(), TestTenantId, TestProductId, "", 25000m);

            act.Should().Throw<ArgumentException>();
        }

        [Fact(DisplayName = "Phase6: FeaturedProduct.Create throws on negative DisplayPrice")]
        public void Create_ThrowsOnNegativePrice()
        {
            var act = () => FeaturedProduct.Create(Guid.NewGuid(), TestTenantId, TestProductId, "Test", -1m);

            act.Should().Throw<ArgumentException>();
        }

        [Fact(DisplayName = "Phase6: UpdateDisplayInfo updates fields + calls UpdateAudit")]
        public void UpdateDisplayInfo_UpdatesFields()
        {
            var fp = FeaturedProduct.Create(Guid.NewGuid(), TestTenantId, TestProductId, "Old", 10000m);
            var originalUpdatedAt = fp.UpdatedAt;

            fp.UpdateDisplayInfo("New Name", 30000m, "New desc", "https://new.img", 10);

            fp.DisplayName.Should().Be("New Name");
            fp.DisplayPrice.Should().Be(30000m);
            fp.DisplayDescription.Should().Be("New desc");
            fp.ImageUrl.Should().Be("https://new.img");
            fp.SortOrder.Should().Be(10);
        }

        [Fact(DisplayName = "Phase6: SetActive toggles IsActive")]
        public void SetActive_TogglesIsActive()
        {
            var fp = FeaturedProduct.Create(Guid.NewGuid(), TestTenantId, TestProductId, "Test", 10000m);

            fp.IsActive.Should().BeTrue();
            fp.SetActive(false);
            fp.IsActive.Should().BeFalse();
            fp.SetActive(true);
            fp.IsActive.Should().BeTrue();
        }

        [Fact(DisplayName = "Phase6: FeaturedProductId implicit conversions work")]
        public void FeaturedProductId_ImplicitConversions()
        {
            Guid g = Guid.NewGuid();
            FeaturedProductId id = g; // Guid → FeaturedProductId
            Guid back = id;           // FeaturedProductId → Guid

            back.Should().Be(g);
            FeaturedProductId.FromGuid(g).Value.Should().Be(g);
        }
    }
}
