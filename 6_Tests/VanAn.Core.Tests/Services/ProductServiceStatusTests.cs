using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using VanAn.CoreHub.Repositories;
using VanAn.CoreHub.Services;
using VanAn.Shared.Domain;
using VanAn.Shared.DTOs;
using Xunit;

namespace VanAn.Core.Tests.Services
{
    /// <summary>
    /// Product Pending-Review T1: CreateProductAsync PendingReview → Status=Pending;
    /// ProductDetailDto.Status map đúng. Mock IProductRepository (AddAsync trả entity).
    /// </summary>
    [Trait("Category", "Unit")]
    [Trait("Service", "ProductService")]
    public class ProductServiceStatusTests
    {
        private readonly Mock<IProductRepository> _repo = new();
        private readonly Mock<IImageStorageService> _images = new();
        private readonly ProductService _service;

        public ProductServiceStatusTests()
        {
            _repo.Setup(r => r.AddAsync(It.IsAny<Product>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((Product p, CancellationToken _) => p);
            _service = new ProductService(_repo.Object, _images.Object, NullLogger<ProductService>.Instance);
        }

        private static CreateProductRequest Req(bool pending = false) => new()
        {
            Name = "Test product",
            Description = "desc",
            Price = 10000m,
            Category = "Cat",
            PendingReview = pending
        };

        [Fact(DisplayName = "PendingReview=true → entity Status=Pending + DTO Status='Pending'")]
        public async Task Create_PendingReviewTrue_StatusPending()
        {
            Product? captured = null;
            _repo.Setup(r => r.AddAsync(It.IsAny<Product>(), It.IsAny<CancellationToken>()))
                .Callback<Product, CancellationToken>((p, _) => captured = p)
                .ReturnsAsync((Product p, CancellationToken _) => p);

            var dto = await _service.CreateProductAsync(Req(pending: true), Guid.NewGuid());

            captured.Should().NotBeNull();
            captured!.Status.Should().Be(ProductStatus.Pending);
            dto.Status.Should().Be("Pending");
        }

        [Fact(DisplayName = "PendingReview=false → Status=Ready + DTO Status='Ready'")]
        public async Task Create_PendingReviewFalse_StatusReady()
        {
            var dto = await _service.CreateProductAsync(Req(pending: false), Guid.NewGuid());

            dto.Status.Should().Be("Ready");
        }

        [Fact(DisplayName = "GetAllForManagementAsync → DTO chứa Status string")]
        public async Task GetAll_DtoContainsStatus()
        {
            var tenantId = Guid.NewGuid();
            var pending = new Product(new TenantId(tenantId), "P1", "", 1m, "C", status: ProductStatus.Pending);
            var ready = new Product(new TenantId(tenantId), "P2", "", 1m, "C");
            _repo.Setup(r => r.GetAllForManagementAsync(It.IsAny<TenantId>(), It.IsAny<CancellationToken>(), It.IsAny<bool>()))
                .ReturnsAsync(new List<Product> { pending, ready });

            var dtos = await _service.GetAllForManagementAsync(tenantId);

            dtos.Should().HaveCount(2);
            dtos.Single(d => d.Name == "P1").Status.Should().Be("Pending");
            dtos.Single(d => d.Name == "P2").Status.Should().Be("Ready");
        }
    }
}
