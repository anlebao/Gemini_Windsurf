namespace VanAn.ShopERP.Models
{
    public class NavigationItem
    {
        public string Title { get; set; } = string.Empty;
        public string Icon { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
        public bool Active { get; set; }
    }
}
