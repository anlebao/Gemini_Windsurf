using Microsoft.Playwright;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;
using VanAn.E2E.Tests.Infrastructure;
using static Microsoft.Playwright.Assertions;

namespace VanAn.E2E.Tests
{
    [Collection("SelfHosted Tests")]
    [Trait("Category", "E2E")]
    public class FrozenStateTests : E2ETestBase
    {
        public FrozenStateTests(SelfHostedTestFactory factory, ITestOutputHelper output) 
            : base(factory, output)
        {
        }

        [Fact]
        public async Task SpeedTest_UI_Modals_Should_Work()
        {
            // Navigate to KhachLink
            await NavigateToKhachLinkAsync();

            // Verify page loaded
            await Expect(Page.Locator("body")).ToBeVisibleAsync();
            
            // Add to cart - button should be present (test data seeded)
            var addToCartButton = Page.Locator("button:has-text('Add to Cart'), button:has-text('Thêm vào giỏ'), button:has-text('Add'), button:has-text('Thêm'), [data-testid='add-to-cart-btn']");
            await Expect(addToCartButton.First).ToBeVisibleAsync(new() { Timeout = 10000 });
            await addToCartButton.First.ClickAsync();
            
            // Check for Loyalty modal - if present, verify it auto-closes
            var loyaltyModal = Page.Locator(".modal:has-text('Loyalty'), .modal:has-text('Khách hàng thân thiết'), .modal:has-text('Thành viên'), [data-testid='loyalty-modal']");
            if (await loyaltyModal.CountAsync() > 0)
            {
                await Expect(loyaltyModal.First).ToBeVisibleAsync();
                await Expect(loyaltyModal.First).ToBeHiddenAsync(new() { Timeout = 5000 });
            }

            // Check for App Download modal - if present, verify it auto-closes
            var appModal = Page.Locator(".modal:has-text('App'), .modal:has-text('Tải ứng dụng'), .modal:has-text('Ứng dụng'), [data-testid='app-modal']");
            if (await appModal.CountAsync() > 0)
            {
                await Expect(appModal.First).ToBeVisibleAsync();
                await Expect(appModal.First).ToBeHiddenAsync(new() { Timeout = 5000 });
            }

            // Test passes if we can navigate and interact with the page
            await Expect(Page.Locator("body")).ToBeVisibleAsync();
        }

        [Fact]
        public async Task OfflineFirst_Resilience_Should_Work()
        {
            // Navigate to KhachLink
            await NavigateToKhachLinkAsync();

            // Enable offline mode
            await Page.Context.SetOfflineAsync(true);

            // Try to place an order offline
            var addToCartButton = Page.Locator("button:has-text('Add to Cart'), button:has-text('Thêm vào giỏ'), [data-testid='add-to-cart-btn']");
            await Expect(addToCartButton.First).ToBeVisibleAsync(new() { Timeout = 10000 });
            await addToCartButton.First.ClickAsync();

            // Look for checkout or order button
            var checkoutButton = Page.Locator("button:has-text('Checkout'), button:has-text('Thanh toán'), button:has-text('Order'), button:has-text('Đặt hàng'), [data-testid='checkout-btn']");
            await Expect(checkoutButton.First).ToBeVisibleAsync(new() { Timeout = 10000 });
            await checkoutButton.First.ClickAsync();

            // Verify graceful handling - should show offline message or queue order
            var offlineMessage = Page.Locator(":has-text('offline'), :has-text('mạng'), :has-text('connection'), [data-testid='offline-message']");
            if (await offlineMessage.CountAsync() > 0)
            {
                await Expect(offlineMessage.First).ToBeVisibleAsync();
            }

            // Restore online mode
            await Page.Context.SetOfflineAsync(false);
            await Task.Delay(500); // Brief pause for network to restore

            // Verify page is still functional
            await Expect(Page.Locator("body")).ToBeVisibleAsync();
        }

        [Fact]
        public async Task ShopERP_Should_Load_Successfully()
        {
            // Navigate to ShopERP
            await NavigateToShopERPAsync();

            // Verify login page loads
            await Expect(Page).ToHaveTitleAsync("Van An Cafe - Shop ERP", new() { Timeout = 10000 });
            
            // Verify login form is present
            var loginForm = Page.Locator("form, [data-testid='login-form']");
            await Expect(loginForm.First).ToBeVisibleAsync(new() { Timeout = 10000 });

            // Verify username and password fields
            var usernameField = Page.Locator("input[name='username'], input[type='text'], input[placeholder*='username'], input[placeholder*='tên'], [data-testid='username-input']");
            var passwordField = Page.Locator("input[name='password'], input[type='password'], input[placeholder*='password'], input[placeholder*='mật khẩu'], [data-testid='password-input']");
            
            await Expect(usernameField.First).ToBeVisibleAsync(new() { Timeout = 10000 });
            await Expect(passwordField.First).ToBeVisibleAsync(new() { Timeout = 10000 });
        }
    }
}
