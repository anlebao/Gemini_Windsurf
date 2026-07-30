namespace VanAn.Shared.Domain.Common
{
    /// <summary>
    /// Reserved wallet OwnerId GUIDs for system-level wallets — Sprint 7.
    /// These are NOT Customer entities — WalletTransaction.OwnerId references these GUIDs directly.
    /// PlatformWallet: Vạn An giữ PlatformFee (Reseller mode margin share).
    /// CommunityFund: quỹ phát triển cộng đồng (Reseller mode margin share).
    /// </summary>
    public static class SystemWalletIds
    {
        /// <summary>Vạn An platform wallet — receives PlatformFee in Reseller mode</summary>
        public static readonly Guid PlatformWallet = Guid.Parse("00000000-0000-0000-0000-000000000001");

        /// <summary>Community fund wallet — receives CommunityFund share in Reseller mode</summary>
        public static readonly Guid CommunityFund = Guid.Parse("00000000-0000-0000-0000-000000000002");
    }
}
