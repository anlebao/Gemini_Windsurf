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

        // 2026-10-24: IconData upload có thể là PNG hỏng (đã từng xảy ra — Chrome 155
        // báo "resource isn't a valid image" → no-acceptable-icon → KHÔNG cài được PWA,
        // mặc dù đọc header vẫn thấy 512x512). Validate nghiêm → fallback icon default.
        if (instance.IconData is { Length: > 0 } && IsValidPng(instance.IconData))
        {
            return File(instance.IconData, instance.IconContentType ?? DefaultContentType,
                enableRangeProcessing: false);
        }

        return File(DefaultIconBytes(instance.Profile), DefaultContentType);
    }

    /// <summary>
    /// Validate PNG đủ nghiêm để khớp decoder icon của Chrome: signature + IHDR
    /// (depth 8, non-interlaced, color type hợp lệ) + IDAT giải nén đúng kích thước
    /// kỳ vọng ((width*channels+1)*height). PNG khai RGBA nhưng pixel data RGB-sized
    /// (bug từ 4d71318a) bị loại — đây chính là root cause "không cài được PWA".
    /// </summary>
    private static bool IsValidPng(byte[] data)
    {
        if (data is null || data.Length < 33) return false;
        ReadOnlySpan<byte> signature = stackalloc byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };
        if (!data.AsSpan(0, 8).SequenceEqual(signature)) return false;

        int width = (data[16] << 24) | (data[17] << 16) | (data[18] << 8) | data[19];
        int height = (data[20] << 24) | (data[21] << 16) | (data[22] << 8) | data[23];
        if (width <= 0 || height <= 0 || width > 4096 || height > 4096) return false;
        if (data[24] != 8 || data[28] != 0) return false; // bit depth 8 + non-interlaced
        int channels = data[25] switch { 0 => 1, 2 => 3, 3 => 1, 4 => 2, 6 => 4, _ => -1 };
        if (channels < 0) return false;
        long expected = ((long)width * channels + 1) * height;

        try
        {
            var idat = new List<byte>();
            int pos = 8;
            while (pos + 8 <= data.Length)
            {
                int len = (data[pos] << 24) | (data[pos + 1] << 16) | (data[pos + 2] << 8) | data[pos + 3];
                string type = System.Text.Encoding.ASCII.GetString(data, pos + 4, 4);
                if (type == "IDAT")
                {
                    if (pos + 8 + len > data.Length) return false;
                    idat.AddRange(data.AsSpan(pos + 8, len).ToArray());
                }
                pos += 12 + len;
                if (type == "IEND") break;
            }
            if (idat.Count == 0) return false;

            using var input = new MemoryStream(idat.ToArray());
            using var zlib = new System.IO.Compression.ZLibStream(input, System.IO.Compression.CompressionMode.Decompress);
            using var output = new MemoryStream();
            zlib.CopyTo(output);
            return output.Length == expected;
        }
        catch
        {
            return false;
        }
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
    /// 2026-10-24 (D1b + v28-lite):
    ///   - Label trông như slug (viết thường, gạch nối, không dấu cách — VD "timlathay")
    ///     → fallback "Vạn An App" thay vì lộ slug lên dialog cài đặt của Chrome.
    ///   - v28-lite: KHÔNG serve related_applications/launch_handler — WebAPK minting
    ///     (Play services) không tạo app dù Chrome báo INSTALLABLE (thí nghiệm 1 biến).
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
                    $"khachlink-icons/{instance.Id}.png?v=2",
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
            "khachlink-icons/default.png?v=2",
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

        // Icon src relative — nginx proxy /khachlink-icons/* → Gateway icons endpoint.
        // 2026-10-24 (v28-lite experiment): KHÔNG serve related_applications + launch_handler.
        // Chrome báo INSTALLABLE (CDP getInstallabilityErrors=[]) nhưng WebAPK minting
        // (Google Play services — engine riêng) không tạo app trên thiết bị thật. Hai
        // trường này là khác biệt cấu trúc duy nhất so với PWA cài được (5giay/photopea).
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
