using System.Security.Claims;
using Moq;
using VanAn.CoreHub.Services;
using VanAn.Shared.Domain;
using VanAn.Shared.Domain.Common;
using VanAn.ShopERP.Services;
using VanAn.UI.Platform.Models;

namespace VanAn.ShopERP.Tests.Services;

/// <summary>
/// THU CHI & CÔNG NỢ + Booking guides (2026-10-07): menu "Hướng dẫn" (SystemAdmin)
/// chứa link HTML guide kế toán + đặt lịch hẹn (wwwroot/guides — deploy qua CD).
/// </summary>
public class ShopErpMenuServiceTests
{
    private static ShopErpMenuService CreateService()
    {
        var tenantProvider = new Mock<ITenantProvider>();
        var featureFlag = new Mock<IVasFeatureFlagService>();
        return new ShopErpMenuService(tenantProvider.Object, featureFlag.Object);
    }

    private static ClaimsPrincipal PrincipalWithRoles(params string[] roles)
    {
        var claims = roles.Select(r => new Claim(ClaimTypes.Role, r)).ToList();
        claims.Add(new Claim(ClaimTypes.Name, "tester"));
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"));
    }

    [Fact]
    public async Task BuildAsync_SystemAdmin_IncludesHướngDẫnGuideLinks()
    {
        // Arrange
        var service = CreateService();
        var user = PrincipalWithRoles("SystemAdmin");

        // Act
        var items = await service.BuildAsync(user);

        // Assert — nhóm "Hướng dẫn" tồn tại + chứa link 2 guide mới
        var huongDan = items.SingleOrDefault(i => i.Title == "Hướng dẫn");
        huongDan.Should().NotBeNull();
        huongDan!.Children.Should().Contain(c => c.Title == "Hướng dẫn kế toán" && c.Url == "guides/Accounting_Guide.html");
        huongDan.Children.Should().Contain(c => c.Title == "Hướng dẫn đặt lịch hẹn" && c.Url == "guides/Booking_Guide.html");
    }

    [Fact]
    public async Task BuildAsync_Owner_DoesNotSeeHướngDẫn()
    {
        // Arrange
        var service = CreateService();
        var user = PrincipalWithRoles("Owner");

        // Act
        var items = await service.BuildAsync(user);

        // Assert — "Hướng dẫn" chỉ dành cho SystemAdmin (hành vi hiện tại giữ nguyên)
        items.Should().NotContain(i => i.Title == "Hướng dẫn");
    }

    [Fact]
    public async Task BuildAsync_Owner_IncludesImportExcel()
    {
        // Arrange — NHẬP LIỆU & SỔ SÁCH P4 (#1): menu Kế Toán có "Import Excel" (Owner)
        var service = CreateService();
        var user = PrincipalWithRoles("Owner");

        // Act
        var items = await service.BuildAsync(user);

        // Assert
        var keToan = items.SingleOrDefault(i => i.Title == "Kế Toán");
        keToan.Should().NotBeNull();
        keToan!.Children.Should().Contain(c => c.Title == "Import Excel" && c.Url == "/accounting/import");
    }

    // ── 2026-10-09 (user directive — 2 role kế toán): menu theo role ──

    [Fact]
    public async Task BuildAsync_ChiefAccountant_Sees3Groups_FullAccounting()
    {
        var tenantProvider = new Mock<ITenantProvider>();
        tenantProvider.Setup(t => t.HasTenant).Returns(true);
        tenantProvider.Setup(t => t.TenantId).Returns(Guid.Parse("00000000-0000-0000-0000-000000000001"));
        var featureFlag = new Mock<IVasFeatureFlagService>();
        featureFlag.Setup(f => f.CanAccessVasReportsAsync(It.IsAny<TenantId>(), It.IsAny<CancellationToken>()))
                   .ReturnsAsync(false); // HKD → Sổ HKD
        var service = new ShopErpMenuService(tenantProvider.Object, featureFlag.Object);
        var user = PrincipalWithRoles("ChiefAccountant");

        var items = await service.BuildAsync(user);

        // 3 nhóm: Kiểm kê + Kế Toán (đầy đủ) + Tài chính
        items.Should().Contain(i => i.Title == "Kiểm kê");
        items.Should().Contain(i => i.Title == "Tài chính");
        var keToan = items.SingleOrDefault(i => i.Title == "Kế Toán");
        keToan.Should().NotBeNull();
        keToan!.Children.Should().Contain(c => c.Title == "Lịch Sử Giao Dịch" && c.Url == "/accounting/history");
        keToan.Children.Should().Contain(c => c.Title == "Import Excel" && c.Url == "/accounting/import");
        keToan.Children.Should().Contain(c => c.Title == "Đóng Kỳ Kế Toán" && c.Url == "/accounting/period-closing");
        keToan.Children.Should().Contain(c => c.Title == "Sổ HKD (TT 152)" && c.Url == "/accounting/hkd-books");
        // KHÔNG thấy các nhóm khác (Vận hành/POS, Sản phẩm, Quản trị...)
        items.Should().NotContain(i => i.Title == "Vận hành");
        items.Should().NotContain(i => i.Title == "Sản phẩm");
        items.Should().NotContain(i => i.Title == "Quản trị");
    }

    [Fact]
    public async Task BuildAsync_DebtAccountant_SeesAccountingSubset_Only()
    {
        var service = CreateService();
        var user = PrincipalWithRoles("DebtAccountant");

        var items = await service.BuildAsync(user);

        // Chỉ nhóm Kế Toán — KHÔNG Kiểm kê/Tài chính/Vận hành.
        items.Should().NotContain(i => i.Title == "Kiểm kê");
        items.Should().NotContain(i => i.Title == "Tài chính");
        items.Should().NotContain(i => i.Title == "Vận hành");
        var keToan = items.SingleOrDefault(i => i.Title == "Kế Toán");
        keToan.Should().NotBeNull();
        // Có: Kế Toán index + Công Nợ + Số Dư Đầu Kỳ + Số Dư Tài Khoản
        keToan!.Children.Should().Contain(c => c.Url == "/accounting");
        keToan.Children.Should().Contain(c => c.Title == "Công Nợ" && c.Url == "/accounting/cong-no");
        keToan.Children.Should().Contain(c => c.Title == "Số Dư Đầu Kỳ" && c.Url == "/accounting/opening-balance");
        keToan.Children.Should().Contain(c => c.Title == "Số Dư Tài Khoản" && c.Url == "/accounting/balance");
        // Ẩn: Lịch sử giao dịch · Import Excel · Đóng kỳ kế toán · Sổ HKD · Báo cáo tài chính
        keToan.Children.Should().NotContain(c => c.Url == "/accounting/history");
        keToan.Children.Should().NotContain(c => c.Url == "/accounting/import");
        keToan.Children.Should().NotContain(c => c.Url == "/accounting/period-closing");
        keToan.Children.Should().NotContain(c => c.Url == "/accounting/hkd-books");
        keToan.Children.Should().NotContain(c => c.Url == "/accounting/financial-reports");
    }

    [Fact]
    public async Task BuildAsync_StoreKeeper_Unchanged_NoAccounting()
    {
        var service = CreateService();
        var user = PrincipalWithRoles("StoreKeeper");

        var items = await service.BuildAsync(user);

        items.Should().Contain(i => i.Title == "Kiểm kê");
        items.Should().NotContain(i => i.Title == "Kế Toán");
        items.Should().NotContain(i => i.Title == "Tài chính");
    }
}
