using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VanAn.CoreHub.Domain.Repositories;
using VanAn.CoreHub.Services;
using VanAn.ShopERP.Filters;
using VanAn.ShopERP.Services;
using VanAn.Shared.Domain;
using VanAn.Shared.Services;

namespace VanAn.ShopERP.Controllers
{
    /// <summary>
    /// Loyalty-C WS-B: Customer profile endpoints that trigger gamification missions.
    /// - POST /api/customer-profile/birthday → set birthday + trigger BirthdayEntry mission (one-time)
    /// - POST /api/customer-profile/pwa-installed → mark PWA install + trigger PWAInstall mission (one-time)
    /// Auth: X-Customer-Token header (same as CustomerIdentityController).
    /// </summary>
    [ApiController]
    [Route("api/customer-profile")]
    [AllowAnonymous]
    [ResolveCustomerTenant]
    public class CustomerProfileController(
        ICustomerTokenService customerTokenService,
        ICustomerRepository customerRepository,
        IMissionService missionService,
        ILogger<CustomerProfileController> logger) : ControllerBase
    {
        private readonly ICustomerTokenService _customerTokenService = customerTokenService;
        private readonly ICustomerRepository _customerRepository = customerRepository;
        private readonly IMissionService _missionService = missionService;
        private readonly ILogger<CustomerProfileController> _logger = logger;

        /// <summary>
        /// Set customer birthday (date only). Triggers BirthdayEntry mission (one-time reward).
        /// Subsequent calls update the birthday but do NOT re-award points (mission is one-time).
        /// </summary>
        [HttpPost("birthday")]
        public async Task<IActionResult> SetBirthday([FromHeader(Name = "X-Customer-Token")] string? token, [FromBody] SetBirthdayRequest request)
        {
            var customerId = _customerTokenService.ValidateToken(token);
            if (!customerId.HasValue)
                return Unauthorized(new { error = "Token không hợp lệ hoặc đã hết hạn." });

            if (request.Birthday == default)
                return BadRequest(new { error = "Ngày sinh không hợp lệ." });

            // Future date or unrealistic past (before 1900) rejected
            if (request.Birthday.Date > DateTime.UtcNow.Date)
                return BadRequest(new { error = "Ngày sinh không thể ở tương lai." });
            if (request.Birthday.Year < 1900)
                return BadRequest(new { error = "Năm sinh không hợp lệ." });

            var customer = await _customerRepository.GetByIdAsync(customerId.Value);
            if (customer == null)
                return NotFound(new { error = "Không tìm thấy khách hàng." });

            // Track whether this is the FIRST time setting birthday (to decide mission trigger)
            bool isFirstEntry = customer.Birthday == null;

            customer.SetBirthday(request.Birthday);
            await _customerRepository.UpdateAsync(customer);
            _logger.LogInformation("Customer {CustomerId} set birthday to {Birthday:yyyy-MM-dd}", customer.Id, request.Birthday);

            // Trigger BirthdayEntry mission only on first entry (mission is one-time anyway,
            // but skipping the call avoids unnecessary DB queries on subsequent updates).
            MissionCompletionResult? missionResult = null;
            if (isFirstEntry)
            {
                missionResult = await _missionService.CompleteMissionAsync(customer.Id, MissionType.BirthdayEntry);
                if (missionResult.Success)
                    _logger.LogInformation("BirthdayEntry mission completed for customer {CustomerId}, +{Points} points",
                        customer.Id, missionResult.PointsAwarded);
            }

            return Ok(new SetBirthdayResponse
            {
                Success = true,
                Birthday = customer.Birthday!.Value.ToString("yyyy-MM-dd"),
                PointsAwarded = missionResult?.PointsAwarded ?? 0,
                Message = missionResult?.Success == true
                    ? $"Đã lưu ngày sinh. +{missionResult.PointsAwarded} điểm thưởng!"
                    : "Đã lưu ngày sinh."
            });
        }

        /// <summary>
        /// Mark PWA install for the authenticated customer. Triggers PWAInstall mission (one-time reward).
        /// Called by KhachLink PWA after first install (via pwa.js beforeinstallprompt / window.matchMedia).
        /// Idempotent: if already marked, returns success without re-awarding points.
        /// </summary>
        [HttpPost("pwa-installed")]
        public async Task<IActionResult> MarkPwaInstalled([FromHeader(Name = "X-Customer-Token")] string? token)
        {
            var customerId = _customerTokenService.ValidateToken(token);
            if (!customerId.HasValue)
                return Unauthorized(new { error = "Token không hợp lệ hoặc đã hết hạn." });

            var customer = await _customerRepository.GetByIdAsync(customerId.Value);
            if (customer == null)
                return NotFound(new { error = "Không tìm thấy khách hàng." });

            // Idempotent: if already installed, return success without re-triggering mission
            if (customer.PWAInstalledAt.HasValue)
            {
                return Ok(new PwaInstalledResponse
                {
                    Success = true,
                    AlreadyInstalled = true,
                    PointsAwarded = 0,
                    Message = "PWA đã được cài đặt trước đó."
                });
            }

            customer.MarkPWAInstalled();
            await _customerRepository.UpdateAsync(customer);
            _logger.LogInformation("Customer {CustomerId} marked PWA installed at {InstalledAt:O}", customer.Id, customer.PWAInstalledAt);

            // Trigger PWAInstall mission (one-time reward)
            var missionResult = await _missionService.CompleteMissionAsync(customer.Id, MissionType.PWAInstall);

            return Ok(new PwaInstalledResponse
            {
                Success = true,
                AlreadyInstalled = false,
                PointsAwarded = missionResult.PointsAwarded,
                Message = missionResult.Success
                    ? $"Đã ghi nhận cài đặt PWA. +{missionResult.PointsAwarded} điểm thưởng!"
                    : "Đã ghi nhận cài đặt PWA."
            });
        }

        /// <summary>
        /// Submit a social share URL (Facebook or TikTok). Validates URL format then triggers the
        /// corresponding share mission (FacebookShare / TikTokShare) with daily cap enforcement.
        /// The share URL is stored as metadata in the MissionCompletion record for audit.
        /// </summary>
        [HttpPost("share")]
        public async Task<IActionResult> SubmitShare([FromHeader(Name = "X-Customer-Token")] string? token, [FromBody] SubmitShareRequest request)
        {
            var customerId = _customerTokenService.ValidateToken(token);
            if (!customerId.HasValue)
                return Unauthorized(new { error = "Token không hợp lệ hoặc đã hết hạn." });

            if (string.IsNullOrWhiteSpace(request.ShareUrl))
                return BadRequest(new { error = "URL chia sẻ không được để trống." });

            // Normalize + validate URL format
            string url = request.ShareUrl.Trim();
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || (uri.Scheme != "http" && uri.Scheme != "https"))
                return BadRequest(new { error = "URL không hợp lệ (phải bắt đầu bằng http:// hoặc https://)." });

            // WS-1.3 SC9/SC10: Validate URL pattern (not just domain) to filter out homepage/profile URLs.
            // Facebook: accept /<user>/posts/<id>, /permalink.php?story_id=, /share/v/, /share/<id>
            // TikTok: accept /@<user>/video/<id>, /<user>/video/<id>
            // Note: We do NOT verify the URL is real (Facebook/TikTok have no callback API — trust-based per task card Q1).
            // We only filter obvious format errors (homepage, profile, empty path).
            string host = uri.Host.ToLowerInvariant();
            string path = uri.AbsolutePath;
            MissionType missionType;
            if (host.Contains("facebook.com") || host.Contains("fb.com"))
            {
                bool validFb = path.Contains("/posts/", StringComparison.OrdinalIgnoreCase)
                            || path.Contains("/permalink", StringComparison.OrdinalIgnoreCase)
                            || uri.Query.Contains("story_id=", StringComparison.OrdinalIgnoreCase)
                            || path.Contains("/share/", StringComparison.OrdinalIgnoreCase);
                if (!validFb)
                    return BadRequest(new { error = "URL Facebook phải là link bài viết (vd: facebook.com/.../posts/...), không phải trang chủ hoặc profile." });
                missionType = MissionType.FacebookShare;
            }
            else if (host.Contains("tiktok.com"))
            {
                bool validTt = path.Contains("/video/", StringComparison.OrdinalIgnoreCase);
                if (!validTt)
                    return BadRequest(new { error = "URL TikTok phải là link video (vd: tiktok.com/@user/video/...), không phải trang chủ hoặc profile." });
                missionType = MissionType.TikTokShare;
            }
            else
                return BadRequest(new { error = "URL phải thuộc facebook.com hoặc tiktok.com." });

            // Verify customer exists
            var customer = await _customerRepository.GetByIdAsync(customerId.Value);
            if (customer == null)
                return NotFound(new { error = "Không tìm thấy khách hàng." });

            // Trigger share mission — MissionService enforces daily cap + awards points atomically.
            // Metadata stores the share URL for audit (proves the customer actually shared).
            var metadata = $"{{\"shareUrl\":\"{url.Replace("\"", "\\\"")}\",\"platform\":\"{missionType}\"}}";
            var missionResult = await _missionService.CompleteMissionAsync(customer.Id, missionType, metadata);

            if (!missionResult.Success)
            {
                _logger.LogInformation("Share mission not completed for customer {CustomerId}: {Error}", customer.Id, missionResult.Error);
                return Ok(new SubmitShareResponse
                {
                    Success = false,
                    PointsAwarded = 0,
                    Message = missionResult.Error ?? "Không thể ghi nhận chia sẻ."
                });
            }

            _logger.LogInformation("Share mission completed for customer {CustomerId} on {Platform}, +{Points} points. URL: {Url}",
                customer.Id, missionType, missionResult.PointsAwarded, url);

            return Ok(new SubmitShareResponse
            {
                Success = true,
                PointsAwarded = missionResult.PointsAwarded,
                NewPointBalance = missionResult.NewPointBalance,
                Message = $"+{missionResult.PointsAwarded} điểm thưởng cho chia sẻ!"
            });
        }
    }

    public class SetBirthdayRequest
    {
        public DateTime Birthday { get; set; }
    }

    public class SetBirthdayResponse
    {
        public bool Success { get; set; }
        public string Birthday { get; set; } = string.Empty;
        public int PointsAwarded { get; set; }
        public string Message { get; set; } = string.Empty;
    }

    public class PwaInstalledResponse
    {
        public bool Success { get; set; }
        public bool AlreadyInstalled { get; set; }
        public int PointsAwarded { get; set; }
        public string Message { get; set; } = string.Empty;
    }

    public class SubmitShareRequest
    {
        public string ShareUrl { get; set; } = string.Empty;
    }

    public class SubmitShareResponse
    {
        public bool Success { get; set; }
        public int PointsAwarded { get; set; }
        public int NewPointBalance { get; set; }
        public string Message { get; set; } = string.Empty;
    }
}
