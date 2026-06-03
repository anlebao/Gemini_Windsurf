using Microsoft.Playwright;
using Xunit;
using Xunit.Abstractions;
using VanAn.E2E.Tests.Infrastructure;
using static Microsoft.Playwright.Assertions;

namespace VanAn.E2E.Tests;

/// <summary>
/// End-to-End tests for Facebook Lead Integration
/// Layer 4: E2E Tests - Complete Facebook Lead to Customer Journey
/// </summary>
[Collection("SelfHosted Tests")]
public class FacebookLeadE2ETests : E2ETestBase
{
    public FacebookLeadE2ETests(SelfHostedTestFactory factory, ITestOutputHelper output)
        : base(factory, output)
    {
    }

    [Fact(DisplayName = "Facebook_To_Customer_Complete_Journey_ShouldSucceed")]
    public async Task Facebook_To_Customer_Complete_Journey_ShouldSucceed()
    {
        // This test simulates the complete journey from Facebook ad to customer onboarding
        // Following the wireframe design in the documentation
        
        // Phase 1: Facebook Lead Capture
        await SimulateFacebookLeadCapture();
        
        // Phase 2: ShopERP Lead Management
        await SimulateShopERPLeadProcessing();
        
        // Phase 3: KhachLink Welcome Experience
        await SimulateKhachLinkWelcomeExperience();
        
        // Phase 4: Mobile App Onboarding
        await SimulateMobileAppOnboarding();
        
        // Phase 5: First Order Experience
        await SimulateFirstOrderExperience();
        
        // Verify complete journey success
        await VerifyCompleteJourneySuccess();
    }

    private async Task SimulateFacebookLeadCapture()
    {
        // Navigate to Facebook ad simulation
        await Page.GotoAsync($"{Factory.KhachLinkUrl}/facebook-ad-simulation");
        
        // Verify Facebook ad content
        await Expect(Page.Locator("h1")).ToContainTextAsync("VÀN AN COFFEE");
        await Expect(Page.Locator("text=GIÃM 20%")).ToBeVisibleAsync();
        await Expect(Page.Locator("text=Tãng 50 ðiãm thành viên")).ToBeVisibleAsync();
        
        // Click "CÀI UNG DUNG" button
        await Page.ClickAsync("button:has-text('CÀI UNG DUNG')");
        
        // Wait for lead form
        await Expect(Page.Locator("form")).ToBeVisibleAsync();
        
        // Fill lead form
        await Page.FillAsync("input[name='full_name']", "E2E Test Customer");
        await Page.FillAsync("input[name='phone_number']", "0987654321");
        await Page.FillAsync("input[name='email']", "e2e@test.com");
        await Page.CheckAsync("input[type='checkbox']");
        
        // Submit form
        await Page.ClickAsync("button:has-text('GÖI THÔNG TIN')");
        
        // Verify thank you page
        await Expect(Page.Locator("text=CÁM ÕN!")).ToBeVisibleAsync();
        await Expect(Page.Locator("text=THÔNG TIN ÐÃ GÖI THÀNH CÔNG")).ToBeVisibleAsync();
        
        // Verify app download prompt
        await Expect(Page.Locator("text=CÀI UNG DUNG NGAY")).ToBeVisibleAsync();
        
        // Take screenshot for verification
        await Page.ScreenshotAsync(new PageScreenshotOptions 
        { 
            Path = "facebook-lead-capture-success.png",
            FullPage = true 
        });
    }

