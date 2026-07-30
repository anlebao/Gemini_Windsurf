namespace VanAn.Shared.Domain
{
    /// <summary>
    /// Commission calculation base — Sprint 7. Determines what CommissionRate is applied to.
    /// Marketplace: OnOrderTotal (commission = orderTotal × rate).
    /// Reseller: OnMargin (commission = PlatformMargin × rate).
    /// </summary>
    public enum CommissionBase
    {
        /// <summary>Marketplace — commission tính trên tổng đơn hàng</summary>
        OnOrderTotal = 0,

        /// <summary>Reseller — commission tính trên margin (SellPrice - CostPrice)</summary>
        OnMargin = 1
    }
}
