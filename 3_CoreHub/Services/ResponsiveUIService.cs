using VanAn.Shared.Omnichannel;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Caching.Memory;
using System.Globalization;

namespace VanAn.CoreHub.Services;

/// <summary>
/// Implementation of responsive UI service for omnichannel mobile-first experience
/// Provides consistent UI/UX across all device types and screen sizes
/// </summary>
public class ResponsiveUIService : IResponsiveUIService
{
    private readonly ILogger<ResponsiveUIService> _logger;
    private readonly IMemoryCache _cache;

    public ResponsiveUIService(ILogger<ResponsiveUIService> logger, IMemoryCache cache)
    {
        _logger = logger;
        _cache = cache;
    }

    public async Task<ResponsiveLayout> GetLayoutConfigAsync(DeviceInfo device)
    {
        await Task.CompletedTask;
        try
        {
            _logger.LogInformation("Getting layout config for device: {DeviceType}, screen: {Width}x{Height}", 
                device.Type, device.Screen.Width, device.Screen.Height);

            var layout = device.Type switch
            {
                DeviceType.Mobile => GetMobileLayout(device.Screen),
                DeviceType.Tablet => GetTabletLayout(device.Screen),
                DeviceType.Desktop => GetDesktopLayout(device.Screen),
                DeviceType.TV => GetDesktopLayout(device.Screen),
                DeviceType.Wearable => GetMobileLayout(device.Screen),
                _ => GetDefaultLayout(device.Screen)
            };

            _logger.LogInformation("Layout configuration determined: {Layout} for device: {DeviceType}", layout, device.Type);
            return layout;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting layout config for device: {DeviceType}", device.Type);
            throw;
        }
    }

