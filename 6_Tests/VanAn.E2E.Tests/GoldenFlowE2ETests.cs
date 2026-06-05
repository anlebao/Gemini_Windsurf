using Microsoft.Playwright;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;
using VanAn.E2E.Tests.Infrastructure;
using static Microsoft.Playwright.Assertions;
using System.Text.Json;

namespace VanAn.E2E.Tests
{
    [Collection("SelfHosted Tests")]
    [Trait("Category", "E2E")]
    public class GoldenFlowE2ETests : E2ETestBase
    {
        private readonly ITestOutputHelper _output;

        public GoldenFlowE2ETests(SelfHostedTestFactory factory, ITestOutputHelper output) 
            : base(factory, output)
        {
            _output = output;
        }

        [Fact]
        public async Task GoldenFlow_VoiceNoteToKitchen_ShouldSucceed()
        {
            // 🎤 CONTEXT 1: KhachLink - Customer Voice Note (Port 5002)
            await NavigateToKhachLinkAsync();

            // Wait for page to load and verify body is visible
            await Expect(Page.Locator("body")).ToBeVisibleAsync();
            
            // Wait for product grid to be present (test data should be seeded)
            var productGrid = Page.Locator("#vibe-product-grid, .product-grid, [data-testid='product-grid']");
            await Expect(productGrid).ToBeVisibleAsync(new() { Timeout = 10000 });
            
            // Find add to cart button - use stable selector
            var addToCartButton = Page.Locator("button:has-text('ADD TO CART'), button:has-text('Thêm vào giỏ'), button:has-text('Add'), button:has-text('Thêm'), [data-testid='add-to-cart-btn']");
            await Expect(addToCartButton.First).ToBeVisibleAsync(new() { Timeout = 10000 });
            await addToCartButton.First.ClickAsync();

            // 🎭 MOCK THE MICROPHONE: Use helper method
            await MockSpeechRecognitionAsync("Cà phê đen không đường, nhiều đá");

            // Navigate to voice note page
            await Page.GotoAsync("http://localhost:5002/voice-note");
            await Page.WaitForLoadStateAsync(LoadState.DOMContentLoaded);

            // Verify voice note page loaded
            await Expect(Page.Locator("body")).ToBeVisibleAsync();
            
            // Look for voice note header - fail if not found (no fallback)
            var voiceNoteHeader = Page.Locator("h2:has-text('Ghi chú giọng nói'), h1:has-text('Voice Note'), h2:has-text('Voice'), .voice-note-container h2, h2:has-text('Ghi chú'), [data-testid='voice-note-header']");
            await Expect(voiceNoteHeader.First).ToBeVisibleAsync(new() { Timeout = 10000 });

            // Start recording (will trigger mocked speech recognition)
            var recordButton = Page.Locator("button:has-text('Bắt đầu ghi âm'), button:has-text('Record'), [data-testid='record-button']");
            await Expect(recordButton).ToBeVisibleAsync(new() { Timeout = 10000 });
            await recordButton.ClickAsync();

            // Wait for transcription to appear (mocked speech should complete)
            var transcriptionText = Page.Locator(".transcription-result p, [data-testid='transcription-text']");
            await Expect(transcriptionText).ToContainTextAsync("Cà phê đen không đường, nhiều đá", new() { Timeout = 5000 });

            // Submit the voice note
            var submitButton = Page.Locator("button:has-text('Gửi ghi chú'), button:has-text('Submit'), [data-testid='submit-voice-note']");
            await Expect(submitButton).ToBeVisibleAsync(new() { Timeout = 10000 });
            await submitButton.ClickAsync();

            // 🛒 CONTEXT 2: ShopERP Admin - Confirm Order (Port 5003)
            await NavigateToShopERPAsync("Admin/Orders");

            // Wait for orders to appear - use WaitForSelector instead of timeout
            var orderRow = Page.Locator("table tbody tr, .order-item, .order-row, [data-testid='order-row']");
            await Expect(orderRow.First).ToBeVisibleAsync(new() { Timeout = 15000 });

            // Click confirm button on first order
            var confirmButton = Page.Locator("button:has-text('Confirm'), button:has-text('Xác nhận'), button:has-text('Duyệt'), [data-testid='confirm-order-btn']");
            await Expect(confirmButton.First).ToBeVisibleAsync(new() { Timeout = 10000 });
            await confirmButton.First.ClickAsync();

            // 👨‍🍳 CONTEXT 3: ShopERP Masterchef - Kitchen View (Port 5003)
            await NavigateToShopERPAsync("Kitchen");

            // Wait for kitchen view to render
            await Expect(Page.Locator("body")).ToBeVisibleAsync();
            
            // Look for kitchen header or items container - fail if not found
            var kitchenHeader = Page.Locator(".kitchen-header, h1:has-text('Bếp'), h1:has-text('Kitchen'), [data-testid='kitchen-header']");
            var kitchenItems = Page.Locator("#kitchen-items, .kitchen-items, [data-testid='kitchen-items']");
            
            // At least one of these should be present
            var hasHeader = await kitchenHeader.CountAsync() > 0;
            var hasItems = await kitchenItems.CountAsync() > 0;
            Assert.True(hasHeader || hasItems, "Kitchen view should have header or items container");
            
            if (hasHeader)
            {
                await Expect(kitchenHeader.First).ToBeVisibleAsync();
            }
            if (hasItems)
            {
                await Expect(kitchenItems.First).ToBeVisibleAsync();
            }

            // 🎯 ULTIMATE ASSERTION: Verify voice note appears in kitchen
            var voiceNoteDisplay = Page.Locator(".voice-note-display, [data-testid='voice-note-display']");
            
            // Wait for voice note to appear (SignalR update)
            await Expect(voiceNoteDisplay.First).ToBeVisibleAsync(new() { Timeout = 15000 });

            // Verify the exact voice note text
            var voiceNoteText = voiceNoteDisplay.Locator(".voice-note-text, strong");
            await Expect(voiceNoteText).ToContainTextAsync("Cà phê đen không đường, nhiều đá");

            // 🎉 GOLDEN FLOW COMPLETE
            _output.WriteLine("✅ Golden Flow E2E Test PASSED: Voice note successfully flowed from KhachLink to Kitchen Display");
        }

