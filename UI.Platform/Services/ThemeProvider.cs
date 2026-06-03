namespace VanAn.UI.Platform.Services
{
    /// <summary>
    /// Frontend Theme Provider Implementation - UI Platform
    /// Manages UI themes for frontend
    /// </summary>
    public class ThemeProvider : IThemeProvider
    {
        private string _currentTheme = "default";

        public string CurrentTheme => _currentTheme;

        public void SetTheme(string theme)
        {
            if (!string.IsNullOrEmpty(theme))
            {
                _currentTheme = theme;
            }
        }

        public List<string> GetAvailableThemes()
        {
            return new List<string>
            {
                "default",
                "dark", 
                "light",
                "blue",
                "green"
            };
        }
    }
}
