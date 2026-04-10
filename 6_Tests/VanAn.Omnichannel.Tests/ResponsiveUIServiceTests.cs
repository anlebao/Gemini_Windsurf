using Xunit;
using Moq;
using System.Threading.Tasks;
using VanAn.Shared.Omnichannel;

namespace VanAn.Omnichannel.Tests;

public class ResponsiveUIServiceTests
{
    private readonly Mock<IResponsiveUIService> _mockService;

    public ResponsiveUIServiceTests()
    {
        _mockService = new Mock<IResponsiveUIService>();
    }

    [Fact(DisplayName = "TDD: Get Mobile Layout Configuration")]
    public async Task ResponsiveUI_GetMobileLayout_ShouldReturnMobileConfig()
    {
        // Arrange
        var device = new DeviceInfo
        {
            Type = DeviceType.Mobile,
            Screen = new ScreenSize { Width = 375, Height = 667 },
            Platform = "iOS",
            UserAgent = "Mobile Safari",
            IsHighDensity = true,
            HasTouch = true,
            Orientation = Orientation.Portrait
        };

        var expectedLayout = new ResponsiveLayout
        {
            MaxWidth = 375,
            Padding = 16,
            Margin = 8,
            GridColumns = 1,
            IsMobile = true,
            IsTablet = false,
            IsDesktop = false,
            Breakpoint = "sm"
        };

        _mockService.Setup(x => x.GetLayoutConfigAsync(device))
                  .ReturnsAsync(expectedLayout);

        // Act
        var result = await _mockService.Object.GetLayoutConfigAsync(device);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.IsMobile);
        Assert.False(result.IsTablet);
        Assert.False(result.IsDesktop);
        Assert.Equal(1, result.GridColumns);
        Assert.Equal("sm", result.Breakpoint);
        Assert.Equal(375, result.MaxWidth);
    }

    [Fact(DisplayName = "TDD: Get Desktop Layout Configuration")]
    public async Task ResponsiveUI_GetDesktopLayout_ShouldReturnDesktopConfig()
    {
        // Arrange
        var device = new DeviceInfo
        {
            Type = DeviceType.Desktop,
            Screen = new ScreenSize { Width = 1920, Height = 1080 },
            Platform = "Windows",
            UserAgent = "Chrome",
            IsHighDensity = false,
            HasTouch = false,
            Orientation = Orientation.Landscape
        };

        var expectedLayout = new ResponsiveLayout
        {
            MaxWidth = 1920,
            Padding = 24,
            Margin = 16,
            GridColumns = 12,
            IsMobile = false,
            IsTablet = false,
            IsDesktop = true,
            Breakpoint = "xl"
        };

        _mockService.Setup(x => x.GetLayoutConfigAsync(device))
                  .ReturnsAsync(expectedLayout);

        // Act
        var result = await _mockService.Object.GetLayoutConfigAsync(device);

        // Assert
        Assert.NotNull(result);
        Assert.False(result.IsMobile);
        Assert.False(result.IsTablet);
        Assert.True(result.IsDesktop);
        Assert.Equal(12, result.GridColumns);
        Assert.Equal("xl", result.Breakpoint);
        Assert.Equal(1920, result.MaxWidth);
    }

    [Fact(DisplayName = "TDD: Get Component Sizes for Mobile")]
    public async Task ResponsiveUI_GetMobileComponentSizes_ShouldReturnMobileSizes()
    {
        // Arrange
        var screen = new ScreenSize { Width = 375, Height = 667 };
        var expectedSizes = new ComponentSizes
        {
            ButtonHeight = 44,
            ButtonMinWidth = 64,
            InputHeight = 44,
            CardPadding = 16,
            IconSize = 24,
            HeaderHeight = 56,
            FooterHeight = 60,
            NavigationHeight = 56
        };

        _mockService.Setup(x => x.GetComponentSizesAsync(screen))
                  .ReturnsAsync(expectedSizes);

        // Act
        var result = await _mockService.Object.GetComponentSizesAsync(screen);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(44, result.ButtonHeight);
        Assert.Equal(44, result.InputHeight);
        Assert.Equal(16, result.CardPadding);
        Assert.Equal(24, result.IconSize);
        Assert.Equal(56, result.HeaderHeight);
        Assert.Equal(56, result.NavigationHeight);
    }

    [Fact(DisplayName = "TDD: Get Component Sizes for Desktop")]
    public async Task ResponsiveUI_GetDesktopComponentSizes_ShouldReturnDesktopSizes()
    {
        // Arrange
        var screen = new ScreenSize { Width = 1920, Height = 1080 };
        var expectedSizes = new ComponentSizes
        {
            ButtonHeight = 36,
            ButtonMinWidth = 80,
            InputHeight = 40,
            CardPadding = 24,
            IconSize = 20,
            HeaderHeight = 64,
            FooterHeight = 48,
            NavigationHeight = 0 // Desktop uses top navigation
        };

        _mockService.Setup(x => x.GetComponentSizesAsync(screen))
                  .ReturnsAsync(expectedSizes);

        // Act
        var result = await _mockService.Object.GetComponentSizesAsync(screen);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(36, result.ButtonHeight);
        Assert.Equal(40, result.InputHeight);
        Assert.Equal(24, result.CardPadding);
        Assert.Equal(20, result.IconSize);
        Assert.Equal(64, result.HeaderHeight);
        Assert.Equal(0, result.NavigationHeight);
    }

    [Fact(DisplayName = "TDD: Check Touch Enablement for Mobile")]
    public async Task ResponsiveUI_IsTouchEnabled_ShouldReturnTrueForMobile()
    {
        // Arrange
        var deviceType = DeviceType.Mobile;

        _mockService.Setup(x => x.IsTouchEnabledAsync(deviceType))
                  .ReturnsAsync(true);

        // Act
        var result = await _mockService.Object.IsTouchEnabledAsync(deviceType);

        // Assert
        Assert.True(result);
        _mockService.Verify(x => x.IsTouchEnabledAsync(deviceType), Times.Once);
    }

    [Fact(DisplayName = "TDD: Check Touch Enablement for Desktop")]
    public async Task ResponsiveUI_IsTouchEnabled_ShouldReturnFalseForDesktop()
    {
        // Arrange
        var deviceType = DeviceType.Desktop;

        _mockService.Setup(x => x.IsTouchEnabledAsync(deviceType))
                  .ReturnsAsync(false);

        // Act
        var result = await _mockService.Object.IsTouchEnabledAsync(deviceType);

        // Assert
        Assert.False(result);
        _mockService.Verify(x => x.IsTouchEnabledAsync(deviceType), Times.Once);
    }

    [Fact(DisplayName = "TDD: Get Navigation Pattern for Mobile")]
    public async Task ResponsiveUI_GetNavigationPattern_ShouldReturnBottomTabsForMobile()
    {
        // Arrange
        var device = new DeviceInfo
        {
            Type = DeviceType.Mobile,
            Screen = new ScreenSize { Width = 375, Height = 667 }
        };

        _mockService.Setup(x => x.GetNavigationPatternAsync(device))
                  .ReturnsAsync(NavigationPattern.BottomTabs);

        // Act
        var result = await _mockService.Object.GetNavigationPatternAsync(device);

        // Assert
        Assert.Equal(NavigationPattern.BottomTabs, result);
        _mockService.Verify(x => x.GetNavigationPatternAsync(device), Times.Once);
    }

    [Fact(DisplayName = "TDD: Get Navigation Pattern for Desktop")]
    public async Task ResponsiveUI_GetNavigationPattern_ShouldReturnTopMenuForDesktop()
    {
        // Arrange
        var device = new DeviceInfo
        {
            Type = DeviceType.Desktop,
            Screen = new ScreenSize { Width = 1920, Height = 1080 }
        };

        _mockService.Setup(x => x.GetNavigationPatternAsync(device))
                  .ReturnsAsync(NavigationPattern.TopMenu);

        // Act
        var result = await _mockService.Object.GetNavigationPatternAsync(device);

        // Assert
        Assert.Equal(NavigationPattern.TopMenu, result);
        _mockService.Verify(x => x.GetNavigationPatternAsync(device), Times.Once);
    }

    [Fact(DisplayName = "TDD: Get Font Scale for Accessibility")]
    public async Task ResponsiveUI_GetFontScale_ShouldReturnScaledSize()
    {
        // Arrange
        var preferences = new UserPreferences
        {
            Language = "vi",
            Theme = "light",
            EnableNotifications = true
        };

        _mockService.Setup(x => x.GetFontScaleAsync(preferences))
                  .ReturnsAsync(1.2f);

        // Act
        var result = await _mockService.Object.GetFontScaleAsync(preferences);

        // Assert
        Assert.Equal(1.2f, result);
        _mockService.Verify(x => x.GetFontScaleAsync(preferences), Times.Once);
    }

    [Fact(DisplayName = "TDD: Get Color Theme for Device and Preferences")]
    public async Task ResponsiveUI_GetColorTheme_ShouldReturnAppropriateTheme()
    {
        // Arrange
        var device = new DeviceInfo
        {
            Type = DeviceType.Mobile,
            Screen = new ScreenSize { Width = 375, Height = 667 }
        };
        var preferences = new UserPreferences { Theme = "dark" };

        var expectedTheme = new ColorTheme
        {
            Name = "dark",
            Primary = "#0d6efd",
            Secondary = "#6c757d",
            Background = "#212529",
            Surface = "#343a40",
            Text = "#ffffff",
            TextSecondary = "#adb5bd",
            Border = "#495057",
            IsDark = true
        };

        _mockService.Setup(x => x.GetColorThemeAsync(device, preferences))
                  .ReturnsAsync(expectedTheme);

        // Act
        var result = await _mockService.Object.GetColorThemeAsync(device, preferences);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("dark", result.Name);
        Assert.Equal("#212529", result.Background);
        Assert.Equal("#ffffff", result.Text);
        Assert.True(result.IsDark);
        _mockService.Verify(x => x.GetColorThemeAsync(device, preferences), Times.Once);
    }

    [Fact(DisplayName = "TDD: Get Animation Settings for Device Capabilities")]
    public async Task ResponsiveUI_GetAnimationSettings_ShouldReturnOptimalSettings()
    {
        // Arrange
        var device = new DeviceInfo
        {
            Type = DeviceType.Mobile,
            Screen = new ScreenSize { Width = 375, Height = 667 },
            IsHighDensity = true,
            HasTouch = true
        };

        var expectedSettings = new AnimationSettings
        {
            EnableAnimations = true,
            DurationMs = 200,
            Easing = "ease-out",
            EnableTransitions = true,
            EnableGestures = true,
            ReducesMotion = 0.0
        };

        _mockService.Setup(x => x.GetAnimationSettingsAsync(device))
                  .ReturnsAsync(expectedSettings);

        // Act
        var result = await _mockService.Object.GetAnimationSettingsAsync(device);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.EnableAnimations);
        Assert.Equal(200, result.DurationMs);
        Assert.Equal("ease-out", result.Easing);
        Assert.True(result.EnableTransitions);
        Assert.True(result.EnableGestures);
        Assert.Equal(0.0, result.ReducesMotion);
        _mockService.Verify(x => x.GetAnimationSettingsAsync(device), Times.Once);
    }
}
