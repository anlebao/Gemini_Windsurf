namespace VanAn.UI.Platform.Services
{
    /// <summary>
    /// Frontend Theme Provider Implementation - UI Platform
    /// Manages UI themes for frontend
    /// </summary>
    public class ThemeProvider : IThemeProvider
    {
        public string CurrentTheme { get; private set; } = "default";

        public void SetTheme(string theme)
        {
            if (!string.IsNullOrEmpty(theme))
            {
                CurrentTheme = theme;
            }
        }

        public List<string> GetAvailableThemes()
        {
            return
            [
                "default",
                "dark",
                "light",
                "blue",
                "green"
            ];
        }
    }
}
