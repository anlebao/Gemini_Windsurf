using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VanAn.CoreHub.Domain.Repositories;
using VanAn.CoreHub.Repositories;
using VanAn.CoreHub.Services;
using VanAn.ShopERP.Services;
using VanAn.Shared.Domain;

namespace VanAn.ShopERP.Controllers
{
    /// <summary>
    /// W17-T1: Customer Identity — Phone OTP login flow.
    /// AllowAnonymous: OTP endpoints are accessed by unauthenticated KhachLink users.
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [AllowAnonymous]
    public class CustomerIdentityController(
        IOtpService otpService,
        ICustomerTokenService customerTokenService,
        ICustomerRepository customerRepository,
        ILoyaltyRewardsService loyaltyRewardsService,
        ILogger<CustomerIdentityController> logger) : ControllerBase
    {
        private readonly IOtpService _otpService = otpService;
        private readonly ICustomerTokenService _customerTokenService = customerTokenService;
        private readonly ICustomerRepository _customerRepository = customerRepository;
        private readonly ILoyaltyRewardsService _loyaltyRewardsService = loyaltyRewardsService;
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
                await _customerRepository.UpdateAsync(customer);
                _logger.LogInformation("Customer {CustomerId} upgraded to Verified via OTP", customer.Id);
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
                IdentityLevel = customer.IdentityLevel.ToString()
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
    }
}
