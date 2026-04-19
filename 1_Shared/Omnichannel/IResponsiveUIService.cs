namespace VanAn.Shared.Omnichannel;

/// <summary>
/// Responsive UI service for omnichannel mobile-first experience
/// Ensures consistent UI/UX across all device types and screen sizes
/// </summary>
public interface IResponsiveUIService
{
    /// <summary>
    /// Get responsive layout configuration for current device
    /// </summary>
    Task<ResponsiveLayout> GetLayoutConfigAsync(DeviceInfo device);

    /// <summary>
    /// Get component sizes based on screen dimensions
    /// </summary>
    Task<ComponentSizes> GetComponentSizesAsync(ScreenSize screen);

    /// <summary>
    /// Determine if touch interactions should be enabled
    /// </summary>
    Task<bool> IsTouchEnabledAsync(DeviceType deviceType);

    /// <summary>
    /// Get appropriate navigation pattern for device
    /// </summary>
    Task<NavigationPattern> GetNavigationPatternAsync(DeviceInfo device);

    /// <summary>
    /// Get font scaling for accessibility
    /// </summary>
    Task<float> GetFontScaleAsync(UserPreferences preferences);

    /// <summary>
    /// Get color theme for current device and user preferences
    /// </summary>
    Task<ColorTheme> GetColorThemeAsync(DeviceInfo device, UserPreferences preferences);

    /// <summary>
    /// Get animation settings based on device capabilities
    /// </summary>
    Task<AnimationSettings> GetAnimationSettingsAsync(DeviceInfo device);
}

/// <summary>
/// Device information for responsive design
/// </summary>
public record DeviceInfo
{
    public DeviceType Type { get; init; }
    public ScreenSize Screen { get; init; }
    public string Platform { get; init; } = string.Empty;
    public string UserAgent { get; init; } = string.Empty;
    public bool IsHighDensity { get; init; }
    public bool HasTouch { get; init; }
    public Orientation Orientation { get; init; } = Orientation.Portrait;

    public DeviceInfo(ScreenSize screen)
    {
        Screen = screen;
    }
}

/// <summary>
/// Responsive layout configuration
/// </summary>
public record ResponsiveLayout
{
    public int MaxWidth { get; init; }
    public int Padding { get; init; }
    public int Margin { get; init; }
    public int GridColumns { get; init; }
    public bool IsMobile { get; init; }
    public bool IsTablet { get; init; }
    public bool IsDesktop { get; init; }
    public string Breakpoint { get; init; } = string.Empty;
}

/// <summary>
/// Component sizes for responsive design
/// </summary>
public record ComponentSizes
{
    public int ButtonHeight { get; init; }
    public int ButtonMinWidth { get; init; }
    public int InputHeight { get; init; }
    public int CardPadding { get; init; }
    public int IconSize { get; init; }
    public int HeaderHeight { get; init; }
    public int FooterHeight { get; init; }
    public int NavigationHeight { get; init; }
}

/// <summary>
/// Navigation patterns for different devices
/// </summary>
public enum NavigationPattern
{
    BottomTabs,      // Mobile
    SideDrawer,      // Tablet
    TopMenu,         // Desktop
    Hamburger,       // Small screens
    Breadcrumb       // Desktop secondary
}

/// <summary>
/// Device types for responsive design
/// </summary>
public enum DeviceType
{
    Mobile,
    Tablet,
    Desktop,
    TV,
    Wearable
}

/// <summary>
/// Screen sizes for responsive breakpoints
/// </summary>
public record ScreenSize
{
    public int Width { get; init; }
    public int Height { get; init; }
    public double AspectRatio => Width > 0 ? (double)Height / Width : 1;
    
    public bool IsSmall => Width < 576;
    public bool IsMedium => Width >= 576 && Width < 768;
    public bool IsLarge => Width >= 768 && Width < 992;
    public bool IsExtraLarge => Width >= 992;
}

/// <summary>
/// Color theme for responsive UI
/// </summary>
public record ColorTheme
{
    public string Name { get; init; } = string.Empty;
    public string Primary { get; init; } = "#007bff";
    public string Secondary { get; init; } = "#6c757d";
    public string Background { get; init; } = "#ffffff";
    public string Surface { get; init; } = "#f8f9fa";
    public string Text { get; init; } = "#212529";
    public string TextSecondary { get; init; } = "#6c757d";
    public string Border { get; init; } = "#dee2e6";
    public bool IsDark { get; init; }
}

/// <summary>
/// Animation settings based on device capabilities
/// </summary>
public record AnimationSettings
{
    public bool EnableAnimations { get; init; }
    public int DurationMs { get; init; }
    public string Easing { get; init; } = "ease";
    public bool EnableTransitions { get; init; }
    public bool EnableGestures { get; init; }
    public double ReducesMotion { get; init; }
}

/// <summary>
/// Screen orientation
/// </summary>
public enum Orientation
{
    Portrait,
    Landscape
}
