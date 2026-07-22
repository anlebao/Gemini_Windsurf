using System.Globalization;

namespace VanAn.Shared.Helpers
{
    /// <summary>
    /// Shared currency formatting helper â€” G4 locked.
    /// Used by both ShopERP and KhachLink to avoid duplication.
    /// </summary>
    public static class CurrencyHelper
    {
        public const string VND_SYMBOL = "\u0111"; // Unicode Ä‘ (U+0111)

        private static readonly CultureInfo ViCulture = CultureInfo.GetCultureInfo("vi-VN");

        public static string FormatVND(decimal amount)
        {
            return $"{amount.ToString("N0", ViCulture)} {VND_SYMBOL}";
        }

        public static string FormatVND(int amount)
        {
            return $"{amount.ToString("N0", ViCulture)} {VND_SYMBOL}";
        }
    }
}
