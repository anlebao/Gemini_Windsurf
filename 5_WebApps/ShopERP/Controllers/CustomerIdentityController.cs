using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using System.Text.Json;
using VanAn.CoreHub.Domain.Repositories;
using VanAn.CoreHub.Infrastructure.Messaging;
using VanAn.CoreHub.Repositories;
using VanAn.CoreHub.Services;
using VanAn.ShopERP.Filters;
using VanAn.ShopERP.Services;
using VanAn.Shared.Domain;
using VanAn.Shared.Services;

namespace VanAn.ShopERP.Controllers
{
    /// <summary>
    /// W17-T1: Customer Identity — Phone OTP login flow.
    /// AllowAnonymous: OTP endpoints are accessed by unauthenticated KhachLink users.
    /// </summary>
    [ApiController]
    [Route("api/customer-identity")]
    [AllowAnonymous]
    [ResolveCustomerTenant]
    public class CustomerIdentityController(
        IOtpService otpService,
        ICustomerTokenService customerTokenService,
        ICustomerRepository customerRepository,
        ILoyaltyRewardsService loyaltyRewardsService,
        IMissionService missionService,
        ICustomerMergeService customerMergeService,
        IOutboxRepository outboxRepository,
        VanAn.CoreHub.Services.LoyaltyReadRouter readRouter,
        ICustomerOtpDelivery otpDelivery,
        IConfiguration configuration,
        ILogger<CustomerIdentityController> logger) : ControllerBase
    {
        private readonly IOtpService _otpService = otpService;
        private readonly ICustomerTokenService _customerTokenService = customerTokenService;
        private readonly ICustomerRepository _customerRepository = customerRepository;
        private readonly ILoyaltyRewardsService _loyaltyRewardsService = loyaltyRewardsService;
        private readonly IMissionService _missionService = missionService;
        private readonly ICustomerMergeService _customerMergeService = customerMergeService;
        private readonly IOutboxRepository _outboxRepository = outboxRepository;
        private readonly IConfiguration _configuration = configuration;
        private readonly ILogger<CustomerIdentityController> _logger = logger;
        // Loyalty Consistency Fix Phase 2 (BUG #7): mode-aware balance for /api/customers/me
        private readonly VanAn.CoreHub.Services.LoyaltyReadRouter _readRouter = readRouter;
        private readonly ICustomerOtpDelivery _otpDelivery = otpDelivery;

        /// <summary>
        /// Send OTP to phone number.
        /// In production: SMS via notification service.
        /// CC-S4 (Issue #4): X-Dev-OTP header removed — was unconditional security hole.
        /// RV tests use POST /api/customer-identity/dev-token (secret-gated) instead.
        /// </summary>
        [HttpPost("otp/send")]
        public async Task<IActionResult> SendOtp([FromBody] SendOtpRequest request)
        {
            // Email-first (2026-11-21): email contact → Brevo OTP channel; phone → SMS (eSMS).
            var useEmail = !string.IsNullOrWhiteSpace(request.Email);
            var contact = useEmail ? request.Email!.Trim() : request.PhoneNumber?.Trim();
            if (string.IsNullOrWhiteSpace(contact))
                return BadRequest(new { error = "Cần email hoặc số điện thoại để nhận mã." });

            var otp = _otpService.GenerateAndStoreOtp(contact);
            var delivered = await _otpDelivery.DeliverAsync(contact, useEmail, otp, HttpContext.RequestAborted);
            if (!delivered)
            {
                _logger.LogWarning("OTP delivery failed for contact {Contact}", MaskContact(contact));
                return StatusCode(502, new { error = "Không gửi được mã xác minh. Vui lòng thử lại sau." });
            }
            _logger.LogInformation("OTP generated + delivered via {Channel} for {Contact}", useEmail ? "email" : "sms", MaskContact(contact));

            return Ok(new { message = "Mã xác minh đã được gửi.", channel = useEmail ? "email" : "sms", contactSuffix = MaskContact(contact) });
        }

        /// <summary>
        /// Verify OTP and return customer token + loyalty info.
        /// Creates a new customer record if phone is first-time.
        /// </summary>
        [HttpPost("otp/verify")]
        public async Task<IActionResult> VerifyOtp([FromBody] VerifyOtpRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.PhoneNumber) || string.IsNullOrWhiteSpace(request.Otp))
                return BadRequest(new { error = "Số điện thoại và OTP không được để trống." });

