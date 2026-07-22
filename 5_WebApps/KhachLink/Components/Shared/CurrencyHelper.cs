namespace VanAn.KhachLink.Components.Shared
{
    /// <summary>
    /// KhachLink CurrencyHelper â€” delegates to shared VanAn.Shared.Helpers.CurrencyHelper (G4).
    /// Kept for backward compatibility with existing KhachLink references.
    /// </summary>
    public static class CurrencyHelper
    {
        public const string VND_SYMBOL = VanAn.Shared.Helpers.CurrencyHelper.VND_SYMBOL;

        public static string FormatVND(decimal amount)
        {
            return VanAn.Shared.Helpers.CurrencyHelper.FormatVND(amount);
        }

        public static string FormatVND(int amount)
        {
            return VanAn.Shared.Helpers.CurrencyHelper.FormatVND(amount);
        }
    }
}
