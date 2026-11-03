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

    [Fact]
    public void LoanIn_Save_ThenRerender_NoKeyNotFound()
    {
        // Regression RV 2026-11-03: _numbers.Clear() sau save → render kế tiếp ném
        // KeyNotFoundException 'amount' → circuit chết production (nút treo "Đang lưu...").
        // Mock JournalVoucherService trả null → NRE trong successMessage bị catch (showError) —
        // điểm khác biệt là KHÔNG được ném KeyNotFound khi re-render.
        var cut = RenderVoucher("loan-in");
        cut.Find("[data-testid='vf-amount']").Change("500000");
        cut.Find("[data-testid='voucher-save']").Click(); // nếu re-render vỡ → bUnit throw tại đây
        // Reach here = re-render sau save OK (không KeyNotFound).
        Assert.True(true);
    }
}