        [Fact(DisplayName = "Complete Order Flow: KhachLink -> ShopERP -> KhachLink")]
        public async Task Complete_Order_Flow_KhachLink_To_ShopERP_To_KhachLink()
        {
            // ** STEP 1: KhachLink UI (Port 5002) - Create Order
            _output.WriteLine("STEP 1: KhachLink UI - Create Order");
            await NavigateToKhachLinkAsync();

            // Find and add product to cart
            var addToCartButton = Page.Locator("button:has-text('ADD TO CART'), button:has-text('Add to Cart'), button:has-text('Add'), [data-testid='add-to-cart-btn']");
            await Expect(addToCartButton.First).ToBeVisibleAsync(new() { Timeout = 10000 });
            await addToCartButton.First.ClickAsync();

            // Fill customer information
            var customerNameField = Page.Locator("input[name='customerName'], input[placeholder*='name'], input[placeholder*='Name'], [data-testid='customer-name']");
            await Expect(customerNameField.First).ToBeVisibleAsync(new() { Timeout = 10000 });
            await customerNameField.First.FillAsync("E2E Test Customer");

            var phoneField = Page.Locator("input[name='phone'], input[placeholder*='phone'], input[placeholder*='Phone'], [data-testid='customer-phone']");
            await Expect(phoneField.First).ToBeVisibleAsync(new() { Timeout = 10000 });
            await phoneField.First.FillAsync("0987654321");

            // Submit order
            var submitButton = Page.Locator("button:has-text('Submit Order'), button:has-text('Submit'), button:has-text('Confirm'), [data-testid='submit-order']");
            await Expect(submitButton.First).ToBeVisibleAsync(new() { Timeout = 10000 });
            await submitButton.First.ClickAsync();

            // Verify order created successfully
            var orderConfirmation = Page.Locator(".order-confirmation, .success-message, .alert-success, [data-testid='order-confirmation']");
            await Expect(orderConfirmation.First).ToBeVisibleAsync(new() { Timeout = 10000 });

            // Get order ID from confirmation
            var orderIdElement = Page.Locator(".order-id, .confirmation-number, [data-testid='order-id']");
            var orderId = await orderIdElement.First.TextContentAsync();
            _output.WriteLine($"Order created successfully: {orderId ?? "ID not captured"}");

            // ** STEP 2: ShopERP UI (Port 5003) - Process Order
            _output.WriteLine("STEP 2: ShopERP UI - Process Order");
            await NavigateToShopERPAsync();

            // Navigate to order management
            var ordersLink = Page.Locator("a:has-text('Orders'), a:has-text('Order Management'), nav a[href*='order'], [data-testid='orders-link']");
            await Expect(ordersLink.First).ToBeVisibleAsync(new() { Timeout = 10000 });
            await ordersLink.First.ClickAsync();

            // Find pending orders
            var pendingOrders = Page.Locator(".order-item.pending, .order-status-pending, tr[data-status='pending'], [data-status='pending']");
            await Expect(pendingOrders.First).ToBeVisibleAsync(new() { Timeout = 15000 });
            await pendingOrders.First.ClickAsync();

            // Confirm order
            var confirmButton = Page.Locator("button:has-text('Confirm'), button:has-text('Approve'), button:has-text('Process'), [data-testid='confirm-order']");
            await Expect(confirmButton.First).ToBeVisibleAsync(new() { Timeout = 10000 });
            await confirmButton.First.ClickAsync();

            // Handle confirmation dialog if present
            var dialogConfirm = Page.Locator(".modal button:has-text('Yes'), .dialog button:has-text('Confirm'), [data-testid='dialog-confirm']");
            if (await dialogConfirm.CountAsync() > 0)
            {
                await dialogConfirm.First.ClickAsync();
            }

            // Verify order status updated
            var statusElement = Page.Locator(".order-status, .status-badge, .current-status, [data-testid='order-status']");
            await Expect(statusElement.First).ToBeVisibleAsync(new() { Timeout = 10000 });
            var statusText = await statusElement.First.TextContentAsync();
            Assert.True(statusText?.ToLower().Contains("confirm") == true || 
                      statusText?.ToLower().Contains("process") == true,
                      $"Order should be confirmed/processed, got: {statusText}");

            _output.WriteLine("Order processed successfully in ShopERP");

            // ** STEP 3: KhachLink UI (Port 5002) - Verify Status Update
            _output.WriteLine("STEP 3: KhachLink UI - Verify Status Update");
            await NavigateToKhachLinkAsync();

            // Navigate to order history/status page
            var orderHistoryLink = Page.Locator("a:has-text('My Orders'), a:has-text('Order History'), a:has-text('Track Order'), [data-testid='order-history-link']");
            await Expect(orderHistoryLink.First).ToBeVisibleAsync(new() { Timeout = 10000 });
            await orderHistoryLink.First.ClickAsync();

            // Find order and check its status
            var orderItems = Page.Locator(".order-item, .order-row, tr.order, [data-testid='order-item']");
            await Expect(orderItems.First).ToBeVisibleAsync(new() { Timeout = 10000 });
            
            // Look for confirmed/processed status
            var orderStatus = Page.Locator(".order-status, .status, .state, [data-testid='order-status']");
            await Expect(orderStatus.First).ToBeVisibleAsync(new() { Timeout = 10000 });
            var finalStatus = await orderStatus.First.TextContentAsync();
            Assert.True(finalStatus?.ToLower().Contains("confirm") == true ||
                      finalStatus?.ToLower().Contains("process") == true ||
                      finalStatus?.ToLower().Contains("ready") == true,
                      $"Order should show confirmed/processed status, got: {finalStatus}");

            // ** COMPLETE FLOW VALIDATION
            _output.WriteLine("COMPLETE ORDER FLOW VALIDATION:");
            _output.WriteLine("  KhachLink: Order created successfully");
            _output.WriteLine("  ShopERP: Order processed and confirmed");
            _output.WriteLine("  KhachLink: Status updated and visible to customer");
            _output.WriteLine("  ** COMPLETE ORDER FLOW TEST PASSED");
        }
    }
}
