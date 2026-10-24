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
    /// 2026-10-24 (D1b/D2/D4 — install-identity fix):
    ///   - Label trông như slug (viết thường, gạch nối, không dấu cách — VD "timlathay")
    ///     → fallback "Vạn An App" thay vì lộ slug lên dialog cài đặt của Chrome.
    ///   - Thêm related_applications (platform=webapp) + launch_handler — page dùng
    ///     navigator.getInstalledRelatedApps() detect WebAPK đã cài (clear site data
    ///     KHÔNG gỡ WebAPK; Chrome ngừng fire beforeinstallprompt khi đã cài).
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
                var label = LooksLikeSlug(instance.Label) ? "Vạn An App" : instance.Label;
                return Content(BuildManifest(
                    label,
                    ShortName(label),
                    instance.HeaderColor,
                    instance.FooterColor,
                    $"khachlink-icons/{instance.Id}.png",
                    instance.Profile,
                    host), "application/manifest+json");
            }
        }

        // Unknown domain → manifest generic.
        return Content(BuildManifest(
            "Vạn An App",
            "Vạn An App",
            null,
            null,
            "khachlink-icons/default.png",
            null,
            host), "application/manifest+json");
    }

    private string BuildManifest(string name, string shortName, string? themeColor, string? backgroundColor, string iconSrc, KhachLinkProfile? profile, string? host)
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

        // related_applications(platform=webapp, url = start_url tuyệt đối) — chỉ add khi
        // biết host. Nếu thiếu, JSON vẫn hợp lệ (trường bị bỏ qua).
        string relatedApplications = string.IsNullOrWhiteSpace(host)
            ? ""
            : $$"""
            ,
            "related_applications": [
              {
                "platform": "webapp",
                "url": "https://{{host}}/"
              }
            ]
            """;

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
          ],
          "launch_handler": {
            "client_mode": "navigate-existing"
          }{{relatedApplications}}
        }
        """;
    }

    /// <summary>D1b: Label slug thô (VD "timlathay", "thuc-an-tu-thien") — không phải tên hiển thị.</summary>
    private static bool LooksLikeSlug(string? label)
        => string.IsNullOrWhiteSpace(label)
           || System.Text.RegularExpressions.Regex.IsMatch(label, @"^[a-z0-9]+(-[a-z0-9]+)*$");

    private static string ShortName(string label)
    {
        if (label.Length <= 12) return label;
        // Cắt tại ranh giới từ trong 12 ký tự — "Giữ Khách - Bán Thêm" → "Giữ Khách".
        var words = label.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var result = string.Empty;
        foreach (var word in words)
        {
            var candidate = result.Length == 0 ? word : $"{result} {word}";
            if (candidate.Length > 12) break;
            result = candidate;
        }
        // Trim separator rớt lại ("Giữ Khách -" → "Giữ Khách").
        var trimmed = result.TrimEnd(' ', '-', '–', '|', ':');
        return trimmed.Length > 0 ? trimmed : label[..11] + "…";
    }

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
