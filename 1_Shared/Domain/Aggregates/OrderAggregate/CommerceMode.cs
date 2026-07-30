namespace VanAn.Shared.Domain
{
    /// <summary>
    /// Commerce mode — Sprint 7. Toggle giữa Marketplace (existing Sprint 0-6) và Reseller (Vạn An mua-bán lại).
    /// Snapshot tại order creation — toggle affect future orders only.
    /// </summary>
    public enum CommerceMode
    {
        /// <summary>Sprint 0-6 hiện tại — tenant bán trực tiếp, Vạn An chỉ là sàn</summary>
        Marketplace = 0,

        /// <summary>Sprint 7 — Vạn An mua từ tenant → bán lại cho customer ("Mua giúp — Bán dùm")</summary>
        Reseller = 1,

        /// <summary>Tenant override: dùng global setting (chỉ dùng cho TenantSettings.CommerceModeOverride)</summary>
        Inherit = -1
    }
}