            if (!VerifyOtpAnyContact(request.Otp, "customer-login", request.Channel, request.PhoneNumber, request.Email))
                return BadRequest(new { error = "OTP không đúng hoặc đã hết hạn." });

            // Find or create customer by phone number.
            // Note: PhoneNumber is PII-encrypted — LINQ comparison requires loading all and filtering
            // in-memory (EF can't translate encrypted-column WHERE clauses reliably).
            var tenantId = GetTenantId(request.TenantId);
            var allCustomers = await _customerRepository.GetAllActiveAsync();
            var customer = allCustomers.FirstOrDefault(c => c.PhoneNumber == request.PhoneNumber);
            if (customer == null)
            {
                var newCustomer = new Customer(new TenantId(tenantId), request.DisplayName ?? "Khách hàng", request.PhoneNumber);
                if (request.DeviceId.HasValue)
                    newCustomer.UpdateCustomerDetails(newCustomer.FullName, newCustomer.PhoneNumber, newCustomer.Email, newCustomer.CustomerTier, request.DeviceId, true);
                customer = await _customerRepository.AddAsync(newCustomer);
                _logger.LogInformation("New customer created: {CustomerId}", customer.Id);

                // TD-CUSTSYNC-001: Enqueue CustomerCreated outbox event for SQLite→PG sync.
                // NatsSyncWorker publishes "vanan.shoperp.customer.created" → Gateway DataSyncSubscriber
                // upserts customer to PostgreSQL so Gateway knows about OTP-verified customers.
                try
                {
                    var payload = new
                    {
                        CustomerId = customer.Id,
                        TenantId = customer.TenantId.Value,
                        FullName = customer.FullName,
                        PhoneNumber = customer.PhoneNumber,
                        Email = customer.Email,
                        DeviceId = customer.DeviceId,
                        IdentityLevel = (int)customer.IdentityLevel
                    };
                    string eventData = JsonSerializer.Serialize(payload);
                    var outboxEvent = new OutboxEvent(
                        customer.TenantId,
                        new ElectronicInvoiceId(Guid.Empty),
                        "CustomerCreated",
                        eventData);
                    await _outboxRepository.EnqueueAsync(outboxEvent);
                    _logger.LogInformation("Enqueued CustomerCreated event to Outbox for customer {CustomerId}", customer.Id);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to enqueue CustomerCreated event for customer {CustomerId}", customer.Id);
                }
            }

            // OTP verification upgrades identity level to Verified
            if (customer.IdentityLevel < IdentityLevel.Verified)
            {
                customer.UpgradeIdentityLevel(IdentityLevel.Verified);
                customer.MarkOtpVerified();
                await _customerRepository.UpdateAsync(customer);
                _logger.LogInformation("Customer {CustomerId} upgraded to Verified via OTP", customer.Id);

                // Loyalty-C WS-B: Trigger OtpVerify mission (one-time reward for first OTP verification)
                _ = await _missionService.CompleteMissionAsync(customer.Id, MissionType.OtpVerify);
            }

            // TD-CUSTSYNC-001 / Issue #106: Merge DeviceId-based guest stubs into login customer.
            // If request has DeviceId, merge any guest stubs from the same device into this login account.
            if (request.DeviceId.HasValue && request.DeviceId.Value != Guid.Empty)
            {
                try
                {
                    var mergeResult = await _customerMergeService.MergeDeviceStubsIntoLoginAsync(customer.Id, request.DeviceId.Value);
                    if (mergeResult.StubsMerged > 0)
                    {
                        _logger.LogInformation("TD-CUSTSYNC-001: Merged {Stubs} guest stub(s), transferred {Points} points to customer {CustomerId}",
                            mergeResult.StubsMerged, mergeResult.PointsTransferred, customer.Id);
                    }
                }
                catch (Exception mergeEx)
                {
                    // Non-blocking: merge failure should NOT prevent login
                    _logger.LogWarning(mergeEx, "TD-CUSTSYNC-001: Merge failed for customer {CustomerId} — login proceeds, merge deferred", customer.Id);
                }
            }

