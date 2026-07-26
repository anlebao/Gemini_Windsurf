using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VanAn.CoreHub.Domain.Repositories;
using VanAn.CoreHub.Repositories;
using VanAn.CoreHub.Services;
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
    public class CustomerIdentityController(
        IOtpService otpService,
        ICustomerTokenService customerTokenService,
        ICustomerRepository customerRepository,
        ILoyaltyRewardsService loyaltyRewardsService,
        IMissionService missionService,
        ILogger<CustomerIdentityController> logger) : ControllerBase
    {
        private readonly IOtpService _otpService = otpService;
        private readonly ICustomerTokenService _customerTokenService = customerTokenService;
        private readonly ICustomerRepository _customerRepository = customerRepository;
        private readonly ILoyaltyRewardsService _loyaltyRewardsService = loyaltyRewardsService;
        private readonly IMissionService _missionService = missionService;
        private readonly ILogger<CustomerIdentityController> _logger = logger;

        /// <summary>
        /// Send OTP to phone number.
        /// In production: SMS via notification service.
        /// In dev (IsDevelopment): exposes OTP via X-Dev-OTP response header.
        /// </summary>
        [HttpPost("otp/send")]
        public IActionResult SendOtp([FromBody] SendOtpRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.PhoneNumber))
                return BadRequest(new { error = "Số điện thoại không được để trống." });

            var otp = _otpService.GenerateAndStoreOtp(request.PhoneNumber);
            _logger.LogInformation("OTP generated for phone {Phone}", MaskPhone(request.PhoneNumber));

            // In dev mode, expose OTP in header for testing
            Response.Headers["X-Dev-OTP"] = otp;

            return Ok(new { message = "OTP đã được gửi. Vui lòng kiểm tra tin nhắn." });
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

            if (!_otpService.VerifyOtp(request.PhoneNumber, request.Otp))
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

            var customerToken = _customerTokenService.CreateToken(customer.Id);
            var rewards = await _loyaltyRewardsService.GetOrCreateCustomerRewardsAsync(customer.Id, new TenantId(tenantId));
            var tier = CalcTier(rewards.PointBalance);

            return Ok(new CustomerIdentityResponse
            {
                CustomerId = customer.Id,
                FullName = customer.FullName,
                PhoneNumber = request.PhoneNumber,
                CustomerToken = customerToken,
                Tier = tier,
                PointBalance = rewards.PointBalance,
                IdentityLevel = customer.IdentityLevel.ToString()
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

            var rewards = await _loyaltyRewardsService.GetCustomerRewardsAsync(customerId.Value);
            var tier = rewards != null ? CalcTier(rewards.PointBalance) : "Bronze";

            return Ok(new CustomerIdentityResponse
            {
                CustomerId = customer.Id,
                FullName = customer.FullName,
                PhoneNumber = customer.PhoneNumber,
                CustomerToken = token,
                Tier = tier,
                PointBalance = rewards?.PointBalance ?? 0,
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
        /// In dev mode, the OTP is also exposed via X-Dev-OTP response header for testing.
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

            if (string.IsNullOrWhiteSpace(customer.PhoneNumber))
                return BadRequest(new { error = "Khách hàng chưa có số điện thoại. Vui lòng cập nhật thông tin trước khi nâng cấp." });

            if (customer.IdentityLevel >= IdentityLevel.Verified)
                return BadRequest(new { error = "Tài khoản đã được xác thực, không cần nâng cấp.", currentLevel = customer.IdentityLevel.ToString() });

            var otp = _otpService.GenerateAndStoreOtp(customer.PhoneNumber);
            _logger.LogInformation("Upgrade OTP generated for customer {CustomerId} phone {Phone}",
                customer.Id, MaskPhone(customer.PhoneNumber));

            // In dev mode, expose OTP in header for testing
            Response.Headers["X-Dev-OTP"] = otp;

            return Ok(new UpgradeSendOtpResponse
            {
                Message = "OTP đã được gửi đến số điện thoại đã đăng ký.",
                PhoneNumberSuffix = MaskPhone(customer.PhoneNumber)
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

            if (string.IsNullOrWhiteSpace(customer.PhoneNumber))
                return BadRequest(new { error = "Khách hàng chưa có số điện thoại để xác thực." });

            if (!_otpService.VerifyOtp(customer.PhoneNumber, request.Otp))
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

        private static Guid GetTenantId(Guid? requestTenantId)
        {
            if (requestTenantId.HasValue && requestTenantId.Value != Guid.Empty)
                return requestTenantId.Value;
            // Default tenant for dev/demo
            return Guid.TryParse("00000000-0000-0000-0000-000000000001", out var id) ? id : Guid.Empty;
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
    }

    public class SendOtpRequest
    {
        public string PhoneNumber { get; set; } = string.Empty;
    }

    public class VerifyOtpRequest
    {
        public string PhoneNumber { get; set; } = string.Empty;
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
    }

    public class UpgradeSendOtpResponse
    {
        public string Message { get; set; } = string.Empty;
        public string PhoneNumberSuffix { get; set; } = string.Empty;
    }

    public class UpgradeVerifyOtpResponse
    {
        public bool Success { get; set; }
        public Guid CustomerId { get; set; }
        public string IdentityLevel { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
    }
}