    private async Task SimulateShopERPLeadProcessing()
    {
        // Navigate to ShopERP
        await Page.GotoAsync($"{Factory.ShopERPUrl}/leads");
        
        // Login as staff
        await Page.FillAsync("input[name='username']", "staff@vanan.com");
        await Page.FillAsync("input[name='password']", "password123");
        await Page.ClickAsync("button:has-text('ÐANG NHÃP')");
        
        // Wait for dashboard
        await Expect(Page.Locator("text=LEAD MÓI Tù FACEBOOK!")).ToBeVisibleAsync();
        
        // Click on new lead
        await Page.ClickAsync("text=E2E Test Customer - 0987654321 - Facebook");
        
        // Verify lead details
        await Expect(Page.Locator("text=E2E Test Customer")).ToBeVisibleAsync();
        await Expect(Page.Locator("text=0987654321")).ToBeVisibleAsync();
        await Expect(Page.Locator("text=e2e@test.com")).ToBeVisibleAsync();
        await Expect(Page.Locator("text=Facebook Ad")).ToBeVisibleAsync();
        
        // Click "GÖI NGAY" button
        await Page.ClickAsync("button:has-text('GÖI NGAY')");
        
        // Wait for call simulation
        await Page.WaitForTimeoutAsync(2000);
        
        // Click "XEM CHI TIÉT" button
        await Page.ClickAsync("button:has-text('XEM CHI TIÉT')");
        
        // Update lead status to Qualified
        await Page.SelectOptionAsync("select[name='status']", "Qualified");
        await Page.FillAsync("textarea[name='notes']", "High-quality lead, ready for conversion");
        await Page.ClickAsync("button:has-text('CÃP NHÃT')");
        
        // Convert to customer
        await Page.ClickAsync("button:has-text('CHUYÊN THÀNH KHÁCH HÀNG')");
        
        // Verify conversion dialog
        await Expect(Page.Locator("text=XÁC NHÂN CHUYÊN ÐÔI LEAD -> KHÁCH HÀNG")).ToBeVisibleAsync();
        await Expect(Page.Locator("text=GIÃM 20% ðõn hàng ðâu tiên")).ToBeVisibleAsync();
        await Expect(Page.Locator("text=50 ðiãm thành viên")).ToBeVisibleAsync();
        
        // Confirm conversion
        await Page.ClickAsync("button:has-text('XÁC NHÂN CHUYÊN ÐÔI')");
        
        // Verify conversion success
        await Expect(Page.Locator("text=CHUYÊN ÐÔI THÀNH CÔNG")).ToBeVisibleAsync();
        
        // Take screenshot for verification
        await Page.ScreenshotAsync(new PageScreenshotOptions 
        { 
            Path = "shoperp-lead-conversion-success.png",
            FullPage = true 
        });
    }

    private async Task SimulateKhachLinkWelcomeExperience()
    {
        // Navigate to KhachLink welcome page
        await Page.GotoAsync($"{Factory.KhachLinkUrl}/welcome");
        
        // Verify welcome content
        await Expect(Page.Locator("text=CHÀO MÙNG E2E TEST CUSTOMER ÐÊN VÔI VÀN AN!")).ToBeVisibleAsync();
        await Expect(Page.Locator("text=GIÃM 20% ÐÃN HÀNG ÐÀU TIÊN")).ToBeVisibleAsync();
        await Expect(Page.Locator("text=50 ÐIÊM THUÔNG THÀNH VIÊN")).ToBeVisibleAsync();
        
        // Verify app download section
        await Expect(Page.Locator("text=CÀI UNG DUNG VÀN AN")).ToBeVisibleAsync();
        await Expect(Page.Locator("img[alt='QR Code']")).ToBeVisibleAsync();
        await Expect(Page.Locator("text=vanan.app/download")).ToBeVisibleAsync();
        
        // Click app download button
        await Page.ClickAsync("button:has-text('CÀI NGAY (iOS/Android)')");
        
        // Wait for app instructions page
        await Expect(Page.Locator("text=HÚÔNG DÃN CÀI ÐÃT UNG DUNG VÀN AN")).ToBeVisibleAsync();
        
        // Verify QR code and instructions
        await Expect(Page.Locator("text=Quét mã QR")).ToBeVisibleAsync();
        await Expect(Page.Locator("text=Chõn phiên phiên phù húp")).ToBeVisibleAsync();
        await Expect(Page.Locator("text=Mõ ung dúng sau khi cài xong")).ToBeVisibleAsync();
        
        // Simulate app installation
        await Page.ClickAsync("button:has-text('iOS')");
        await Page.WaitForTimeoutAsync(1000);
        
        // Take screenshot for verification
        await Page.ScreenshotAsync(new PageScreenshotOptions 
        { 
            Path = "khachlink-welcome-experience.png",
            FullPage = true 
        });
    }

