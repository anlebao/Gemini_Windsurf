using VanAn.Shared.Domain;
using VanAn.KhachLink.Services;
using VanAn.KhachLink.Models;
using VanAn.Integration.Tests.Infrastructure;
using Microsoft.Extensions.Logging;
using Xunit;
using FluentAssertions;
using Moq;

namespace VanAn.Integration.Tests;

[Trait("Category", "Unit")]
[Trait("Service", "SyncConflictResolver")]
public class SyncConflictResolverTests
{
    private readonly SyncConflictResolver _resolver;
    private readonly TenantId _tenantId;

    public SyncConflictResolverTests()
    {
        var mockLogger = new Mock<ILogger<SyncConflictResolver>>();
        _resolver = new SyncConflictResolver(mockLogger.Object);
        _tenantId = TestEntityBuilder.CreateTenantId();
    }

    [Fact(DisplayName = "ResolveCartConflict — server item with new ProductId is added to merged result")]
    public async Task ResolveCartConflict_ServerExtraItem_AddedToMergedResult()
    {
        var offlineProductId = Guid.NewGuid();
        var serverProductId = Guid.NewGuid();

        var offlineItems = new List<OfflineOrderItemDto>
        {
            new OfflineOrderItemDto
            {
                ProductId = offlineProductId.ToString(),
                Quantity = 1,
                UnitPrice = 10000m,
                TotalPrice = 10000m
            }
        };

        var serverItems = new List<CartItem>
        {
            TestEntityBuilder.CreateCartItem(productId: serverProductId, quantity: 2, unitPrice: 20000m)
        };

        var result = await _resolver.ResolveCartConflictAsync(offlineItems, serverItems);

        result.Action.Should().Be(ResolutionAction.Merge);
        result.MergedOrder!.Items.Should().HaveCount(2);
    }

    [Fact(DisplayName = "ResolveCartConflict — offline item is preserved when server does not have it")]
    public async Task ResolveCartConflict_OfflineItem_PreservedInMerge()
    {
        var offlineProductId = Guid.NewGuid();
        var offlineItems = new List<OfflineOrderItemDto>
        {
            new OfflineOrderItemDto
            {
                ProductId = offlineProductId.ToString(),
                Quantity = 1,
                UnitPrice = 15000m,
                TotalPrice = 15000m
            }
        };

        var result = await _resolver.ResolveCartConflictAsync(offlineItems, new List<CartItem>());

        result.Action.Should().Be(ResolutionAction.Merge);
        result.MergedOrder!.Items.Should().HaveCount(1);
        result.MergedOrder.Items[0].ProductId.Should().Be(offlineProductId.ToString());
    }

    [Fact(DisplayName = "ResolveCartConflict — duplicate ProductId in server is NOT re-added to merged result")]
    public async Task ResolveCartConflict_ServerDuplicateProductId_NotReAdded()
    {
        var sharedProductId = Guid.NewGuid();

        var offlineItems = new List<OfflineOrderItemDto>
        {
            new OfflineOrderItemDto
            {
                ProductId = sharedProductId.ToString(),
                Quantity = 1,
                UnitPrice = 10000m,
                TotalPrice = 10000m
            }
        };

        var serverItems = new List<CartItem>
        {
            TestEntityBuilder.CreateCartItem(productId: sharedProductId, quantity: 3, unitPrice: 10000m)
        };

        var result = await _resolver.ResolveCartConflictAsync(offlineItems, serverItems);

        result.MergedOrder!.Items.Should().HaveCount(1,
            "ProductId already exists in offline — server item must NOT be duplicated");
    }

    [Fact(DisplayName = "ResolveCartConflict — server item is merged using CartItem.ProductId, not CartItem.Id")]
    public async Task ResolveCartConflict_DeduplicatesByProductId_NotByCartItemId()
    {
        var productId = Guid.NewGuid();

        var offlineItems = new List<OfflineOrderItemDto>
        {
            new OfflineOrderItemDto
            {
                ProductId = productId.ToString(),
                Quantity = 2,
                UnitPrice = 25000m,
                TotalPrice = 50000m
            }
        };

        var serverItem = TestEntityBuilder.CreateCartItem(productId: productId);
        var serverItems = new List<CartItem> { serverItem };

        var result = await _resolver.ResolveCartConflictAsync(offlineItems, serverItems);

        result.MergedOrder!.Items.Should().HaveCount(1,
            "Deduplication key must be ProductId — CartItem.Id is irrelevant for conflict resolution");
        result.MergedOrder.Items[0].ProductId.Should().Be(productId.ToString());
    }
}
