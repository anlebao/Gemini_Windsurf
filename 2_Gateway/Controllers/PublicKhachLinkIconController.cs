using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VanAn.CoreHub.Services;
using VanAn.Shared.Domain.Aggregates.KhachLinkAggregate;

namespace VanAn.Gateway.Controllers;

/// <summary>
/// Icon app (PWA) per KhachLink instance (2026-10-15, user directive):
///   GET /api/public/khachlink-instances/icons/{instanceId}  — bytes icon (override hoặc default theo Profile)
///   GET /api/public/khachlink-instances/icons/default       — icon generic (fallback unknown domain)
///   GET /api/public/khachlink-instances/manifest            — manifest PWA động theo Host header
/// KhachLink nginx proxy: /manifest.json → manifest · /khachlink-icons/* → icons (cùng origin).
/// Default icons theo Profile nằm trong wwwroot/khachlink-icons (6 PNG).
/// </summary>
[ApiController]
[Route("api/public/khachlink-instances")]
[Authorize] // W12-G7: class-level [Authorize] bắt buộc; các action public dùng [AllowAnonymous].
public class PublicKhachLinkIconController(
    IKhachLinkInstanceService instanceService,
    IWebHostEnvironment env,
    ILogger<PublicKhachLinkIconController> logger) : ControllerBase
{
    private readonly IKhachLinkInstanceService _instanceService = instanceService;
    private readonly IWebHostEnvironment _env = env;
    private readonly ILogger<PublicKhachLinkIconController> _logger = logger;

    private const string DefaultContentType = "image/png";

    /// <summary>Icon bytes của instance (override) hoặc default theo Profile. 404 nếu không tồn tại.</summary>
    [HttpGet("icons/{instanceId:guid}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetIcon(Guid instanceId, CancellationToken ct = default)
    {
        var instance = await _instanceService.GetByIdAsync(instanceId, ct);
        if (instance is null)
            return NotFound();

        if (instance.IconData is { Length: > 0 })
        {
            return File(instance.IconData, instance.IconContentType ?? DefaultContentType,
                enableRangeProcessing: false);
        }

        return File(DefaultIconBytes(instance.Profile), DefaultContentType);
    }

    /// <summary>Icon generic — fallback khi chưa biết instance (unknown domain).</summary>
    [HttpGet("icons/default")]
    [AllowAnonymous]
    public IActionResult GetDefaultIcon()
        => File(DefaultIconBytes(null), DefaultContentType);

    /// <summary>
    /// Manifest PWA động theo Host header → instance (Label/theme/icon riêng).
    /// Unknown domain → manifest generic (icon default) để PWA vẫn cài được.
    /// Icon src là đường dẫn TƯƠNG ĐỐI (khachlink-icons/...) — nginx proxy cùng origin.
    /// </summary>
    [HttpGet("manifest")]
    [AllowAnonymous]
    public async Task<IActionResult> GetManifest(CancellationToken ct = default)
    {
        string? host = Request.Host.Host?.ToLowerInvariant();
        if (!string.IsNullOrWhiteSpace(host))
        {
            var instance = await _instanceService.GetByDomainAsync(host, ct);
            if (instance is not null)
            {
                return Content(BuildManifest(
                    instance.Label,
                    ShortName(instance.Label),
                    instance.HeaderColor,
                    instance.FooterColor,
                    $"khachlink-icons/{instance.Id}.png",
                    instance.Profile), "application/manifest+json");
            }
        }

        // Unknown domain → manifest generic.
        return Content(BuildManifest(
            "Vạn An",
            "Vạn An",
            null,
            null,
            "khachlink-icons/default.png",
            null), "application/manifest+json");
    }

    private string BuildManifest(string name, string shortName, string? themeColor, string? backgroundColor, string iconSrc, KhachLinkProfile? profile)
    {
        string theme = themeColor ?? "#2563eb";
        string background = backgroundColor ?? "#ffffff";
        string description = profile switch
        {
            KhachLinkProfile.Directory => "Danh bạ doanh nghiệp địa phương — tìm cửa hàng, dịch vụ gần bạn",
            KhachLinkProfile.Logistics => "Sàn giao nhận — giao hàng nhanh, đặt xe tải",
            KhachLinkProfile.JobMarket => "Chợ việc làm — kết nối người tìm việc và nhà tuyển dụng",
            KhachLinkProfile.Reseller => "Nền tảng bán hàng trung gian — kinh doanh online dễ dàng",
            KhachLinkProfile.FullCommerce => "Mua sắm trực tuyến — nhiều ưu đãi và quà tặng",
            _ => "Vạn An — ứng dụng của bạn"
        };

        // Icon src relative — nginx proxy /khachlink-icons/* → Gateway icons endpoint.
        return $$"""
        {
          "id": "/",
          "name": "{{Escape(name)}}",
          "short_name": "{{Escape(shortName)}}",
          "description": "{{Escape(description)}}",
          "start_url": "/",
          "display": "standalone",
          "background_color": "{{background}}",
          "theme_color": "{{theme}}",
          "orientation": "portrait",
          "scope": "/",
          "lang": "vi",
          "categories": ["business", "lifestyle", "shopping"],
          "icons": [
            {
              "src": "{{iconSrc}}",
              "sizes": "192x192",
              "type": "image/png",
              "purpose": "any maskable"
            },
            {
              "src": "{{iconSrc}}",
              "sizes": "512x512",
              "type": "image/png",
              "purpose": "any maskable"
            }
          ]
        }
        """;
    }

    private static string ShortName(string label)
        => label.Length <= 12 ? label : label[..11] + "…";

    private static string Escape(string value)
    {
        // Giữ ký tự tiếng Việt (không escape \uXXXX) — JSON UTF-8 browsers xử lý tốt.
        var options = new System.Text.Json.JsonSerializerOptions
        {
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };
        return System.Text.Json.JsonSerializer.Serialize(value, options)[1..^1];
    }

    private byte[] DefaultIconBytes(KhachLinkProfile? profile)
    {
        string fileName = profile switch
        {
            KhachLinkProfile.FullCommerce => "fullcommerce.png",
            KhachLinkProfile.Directory => "directory.png",
            KhachLinkProfile.Logistics => "logistics.png",
            KhachLinkProfile.JobMarket => "jobmarket.png",
            KhachLinkProfile.Reseller => "reseller.png",
            _ => "default.png"
        };

        try
        {
            string path = Path.Combine(_env.WebRootPath ?? Path.Combine(AppContext.BaseDirectory, "wwwroot"), "khachlink-icons", fileName);
            if (System.IO.File.Exists(path))
                return System.IO.File.ReadAllBytes(path);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read default icon {File}", fileName);
        }

        return new byte[0];
    }
}
