namespace VanAn.KhachLink.Components.Shared
{
    public static class CurrencyHelper
    {
        public const string VND_SYMBOL = "&#x111;"; // HTML Entity for đ
        
        public static string FormatVND(decimal amount)
        {
            return $"{amount.ToString("N0", System.Globalization.CultureInfo.InvariantCulture)}{VND_SYMBOL}";
        }
        
        public static string FormatVND(int amount)
        {
            return $"{amount.ToString("N0", System.Globalization.CultureInfo.InvariantCulture)}{VND_SYMBOL}";
        }
    }
}
