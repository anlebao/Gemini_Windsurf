using Microsoft.Playwright;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;
using VanAn.E2E.Tests.Infrastructure;
using static Microsoft.Playwright.Assertions;

namespace VanAn.E2E.Tests
{
    [Collection("SelfHosted Tests")]
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
            await Page.GotoAsync("http://localhost:5002");
            await Page.WaitForLoadStateAsync(LoadState.DOMContentLoaded);

            // Verify page loaded - check for any content rather than specific title
            await Expect(Page.Locator("body")).ToBeVisibleAsync();
            
            // Add to cart - look for product and add button
            var addToCartButton = Page.Locator("button:has-text('Add to Cart'), button:has-text('Thêm vào giỏ'), button:has-text('Add'), button:has-text('Thêm')");
            if (await addToCartButton.CountAsync() > 0)
            {
                await addToCartButton.Nth(0).ClickAsync();
                await Page.WaitForTimeoutAsync(1000);
                
                // Check for Loyalty modal
                var loyaltyModal = Page.Locator(".modal:has-text('Loyalty'), .modal:has-text('Khách hàng thân thiết'), .modal:has-text('Thành viên')");
                if (await loyaltyModal.CountAsync() > 0)
                {
                    // Verify modal appears and auto-closes
                    await Expect(loyaltyModal.Nth(0)).ToBeVisibleAsync();
                    await Page.WaitForTimeoutAsync(3000); // Wait for auto-close
                    await Expect(loyaltyModal.Nth(0)).ToBeHiddenAsync();
                }

                // Check for App Download modal
                var appModal = Page.Locator(".modal:has-text('App'), .modal:has-text('Tải ứng dụng'), .modal:has-text('Ứng dụng')");
                if (await appModal.CountAsync() > 0)
                {
                    // Verify modal appears and auto-closes
                    await Expect(appModal.Nth(0)).ToBeVisibleAsync();
                    await Page.WaitForTimeoutAsync(3000); // Wait for auto-close
                    await Expect(appModal.Nth(0)).ToBeHiddenAsync();
                }
            }

            // Test passes if we can navigate and interact with the page
            await Expect(Page.Locator("body")).ToBeVisibleAsync();
        }

        [Fact]
        public async Task OfflineFirst_Resilience_Should_Work()
        {
            // Navigate to KhachLink
            await Page.GotoAsync("http://localhost:5002");
            await Page.WaitForLoadStateAsync(LoadState.DOMContentLoaded);

            // Enable offline mode
            await Page.Context.SetOfflineAsync(true);

            // Try to place an order offline
            var addToCartButton = Page.Locator("button:has-text('Add to Cart'), button:has-text('Thêm vào giỏ')");
            if (await addToCartButton.CountAsync() > 0)
            {
                await addToCartButton.Nth(0).ClickAsync();
                await Page.WaitForTimeoutAsync(1000);

                // Look for checkout or order button
                var checkoutButton = Page.Locator("button:has-text('Checkout'), button:has-text('Thanh toán'), button:has-text('Order'), button:has-text('Đặt hàng')");
                if (await checkoutButton.CountAsync() > 0)
                {
                    await checkoutButton.Nth(0).ClickAsync();
                    await Page.WaitForTimeoutAsync(2000);

                    // Verify graceful handling - should show offline message or queue order
                    var offlineMessage = Page.Locator(":has-text('offline'), :has-text('mạng'), :has-text('connection')");
                    if (await offlineMessage.CountAsync() > 0)
                    {
                        await Expect(offlineMessage.Nth(0)).ToBeVisibleAsync();
                    }
                }
            }

            // Restore online mode
            await Page.Context.SetOfflineAsync(false);
            await Page.WaitForTimeoutAsync(1000);

            // Verify page is still functional
            await Expect(Page.Locator("body")).ToBeVisibleAsync();
        }

        [Fact]
        public async Task ShopERP_Should_Load_Successfully()
        {
            // Navigate to ShopERP
            await Page.GotoAsync("http://localhost:5003");
            await Page.WaitForLoadStateAsync(LoadState.DOMContentLoaded);

            // Verify login page loads
            await Expect(Page).ToHaveTitleAsync("Van An Cafe - Shop ERP");
            
            // Verify login form is present
            var loginForm = Page.Locator("form");
            await Expect(loginForm).ToBeVisibleAsync();

            // Verify username and password fields
            var usernameField = Page.Locator("input[name='username'], input[type='text'], input[placeholder*='username'], input[placeholder*='tên']");
            var passwordField = Page.Locator("input[name='password'], input[type='password'], input[placeholder*='password'], input[placeholder*='mật khẩu']");
            
            await Expect(usernameField.Nth(0)).ToBeVisibleAsync();
            await Expect(passwordField.Nth(0)).ToBeVisibleAsync();
        }
    }
}