    public async Task<ComponentSizes> GetComponentSizesAsync(ScreenSize screen)
    {
        await Task.CompletedTask;
        try
        {
            _logger.LogInformation("Getting component sizes for screen: {Width}x{Height}", 
                screen.Width, screen.Height);

            var sizes = screen.Width switch
            {
                < 768 => GetMobileComponentSizes(),
                < 1024 => GetTabletComponentSizes(),
                < 1440 => GetSmallDesktopComponentSizes(),
                _ => GetDesktopComponentSizes()
            };

            _logger.LogInformation("Component sizes determined: {ButtonHeight}x{ButtonWidth}", 
                sizes.ButtonHeight, sizes.ButtonMinWidth);

            return sizes;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting component sizes for screen: {Width}x{Height}", 
                screen.Width, screen.Height);
            throw;
        }
    }

    public async Task<bool> IsTouchEnabledAsync(DeviceType deviceType)
    {
        await Task.CompletedTask;
        try
        {
            _logger.LogInformation("Checking touch enablement for device type: {DeviceType}", deviceType);

            var isTouchEnabled = deviceType switch
            {
                DeviceType.Mobile => true,
                DeviceType.Tablet => true,
                DeviceType.Wearable => true,
                DeviceType.TV => false,
                DeviceType.Desktop => false,
                _ => false
            };

            _logger.LogInformation("Touch enablement determined: {IsTouchEnabled} for device type: {DeviceType}", isTouchEnabled, deviceType);
            return isTouchEnabled;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking touch enablement for device type: {DeviceType}", deviceType);
            throw;
        }
    }

    public async Task<NavigationPattern> GetNavigationPatternAsync(DeviceInfo device)
    {
        await Task.CompletedTask;
        try
        {
            _logger.LogInformation("Getting navigation pattern for device: {DeviceType}", device.Type);

            var pattern = device.Type switch
            {
                DeviceType.Mobile => device.Screen.Width < 480 ? NavigationPattern.Hamburger : NavigationPattern.BottomTabs,
                DeviceType.Tablet => NavigationPattern.SideDrawer,
                DeviceType.Desktop => NavigationPattern.TopMenu,
                DeviceType.TV => NavigationPattern.TopMenu,
                DeviceType.Wearable => NavigationPattern.Hamburger,
                _ => NavigationPattern.TopMenu
            };

            _logger.LogInformation("Navigation pattern determined: {Pattern} for device: {DeviceType}", pattern, device.Type);
            return pattern;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting navigation pattern for device: {DeviceType}", device.Type);
            throw;
        }
    }

    public async Task<float> GetFontScaleAsync(UserPreferences preferences)
    {
        await Task.CompletedTask;
        try
        {
            _logger.LogInformation("Getting font scale for user preferences");

            // Default font scale based on language and accessibility
            var fontScale = preferences.Language switch
            {
                "vi" => 1.0f, // Vietnamese characters display well at default size
                "en" => 1.0f,
                _ => 1.0f
            };

            // TODO: Consider user accessibility settings
            // if (preferences.Accessibility?.LargeText == true)
            //     fontScale *= 1.2f;

            _logger.LogInformation("Font scale determined: {FontScale} for language: {Language}", fontScale, preferences.Language);
            return fontScale;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting font scale for user preferences");
            throw;
        }
    }

    public async Task<ColorTheme> GetColorThemeAsync(DeviceInfo device, UserPreferences preferences)
    {
        await Task.CompletedTask;
        try
        {
            _logger.LogInformation("Getting color theme for device: {DeviceType}, theme: {Theme}", device.Type, preferences.Theme);

            var theme = preferences.Theme.ToLower(CultureInfo.InvariantCulture) switch
            {
                "dark" => GetDarkTheme(),
                "light" => GetLightTheme(),
                "auto" => device.Type == DeviceType.Mobile ? GetLightTheme() : GetLightTheme(), // TODO: Implement system theme detection
                _ => GetLightTheme()
            };

            _logger.LogInformation("Color theme determined: {ThemeName}, is dark: {IsDark}", theme.Name, theme.IsDark);
            return theme;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting color theme for device: {DeviceType}", device.Type);
            throw;
        }
    }

    public async Task<AnimationSettings> GetAnimationSettingsAsync(DeviceInfo device)
    {
        await Task.CompletedTask;
        try
        {
            _logger.LogInformation("Getting animation settings for device: {DeviceType}", device.Type);

            var settings = device.Type switch
            {
                DeviceType.Mobile => GetMobileAnimationSettings(device.IsHighDensity),
                DeviceType.Tablet => GetTabletAnimationSettings(device.IsHighDensity),
                DeviceType.Desktop => GetDesktopAnimationSettings(device.IsHighDensity),
                DeviceType.TV => GetTVAnimationSettings(),
                DeviceType.Wearable => GetWearableAnimationSettings(),
                _ => GetDefaultAnimationSettings()
            };

            _logger.LogInformation("Animation settings determined: enabled: {Enabled}, duration: {Duration}ms", 
                settings.EnableAnimations, settings.DurationMs);

            return settings;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting animation settings for device: {DeviceType}", device.Type);
            throw;
        }
    }

    private static ResponsiveLayout GetDefaultLayout(ScreenSize screen)
    {
        return new ResponsiveLayout
        {
            MaxWidth = screen.Width,
            Padding = 16,
            Margin = 16,
            GridColumns = 1,
            IsMobile = false,
            IsTablet = false,
            IsDesktop = false,
            Breakpoint = "default"
        };
    }

    private static ComponentSizes GetMobileComponentSizes()
    {
        return new ComponentSizes
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
    }

    private static ComponentSizes GetTabletComponentSizes()
    {
        return new ComponentSizes
        {
            ButtonHeight = 40,
            ButtonMinWidth = 72,
            InputHeight = 40,
            CardPadding = 20,
            IconSize = 22,
            HeaderHeight = 60,
            FooterHeight = 56,
            NavigationHeight = 0 // Tablet uses side navigation
        };
    }

    private static ComponentSizes GetSmallDesktopComponentSizes()
    {
        return new ComponentSizes
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
    }

    private static ComponentSizes GetDesktopComponentSizes()
    {
        return new ComponentSizes
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
    }

    private static ColorTheme GetLightTheme()
    {
        return new ColorTheme
        {
            Name = "light",
            Primary = "#1976d2",
            Secondary = "#424242",
            Background = "#ffffff",
            Surface = "#f5f5f5",
            IsDark = false
        };
    }

    private static ColorTheme GetDarkTheme()
    {
        return new ColorTheme
        {
            Name = "dark",
            Primary = "#1976d2",
            Secondary = "#424242",
            Background = "#212529",
            Surface = "#343a40",
            IsDark = true
        };
    }

    private static AnimationSettings GetMobileAnimationSettings(bool isHighDensity)
    {
        return new AnimationSettings
        {
            EnableAnimations = true,
            DurationMs = isHighDensity ? 150 : 200,
            Easing = "ease-out",
            EnableTransitions = true,
            EnableGestures = true,
            ReducesMotion = 0.0
        };
    }

    private static AnimationSettings GetTabletAnimationSettings(bool isHighDensity)
    {
        return new AnimationSettings
        {
            EnableAnimations = true,
            DurationMs = isHighDensity ? 175 : 225,
            Easing = "ease-in-out",
            EnableTransitions = true,
            EnableGestures = true,
            ReducesMotion = 0.0
        };
    }

    private static AnimationSettings GetDesktopAnimationSettings(bool isHighDensity)
    {
        return new AnimationSettings
        {
            EnableAnimations = true,
            DurationMs = 250,
            Easing = "ease",
            EnableTransitions = true,
            EnableGestures = false,
            ReducesMotion = 0.0
        };
    }

    private static AnimationSettings GetTVAnimationSettings()
    {
        return new AnimationSettings
        {
            EnableAnimations = true,
            DurationMs = 300,
            Easing = "ease-in-out",
            EnableTransitions = true,
            EnableGestures = false,
            ReducesMotion = 0.2
        };
    }

    private static AnimationSettings GetWearableAnimationSettings()
    {
        return new AnimationSettings
        {
            EnableAnimations = false, // Minimal animations for wearables
            DurationMs = 100,
            Easing = "linear",
            EnableTransitions = false,
            EnableGestures = true,
            ReducesMotion = 0.8
        };
    }

    private static AnimationSettings GetDefaultAnimationSettings()
    {
        return new AnimationSettings
        {
            EnableAnimations = true,
            DurationMs = 300,
            Easing = "ease-in-out",
            EnableTransitions = true,
            EnableGestures = true,
            ReducesMotion = 0.0
        };
    }

    private static ResponsiveLayout GetMobileLayout(ScreenSize screen)
    {
        return new ResponsiveLayout
        {
            MaxWidth = screen.Width,
            Padding = 8,
            Margin = 4,
            GridColumns = 2,
            IsMobile = true,
            IsTablet = false,
            IsDesktop = false,
            Breakpoint = "xs"
        };
    }

    private static ResponsiveLayout GetTabletLayout(ScreenSize screen)
    {
        return new ResponsiveLayout
        {
            MaxWidth = screen.Width,
            Padding = 16,
            Margin = 8,
            GridColumns = 4,
            IsMobile = false,
            IsTablet = true,
            IsDesktop = false,
            Breakpoint = "md"
        };
    }

    private static ResponsiveLayout GetDesktopLayout(ScreenSize screen)
    {
        return new ResponsiveLayout
        {
            MaxWidth = screen.Width,
            Padding = 24,
            Margin = 16,
            GridColumns = 12,
            IsMobile = false,
            IsTablet = false,
            IsDesktop = true,
            Breakpoint = screen.Width >= 1200 ? "xl" : "lg"
        };
    }

}
