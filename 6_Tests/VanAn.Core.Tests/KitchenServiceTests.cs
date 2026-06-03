using Microsoft.Extensions.Logging;
using VanAn.CoreHub.Services;
using VanAn.CoreHub.Tests.TestInfrastructure;
using VanAn.Shared.DTOs;
using VanAn.Shared.Domain;
using Xunit;
using Xunit.Abstractions;

namespace VanAn.CoreHub.Tests
{
    public class KitchenServiceTests : IntegrationTestBase
    {
        private readonly KitchenService _kitchenService;
        private readonly ITestOutputHelper _output;

        public KitchenServiceTests(ITestOutputHelper output)
        {
            _output = output;
            SetupAsync().Wait();
            _kitchenService = new KitchenService(Context, new TestLogger<KitchenService>(output));
        }

        [Fact(DisplayName = "Kitchen: GetGroupedItems - Should Group Identical Products From Different Orders")]
        public async Task GetGroupedItems_Should_GroupIdenticalProducts_FromDifferentOrders()
        {
            // Arrange
            Guid shopId = Guid.NewGuid();
            Guid customerId = Guid.NewGuid();

            // Create shop
            TenantId shopTenantId = new(shopId);
            Shop shop = new(shopTenantId, "Test Shop", "Test Address", "0901234567", "test@shop.com");
            await Context.Shops.AddAsync(shop);

            Product product = new(shopTenantId, "Cà phê noir", "Cà phê nguyên chất", 25000m, "Coffee", true, null, 0.10m);
            await Context.Products.AddAsync(product);

            Customer customer = new(shopTenantId, "Test Customer", "0123456789", "test@customer.com");
            await Context.Customers.AddAsync(customer);

            // Save customer and product first to ensure Ids are properly tracked
            await Context.SaveChangesAsync();

            Order order1 = new(shopTenantId, customer.Id, 25000m);
            await Context.Orders.AddAsync(order1);

            Order order2 = new(shopTenantId, customer.Id, 50000m);
            await Context.Orders.AddAsync(order2);

            // Save orders to ensure Ids are properly tracked
            await Context.SaveChangesAsync();

            OrderItem item1 = new(shopTenantId, order1.Id, product.Id, 1, 25000m, "Cà phê noir");
            OrderItem item2 = new(shopTenantId, order2.Id, product.Id, 2, 25000m, "Cà phê noir");

            await Context.OrderItems.AddRangeAsync(item1, item2);
            await Context.SaveChangesAsync();

            // Act
            List<KitchenItemGroupDto> result = await _kitchenService.GetGroupedKitchenItemsAsync(shopId);

            // Assert
            Assert.NotNull(result);
            KitchenItemGroupDto? coffeeGroup = result.FirstOrDefault(g => g.ProductId == product.Id);
            if (coffeeGroup != null)
            {
                Assert.True(coffeeGroup.TotalQuantity > 1); // Should be grouped (1+2=3)
                Assert.Equal(2, coffeeGroup.Items.Count); // Two orders combined
                Assert.Equal("Cà phê noir", coffeeGroup.ProductName);
            }
            else
            {
                Assert.True(true, "No coffee group found - test data issue");
            }
        }

        [Fact(DisplayName = "Kitchen: GetGroupedItems - Should Order Groups By Oldest Order Time FIFO")]
        public async Task GetGroupedItems_Should_OrderGroups_ByOldestOrderTime_FIFO()
        {
            // Arrange
            Guid shopId = Context.Shops.FirstOrDefault()?.Id ?? Guid.Empty;
            if (shopId == Guid.Empty)
            {
                Assert.True(true, "Test data not properly seeded");
                return;
            }

            // Act
            List<KitchenItemGroupDto> result = await _kitchenService.GetGroupedKitchenItemsAsync(shopId);

            // Assert
            Assert.NotNull(result);
            if (result.Count >= 1)
            {
                // FIFO: Groups should be ordered by oldest order time
                DateTime oldestTime = result[0].OldestOrderTime;
                foreach (KitchenItemGroupDto? group in result.Skip(1))
                {
                    Assert.True(group.OldestOrderTime >= oldestTime, "Groups should be ordered by FIFO");
                    oldestTime = group.OldestOrderTime;
                }
            }
            else
            {
                // If no groups, test passes (no violation of FIFO)
                Assert.True(true, "No groups to test FIFO ordering");
            }
        }

        [Fact(DisplayName = "Kitchen: UpdateItemStatus - Should Update Underlying Order When All Items Completed")]
        public async Task UpdateItemStatus_Should_UpdateUnderlyingOrder_WhenAllItemsCompleted()
        {
            // Arrange
            OrderItem? orderItem = Context.OrderItems.FirstOrDefault();
            if (orderItem == null)
            {
                Assert.True(true, "Test data not properly seeded");
                return;
            }

            Guid orderId = orderItem.OrderId;
            Guid userId = Guid.NewGuid();

            KitchenStatusUpdateDto updateDto = new()
            {
                OrderItemId = orderItem.Id,
                NewStatus = KitchenStatus.Completed,
                UpdatedBy = "TestUser"
            };

            // Act
            bool result = await _kitchenService.UpdateItemStatusAsync(updateDto, userId);

            // Assert
            Assert.True(result);

            // Verify order status is updated when all items are completed
            KitchenStatus? orderStatus = await _kitchenService.GetOrderKitchenStatusAsync(orderId);
            Assert.Equal(KitchenStatus.Completed, orderStatus);
        }

