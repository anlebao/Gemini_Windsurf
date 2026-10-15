using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using VanAn.CoreHub.Services;
using VanAn.CoreHub.Tests.TestInfrastructure;
using VanAn.Gateway.Controllers;
using VanAn.Shared.Domain.Aggregates.KhachLinkAggregate;
using Xunit;

namespace VanAn.Core.Tests.Services;

/// <summary>
/// Icon app (PWA) per KhachLink instance (2026-10-15): Domain guard · service set/clear ·
/// manifest động theo Host · icons endpoint (override vs default theo Profile).
/// </summary>
public class KhachLinkInstanceIconTests
{
    // ── Domain ──────────────────────────────────────────────────────────────

    [Fact(DisplayName = "KLI-1: UpdateIcon set bytes + contentType + IconUpdatedAt")]
    public void Domain_UpdateIcon_SetsFields()
    {
        var instance = new KhachLinkInstance("Test", KhachLinkProfile.Directory, "dir.example.com");
        byte[] png = { 0x89, 0x50, 0x4E, 0x47, 1, 2, 3 };

        instance.UpdateIcon(png, "image/png");

        Assert.Equal(png, instance.IconData);
        Assert.Equal("image/png", instance.IconContentType);
        Assert.NotNull(instance.IconUpdatedAt);
    }

    [Fact(DisplayName = "KLI-2: UpdateIcon null = clear override (về default theo Profile)")]
    public void Domain_UpdateIcon_Null_Clears()
    {
        var instance = new KhachLinkInstance("Test", KhachLinkProfile.Directory, "dir.example.com");
        instance.UpdateIcon(new byte[] { 1, 2, 3 }, "image/png");

        instance.UpdateIcon(null, null);

        Assert.Null(instance.IconData);
        Assert.Null(instance.IconContentType);
        Assert.Null(instance.IconUpdatedAt);
    }

    [Fact(DisplayName = "KLI-3: UpdateIcon > 2MB → throw")]
    public void Domain_UpdateIcon_TooLarge_Throws()
    {
        var instance = new KhachLinkInstance("Test", KhachLinkProfile.FullCommerce, "shop.example.com");
        Assert.Throws<ArgumentException>(() => instance.UpdateIcon(new byte[2 * 1024 * 1024 + 1], "image/png"));
    }

    // ── Service ─────────────────────────────────────────────────────────────

    [Fact(DisplayName = "KLI-4: service set + clear icon (PG factory)")]
    public async Task Service_SetAndClearIcon_Works()
    {
        using TestContextScope scope = VanAnDbContextTestFactory.Create();
        var db = scope.Context;
        var svc = new KhachLinkInstanceService(db, new Mock<IAuditTrailService>().Object, NullLogger<KhachLinkInstanceService>.Instance);

        var instance = new KhachLinkInstance("Test", KhachLinkProfile.Logistics, "log.example.com");
        db.KhachLinkInstances.Add(instance);
        await db.SaveChangesAsync();

        Assert.True(await svc.UpdateIconAsync(instance.Id, new byte[] { 9, 8, 7 }, "image/png"));
        var reloaded = await svc.GetByIdAsync(instance.Id);
        Assert.NotNull(reloaded!.IconData);
        Assert.Equal("image/png", reloaded.IconContentType);

        Assert.True(await svc.UpdateIconAsync(instance.Id, null, null));
        reloaded = await svc.GetByIdAsync(instance.Id);
        Assert.Null(reloaded!.IconData);

        Assert.False(await svc.UpdateIconAsync(Guid.NewGuid(), new byte[] { 1 }, "image/png")); // không tồn tại
    }

    // ── Controller: manifest + icons ────────────────────────────────────────

    private static (PublicKhachLinkIconController controller, Mock<IKhachLinkInstanceService> service, string wwwroot) BuildController()
    {
        var service = new Mock<IKhachLinkInstanceService>();
        string wwwroot = Path.Combine(Path.GetTempPath(), "kli-icons-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(wwwroot, "khachlink-icons"));
        // Default icon theo profile
        File.WriteAllBytes(Path.Combine(wwwroot, "khachlink-icons", "directory.png"), new byte[] { 1, 2, 3 });

        var env = new Mock<IWebHostEnvironment>();
        env.Setup(e => e.WebRootPath).Returns(wwwroot);

        var controller = new PublicKhachLinkIconController(
            service.Object, env.Object, NullLogger<PublicKhachLinkIconController>.Instance);
        controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };
        return (controller, service, wwwroot);
    }

    [Fact(DisplayName = "KLI-5: manifest theo Host → instance (name/theme/icon riêng)")]
    public async Task Manifest_ByHost_ReturnsInstanceManifest()
    {
        var (controller, service, _) = BuildController();
        var instance = new KhachLinkInstance("Danh bạ Tìm Là Thấy", KhachLinkProfile.Directory, "timlathay.com");
        service.Setup(s => s.GetByDomainAsync("timlathay.com", It.IsAny<CancellationToken>())).ReturnsAsync(instance);

        controller.HttpContext.Request.Host = new HostString("timlathay.com");
        var result = (ContentResult)await controller.GetManifest(CancellationToken.None);

        Assert.Equal("application/manifest+json", result.ContentType);
        Assert.Contains("Danh bạ Tìm Là Thấy", result.Content);
        Assert.Contains($"khachlink-icons/{instance.Id}.png", result.Content);
    }

    [Fact(DisplayName = "KLI-6: manifest unknown domain → manifest generic (default icon)")]
    public async Task Manifest_UnknownDomain_ReturnsGeneric()
    {
        var (controller, service, _) = BuildController();
        service.Setup(s => s.GetByDomainAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync((KhachLinkInstance?)null);

        controller.HttpContext.Request.Host = new HostString("unknown.example.com");
        var result = (ContentResult)await controller.GetManifest(CancellationToken.None);

        Assert.Contains("khachlink-icons/default.png", result.Content);
    }

    [Fact(DisplayName = "KLI-7: icons/{id} — override bytes khi có; default theo Profile khi không")]
    public async Task Icons_OverrideOrDefault()
    {
        var (controller, service, _) = BuildController();

        // Có override
        var withIcon = new KhachLinkInstance("Shop A", KhachLinkProfile.FullCommerce, "a.example.com");
        withIcon.UpdateIcon(new byte[] { 5, 6, 7, 8 }, "image/png");
        service.Setup(s => s.GetByIdAsync(withIcon.Id, It.IsAny<CancellationToken>())).ReturnsAsync(withIcon);
        var overrideResult = (FileContentResult)await controller.GetIcon(withIcon.Id, CancellationToken.None);
        Assert.Equal(new byte[] { 5, 6, 7, 8 }, overrideResult.FileContents);
        Assert.Equal("image/png", overrideResult.ContentType);

        // Không override → default theo Profile (directory.png)
        var noIcon = new KhachLinkInstance("Dir B", KhachLinkProfile.Directory, "b.example.com");
        service.Setup(s => s.GetByIdAsync(noIcon.Id, It.IsAny<CancellationToken>())).ReturnsAsync(noIcon);
        var defaultResult = (FileContentResult)await controller.GetIcon(noIcon.Id, CancellationToken.None);
        Assert.Equal(new byte[] { 1, 2, 3 }, defaultResult.FileContents);

        // Không tồn tại → 404
        service.Setup(s => s.GetByIdAsync(Guid.NewGuid(), It.IsAny<CancellationToken>())).ReturnsAsync((KhachLinkInstance?)null);
        Assert.IsType<NotFoundResult>(await controller.GetIcon(Guid.NewGuid(), CancellationToken.None));
    }
}