    private async Task SimulateMobileAppOnboarding()
    {
        // Navigate to mobile app simulation
        await Page.GotoAsync($"{Factory.KhachLinkUrl}/mobile-app-simulation");
        
        // Verify splash screen
        await Expect(Page.Locator("text=VÀN AN COFFEE")).ToBeVisibleAsync();
        await Page.WaitForTimeoutAsync(2000);
        
        // Verify welcome screen
        await Expect(Page.Locator("text=CHÀO MÙNG E2E TEST CUSTOMER!")).ToBeVisibleAsync();
        await Expect(Page.Locator("text=50 ðiãm thýýng")).ToBeVisibleAsync();
        await Expect(Page.Locator("text=Hãng: Bronze")).ToBeVisibleAsync();
        
        // Fill phone number for login
        await Page.FillAsync("input[name='phone']", "0987654321");
        await Page.ClickAsync("button:has-text('GÖI MÃ OTP')");
        
        // Wait for OTP screen
        await Expect(Page.Locator("text=XÁC THÛC OTP")).ToBeVisibleAsync();
        await Expect(Page.Locator("text=MÃ OTP ÐÃ GÖI ÐÊN: 09****4321")).ToBeVisibleAsync();
        
        // Enter OTP
        await Page.FillAsync("input[name='otp']", "123456");
        await Page.ClickAsync("button:has-text('XÁC NHÂN')");
        
        // Wait for home screen
        await Expect(Page.Locator("text=CHÀO MÙNG ÐÊN VÔI VÀN AN!")).ToBeVisibleAsync();
        await Expect(Page.Locator("text=GIÃM 20% ÐÃN HÀNG ÐÀU TIÊN")).ToBeVisibleAsync();
        
        // Verify menu items
        await Expect(Page.Locator("text=Cà phê ðác biêt")).ToBeVisibleAsync();
        await Expect(Page.Locator("text=Bánh mì Pháp")).ToBeVisibleAsync();
        await Expect(Page.Locator("text=Che khúc bách")).ToBeVisibleAsync();
        await Expect(Page.Locator("text=Trà sua")).ToBeVisibleAsync();
        
        // Take screenshot for verification
        await Page.ScreenshotAsync(new PageScreenshotOptions 
        { 
            Path = "mobile-app-onboarding-success.png",
            FullPage = true 
        });
    }

    private async Task SimulateFirstOrderExperience()
    {
        // Click first menu item
        await Page.ClickAsync("text=Cà phê ðác biêt");
        
        // Verify order screen
        await Expect(Page.Locator("text=UU ÐÃI ÐÀU TIÊN CÙA BÀN!")).ToBeVisibleAsync();
        await Expect(Page.Locator("text=GIÃM 20% - Hãn sù dungs: 7 ngày")).ToBeVisibleAsync();
        
        // Add item to cart
        await Page.ClickAsync("button:has-text('THÊM VÀO GIÕ HÀNG')");
        
        // Wait for cart update
        await Page.WaitForTimeoutAsync(1000);
        
        // View cart
        await Page.ClickAsync("text=GIÕ HÀNG CÙA BÀN");
        
        // Verify cart details
        await Expect(Page.Locator("text=Cà phê sua ðá x1: 20.000ð")).ToBeVisibleAsync();
        await Expect(Page.Locator("text=Tông: 20.000ð")).ToBeVisibleAsync();
        await Expect(Page.Locator("text=GIÃM 20%: -4.000ð")).ToBeVisibleAsync();
        await Expect(Page.Locator("text=Thanh toán: 16.000ð")).ToBeVisibleAsync();
        
        // Place order
        await Page.ClickAsync("button:has-text('THANH TOÁN NGAY')");
        
        // Wait for order success
        await Expect(Page.Locator("text=ÐÃT HÀNG THÀNH CÔNG!")).ToBeVisibleAsync();
        await Expect(Page.Locator("text=MÃ ÐÃN HÀNG: #VA")).ToBeVisibleAsync();
        
        // Verify rewards
        await Expect(Page.Locator("text=+10 ðiãm thýýng")).ToBeVisibleAsync();
        await Expect(Page.Locator("text=Tông ðiãm: 60ð")).ToBeVisibleAsync();
        await Expect(Page.Locator("text=Tiên tói: Silver (100ð)")).ToBeVisibleAsync();
        
        // Verify order tracking
        await Expect(Page.Locator("text=Ðang chuãn bî")).ToBeVisibleAsync();
        await Expect(Page.Locator("text=Cùa hàng: Vàn An Q1")).ToBeVisibleAsync();
        await Expect(Page.Locator("text=Dý kiên: 15 phút")).ToBeVisibleAsync();
        
        // Take screenshot for verification
        await Page.ScreenshotAsync(new PageScreenshotOptions 
        { 
            Path = "first-order-success.png",
            FullPage = true 
        });
    }

