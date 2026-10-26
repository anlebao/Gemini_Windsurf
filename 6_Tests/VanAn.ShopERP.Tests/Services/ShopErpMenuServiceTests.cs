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

    /// <summary>Flatten đệ quy — menu Kế Toán có nhóm con 2 cấp (2026-10-17).</summary>
    private static IEnumerable<NavigationItem> Flatten(IEnumerable<NavigationItem> items)
    {
        foreach (var i in items)
        {
            yield return i;
            foreach (var c in Flatten(i.Children))
            {
                yield return c;
            }
        }
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
    public async Task BuildAsync_SystemAdmin_HasRootOnboardingOcrItem()
    {
        // Arrange
        var service = CreateService();
        var user = PrincipalWithRoles("SystemAdmin");

        // Act
        var items = await service.BuildAsync(user);

        // Assert — 2026-10-26 user directive: root-level item trỏ thẳng page riêng OCR
        var ocr = items.SingleOrDefault(i => i.Title == "Onboarding OCR");
        ocr.Should().NotBeNull();
        ocr!.Url.Should().Be("/admin/onboarding-ocr");
        ocr.Children.Should().BeNullOrEmpty();
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
        // 2026-10-17: Sổ HKD lồng trong nhóm con "Sổ HKD"; các sổ lồng trong "Sổ Sách"
        var soHkd = keToan.Children.SingleOrDefault(c => c.Title == "Sổ HKD");
        soHkd.Should().NotBeNull();
        soHkd!.Children.Should().Contain(c => c.Title == "Sổ HKD (TT 152)" && c.Url == "/accounting/hkd-books");
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
        // Có: Kế Toán index + Báo Cáo Quản Trị>Công Nợ + Số Dư Đầu Kỳ + Số Dư Tài Khoản
        keToan!.Children.Should().Contain(c => c.Url == "/accounting");
        var bcQuanTri = keToan.Children.SingleOrDefault(c => c.Title == "Báo Cáo Quản Trị");
        bcQuanTri.Should().NotBeNull();
        bcQuanTri!.Children.Should().Contain(c => c.Title == "Công Nợ" && c.Url == "/accounting/cong-no");
        keToan.Children.Should().Contain(c => c.Title == "Số Dư Đầu Kỳ" && c.Url == "/accounting/opening-balance");
        keToan.Children.Should().Contain(c => c.Title == "Số Dư Tài Khoản" && c.Url == "/accounting/balance");
        // Ẩn (mọi cấp): Lịch sử giao dịch · Import Excel · Đóng kỳ · Sổ sách · Sổ HKD · BCTC
        var flat = Flatten(keToan.Children).ToList();
        flat.Should().NotContain(c => c.Url == "/accounting/history");
        flat.Should().NotContain(c => c.Url == "/accounting/import");
        flat.Should().NotContain(c => c.Url == "/accounting/period-closing");
        flat.Should().NotContain(c => c.Url == "/accounting/hkd-books");
        flat.Should().NotContain(c => c.Url == "/accounting/financial-reports");
        flat.Should().NotContain(c => c.Title == "Sổ Sách");
        flat.Should().NotContain(c => c.Title == "Sổ HKD");
        flat.Should().NotContain(c => c.Title == "Báo Cáo Tài Chính");
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

    // ── Issue #192 (2026-10-15): menu khớp loại hình tenant — "Thành viên HTX" CHỈ cho HTX ──

    [Fact]
    public async Task BuildAsync_Owner_HtxTenant_SeesThanhVienHtxMenu()
    {
        var tenantProvider = new Mock<ITenantProvider>();
        tenantProvider.Setup(t => t.HasTenant).Returns(true);
        tenantProvider.Setup(t => t.TenantId).Returns(Guid.Parse("00000000-0000-0000-0000-000000000001"));
        var featureFlag = new Mock<IVasFeatureFlagService>();
        featureFlag.Setup(f => f.GetTenantTypeAsync(It.IsAny<TenantId>(), It.IsAny<CancellationToken>()))
                   .ReturnsAsync(TenantType.HTX);
        var service = new ShopErpMenuService(tenantProvider.Object, featureFlag.Object);
        var user = PrincipalWithRoles("Owner");

        var items = await service.BuildAsync(user);

        var thanhVienHtx = items.SingleOrDefault(i => i.Title == "Thành viên HTX");
        thanhVienHtx.Should().NotBeNull();
        thanhVienHtx!.Children.Should().Contain(c => c.Url == "/admin/membership");
        thanhVienHtx.Children.Should().Contain(c => c.Url == "/admin/membership/htx-profile");
    }

    [Fact]
    public async Task BuildAsync_Owner_HkdTenant_DoesNotSeeThanhVienHtxMenu()
    {
        var tenantProvider = new Mock<ITenantProvider>();
        tenantProvider.Setup(t => t.HasTenant).Returns(true);
        tenantProvider.Setup(t => t.TenantId).Returns(Guid.Parse("00000000-0000-0000-0000-000000000001"));
        var featureFlag = new Mock<IVasFeatureFlagService>();
        featureFlag.Setup(f => f.GetTenantTypeAsync(It.IsAny<TenantId>(), It.IsAny<CancellationToken>()))
                   .ReturnsAsync(TenantType.HKD);
        var service = new ShopErpMenuService(tenantProvider.Object, featureFlag.Object);
        var user = PrincipalWithRoles("Owner");

        var items = await service.BuildAsync(user);

        items.Should().NotContain(i => i.Title == "Thành viên HTX");
    }

    [Fact]
    public async Task BuildAsync_Owner_EnterpriseTenant_DoesNotSeeThanhVienHtxMenu()
    {
        var tenantProvider = new Mock<ITenantProvider>();
        tenantProvider.Setup(t => t.HasTenant).Returns(true);
        tenantProvider.Setup(t => t.TenantId).Returns(Guid.Parse("00000000-0000-0000-0000-000000000001"));
        var featureFlag = new Mock<IVasFeatureFlagService>();
        featureFlag.Setup(f => f.GetTenantTypeAsync(It.IsAny<TenantId>(), It.IsAny<CancellationToken>()))
                   .ReturnsAsync(TenantType.Enterprise_SME);
        var service = new ShopErpMenuService(tenantProvider.Object, featureFlag.Object);
        var user = PrincipalWithRoles("Owner");

        var items = await service.BuildAsync(user);

        items.Should().NotContain(i => i.Title == "Thành viên HTX");
    }

    // ── Notification Hub (2026-10-15): menu "Thông báo" cho Owner/ChiefAccountant có tenant ──

    [Fact]
    public async Task BuildAsync_OwnerWithTenant_SeesThongBaoMenu()
    {
        var tenantProvider = new Mock<ITenantProvider>();
        tenantProvider.Setup(t => t.HasTenant).Returns(true);
        tenantProvider.Setup(t => t.TenantId).Returns(Guid.Parse("00000000-0000-0000-0000-000000000001"));
        var service = new ShopErpMenuService(tenantProvider.Object, new Mock<IVasFeatureFlagService>().Object);
        var user = PrincipalWithRoles("Owner");

        var items = await service.BuildAsync(user);

        items.Should().Contain(i => i.Title == "Thông báo" && i.Url == "/notifications");
    }

    [Fact]
    public async Task BuildAsync_OwnerWithoutTenant_NoThongBaoMenu()
    {
        // SystemAdmin chưa impersonate (không tenant) — ẩn "Thông báo" (API cần tenant_id).
        var service = CreateService();
        var user = PrincipalWithRoles("Owner");

        var items = await service.BuildAsync(user);

        items.Should().NotContain(i => i.Title == "Thông báo");
    }

    // ── 2026-10-17 (user directive): nhóm menu con trong "Kế Toán" — 4 nhóm ──

    [Fact]
    public async Task BuildAsync_Owner_HkdTenant_AccountingSubGroups()
    {
        var tenantProvider = new Mock<ITenantProvider>();
        tenantProvider.Setup(t => t.HasTenant).Returns(true);
        tenantProvider.Setup(t => t.TenantId).Returns(Guid.Parse("00000000-0000-0000-0000-000000000001"));
        var featureFlag = new Mock<IVasFeatureFlagService>();
        featureFlag.Setup(f => f.CanAccessVasReportsAsync(It.IsAny<TenantId>(), It.IsAny<CancellationToken>()))
                   .ReturnsAsync(false); // HKD → nhóm "Sổ HKD", không có "Báo Cáo Tài Chính"
        var service = new ShopErpMenuService(tenantProvider.Object, featureFlag.Object);
        var user = PrincipalWithRoles("Owner");

        var items = await service.BuildAsync(user);

        var keToan = items.Single(i => i.Title == "Kế Toán");

        // Nhóm "Sổ Sách" — 3 sổ DN
        var soSach = keToan.Children.Single(c => c.Title == "Sổ Sách");
        soSach.Children.Should().Contain(c => c.Url == "/accounting/journal");
        soSach.Children.Should().Contain(c => c.Url == "/accounting/ledger");
        soSach.Children.Should().Contain(c => c.Url == "/accounting/sub-ledger");

        // Nhóm "Báo Cáo Quản Trị" — Công Nợ
        var bcqt = keToan.Children.Single(c => c.Title == "Báo Cáo Quản Trị");
        bcqt.Children.Should().Contain(c => c.Url == "/accounting/cong-no");

        // HKD → nhóm "Sổ HKD" có link hkd-books; KHÔNG có nhóm "Báo Cáo Tài Chính"
        var soHkd = keToan.Children.Single(c => c.Title == "Sổ HKD");
        soHkd.Children.Should().Contain(c => c.Url == "/accounting/hkd-books");
        keToan.Children.Should().NotContain(c => c.Title == "Báo Cáo Tài Chính");

        // Link cũ không còn nằm phẳng (đã vào nhóm)
        keToan.Children.Should().NotContain(c => c.Url == "/accounting/cong-no");
        keToan.Children.Should().NotContain(c => c.Url == "/accounting/ledger");
    }

    [Fact]
    public async Task BuildAsync_Owner_EnterpriseTenant_BaoCaoTaiChinhGroup_NoSoHkd()
    {
        var tenantProvider = new Mock<ITenantProvider>();
        tenantProvider.Setup(t => t.HasTenant).Returns(true);
        tenantProvider.Setup(t => t.TenantId).Returns(Guid.Parse("00000000-0000-0000-0000-000000000002"));
        var featureFlag = new Mock<IVasFeatureFlagService>();
        featureFlag.Setup(f => f.CanAccessVasReportsAsync(It.IsAny<TenantId>(), It.IsAny<CancellationToken>()))
                   .ReturnsAsync(true); // DN → nhóm "Báo Cáo Tài Chính", không có "Sổ HKD"
        var service = new ShopErpMenuService(tenantProvider.Object, featureFlag.Object);
        var user = PrincipalWithRoles("Owner");

        var items = await service.BuildAsync(user);

        var keToan = items.Single(i => i.Title == "Kế Toán");
        var bctc = keToan.Children.Single(c => c.Title == "Báo Cáo Tài Chính");
        bctc.Children.Should().Contain(c => c.Url == "/accounting/financial-reports");
        keToan.Children.Should().NotContain(c => c.Title == "Sổ HKD");
        Flatten(keToan.Children).Should().NotContain(c => c.Url == "/accounting/hkd-books");
    }
}
