using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Services;

/// <summary>
/// CC-S4 fix: single source of truth for the salesman commission base.
///
/// Before this, the base was the WHOLE order (<c>Order.TotalAmount</c>, which also includes shipping
/// and every other product in the cart), and <c>WalletService</c> used a different formula
/// (<c>margin × rate</c>) — so the reserved commission and the actual <c>SalesReferral</c> amount
/// could disagree.
///
/// Rules:
/// - Marketplace (and Reseller with <see cref="CommissionBase.OnOrderTotal"/>): base = the referred
///   line items' <c>SubTotal</c> — pre-VAT, shipping excluded. Only the products the salesman
///   actually referred earn commission.
/// - Reseller with <see cref="CommissionBase.OnMargin"/>: per-item cost is not tracked, so the
///   order-level margin is pro-rated by the referred lines' share of the order sub-total.
/// - Referred product not present in the order → base 0 → no commission.
/// </summary>
public static class ReferralCommissionCalculator
{
    /// <summary>Sum of the referred line items' SubTotal (Quantity × UnitPrice, pre-VAT).</summary>
    public static decimal ReferredSubTotal(Order order)
    {
        if (order.ReferralProductId == null || order.ReferralProductId == Guid.Empty)
            return 0m;

        return order.Items
            .Where(i => i.ProductId == order.ReferralProductId.Value)
            .Sum(i => i.SubTotal);
    }

    /// <summary>
    /// Commission base for this order. Returns 0 when the referred product is not in the order
    /// (caller must then skip commission creation).
    /// </summary>
    public static decimal ComputeBase(Order order, ProductReferralConfig config)
    {
        var referredSubTotal = ReferredSubTotal(order);
        if (referredSubTotal <= 0m)
            return 0m;

        if (config.CommissionBase == CommissionBase.OnMargin && order.CommerceMode == CommerceMode.Reseller)
        {
            var orderMargin = order.PlatformMargin ?? ((order.SellPrice ?? 0m) - (order.CostPrice ?? 0m));
            var orderSubTotal = order.SubTotal > 0m ? order.SubTotal : referredSubTotal;
            // Pro-rate: margin is an order-level figure; attribute the referred lines' share.
            return orderMargin * (referredSubTotal / orderSubTotal);
        }

        return referredSubTotal;
    }
}
