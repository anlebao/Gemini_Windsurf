namespace VanAn.ShopERP.Services
{
    /// <summary>
    /// Shared loyalty tier ladder + calculator — single source for tier name and
    /// next-tier threshold (previously duplicated as private CalcTier in
    /// CustomerIdentityController + LoyaltyController; Profile Contextual UX v2
    /// added a third consumer via /api/customer-context/me).
    /// Keep thresholds in sync with KhachLink LoyaltyCard.razor display.
    /// </summary>
    internal static class LoyaltyTierCalculator
    {
        private static readonly (string Name, int Threshold)[] Ladder =
        {
            ("Silver",   1_000),
            ("Gold",     5_000),
            ("Platinum", 20_000),
        };

        public static string Calc(int points) => points switch
        {
            >= 20000 => "Platinum",
            >= 5000 => "Gold",
            >= 1000 => "Silver",
            _ => "Bronze"
        };

        /// <summary>Ngưỡng điểm của hạng kế tiếp; null khi đã max (Platinum).</summary>
        public static int? NextThreshold(int points)
        {
            foreach (var (_, threshold) in Ladder)
            {
                if (points < threshold) return threshold;
            }
            return null;
        }
    }
}
