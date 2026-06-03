namespace VanAn.UI.Platform.Services
{
    /// <summary>
    /// Frontend Theme Provider - UI Platform
    /// Manages UI themes and styling
    /// </summary>
    public interface IThemeProvider
    {
        string CurrentTheme { get; }
        void SetTheme(string theme);
        List<string> GetAvailableThemes();
    }
}
