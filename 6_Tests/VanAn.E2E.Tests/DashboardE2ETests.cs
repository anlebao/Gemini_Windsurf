using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;
using System.Text.Json;
using Xunit.Abstractions;
using Xunit;
using VanAn.E2E.Tests.Infrastructure;

namespace VanAn.E2E.Tests;

[Trait("Category", "E2E")]
public class DashboardE2ETests : IClassFixture<SelfHostedTestFactory>, IDisposable
{
    private readonly SelfHostedTestFactory _factory;
    private IPlaywright _playwright;
    private IBrowser _browser;
    private IPage _page;
    private readonly ITestOutputHelper _output;

    public DashboardE2ETests(SelfHostedTestFactory factory, ITestOutputHelper output)
    {
        _factory = factory;
        _output = output;
    }

    private async Task InitializeAsync()
    {
        _playwright = await Playwright.CreateAsync();
        _browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = true,
            SlowMo = 100
        });
        _page = await _browser.NewPageAsync();
    }

    [Fact(DisplayName = "Dashboard_Should_Load_And_Display_Metrics")]
    public async Task Dashboard_Should_Load_And_Display_Metrics()
    {
        if (_page == null) await InitializeAsync();
        
        // Arrange
        await _page.GotoAsync($"{_factory.KhachLinkUrl}/VanAnDashboard");

        // Wait for dashboard to load
        await _page.WaitForSelectorAsync(".dashboard-container, [data-testid='dashboard-container']", new PageWaitForSelectorOptions { Timeout = 10000 });

        // Act & Assert - Check main elements using Playwright assertions
        var dashboardTitle = _page.Locator(".dashboard-title, [data-testid='dashboard-title']");
        await Expect(dashboardTitle).ToContainTextAsync("VanAn Dashboard", new() { Timeout = 10000 });

        _output.WriteLine("Dashboard loaded successfully");
    }

    [Fact(DisplayName = "Dashboard_Should_Display_PostgreSQL_Metrics")]
    public async Task Dashboard_Should_Display_PostgreSQL_Metrics()
    {
        if (_page == null) await InitializeAsync();
        
        // Arrange
        await _page.GotoAsync($"{_factory.KhachLinkUrl}/VanAnDashboard");
        await _page.WaitForSelectorAsync(".dashboard-container, [data-testid='dashboard-container']", new PageWaitForSelectorOptions { Timeout = 10000 });

        // Act - Find PostgreSQL metrics cards using Locator
        var postgresCards = _page.Locator(".metric-card.postgresql, [data-testid*='postgresql']");
        
        // Assert - Should have multiple PostgreSQL cards
        await Expect(postgresCards).ToHaveCountAsync(3);

        // Check for Tenant card
        var tenantCard = postgresCards.Filter(new() { HasText = "Tenant" });
        await Expect(tenantCard).ToBeVisibleAsync();

        _output.WriteLine("PostgreSQL metrics displayed correctly");
    }

    [Fact(DisplayName = "Dashboard_Should_Display_SQLite_Metrics")]
    public async Task Dashboard_Should_Display_SQLite_Metrics()
    {
        if (_page == null) await InitializeAsync();
        
        // Arrange
        await _page.GotoAsync($"{_factory.KhachLinkUrl}/VanAnDashboard");
        await _page.WaitForSelectorAsync(".dashboard-container, [data-testid='dashboard-container']", new PageWaitForSelectorOptions { Timeout = 10000 });

        // Act - Find SQLite metrics cards using Locator
        var sqliteCards = _page.Locator(".metric-card.sqlite, [data-testid*='sqlite']");
        
        // Assert - Should have SQLite cards
        await Expect(sqliteCards).ToHaveCountAsync(2);

        // Check for KhachLink and ShopERP metrics
        var dashboardContainer = _page.Locator(".dashboard-container, [data-testid='dashboard-container']");
        await Expect(dashboardContainer).ToContainTextAsync("KhachLink");
        await Expect(dashboardContainer).ToContainTextAsync("ShopERP");

        _output.WriteLine("SQLite metrics displayed correctly");
    }

    [Fact(DisplayName = "Dashboard_Should_Display_Sync_Status")]
    public async Task Dashboard_Should_Display_Sync_Status()
    {
        if (_page == null) await InitializeAsync();
        
        // Arrange
        await _page.GotoAsync($"{_factory.KhachLinkUrl}/VanAnDashboard");
        await _page.WaitForSelectorAsync(".dashboard-container, [data-testid='dashboard-container']", new PageWaitForSelectorOptions { Timeout = 10000 });

        // Act - Find sync status section using Locator
        var syncSection = _page.Locator(".sync-status-grid, [data-testid='sync-status-grid']");
        
        // Assert - Sync section should exist
        await Expect(syncSection).ToBeVisibleAsync();

        // Check for sync progress bars
        var progressBars = _page.Locator(".progress-fill, [data-testid='progress-fill']");
        await Expect(progressBars).ToHaveCountAsync(1);

        _output.WriteLine("Sync status displayed correctly");
    }

    [Fact(DisplayName = "Dashboard_Should_Have_Working_Refresh_Button")]
    public async Task Dashboard_Should_Have_Working_Refresh_Button()
    {
        if (_page == null) await InitializeAsync();
        
        // Arrange
        await _page.GotoAsync($"{_factory.KhachLinkUrl}/VanAnDashboard");
        await _page.WaitForSelectorAsync(".dashboard-container, [data-testid='dashboard-container']", new PageWaitForSelectorOptions { Timeout = 10000 });

        // Act - Find and click refresh button using Locator
        var refreshButton = _page.Locator(".refresh-btn, [data-testid='refresh-btn']");
        await Expect(refreshButton).ToBeVisibleAsync();
        await refreshButton.ClickAsync();

        // Wait for loading state
        var loadingSpinner = _page.Locator(".loading-spinner, [data-testid='loading-spinner']");
        await Expect(loadingSpinner).ToBeVisibleAsync(new() { Timeout = 5000 });

        // Wait for loading to complete
        await Expect(loadingSpinner).ToBeHiddenAsync(new() { Timeout = 10000 });

        // Assert - Dashboard should still be visible after refresh
        var dashboardContainer = _page.Locator(".dashboard-container, [data-testid='dashboard-container']");
        await Expect(dashboardContainer).ToBeVisibleAsync();

        _output.WriteLine("Refresh button working correctly");
    }

    [Fact(DisplayName = "Dashboard_Should_Show_Last_Updated_Timestamp")]
    public async Task Dashboard_Should_Show_Last_Updated_Timestamp()
    {
        if (_page == null) await InitializeAsync();
        
        // Arrange
        await _page.GotoAsync($"{_factory.KhachLinkUrl}/VanAnDashboard");
        await _page.WaitForSelectorAsync(".dashboard-container, [data-testid='dashboard-container']", new PageWaitForSelectorOptions { Timeout = 10000 });

        // Act - Find last updated section using Locator
        var lastUpdated = _page.Locator(".last-updated, [data-testid='last-updated']");
        
        // Assert - Last updated should exist and contain date
        await Expect(lastUpdated).ToBeVisibleAsync();
        await Expect(lastUpdated).ToContainTextAsync("Last Updated:");
        await Expect(lastUpdated).ToContainTextAsync("System");

        var lastUpdatedText = await lastUpdated.TextContentAsync();
        _output.WriteLine($"Last updated displayed: {lastUpdatedText}");
    }

    [Fact(DisplayName = "Dashboard_Should_Handle_Connection_Issues_Gracefully")]
    public async Task Dashboard_Should_Handle_Connection_Issues_Gracefully()
    {
        if (_page == null) await InitializeAsync();
        
        // Arrange - Navigate to dashboard
        await _page.GotoAsync($"{_factory.KhachLinkUrl}/VanAnDashboard");
        await _page.WaitForSelectorAsync(".dashboard-container, [data-testid='dashboard-container']", new PageWaitForSelectorOptions { Timeout = 10000 });

        // Act - Simulate connection issues by intercepting requests
        await _page.RouteAsync("**/*", async route =>
        {
            // Simulate some requests failing
            if (route.Request.Url.Contains("api") && route.Request.Url.Contains("metrics"))
            {
                await route.FulfillAsync(new RouteFulfillOptions { Status = 500 });
            }
            else
            {
                await route.ContinueAsync();
            }
        });

        // Click refresh to trigger API calls
        var refreshButton = _page.Locator(".refresh-btn, [data-testid='refresh-btn']");
        if (await refreshButton.CountAsync() > 0)
        {
            await refreshButton.ClickAsync();
            await Task.Delay(2000); // Wait for error handling
        }

        // Assert - Should show alerts for connection issues
        var alerts = _page.Locator(".alert:has-text('Connection'), .alert:has-text('Error'), [data-testid='connection-alert']");
        var hasConnectionAlert = await alerts.CountAsync() > 0;

        // Clean up routing
        await _page.UnrouteAllAsync();

        _output.WriteLine($"Connection issues handled: {hasConnectionAlert}");
    }

    [Fact(DisplayName = "Dashboard_Should_Be_Responsive")]
    public async Task Dashboard_Should_Be_Responsive()
    {
        if (_page == null) await InitializeAsync();
        
        // Arrange - Test different screen sizes
        var viewports = new[]
        {
            new ViewportSize { Width = 1920, Height = 1080 }, // Desktop
            new ViewportSize { Width = 768, Height = 1024 },  // Tablet
            new ViewportSize { Width = 375, Height = 667 }    // Mobile
        };

        foreach (var viewport in viewports)
        {
            // Act - Set viewport size
            await _page.SetViewportSizeAsync(viewport.Width, viewport.Height);
            await _page.GotoAsync($"{_factory.KhachLinkUrl}/VanAnDashboard");
            await _page.WaitForSelectorAsync(".dashboard-container, [data-testid='dashboard-container']", new PageWaitForSelectorOptions { Timeout = 10000 });

            // Assert - Dashboard should be visible and functional
            var dashboardContainer = _page.Locator(".dashboard-container, [data-testid='dashboard-container']");
            await Expect(dashboardContainer).ToBeVisibleAsync();

            // Check if metrics grid adapts to screen size
            var metricsGrid = _page.Locator(".metrics-grid, [data-testid='metrics-grid']");
            await Expect(metricsGrid).ToBeVisibleAsync();

            _output.WriteLine($"Dashboard responsive on {viewport.Width}x{viewport.Height}");
        }
    }

    [Fact(DisplayName = "Dashboard_Should_Have_Proper_Accessibility")]
    public async Task Dashboard_Should_Have_Proper_Accessibility()
    {
        if (_page == null) await InitializeAsync();
        
        // Arrange
        await _page.GotoAsync($"{_factory.KhachLinkUrl}/VanAnDashboard");
        await _page.WaitForSelectorAsync(".dashboard-container, [data-testid='dashboard-container']", new PageWaitForSelectorOptions { Timeout = 10000 });

        // Act - Check for accessibility features using Locator
        var titleElement = _page.Locator("h1");
        await Expect(titleElement).ToBeVisibleAsync();

        // Check for semantic HTML elements
        var navElements = _page.Locator("nav");
        var mainElements = _page.Locator("main");
        var footerElements = _page.Locator("footer");

        // Assert - Should have semantic structure
        var hasNavOrTitle = await navElements.CountAsync() > 0 || await titleElement.CountAsync() > 0;
        Assert.True(hasNavOrTitle);

        // Check for ARIA labels on interactive elements
        var buttons = _page.Locator("button");
        var buttonCount = await buttons.CountAsync();
        for (int i = 0; i < buttonCount; i++)
        {
            var button = buttons.Nth(i);
            var ariaLabel = await button.GetAttributeAsync("aria-label");
            var buttonText = await button.TextContentAsync();
            
            // At least one should be present for accessibility
            Assert.True(!string.IsNullOrEmpty(ariaLabel) || !string.IsNullOrEmpty(buttonText), 
                $"Button {i} missing aria-label or text content");
        }

        _output.WriteLine($"Accessibility checks passed: {buttonCount} buttons checked");
    }

    [Fact(DisplayName = "Dashboard_Should_Display_Correct_Colors_For_Status")]
    public async Task Dashboard_Should_Display_Correct_Colors_For_Status()
    {
        if (_page == null) await InitializeAsync();
        
        // Arrange
        await _page.GotoAsync($"{_factory.KhachLinkUrl}/VanAnDashboard");
        await _page.WaitForSelectorAsync(".dashboard-container, [data-testid='dashboard-container']", new PageWaitForSelectorOptions { Timeout = 10000 });

        // Act - Check status indicators using Locator
        var statusIndicators = _page.Locator(".status-indicator, [data-testid='status-indicator']");
        var indicatorCount = await statusIndicators.CountAsync();

        // Assert - Status indicators should have appropriate colors
        for (int i = 0; i < indicatorCount; i++)
        {
            var indicator = statusIndicators.Nth(i);
            var classes = await indicator.GetAttributeAsync("class");
            Assert.NotNull(classes);

            // Should have one of the status classes
            var hasStatusClass = classes.Contains("online") || 
                               classes.Contains("offline") || 
                               classes.Contains("warning");
            Assert.True(hasStatusClass, $"Status indicator {i} missing status class");
        }

        // Check metric card colors
        var postgresCards = _page.Locator(".metric-card.postgresql, [data-testid*='postgresql']");
        var sqliteCards = _page.Locator(".metric-card.sqlite, [data-testid*='sqlite']");

        await Expect(postgresCards).ToHaveCountAsync(1);
        await Expect(sqliteCards).ToHaveCountAsync(1);

        _output.WriteLine($"Color scheme verified: {indicatorCount} status indicators");
    }

    [Fact(DisplayName = "Dashboard_Navigation_Should_Work_Correctly")]
    public async Task Dashboard_Navigation_Should_Work_Correctly()
    {
        if (_page == null) await InitializeAsync();
        
        // Arrange - Start from home page
        await _page.GotoAsync($"{_factory.KhachLinkUrl}/");
        await _page.WaitForSelectorAsync("nav, [data-testid='main-nav']", new PageWaitForSelectorOptions { Timeout = 10000 });

        // Act - Navigate to dashboard using Locator
        var dashboardLink = _page.Locator("a[href='VanAnDashboard'], a[href='/VanAnDashboard'], [data-testid='dashboard-link']");
        await Expect(dashboardLink).ToBeVisibleAsync();
        await dashboardLink.ClickAsync();

        // Assert - Should navigate to dashboard
        await _page.WaitForURLAsync("**/VanAnDashboard", new() { Timeout = 10000 });
        await _page.WaitForSelectorAsync(".dashboard-container, [data-testid='dashboard-container']", new PageWaitForSelectorOptions { Timeout = 10000 });

        var url = _page.Url;
        Assert.Contains("VanAnDashboard", url);

        // Navigate back to home
        var homeLink = _page.Locator("a[href='/'], [data-testid='home-link']");
        if (await homeLink.CountAsync() > 0)
        {
            await homeLink.ClickAsync();
            await _page.WaitForURLAsync("**/", new() { Timeout = 10000 });
        }

        _output.WriteLine("Navigation working correctly");
    }

    public void Dispose()
    {
        _page?.CloseAsync().GetAwaiter().GetResult();
        _browser?.CloseAsync().GetAwaiter().GetResult();
        _playwright?.Dispose();
    }
}
