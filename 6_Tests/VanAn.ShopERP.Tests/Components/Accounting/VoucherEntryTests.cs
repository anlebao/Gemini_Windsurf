using VanAn.ShopERP.Tests.Components;
using Bunit;
using Xunit;

namespace VanAn.ShopERP.Tests.Components.Accounting;

/// <summary>
/// Task PH2 — VoucherEntry generic page (registry-driven).
/// Regression: RV 2026-11-03 — @bind dictionary rỗng ném KeyNotFoundException ở prerender
/// (500 cho mọi phiếu generic không có ?desc=). Khởi tạo key trước render đã fix.
/// ITenantProvider mock từ ComponentTestBase (HasTenant=true).
/// </summary>
public class VoucherEntryTests : ComponentTestBase
{
    private IRenderedComponent<VanAn.ShopERP.Components.Pages.Accounting.VoucherEntry> RenderVoucher(string code)
        => RenderComponent<VanAn.ShopERP.Components.Pages.Accounting.VoucherEntry>(
            parameters => parameters.Add(p => p.Code, code));

    [Fact]
    public void LoanIn_Renders_WithoutDesc_NoKeyNotFound()
    {
        // Regression: trước fix → KeyNotFoundException 'amount' (500 production).
        var cut = RenderVoucher("loan-in");
        Assert.Single(cut.FindAll("[data-testid='voucher-save']"));
        Assert.Single(cut.FindAll("[data-testid='vf-account']"));
        Assert.Single(cut.FindAll("[data-testid='vf-amount']"));
        Assert.Equal("Phiếu Vay Nhận Tiền", cut.Find("h1").TextContent.Trim());
    }

    [Fact]
    public void LoanRepayment_Renders_WithoutDesc()
    {
        var cut = RenderVoucher("loan-repayment");
        Assert.Single(cut.FindAll("[data-testid='voucher-save']"));
        Assert.Equal("Phiếu Trả Nợ Vay", cut.Find("h1").TextContent.Trim());
    }

    [Fact]
    public void HostedType_RendersRouteLink_NotForm()
    {
        // HostPage guard: /vouchers/revenue → link trang host, không render form.
        var cut = RenderVoucher("revenue");
        Assert.Single(cut.FindAll("[data-testid='hosted-route-link']"));
        Assert.Empty(cut.FindAll("[data-testid='voucher-save']"));
    }
}