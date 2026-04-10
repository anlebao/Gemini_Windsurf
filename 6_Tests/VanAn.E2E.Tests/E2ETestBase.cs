using Microsoft.Playwright;
using Microsoft.AspNetCore.Mvc.Testing;
using System;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;
using VanAn.E2E.Tests.Infrastructure;

namespace VanAn.E2E.Tests;

/// <summary>
/// Base test class for E2E tests with Playwright setup
/// </summary>
public class E2ETestBase : IAsyncLifetime
{
    private IPlaywright? _playwright;
    private IBrowser? _browser;
    private IPage? _page;
    private readonly SelfHostedTestFactory _factory;
    private readonly ITestOutputHelper _output;

    public IPage Page => _page ?? throw new InvalidOperationException("Page not initialized");
    public IBrowser Browser => _browser ?? throw new InvalidOperationException("Browser not initialized");
    public IPlaywright Playwright => _playwright ?? throw new InvalidOperationException("Playwright not initialized");
    public SelfHostedTestFactory Factory => _factory;

    public E2ETestBase(SelfHostedTestFactory factory, ITestOutputHelper output)
    {
        _factory = factory;
        _output = output;
    }

    public async Task InitializeAsync()
    {
        // Initialize Playwright
        _playwright = await Microsoft.Playwright.Playwright.CreateAsync();

        // Configure browser options for CINEMATIC UAT
        var browserOptions = new BrowserTypeLaunchOptions
        {
            Headless = false, // HEADED MODE for Architect demonstration
            SlowMo = 2000, // 2s delay - cinematic slow motion
            Args = new[]
            {
                "--disable-web-security",
                "--disable-features=VizDisplayCompositor",
                "--no-sandbox",
                "--disable-setuid-sandbox",
                "--start-maximized", // Start maximized for better visibility
                "--disable-dev-shm-usage"
            }
        };

        // Launch browser
        _browser = await _playwright.Chromium.LaunchAsync(browserOptions);

        // Create new page
        _page = await _browser.NewPageAsync();
        
        // Set desktop viewport for better visibility during UAT
        await _page.SetViewportSizeAsync(1920, 1080);
        
        // Set user agent for desktop testing during UAT
        await _page.SetExtraHTTPHeadersAsync(new Dictionary<string, string>
        {
            ["User-Agent"] = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36"
        });
    }

    public async Task DisposeAsync()
    {
        if (_page != null)
        {
            await _page.CloseAsync();
            _page = null;
        }

        if (_browser != null)
        {
            await _browser.CloseAsync();
            _browser = null;
        }

        if (_playwright != null)
        {
            _playwright.Dispose();
            _playwright = null;
        }
    }

    /// <summary>
    /// Switch to desktop viewport for ShopERP testing
    /// </summary>
    public async Task SwitchToDesktopAsync()
    {
        if (_page == null) throw new InvalidOperationException("Page not initialized");
        
        await _page.SetViewportSizeAsync(1920, 1080);
        await _page.SetExtraHTTPHeadersAsync(new Dictionary<string, string>
        {
            ["User-Agent"] = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36"
        });
    }

    /// <summary>
    /// Switch to tablet viewport for cross-device testing
    /// </summary>
    public async Task SwitchToTabletAsync()
    {
        if (_page == null) throw new InvalidOperationException("Page not initialized");
        
        await _page.SetViewportSizeAsync(768, 1024);
        await _page.SetExtraHTTPHeadersAsync(new Dictionary<string, string>
        {
            ["User-Agent"] = "Mozilla/5.0 (iPad; CPU OS 14_0 like Mac OS X) AppleWebKit/605.1.15"
        });
    }

    /// <summary>
    /// Take screenshot for debugging and visual regression
    /// </summary>
    public async Task TakeScreenshotAsync(string testName)
    {
        if (_page == null) throw new InvalidOperationException("Page not initialized");
        
        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        var fileName = $"screenshot_{testName}_{timestamp}.png";
        
        await _page.ScreenshotAsync(new PageScreenshotOptions
        {
            Path = fileName,
            FullPage = true
        });
    }

