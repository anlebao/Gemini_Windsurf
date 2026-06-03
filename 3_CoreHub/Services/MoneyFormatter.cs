namespace VanAn.CoreHub.Services
{
    /// <summary>
    /// Currency formatting utilities for Vietnamese Dong (VND)
    /// </summary>
    public static class MoneyFormatter
    {
        /// <summary>
        /// Format decimal amount as Vietnamese Dong with thousand separators
        /// </summary>
        public static string FormatVND(decimal amount)
        {
            if (amount == 0m)
            {
                return "0 ₫";
            }
            // Vietnamese format: thousands separated by dots, no decimals
            string formatted = string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0:N0}", amount)
                .Replace(",", ".");
            return $"{formatted} ₫";
        }

        /// <summary>
        /// Parse VND formatted string back to decimal
        /// </summary>
        public static decimal ParseVND(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                return 0m;
            }

            string cleaned = input.Replace("₫", "").Replace(".", "").Replace(",", "").Trim();
            return decimal.TryParse(cleaned, out decimal result) ? result : 0m;
        }
    }
}
