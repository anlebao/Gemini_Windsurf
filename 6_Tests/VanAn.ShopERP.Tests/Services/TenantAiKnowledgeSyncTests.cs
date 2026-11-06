using Moq;
using VanAn.Shared.DTOs;
using VanAn.Shared.Services;
using VanAn.ShopERP.Services;
using Xunit;
using FluentAssertions;

namespace VanAn.ShopERP.Tests.Services;

/// <summary>task_ai_p2 — sync client contract: mapping, flag-off zero HTTP, fail-open.</summary>
public sealed class TenantAiKnowledgeSyncTests
{
    private static readonly Guid Tenant = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");
    private static readonly Guid ProductId = Guid.NewGuid();

    private readonly Mock<ITenantAiClient> _client = new();
    private readonly Mock<IProductService> _products = new();
    private readonly Mock<IShopFeatureSettingsService> _flags = new();
    private readonly TenantAiKnowledgeSyncService _sut;

    public TenantAiKnowledgeSyncTests()
    {
        _sut = new TenantAiKnowledgeSyncService(
            _client.Object, _products.Object, _flags.Object,
            new Microsoft.Extensions.Logging.Abstractions.NullLogger<TenantAiKnowledgeSyncService>());
    }

    private static ProductDetailDto Dto(bool active = true) => new()
    {
        ProductId = ProductId, TenantId = Tenant, Name = "Phở chay",
        Description = "Nước dùng nấm", Price = 50_000m, Category = "Món chính", IsActive = active
    };

    [Fact]
    public void Map_MapsAllFields()
    {
        var item = TenantAiKnowledgeSyncService.Map(Dto());
        item.ProductId.Should().Be(ProductId);
        item.Name.Should().Be("Phở chay");
        item.Price.Should().Be(50_000m);
        item.Available.Should().BeTrue();
        item.CategoryName.Should().Be("Món chính");
    }

    [Fact]
    public async Task FlagOff_MakesZeroHttpCalls()
    {
        _flags.Setup(f => f.IsEnabledAsync(Tenant, TenantAiKnowledgeSyncService.FlagName, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        await _sut.NotifyProductChangedAsync(Tenant, ProductId);
        await _sut.NotifyProductDeletedAsync(Tenant, ProductId);
        (await _sut.SyncNowAsync(Tenant)).Should().Be(0);

        _client.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task FlagOn_PushesMappedProduct()
    {
        _flags.Setup(f => f.IsEnabledAsync(Tenant, TenantAiKnowledgeSyncService.FlagName, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _products.Setup(p => p.GetProductForManagementAsync(ProductId, Tenant, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Dto());

        await _sut.NotifyProductChangedAsync(Tenant, ProductId);

        _client.Verify(c => c.PushProductsBatchAsync(
            Tenant,
            It.Is<IReadOnlyList<TenantAiProductItem>>(l => l.Count == 1 && l[0].ProductId == ProductId && l[0].Name == "Phở chay"),
            false, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task FlagOn_DeletedProduct_PushesDelete()
    {
        _flags.Setup(f => f.IsEnabledAsync(Tenant, TenantAiKnowledgeSyncService.FlagName, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        await _sut.NotifyProductDeletedAsync(Tenant, ProductId);

        _client.Verify(c => c.DeleteProductAsync(Tenant, ProductId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ServiceDown_FailOpen_NoThrow()
    {
        _flags.Setup(f => f.IsEnabledAsync(Tenant, TenantAiKnowledgeSyncService.FlagName, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _products.Setup(p => p.GetProductForManagementAsync(ProductId, Tenant, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Dto());
        _client.Setup(c => c.PushProductsBatchAsync(
                It.IsAny<Guid>(), It.IsAny<IReadOnlyList<TenantAiProductItem>>(),
                It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("vanan-ai down"));

        var act = () => _sut.NotifyProductChangedAsync(Tenant, ProductId);
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task SyncNow_FullSync_SendsAllProducts()
    {
        _flags.Setup(f => f.IsEnabledAsync(Tenant, TenantAiKnowledgeSyncService.FlagName, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _products.Setup(p => p.GetAllForManagementAsync(Tenant, It.IsAny<CancellationToken>(), false))
            .ReturnsAsync([Dto(), new ProductDetailDto { ProductId = Guid.NewGuid(), TenantId = Tenant, Name = "B", Price = 1m, IsActive = true }]);
        _client.Setup(c => c.PushProductsBatchAsync(
                Tenant, It.IsAny<IReadOnlyList<TenantAiProductItem>>(), true,
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TenantAiBatchResponse(2, 0, "k", false));

        (await _sut.SyncNowAsync(Tenant)).Should().Be(2);
        _client.Verify(c => c.PushProductsBatchAsync(
            Tenant, It.Is<IReadOnlyList<TenantAiProductItem>>(l => l.Count == 2), true,
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}