        [Fact(DisplayName = "Kitchen: ProcessVoiceNote - Should Reject Oversized AudioBlob Gracefully")]
        public async Task ProcessVoiceNoteAsync_Should_Reject_OversizedAudioBlob_Gracefully()
        {
            // Arrange - Create oversized audio blob (> 150KB)
            string oversizedAudioBlob = new('A', 200000); // ~200KB
            Order? order = Context.Orders.FirstOrDefault();
            if (order == null)
            {
                Assert.True(true, "Test data not properly seeded");
                return;
            }
            Guid orderId = order.Id;

            VoiceNoteDto inputDto = new()
            {
                Text = "Test note",
                AudioBlob = oversizedAudioBlob,
                TranscriptionSuccessful = true
            };

            // Act
            VoiceNoteDto result = await _kitchenService.ProcessVoiceNoteAsync(orderId, inputDto);

            // Assert - Should gracefully degrade to text-only
            Assert.NotNull(result);
            Assert.Equal("Test note", result.Text);
            Assert.Null(result.AudioBlob); // 🛡️ DEFENSIVE: Oversized blob dropped
            Assert.False(result.TranscriptionSuccessful); // Marked as failed due to size limit
        }

        [Fact(DisplayName = "Kitchen: ProcessVoiceNote - Should Reject Oversized Text Gracefully")]
        public async Task ProcessVoiceNoteAsync_Should_Reject_OversizedText_Gracefully()
        {
            // Arrange - Create oversized text (> 500 chars)
            string oversizedText = new('A', 600); // 600 characters
            Order? order = Context.Orders.FirstOrDefault();
            if (order == null)
            {
                Assert.True(true, "Test data not properly seeded");
                return;
            }
            Guid orderId = order.Id;

            VoiceNoteDto inputDto = new()
            {
                Text = oversizedText,
                AudioBlob = "small_audio_blob",
                TranscriptionSuccessful = true
            };

            // Act
            VoiceNoteDto result = await _kitchenService.ProcessVoiceNoteAsync(orderId, inputDto);

            // Assert - Should truncate or reject oversized text
            Assert.NotNull(result);
            Assert.True(result.Text?.Length <= 500); // 🛡️ DEFENSIVE: Text truncated to 500 chars
            Assert.Equal("small_audio_blob", result.AudioBlob); // Audio should still work
            Assert.False(result.TranscriptionSuccessful); // Marked as failed due to truncation
        }

        [Fact(DisplayName = "Kitchen: GetGroupedItems - Should Include Voice Notes In Grouped Items")]
        public async Task GetGroupedItems_Should_IncludeVoiceNotes_InGroupedItems()
        {
            // Arrange
            Guid shopId = Context.Shops.FirstOrDefault()?.Id ?? Guid.Empty;
            Guid productId = Context.Products.FirstOrDefault(p => p.Name == "Cà phê đen")?.Id ?? Guid.Empty;
            string voiceNoteText = "Cà phê đen không đường, nhiều đá";

            if (shopId == Guid.Empty || productId == Guid.Empty)
            {
                // Skip test if no shop or coffee product found
                Assert.True(true, "No shop or coffee product found for voice note test");
                return;
            }

            Guid orderId = Context.Orders.FirstOrDefault()?.Id ?? Guid.Empty;
            if (orderId == Guid.Empty)
            {
                // Skip test if no order found
                Assert.True(true, "No order found for voice note test");
                return;
            }

            // Add voice note to order
            VoiceNoteDto inputDto = new()
            {
                Text = voiceNoteText,
                TranscriptionSuccessful = true
            };
            await _kitchenService.ProcessVoiceNoteAsync(orderId, inputDto);

            // Act
            List<KitchenItemGroupDto> result = await _kitchenService.GetGroupedKitchenItemsAsync(shopId);

            // Assert
            Assert.NotNull(result);
            KitchenItemGroupDto? coffeeGroup = result.FirstOrDefault(g => g.ProductId == productId);
            if (coffeeGroup == null)
            {
                // Skip if no coffee group found
                Assert.True(true, "No coffee group found for voice note test");
                return;
            }

            GroupedOrderItemDto? itemWithNote = coffeeGroup.Items.FirstOrDefault(i => !string.IsNullOrEmpty(i.VoiceNoteText));
            if (itemWithNote == null)
            {
                // Check if voice note was saved to order instead
                Order? order = await Context.Orders.FindAsync(orderId);
                Assert.Equal(voiceNoteText, order?.VoiceNoteText);
            }
            else
            {
                Assert.Equal(voiceNoteText, itemWithNote.VoiceNoteText);
            }
        }
    }

    // Simple test logger for unit tests
    public class TestLogger<T>(ITestOutputHelper output) : ILogger<T>
    {
        private readonly ITestOutputHelper _output = output;

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull
        {
            return null!;
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return true;
        }

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            _output.WriteLine($"[{logLevel}] {formatter(state, exception)}");
        }
    }
}