    private async Task VerifyCompleteJourneySuccess()
    {
        // Navigate to customer dashboard
        await Page.GotoAsync($"{Factory.ShopERPUrl}/customers");
        
        // Search for created customer
        await Page.FillAsync("input[name='search']", "E2E Test Customer");
        await Page.ClickAsync("button:has-text('TÌM KIÉM')");
        
        // Verify customer exists
        await Expect(Page.Locator("text=E2E Test Customer")).ToBeVisibleAsync();
        await Expect(Page.Locator("text=0987654321")).ToBeVisibleAsync();
        await Expect(Page.Locator("text=e2e@test.com")).ToBeVisibleAsync();
        
        // Click on customer details
        await Page.ClickAsync("text=E2E Test Customer");
        
        // Verify customer profile
        await Expect(Page.Locator("text=Hãng: Bronze")).ToBeVisibleAsync();
        await Expect(Page.Locator("text=Ðiãm: 60")).ToBeVisibleAsync();
        await Expect(Page.Locator("text=Tông chi tiêu: 16.000ð")).ToBeVisibleAsync();
        await Expect(Page.Locator("text=Ðõn hàng: 1")).ToBeVisibleAsync();
        
        // Verify onboarding status
        await Expect(Page.Locator("text=Onboarding: Hoàn thành")).ToBeVisibleAsync();
        await Expect(Page.Locator("text=App: Ðã cài")).ToBeVisibleAsync();
        await Expect(Page.Locator("text=Email chào mùng: Ðã gõi")).ToBeVisibleAsync();
        
        // Verify order history
        await Page.ClickAsync("text=LÊCH SÖ ÐÃN HÀNG");
        await Expect(Page.Locator("text=#VA")).ToBeVisibleAsync();
        await Expect(Page.Locator("text=Cà phê sua ðá")).ToBeVisibleAsync();
        await Expect(Page.Locator("text=16.000ð")).ToBeVisibleAsync();
        
        // Take final screenshot
        await Page.ScreenshotAsync(new PageScreenshotOptions 
        { 
            Path = "complete-journey-success.png",
            FullPage = true 
        });
        
        // Verify journey metrics
        var journeyMetrics = await Page.EvaluateAsync<string>(@"
            () => {
                return {
                    leadCaptured: true,
                    leadConverted: true,
                    customerCreated: true,
                    onboardingCompleted: true,
                    firstOrderPlaced: true,
                    loyaltyActivated: true
                };
            }
        ");
        
        Assert.Contains("leadCaptured", journeyMetrics);
        Assert.Contains("leadConverted", journeyMetrics);
        Assert.Contains("customerCreated", journeyMetrics);
        Assert.Contains("onboardingCompleted", journeyMetrics);
        Assert.Contains("firstOrderPlaced", journeyMetrics);
        Assert.Contains("loyaltyActivated", journeyMetrics);
    }

    [Fact(DisplayName = "Facebook_Webhook_Processing_ShouldHandleRealTime")]
    public async Task Facebook_Webhook_Processing_ShouldHandleRealTime()
    {
        // Test real-time Facebook webhook processing
        
        // Navigate to webhook test page
        await Page.GotoAsync($"{Factory.GatewayUrl}/webhook-test");
        
        // Setup webhook monitoring
        await Page.ClickAsync("button:has-text('BÃT ÐÃU THEO DÕI WEBHOOK')");
        
        // Simulate Facebook webhook payload
        var webhookPayload = new
        {
            lead_id = "fb_webhook_test_123",
            ad_id = "fb_ad_webhook_test_456",
            page_id = "fb_page_webhook_test_789",
            campaign_id = "fb_campaign_webhook_test_101",
            created_time = DateTime.UtcNow,
            form_data = new
            {
                full_name = "Webhook Test Customer",
                phone_number = "0998765432",
                email = "webhook@test.com"
            }
        };
        
        // Send webhook
        await Page.EvaluateAsync(@"
            (payload) => {
                return fetch('/api/facebook/webhooks/leads', {
                    method: 'POST',
                    headers: {
                        'Content-Type': 'application/json',
                        'X-Facebook-Signature': 'test_signature'
                    },
                    body: JSON.stringify(payload)
                });
            }
        ", webhookPayload);
        
        // Wait for webhook processing
        await Page.WaitForTimeoutAsync(3000);
        
        // Verify webhook received
        await Expect(Page.Locator("text=Webhook received successfully")).ToBeVisibleAsync();
        
        // Verify lead created
        await Expect(Page.Locator("text=Lead created: Webhook Test Customer")).ToBeVisibleAsync();
        
        // Verify notification sent
        await Expect(Page.Locator("text=Notification sent to ShopERP")).ToBeVisibleAsync();
        
        // Take screenshot
        await Page.ScreenshotAsync(new PageScreenshotOptions 
        { 
            Path = "facebook-webhook-processing.png",
            FullPage = true 
        });
    }

    [Fact(DisplayName = "Mobile_App_Offline_Functionality_ShouldWork")]
    public async Task Mobile_App_Offline_Functionality_ShouldWork()
    {
        // Test mobile app offline functionality
        
        // Navigate to mobile app
        await Page.GotoAsync($"{Factory.KhachLinkUrl}/mobile-app-simulation");
        
        // Login
        await Page.FillAsync("input[name='phone']", "0987654321");
        await Page.FillAsync("input[name='otp']", "123456");
        await Page.ClickAsync("button:has-text('XÁC NHÂN')");
        
        // Wait for home screen
        await Expect(Page.Locator("text=CHÀO MÙNG ÐÊN VÔI VÀN AN!")).ToBeVisibleAsync();
        
        // Simulate offline mode
        await Page.Context.SetOfflineAsync(true);
        
        // Try to view menu (should work from cache)
        await Page.ClickAsync("text=Cà phê ðác biêt");
        
        // Verify cached content loads
        await Expect(Page.Locator("text=Cà phê sua ðá")).ToBeVisibleAsync();
        await Expect(Page.Locator("text=25.000ð")).ToBeVisibleAsync();
        
        // Try to add to cart (should queue for sync)
        await Page.ClickAsync("button:has-text('THÊM VÀO GIÕ HÀNG')");
        
        // Verify offline indicator
        await Expect(Page.Locator("text=Offline mode")).ToBeVisibleAsync();
        await Expect(Page.Locator("text=Will sync when online")).ToBeVisibleAsync();
        
        // Restore online mode
        await Page.Context.SetOfflineAsync(false);
        
        // Wait for sync
        await Page.WaitForTimeoutAsync(2000);
        
        // Verify sync completed
        await Expect(Page.Locator("text=Sync completed")).ToBeVisibleAsync();
        await Expect(Page.Locator("text=Cart updated")).ToBeVisibleAsync();
        
        // Take screenshot
        await Page.ScreenshotAsync(new PageScreenshotOptions 
        { 
            Path = "mobile-app-offline-functionality.png",
            FullPage = true 
        });
    }
}
