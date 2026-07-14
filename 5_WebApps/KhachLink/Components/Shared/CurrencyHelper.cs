namespace VanAn.KhachLink.Components.Shared
{
    public static class CurrencyHelper
    {
        public const string VND_SYMBOL = "\u0111"; // Unicode đ (U+0111)

        public static string FormatVND(decimal amount)
        {
            return $"{amount.ToString("N0", System.Globalization.CultureInfo.GetCultureInfo("vi-VN"))} {VND_SYMBOL}";
        }

        public static string FormatVND(int amount)
        {
            return $"{amount.ToString("N0", System.Globalization.CultureInfo.GetCultureInfo("vi-VN"))} {VND_SYMBOL}";
        }
    }
}
