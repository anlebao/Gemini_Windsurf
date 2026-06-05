namespace VanAn.UI.Platform.Tokens
{
    /// <summary>
    /// Minimal theme system for UI components
    /// </summary>
    public class Theme
    {
        /// <summary>
        /// Theme name
        /// </summary>
        public string Name { get; init; } = "Default";

        /// <summary>
        /// Is dark theme
        /// </summary>
        public bool IsDark { get; init; }

        /// <summary>
        /// Border radius for components
        /// </summary>
        public string BorderRadius { get; init; } = "0.375rem";

        /// <summary>
        /// Box shadow for components
        /// </summary>
        public string BoxShadow { get; init; } = "0 1px 3px rgba(0, 0, 0, 0.1)";

        /// <summary>
        /// Font family
        /// </summary>
        public string FontFamily { get; init; } = "system-ui, -apple-system, sans-serif";

        /// <summary>
        /// Font size base
        /// </summary>
        public string FontSizeBase { get; init; } = "1rem";

        /// <summary>
        /// Line height
        /// </summary>
        public string LineHeight { get; init; } = "1.5";

        /// <summary>
        /// Transition duration
        /// </summary>
        public string TransitionDuration { get; init; } = "0.15s";

        /// <summary>
        /// Create default theme
        /// </summary>
        public static Theme Default => new();

        /// <summary>
        /// Create dark theme
        /// </summary>
        public static Theme Dark => new()
        {
            Name = "Dark",
            IsDark = true,
            BoxShadow = "0 1px 3px rgba(0, 0, 0, 0.3)"
        };
    }
}