    /// <summary>
    /// Wait for network idle to ensure page is fully loaded
    /// </summary>
    public async Task WaitForNetworkIdleAsync()
    {
        if (_page == null) throw new InvalidOperationException("Page not initialized");
        
        await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);
    }

    /// <summary>
    /// Navigate to application with proper error handling
    /// </summary>
    public async Task NavigateToAppAsync(string path = "")
    {
        if (_page == null) throw new InvalidOperationException("Page not initialized");
        
        var baseUrl = "http://localhost:5002"; // KhachLink local development URL
        var fullUrl = $"{baseUrl}/{path.TrimStart('/')}";
        
        try
        {
            await _page.GotoAsync(fullUrl, new PageGotoOptions
            {
                WaitUntil = WaitUntilState.NetworkIdle,
                Timeout = 30000
            });
            
            await WaitForNetworkIdleAsync();
        }
        catch (Exception ex)
        {
            await TakeScreenshotAsync($"navigation_error_{path}");
            throw new InvalidOperationException($"Failed to navigate to {fullUrl}: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Navigate to KhachLink mobile app
    /// </summary>
    public async Task NavigateToKhachLinkAsync(string path = "")
    {
        await NavigateToAppAsync(path); // KhachLink runs on localhost:5002
    }

    /// <summary>
    /// Navigate to ShopERP desktop app
    /// </summary>
    public async Task NavigateToShopERPAsync(string path = "")
    {
        await SwitchToDesktopAsync();
        
        if (_page == null) throw new InvalidOperationException("Page not initialized");
        
        var baseUrl = "http://localhost:5003"; // ShopERP local development URL
        var fullUrl = $"{baseUrl}/{path.TrimStart('/')}";
        
        try
        {
            await _page.GotoAsync(fullUrl, new PageGotoOptions
            {
                WaitUntil = WaitUntilState.NetworkIdle,
                Timeout = 30000
            });
            
            await WaitForNetworkIdleAsync();
        }
        catch (Exception ex)
        {
            await TakeScreenshotAsync($"navigation_error_shoperp_{path}");
            throw new InvalidOperationException($"Failed to navigate to {fullUrl}: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Navigate to admin dashboard
    /// </summary>
    public async Task NavigateToAdminAsync(string path = "")
    {
        await SwitchToDesktopAsync();
        await NavigateToShopERPAsync($"admin/{path.TrimStart('/')}"); // Admin runs on ShopERP
    }

    /// <summary>
    /// Helper method to wait for element to be visible and clickable
    /// </summary>
    protected async Task<IElementHandle> WaitForElementAsync(string selector, int timeoutMs = 10000)
    {
        return await Page.WaitForSelectorAsync(selector, new PageWaitForSelectorOptions
        {
            State = WaitForSelectorState.Visible,
            Timeout = timeoutMs
        }) ?? throw new InvalidOperationException($"Element '{selector}' not found");
    }

    /// <summary>
    /// Helper method to click element with retry logic
    /// </summary>
    protected async Task ClickElementAsync(string selector, int maxRetries = 3)
    {
        for (int i = 0; i < maxRetries; i++)
        {
            try
            {
                var element = await WaitForElementAsync(selector);
                await element.ClickAsync();
                return;
            }
            catch (Exception) when (i < maxRetries - 1)
            {
                await Task.Delay(1000);
                continue;
            }
        }
        
        throw new InvalidOperationException($"Failed to click element {selector} after {maxRetries} attempts");
    }

    /// <summary>
    /// Helper method to fill input field
    /// </summary>
    protected async Task FillInputAsync(string selector, string value)
    {
        var element = await WaitForElementAsync(selector);
        await element.FillAsync(value);
    }

    /// <summary>
    /// Helper method to get text content
    /// </summary>
    protected async Task<string> GetTextAsync(string selector)
    {
        var element = await WaitForElementAsync(selector);
        return await element.TextContentAsync() ?? string.Empty;
    }

    /// <summary>
    /// Helper method to check if element exists
    /// </summary>
    protected async Task<bool> ElementExistsAsync(string selector)
    {
        try
        {
            await Page.WaitForSelectorAsync(selector, new PageWaitForSelectorOptions
            {
                State = WaitForSelectorState.Attached,
                Timeout = 5000
            });
            return true;
        }
        catch
        {
            return false;
        }
    }
}