            var customerToken = _customerTokenService.CreateToken(customer.Id);
            var rewards = await _loyaltyRewardsService.GetOrCreateCustomerRewardsAsync(customer.Id, new TenantId(tenantId));
            // Loyalty Consistency Fix Phase 2 (BUG #7): mode-aware balance
            int effectiveBalance = await _readRouter.GetEffectiveBalanceAsync(customer.TenantId.Value, customer.DeviceId, rewards.PointBalance);
            var tier = CalcTier(effectiveBalance);

            return Ok(new CustomerIdentityResponse
            {
                CustomerId = customer.Id,
                FullName = customer.FullName,
                PhoneNumber = request.PhoneNumber,
                CustomerToken = customerToken,
                Tier = tier,
                PointBalance = effectiveBalance,
                IdentityLevel = customer.IdentityLevel.ToString()
            });
        }

        /// <summary>
        /// CC-S4 (Issue #4): Dev-token endpoint — mints a long-lived customer token for test users.
        /// Replaces the X-Dev-OTP bypass (which was an unconditional security hole).
        ///
        /// Security: gated by X-Dev-Secret header matching config "DevToken:Secret".
        /// - Empty/unset secret → endpoint disabled (404). Production MUST set DEV_TOKEN_SECRET env var.
        /// - Wrong secret → 401.
        /// - Correct secret → finds or creates customer by phone, returns long-lived token (365 days).
        ///
        /// RV scripts call this once with the secret, then use the returned X-Customer-Token
        /// for all subsequent customer API calls — skipping the OTP flow entirely.
        /// </summary>
        [HttpPost("dev-token")]
        public async Task<IActionResult> CreateDevToken([FromBody] DevTokenRequest request)
        {
            // Gate: verify secret from config (env var DEV_TOKEN_SECRET in production)
            var secret = _configuration["DevToken:Secret"];
            if (string.IsNullOrWhiteSpace(secret))
            {
                _logger.LogWarning("Dev-token endpoint called but DevToken:Secret not configured — returning 404");
                return NotFound();
            }

            var providedSecret = Request.Headers["X-Dev-Secret"].FirstOrDefault();
            if (string.IsNullOrEmpty(providedSecret) || providedSecret != secret)
            {
                _logger.LogWarning("Dev-token endpoint called with invalid secret — returning 401");
                return Unauthorized(new { error = "Invalid dev secret." });
            }

            if (string.IsNullOrWhiteSpace(request.PhoneNumber))
                return BadRequest(new { error = "Số điện thoại không được để trống." });

            // Find or create customer by phone (same logic as OTP verify)
            var tenantId = GetTenantId(request.TenantId);
            var allCustomers = await _customerRepository.GetAllActiveAsync();
            var customer = allCustomers.FirstOrDefault(c => c.PhoneNumber == request.PhoneNumber);
            if (customer == null)
            {
                var newCustomer = new Customer(new TenantId(tenantId),
                    request.DisplayName ?? "RV Test Customer", request.PhoneNumber);
                customer = await _customerRepository.AddAsync(newCustomer);
                _logger.LogInformation("Dev-token: created test customer {CustomerId} for phone {Phone}",
                    customer.Id, MaskPhone(request.PhoneNumber));
            }

            // Mint long-lived token (365 days) for test user
            var devToken = _customerTokenService.CreateLongLivedToken(customer.Id, 365);
            _logger.LogInformation("Dev-token: minted 365-day token for customer {CustomerId}", customer.Id);

            return Ok(new DevTokenResponse
            {
                CustomerId = customer.Id,
                FullName = customer.FullName,
                PhoneNumber = request.PhoneNumber,
                CustomerToken = devToken,
                ExpiresInDays = 365
            });
        }

        /// <summary>W17-T1: Validate an existing token and return customer info.</summary>
        [HttpGet("me")]
        public async Task<IActionResult> GetMe([FromHeader(Name = "X-Customer-Token")] string? token)
        {
            if (string.IsNullOrEmpty(token))
                return Unauthorized(new { error = "Token không hợp lệ." });

            var customerId = _customerTokenService.ValidateToken(token);
            if (!customerId.HasValue)
                return Unauthorized(new { error = "Token hết hạn hoặc không hợp lệ." });

            var customer = await _customerRepository.GetByIdAsync(customerId.Value);
            if (customer == null) return NotFound();

            // Use GetOrCreateCustomerRewardsAsync (not GetCustomerRewardsAsync) so customers
            // created via NATS sync (no OTP verify flow) still get a LoyaltyRewards record
            // when they view their profile. Without this, PointBalance shows 0 forever
            // because no LoyaltyRewards row exists for them.
            var rewards = await _loyaltyRewardsService.GetOrCreateCustomerRewardsAsync(customerId.Value, customer.TenantId);
            // Loyalty Consistency Fix Phase 2 (BUG #7): mode-aware balance
            int effectiveBalance = await _readRouter.GetEffectiveBalanceAsync(customer.TenantId.Value, customer.DeviceId, rewards.PointBalance);
            var tier = CalcTier(effectiveBalance);

            return Ok(new CustomerIdentityResponse
            {
                CustomerId = customer.Id,
                FullName = customer.FullName,
                PhoneNumber = customer.PhoneNumber,
                CustomerToken = token,
                Tier = tier,
                PointBalance = effectiveBalance,
                IdentityLevel = customer.IdentityLevel.ToString(),
                Birthday = customer.Birthday
            });
        }

        /// <summary>
        /// Tiered Auth Phase 2: Send OTP to the authenticated customer's registered phone number
        /// for identity upgrade (Social → Verified). Unlike the anonymous /otp/send flow, this endpoint:
        /// - Requires a valid X-Customer-Token (customer must already be logged in via social login)
        /// - Does NOT create a new customer — only upgrades an existing one
        /// - OTP is sent to the phone number already on file for the customer
        /// CC-S4 (Issue #4): X-Dev-OTP header removed — was unconditional security hole.
        /// </summary>
        [HttpPost("upgrade/send-otp")]
        public async Task<IActionResult> SendUpgradeOtp([FromHeader(Name = "X-Customer-Token")] string? token)
        {
            var customerId = _customerTokenService.ValidateToken(token);
            if (!customerId.HasValue)
                return Unauthorized(new { error = "Token không hợp lệ hoặc đã hết hạn." });

            var customer = await _customerRepository.GetByIdAsync(customerId.Value);
            if (customer == null)
                return NotFound(new { error = "Không tìm thấy khách hàng." });

            if (string.IsNullOrWhiteSpace(customer.PhoneNumber) && string.IsNullOrWhiteSpace(customer.Email))
                return BadRequest(new { error = "Khách hàng chưa có email/số điện thoại. Vui lòng cập nhật thông tin trước khi nâng cấp." });

            if (customer.IdentityLevel >= IdentityLevel.Verified)
                return BadRequest(new { error = "Tài khoản đã được xác thực, không cần nâng cấp.", currentLevel = customer.IdentityLevel.ToString() });

            // Email-first: ưu tiên email đã đăng ký; fallback SMS tới SĐT.
            var useEmail = !string.IsNullOrWhiteSpace(customer.Email);
            var contact = useEmail ? customer.Email!.Trim() : customer.PhoneNumber.Trim();
            var otp = _otpService.GenerateAndStoreOtp(contact, "customer-upgrade");
            var delivered = await _otpDelivery.DeliverAsync(contact, useEmail, otp, HttpContext.RequestAborted);
            if (!delivered)
            {
                _logger.LogWarning("Upgrade OTP delivery failed for customer {CustomerId}", customer.Id);
                return StatusCode(502, new { error = "Không gửi được mã xác minh. Vui lòng thử lại sau." });
            }
            _logger.LogInformation("Upgrade OTP generated + delivered via {Channel} for customer {CustomerId}", useEmail ? "email" : "sms", customer.Id);

            // CC-S4 (Issue #4): X-Dev-OTP header removed — was unconditional security hole.
            return Ok(new UpgradeSendOtpResponse
            {
                Message = $"Mã xác minh đã được gửi đến {(useEmail ? "email" : "số điện thoại")} đã đăng ký.",
                PhoneNumberSuffix = MaskContact(contact),
                Channel = useEmail ? "email" : "sms",
                ContactSuffix = MaskContact(contact)
            });
        }

        /// <summary>
        /// Tiered Auth Phase 2: Verify OTP and upgrade the authenticated customer's IdentityLevel to Verified.
        /// Requires X-Customer-Token (already logged in via social login) + correct OTP for the phone on file.
        /// </summary>
        [HttpPost("upgrade/verify-otp")]
        public async Task<IActionResult> VerifyUpgradeOtp([FromHeader(Name = "X-Customer-Token")] string? token, [FromBody] VerifyUpgradeOtpRequest request)
        {
            var customerId = _customerTokenService.ValidateToken(token);
            if (!customerId.HasValue)
                return Unauthorized(new { error = "Token không hợp lệ hoặc đã hết hạn." });

            if (string.IsNullOrWhiteSpace(request.Otp))
                return BadRequest(new { error = "OTP không được để trống." });

            var customer = await _customerRepository.GetByIdAsync(customerId.Value);
            if (customer == null)
                return NotFound(new { error = "Không tìm thấy khách hàng." });

            if (customer.IdentityLevel >= IdentityLevel.Verified)
                return BadRequest(new { error = "Tài khoản đã được xác thực.", currentLevel = customer.IdentityLevel.ToString() });

            if (string.IsNullOrWhiteSpace(customer.PhoneNumber) && string.IsNullOrWhiteSpace(customer.Email))
                return BadRequest(new { error = "Khách hàng chưa có email/số điện thoại để xác thực." });

            if (!VerifyOtpAnyContact(request.Otp, "customer-upgrade", request.Channel, customer.PhoneNumber, customer.Email))
                return BadRequest(new { error = "OTP không đúng hoặc đã hết hạn." });

            customer.UpgradeIdentityLevel(IdentityLevel.Verified);
            customer.MarkOtpVerified();
            await _customerRepository.UpdateAsync(customer);
            _logger.LogInformation("Customer {CustomerId} upgraded to Verified via upgrade OTP flow", customer.Id);

            // Loyalty-C WS-B: Trigger OtpVerify mission (one-time reward for first OTP verification)
            var missionResult = await _missionService.CompleteMissionAsync(customer.Id, MissionType.OtpVerify);

            return Ok(new UpgradeVerifyOtpResponse
            {
                Success = true,
                CustomerId = customer.Id,
                IdentityLevel = customer.IdentityLevel.ToString(),
                Message = missionResult.Success
                    ? $"Nâng cấp xác thực thành công. +{missionResult.PointsAwarded} điểm thưởng!"
                    : "Nâng cấp xác thực thành công. Bạn có thể đổi điểm thưởng ngay bây giờ."
            });
        }

        private Guid GetTenantId(Guid? requestTenantId)
        {
            if (requestTenantId.HasValue && requestTenantId.Value != Guid.Empty)
                return requestTenantId.Value;
            // Default tenant from config (dev/demo fallback)
            var tenantIdStr = _configuration["Seed:TenantId"] ?? "00000000-0000-0000-0000-000000000001";
            return Guid.TryParse(tenantIdStr, out var id) ? id : Guid.Empty;
        }

        private static string CalcTier(int points) => points switch
        {
            >= 20000 => "Platinum",
            >= 5000 => "Gold",
            >= 1000 => "Silver",
            _ => "Bronze"
        };

        private static string MaskPhone(string phone) =>
            phone.Length > 4 ? $"****{phone[^4..]}" : "****";

        /// <summary>2026-11-21: mask dùng chung cho email/SĐT — giữ 4 ký tự cuối.</summary>
        private static string MaskContact(string contact) =>
            contact.Length > 4 ? $"****{contact[^4..]}" : "****";

        /// <summary>
        /// Chọn contact verify OTP theo channel hint ("email"|"sms"). Channel null → thử phone rồi email
        /// (tương thích client cũ không gửi channel). Trả contact đầu tiên verify được, null nếu không.
        /// </summary>
        private bool VerifyOtpAnyContact(string otp, string purpose, string? channel, string? phone, string? email)
        {
            var candidates = channel?.ToLowerInvariant() switch
            {
                "email" => new[] { email },
                "sms" => new[] { phone },
                _ => new[] { phone, email }
            };
            foreach (var c in candidates)
            {
                if (!string.IsNullOrWhiteSpace(c) && _otpService.VerifyOtp(c.Trim(), otp, purpose))
                    return true;
            }
            return false;
        }

        /// <summary>Contact để verify login OTP — channel hint hoặc phone-first.</summary>
        private string? ResolveOtpContact(string? channel, string? phone, string? email)
        {
            if (channel?.Equals("email", StringComparison.OrdinalIgnoreCase) == true)
                return string.IsNullOrWhiteSpace(email) ? null : email.Trim();
            if (channel?.Equals("sms", StringComparison.OrdinalIgnoreCase) == true)
                return string.IsNullOrWhiteSpace(phone) ? null : phone.Trim();
            return !string.IsNullOrWhiteSpace(phone) ? phone.Trim()
                 : !string.IsNullOrWhiteSpace(email) ? email.Trim() : null;
        }
    }

    public class SendOtpRequest
    {
        public string PhoneNumber { get; set; } = string.Empty;
        /// <summary>Email-first (2026-11-21): nếu có email → OTP gửi qua email, phone chỉ là fallback.</summary>
        public string? Email { get; set; }
    }

    public class VerifyOtpRequest
    {
        public string PhoneNumber { get; set; } = string.Empty;
        public string? Email { get; set; }
        /// <summary>"email" | "sms" — kênh đã nhận mã (server trả về từ otp/send). Null = thử cả hai.</summary>
        public string? Channel { get; set; }
        public string Otp { get; set; } = string.Empty;
        public string? DisplayName { get; set; }
        public Guid? DeviceId { get; set; }
        public Guid? TenantId { get; set; }
    }

    public class CustomerIdentityResponse
    {
        public Guid CustomerId { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public string CustomerToken { get; set; } = string.Empty;
        public string Tier { get; set; } = "Bronze";
        public int PointBalance { get; set; }
        public string IdentityLevel { get; set; } = "Social";
        /// <summary>Loyalty-C WS-B: Customer birthday (date-only, null if not set).</summary>
        public DateTime? Birthday { get; set; }
    }

    // Tiered Auth Phase 2: Upgrade OTP flow DTOs (authenticated — for Social → Verified upgrade)
    public class VerifyUpgradeOtpRequest
    {
        public string Otp { get; set; } = string.Empty;
        /// <summary>"email" | "sms" — kênh đã nhận mã (server trả về từ upgrade/send-otp). Null = thử cả hai.</summary>
        public string? Channel { get; set; }
    }

    public class UpgradeSendOtpResponse
    {
        public string Message { get; set; } = string.Empty;
        public string PhoneNumberSuffix { get; set; } = string.Empty;
        /// <summary>Kênh đã gửi mã: "email" | "sms".</summary>
        public string Channel { get; set; } = "sms";
        /// <summary>4 ký tự cuối của contact nhận mã (email hoặc SĐT).</summary>
        public string ContactSuffix { get; set; } = string.Empty;
    }

    public class UpgradeVerifyOtpResponse
    {
        public bool Success { get; set; }
        public Guid CustomerId { get; set; }
        public string IdentityLevel { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
    }

    // CC-S4 (Issue #4): Dev-token endpoint DTOs (secret-gated test user token minting)
    public class DevTokenRequest
    {
        public string PhoneNumber { get; set; } = string.Empty;
        public string? DisplayName { get; set; }
        public Guid? TenantId { get; set; }
    }

    public class DevTokenResponse
    {
        public Guid CustomerId { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public string CustomerToken { get; set; } = string.Empty;
        public int ExpiresInDays { get; set; }
    }
}
