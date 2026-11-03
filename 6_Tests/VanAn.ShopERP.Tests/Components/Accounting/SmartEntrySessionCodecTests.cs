using VanAn.CoreHub.Services;
using VanAn.ShopERP.Components.Accounting;
using Xunit;

namespace VanAn.ShopERP.Tests.Components.Accounting;

/// <summary>PH3 — codec multi JSON cho sessionStorage (khôi phục deterministic sau full reload).</summary>
public class SmartEntrySessionCodecTests
{
    private static ParsedVoucher Voucher(VoucherIntent intent, decimal amount, string desc)
        => new(intent, amount, new DateTime(2026, 11, 3), "cash", null, null,
            intent is VoucherIntent.Receivable or VoucherIntent.ReceivablePayment ? "khách A" : null,
            intent == VoucherIntent.Expense ? "utilities" : null, null, desc,
            "High", Array.Empty<string>(), Array.Empty<string>());

    [Fact]
    public void Serialize_Deserialize_RoundTrip_PreservesAllFields()
    {
        var multi = new List<ParsedVoucher>
        {
            Voucher(VoucherIntent.Revenue, 2_000_000m, "thu tiền bán hàng 2 triệu"),
            Voucher(VoucherIntent.Expense, 1_000_000m, "chi tiền điện 1 triệu"),
        };

        var json = SmartEntrySessionCodec.Serialize(multi);
        Assert.False(string.IsNullOrEmpty(json));

        var restored = SmartEntrySessionCodec.Deserialize(json);
        Assert.NotNull(restored);
        Assert.Equal(2, restored!.Count);
        Assert.Equal(VoucherIntent.Revenue, restored[0].Intent);
        Assert.Equal(2_000_000m, restored[0].Amount);
        Assert.Equal(new DateTime(2026, 11, 3), restored[0].Date);
        Assert.Equal("cash", restored[0].PaymentMethod);
        Assert.Equal("thu tiền bán hàng 2 triệu", restored[0].Description);
        Assert.Equal("High", restored[0].Confidence);
        Assert.Equal(VoucherIntent.Expense, restored[1].Intent);
        Assert.Equal("utilities", restored[1].Category);
    }

    [Fact]
    public void Deserialize_InvalidJson_ReturnsNull()
    {
        Assert.Null(SmartEntrySessionCodec.Deserialize("not-json"));
        Assert.Null(SmartEntrySessionCodec.Deserialize(""));
        Assert.Null(SmartEntrySessionCodec.Deserialize("[]"));
    }
}