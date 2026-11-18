using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VanAn.CoreHub.Domain.Repositories;
using VanAn.ShopERP.Filters;
using VanAn.ShopERP.Services;
using VanAn.Shared.Domain;

namespace VanAn.ShopERP.Controllers;

/// <summary>
/// ATRS-21: internal endpoint cho atrs mint customer_token sau passkey login.
/// Auth = [InternalApiKey] (X-Internal-Api-Key == InternalLoyalty:ApiKey) — KHÔNG public.
/// by-device: tìm real account theo DeviceId (cross-tenant) → guest stub cùng tenant → tạo stub mới.
/// by-id: resolve customerId đã biết từ atrs TrustSubjectLink → token.
/// </summary>
[ApiController]
[Route("api/internal/customer-token")]
[AllowAnonymous] // auth thật sự do [InternalApiKey] đảm nhiệm (mirror MirrorOrdersController)
[InternalApiKey]
public class InternalCustomerTokenController(
    ICustomerRepository customerRepository,
    ICustomerTokenService customerTokenService,
    IConfiguration configuration,
    ILogger<InternalCustomerTokenController> logger) : ControllerBase
{
    public sealed record ByDeviceRequest(Guid DeviceId, Guid? TenantId);
    public sealed record ByIdRequest(Guid CustomerId);
    public sealed record TokenResponse(string CustomerToken, Guid CustomerId, bool Created);

    [HttpPost("by-device")]
    public async Task<IActionResult> ByDevice([FromBody] ByDeviceRequest request, CancellationToken ct)
    {
        if (request.DeviceId == Guid.Empty)
            return BadRequest(new { error = "deviceId is required" });

        // Ưu tiên real account cross-tenant (guest stubs bị loại) — passkey login phải gắn
        // vào account khách thật nếu đã từng OAuth/OTP, không phải stub vô hình.
        var customer = await customerRepository.GetRealAccountByDeviceIdAsync(request.DeviceId);
        var created = false;

        if (customer is null)
        {
            // Cross-tenant any-level lookup — ambient tenant không tồn tại trong internal call;
            // GetByDeviceIdAsync (tenant-filtered) sẽ miss Guest stub → duplicate customer mỗi login.
            customer = await customerRepository.GetAnyCustomerByDeviceIdAsync(request.DeviceId);
        }

        if (customer is null)
        {
            var tenantId = request.TenantId ?? GetSeedTenantId();
            if (tenantId == Guid.Empty)
            {
                logger.LogError("InternalCustomerToken by-device: không resolve được tenant cho device {DeviceId}", request.DeviceId);
                return StatusCode(500, new { error = "tenant resolution failed" });
            }

            customer = new Customer(new TenantId(tenantId), "Khách hàng", string.Empty);
            customer.UpdateCustomerDetails("Khách hàng", string.Empty, null, "Bronze", request.DeviceId, true);
            // Guest level: passkey chứng minh device possession, không phải liên hệ verified.
            // Guest stub mergeable về account thật khi user OAuth/OTP sau này (CustomerMergeService).
            customer.MarkAsGuestStub();
            customer = await customerRepository.AddAsync(customer);
            created = true;
            logger.LogInformation("InternalCustomerToken by-device: created stub customer {CustomerId} for device {DeviceId}",
                customer.Id, request.DeviceId);
        }

        var token = customerTokenService.CreateToken(customer.Id);
        return Ok(new TokenResponse(token, customer.Id, created));
    }

    [HttpPost("by-id")]
    public async Task<IActionResult> ById([FromBody] ByIdRequest request, CancellationToken ct)
    {
        if (request.CustomerId == Guid.Empty)
            return BadRequest(new { error = "customerId is required" });

        var customer = await customerRepository.GetByIdAsync(request.CustomerId);
        if (customer is null || customer.IsDeleted)
            return NotFound();

        var token = customerTokenService.CreateToken(customer.Id);
        return Ok(new TokenResponse(token, customer.Id, false));
    }

    private Guid GetSeedTenantId()
    {
        var raw = configuration["Seed:TenantId"];
        return Guid.TryParse(raw, out var id) ? id : Guid.Empty;
    }
}
