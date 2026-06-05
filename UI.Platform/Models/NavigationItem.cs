namespace VanAn.UI.Platform.Models
{
    /// <summary>
    /// Navigation Item Model - UI Platform
    /// Represents navigation menu items
    /// </summary>
    public class NavigationItem
    {
        public string Id { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
        public string Icon { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public List<NavigationItem> Children { get; set; } = [];
    }
}
