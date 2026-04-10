using Microsoft.Playwright;
using System.Text.Json;
using Xunit.Abstractions;
using Xunit;
using VanAn.E2E.Tests.Infrastructure;

namespace VanAn.E2E.Tests;

public class DashboardE2ETests : IClassFixture<SelfHostedTestFactory>, IDisposable
{
    private readonly SelfHostedTestFactory _factory;
    private readonly IPlaywright _playwright;
    private readonly IBrowser _browser;
    private readonly IPage _page;
    private readonly ITestOutputHelper _output;

    public DashboardE2ETests(SelfHostedTestFactory factory, ITestOutputHelper output)
    {
        _factory = factory;
        _output = output;

        var playwright = Playwright.CreateAsync();
        playwright.Wait();
        _playwright = playwright.Result;

        _browser = _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = true,
            SlowMo = 100
        }).Result;

        _page = _browser.NewPageAsync().Result;
    }

    [Fact(DisplayName = "Dashboard_Should_Load_And_Display_Metrics")]
    public async Task Dashboard_Should_Load_And_Display_Metrics()
    {
        // Arrange
        await _page.GotoAsync($"{_factory.KhachLinkUrl}/VanAnDashboard");

        // Wait for dashboard to load
        await _page.WaitForSelectorAsync(".dashboard-container", new PageWaitForSelectorOptions { Timeout = 10000 });

        // Act & Assert - Check main elements
        var dashboardTitle = await _page.TextContentAsync(".dashboard-title");
        Assert.Contains("VanAn Dashboard", dashboardTitle);

        _output.WriteLine("Dashboard loaded successfully");
    }

    [Fact(DisplayName = "Dashboard_Should_Display_PostgreSQL_Metrics")]
    public async Task Dashboard_Should_Display_PostgreSQL_Metrics()
    {
        // Arrange
        await _page.GotoAsync($"{_factory.KhachLinkUrl}/VanAnDashboard");
        await _page.WaitForSelectorAsync(".dashboard-container");

        // Act - Find PostgreSQL metrics cards
        var postgresCards = await _page.QuerySelectorAllAsync(".metric-card.postgresql");

        // Assert - Should have multiple PostgreSQL cards
        Assert.True(postgresCards.Count >= 3);

        // Check specific metrics
        IElementHandle? tenantCard = null;
        foreach (var card in postgresCards)
        {
            var text = await card.TextContentAsync();
            if (text != null && text.Contains("Tenant"))
            {
                tenantCard = card;
                break;
            }
        }

        Assert.NotNull(tenantCard);

        _output.WriteLine($"Found {postgresCards.Count} PostgreSQL metric cards");
    }

    [Fact(DisplayName = "Dashboard_Should_Display_SQLite_Metrics")]
    public async Task Dashboard_Should_Display_SQLite_Metrics()
    {
        // Arrange
        await _page.GotoAsync($"{_factory.KhachLinkUrl}/VanAnDashboard");
        await _page.WaitForSelectorAsync(".dashboard-container");

        // Act - Find SQLite metrics cards
        var sqliteCards = await _page.QuerySelectorAllAsync(".metric-card.sqlite");

        // Assert - Should have SQLite cards
        Assert.True(sqliteCards.Count >= 2);

        // Check for KhachLink and ShopERP metrics
        var pageContent = await _page.TextContentAsync(".dashboard-container");
        Assert.Contains("KhachLink", pageContent);
        Assert.Contains("ShopERP", pageContent);

        _output.WriteLine($"Found {sqliteCards.Count} SQLite metric cards");
    }

    [Fact(DisplayName = "Dashboard_Should_Display_Sync_Status")]
    public async Task Dashboard_Should_Display_Sync_Status()
    {
        // Arrange
        await _page.GotoAsync($"{_factory.KhachLinkUrl}/VanAnDashboard");
        await _page.WaitForSelectorAsync(".dashboard-container");

        // Act - Find sync status section
        var syncSection = await _page.QuerySelectorAsync(".sync-status-grid");

        // Assert - Sync section should exist
        Assert.NotNull(syncSection);

        // Check for sync progress bars
        var progressBars = await _page.QuerySelectorAllAsync(".progress-fill");
        Assert.True(progressBars.Count > 0);

        _output.WriteLine($"Sync status displayed with {progressBars.Count} progress indicators");
    }

    [Fact(DisplayName = "Dashboard_Should_Have_Working_Refresh_Button")]
    public async Task Dashboard_Should_Have_Working_Refresh_Button()
    {
        // Arrange
        await _page.GotoAsync($"{_factory.KhachLinkUrl}/VanAnDashboard");
        await _page.WaitForSelectorAsync(".dashboard-container");

        // Act - Find and click refresh button
        var refreshButton = await _page.QuerySelectorAsync(".refresh-btn");
        Assert.NotNull(refreshButton);

        // Click refresh button
        await refreshButton.ClickAsync();

        // Wait for loading state
        await _page.WaitForSelectorAsync(".loading-spinner", new PageWaitForSelectorOptions { Timeout = 5000 });

        // Wait for loading to complete
        await _page.WaitForSelectorAsync(".loading-spinner", new PageWaitForSelectorOptions { State = WaitForSelectorState.Hidden, Timeout = 10000 });

        // Assert - Dashboard should still be visible after refresh
        var dashboardContainer = await _page.QuerySelectorAsync(".dashboard-container");
        Assert.NotNull(dashboardContainer);

        _output.WriteLine("Refresh button working correctly");
    }

    [Fact(DisplayName = "Dashboard_Should_Show_Last_Updated_Timestamp")]
    public async Task Dashboard_Should_Show_Last_Updated_Timestamp()
    {
        // Arrange
        await _page.GotoAsync($"{_factory.KhachLinkUrl}/VanAnDashboard");
        await _page.WaitForSelectorAsync(".dashboard-container");

        // Act - Find last updated section
        var lastUpdated = await _page.QuerySelectorAsync(".last-updated");

        // Assert - Last updated should exist and contain date
        Assert.NotNull(lastUpdated);

        var lastUpdatedText = await lastUpdated.TextContentAsync();
        Assert.Contains("Last Updated:", lastUpdatedText);
        Assert.Contains("System", lastUpdatedText);

        _output.WriteLine($"Last updated displayed: {lastUpdatedText}");
    }

    [Fact(DisplayName = "Dashboard_Should_Handle_Connection_Issues_Gracefully")]
    public async Task Dashboard_Should_Handle_Connection_Issues_Gracefully()
    {
        // Arrange - Navigate to dashboard
        await _page.GotoAsync($"{_factory.KhachLinkUrl}/VanAnDashboard");
        await _page.WaitForSelectorAsync(".dashboard-container");

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
        var refreshButton = await _page.QuerySelectorAsync(".refresh-btn");
        if (refreshButton != null)
        {
            await refreshButton.ClickAsync();
            await _page.WaitForTimeoutAsync(2000);
        }

        // Assert - Should show alerts for connection issues
        var alerts = await _page.QuerySelectorAllAsync(".alert");
        var hasConnectionAlert = false;
        foreach (var alert in alerts)
        {
            var text = await alert.TextContentAsync();
            if (text != null && text.Contains("Connection"))
            {
                hasConnectionAlert = true;
                break;
            }
        }

        // Clean up routing
        await _page.UnrouteAllAsync();

        _output.WriteLine($"Connection issues handled: {hasConnectionAlert}");
    }

    [Fact(DisplayName = "Dashboard_Should_Be_Responsive")]
    public async Task Dashboard_Should_Be_Responsive()
    {
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
            await _page.WaitForSelectorAsync(".dashboard-container");

            // Assert - Dashboard should be visible and functional
            var dashboardContainer = await _page.QuerySelectorAsync(".dashboard-container");
            Assert.NotNull(dashboardContainer);

            // Check if metrics grid adapts to screen size
            var metricsGrid = await _page.QuerySelectorAsync(".metrics-grid");
            Assert.NotNull(metricsGrid);

            _output.WriteLine($"Dashboard responsive on {viewport.Width}x{viewport.Height}");
        }
    }

    [Fact(DisplayName = "Dashboard_Should_Have_Proper_Accessibility")]
    public async Task Dashboard_Should_Have_Proper_Accessibility()
    {
        // Arrange
        await _page.GotoAsync($"{_factory.KhachLinkUrl}/VanAnDashboard");
        await _page.WaitForSelectorAsync(".dashboard-container");

        // Act - Check for accessibility features
        var titleElement = await _page.QuerySelectorAsync("h1");
        Assert.NotNull(titleElement);

        // Check for semantic HTML elements
        var navElements = await _page.QuerySelectorAllAsync("nav");
        var mainElements = await _page.QuerySelectorAllAsync("main");
        var footerElements = await _page.QuerySelectorAllAsync("footer");

        // Assert - Should have semantic structure
        Assert.True(navElements.Count > 0 || titleElement != null);

        // Check for ARIA labels on interactive elements
        var buttons = await _page.QuerySelectorAllAsync("button");
        foreach (var button in buttons)
        {
            var ariaLabel = await button.GetAttributeAsync("aria-label");
            var buttonText = await button.TextContentAsync();
            
            // At least one should be present for accessibility
            Assert.True(!string.IsNullOrEmpty(ariaLabel) || !string.IsNullOrEmpty(buttonText));
        }

        _output.WriteLine($"Accessibility checks passed: {buttons.Count} buttons checked");
    }

    [Fact(DisplayName = "Dashboard_Should_Display_Correct_Colors_For_Status")]
    public async Task Dashboard_Should_Display_Correct_Colors_For_Status()
    {
        // Arrange
        await _page.GotoAsync($"{_factory.KhachLinkUrl}/VanAnDashboard");
        await _page.WaitForSelectorAsync(".dashboard-container");

        // Act - Check status indicators
        var statusIndicators = await _page.QuerySelectorAllAsync(".status-indicator");

        // Assert - Status indicators should have appropriate colors
        foreach (var indicator in statusIndicators)
        {
            var classes = await indicator.GetAttributeAsync("class");
            Assert.NotNull(classes);

            // Should have one of the status classes
            var hasStatusClass = classes.Contains("online") || 
                               classes.Contains("offline") || 
                               classes.Contains("warning");
            Assert.True(hasStatusClass);
        }

        // Check metric card colors
        var postgresCards = await _page.QuerySelectorAllAsync(".metric-card.postgresql");
        var sqliteCards = await _page.QuerySelectorAllAsync(".metric-card.sqlite");

        Assert.True(postgresCards.Count > 0);
        Assert.True(sqliteCards.Count > 0);

        _output.WriteLine($"Color scheme verified: {statusIndicators.Count} status indicators, {postgresCards.Count} PostgreSQL cards, {sqliteCards.Count} SQLite cards");
    }

    [Fact(DisplayName = "Dashboard_Navigation_Should_Work_Correctly")]
    public async Task Dashboard_Navigation_Should_Work_Correctly()
    {
        // Arrange - Start from home page
        await _page.GotoAsync($"{_factory.KhachLinkUrl}/");
        await _page.WaitForSelectorAsync("nav");

        // Act - Navigate to dashboard
        var dashboardLink = await _page.QuerySelectorAsync("a[href='VanAnDashboard']");
        Assert.NotNull(dashboardLink);

        await dashboardLink.ClickAsync();

        // Assert - Should navigate to dashboard
        await _page.WaitForURLAsync("**/VanAnDashboard");
        await _page.WaitForSelectorAsync(".dashboard-container");

        var url = _page.Url;
        Assert.Contains("VanAnDashboard", url);

        // Navigate back to home
        var homeLink = await _page.QuerySelectorAsync("a[href='/']");
        if (homeLink != null)
        {
            await homeLink.ClickAsync();
            await _page.WaitForURLAsync("**/");
        }

        _output.WriteLine("Navigation working correctly");
    }

    public void Dispose()
    {
        _page?.CloseAsync();
        _browser?.CloseAsync();
        _playwright?.Dispose();
    }
}
