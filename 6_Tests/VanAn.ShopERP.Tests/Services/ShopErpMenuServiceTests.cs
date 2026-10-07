using System.Security.Claims;
using VanAn.CoreHub.Services;
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
}
