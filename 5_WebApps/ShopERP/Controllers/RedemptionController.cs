using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VanAn.CoreHub.Services;
using VanAn.ShopERP.Filters;
using VanAn.ShopERP.Services;
using VanAn.Shared.Domain;
using VanAn.Shared.Services;

namespace VanAn.ShopERP.Controllers
{
    /// <summary>
    /// Loyalty-B: Redemption system endpoints.
    /// Admin: catalog CRUD + fulfillment + history.
    /// Customer: browse catalog + redeem + view vouchers.
    /// Routes:
    ///   === Admin (cookie auth) ===
    ///   GET    /api/redemption/catalog              — list all catalog items (incl. inactive)
    ///   GET    /api/redemption/catalog/active       — list active catalog items (customer-facing)
    ///   GET    /api/redemption/catalog/{id}         — get catalog item detail
    ///   POST   /api/redemption/catalog              — create catalog item
    ///   PUT    /api/redemption/catalog/{id}         — update catalog item
    ///   POST   /api/redemption/catalog/{id}/deactivate — deactivate catalog item
    ///   DELETE /api/redemption/catalog/{id}         — soft-delete catalog item
    ///   POST   /api/redemption/fulfill              — fulfill voucher by code (admin scan)
    ///   POST   /api/redemption/cancel/{recordId}    — cancel redemption + refund points
    ///   GET    /api/redemption/history              — recent redemption records (admin)
    ///   === Customer (token auth) ===
    ///   GET    /api/redemption/my/redemptions       — customer's redemption history
    ///   GET    /api/redemption/my/vouchers          — customer's vouchers
    ///   POST   /api/redemption/redeem               — customer redeems catalog item
    /// </summary>
    [ApiController]
    [Route("api/redemption")]
    [ResolveCustomerTenant]
    public class RedemptionController(
        IRedemptionService redemptionService,
        ICustomerTokenService customerTokenService,
        ILogger<RedemptionController> logger) : ControllerBase
    {
        private readonly IRedemptionService _redemptionService = redemptionService;
        private readonly ICustomerTokenService _customerTokenService = customerTokenService;
        private readonly ILogger<RedemptionController> _logger = logger;

        // === Admin: Catalog CRUD ===

        [HttpGet("catalog")]
        [Authorize]
        public async Task<IActionResult> GetAllCatalog()
        {
            var items = await _redemptionService.GetAllCatalogAsync();
            return Ok(items.Select(MapCatalogItemDto).ToList());
        }

        [HttpGet("catalog/active")]
        [AllowAnonymous]
        public async Task<IActionResult> GetActiveCatalog()
        {
            var items = await _redemptionService.GetActiveCatalogAsync();
            return Ok(items.Select(MapCatalogItemDto).ToList());
        }

        [HttpGet("catalog/{id:guid}")]
        [AllowAnonymous]
        public async Task<IActionResult> GetCatalogItem(Guid id)
        {
            var item = await _redemptionService.GetCatalogItemAsync(id);
            if (item == null) return NotFound();
            return Ok(MapCatalogItemDto(item));
        }

        [HttpPost("catalog")]
        [Authorize]
        public async Task<IActionResult> CreateCatalogItem([FromBody] CreateCatalogItemRequest request)
        {
            try
            {
                var item = await _redemptionService.CreateCatalogItemAsync(
                    request.ProductName, request.Description, request.ImageUrl,
                    request.PointsRequired, request.StockCount, request.ValidTo, request.VoucherExpiryDays);
                return CreatedAtAction(nameof(GetCatalogItem), new { id = item.Id }, MapCatalogItemDto(item));
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        [HttpPut("catalog/{id:guid}")]
        [Authorize]
        public async Task<IActionResult> UpdateCatalogItem(Guid id, [FromBody] UpdateCatalogItemRequest request)
        {
            try
            {
                var item = await _redemptionService.UpdateCatalogItemAsync(
                    id, request.ProductName, request.Description, request.ImageUrl,
                    request.PointsRequired, request.StockCount, request.ValidTo, request.VoucherExpiryDays);
                return Ok(MapCatalogItemDto(item));
            }
            catch (KeyNotFoundException) { return NotFound(); }
            catch (ArgumentException ex) { return BadRequest(new { error = ex.Message }); }
        }

        [HttpPost("catalog/{id:guid}/deactivate")]
        [Authorize]
        public async Task<IActionResult> DeactivateCatalogItem(Guid id)
        {
            bool ok = await _redemptionService.DeactivateCatalogItemAsync(id);
            return ok ? Ok(new { success = true }) : NotFound();
        }

        [HttpDelete("catalog/{id:guid}")]
        [Authorize]
        public async Task<IActionResult> DeleteCatalogItem(Guid id)
        {
            bool ok = await _redemptionService.DeleteCatalogItemAsync(id);
            return ok ? Ok(new { success = true }) : NotFound();
        }

        // === Admin: Fulfillment + History ===

        [HttpPost("fulfill")]
        [Authorize]
        public async Task<IActionResult> FulfillVoucher([FromBody] FulfillVoucherRequest request)
        {
            bool ok = await _redemptionService.FulfillAsync(request.VoucherCode, request.Notes);
            if (!ok) return BadRequest(new { error = "Voucher không hợp lệ, đã sử dụng, hoặc đã hết hạn." });
            return Ok(new { success = true });
        }

        [HttpPost("cancel/{recordId:guid}")]
        [Authorize]
        public async Task<IActionResult> CancelRedemption(Guid recordId, [FromBody] CancelRequest? request)
        {
            bool ok = await _redemptionService.CancelAsync(recordId, request?.Notes);
            if (!ok) return BadRequest(new { error = "Không thể hủy (không tồn tại hoặc không ở trạng thái Pending)." });
            return Ok(new { success = true });
        }

        [HttpGet("history")]
        [Authorize]
        public async Task<IActionResult> GetHistory([FromQuery] int count = 50)
        {
            var records = await _redemptionService.GetRecentRedemptionsAsync(count);
            return Ok(records.Select(MapRedemptionRecordDto).ToList());
        }

        // === Customer: Redeem + View ===

        [HttpGet("my/redemptions")]
        [AllowAnonymous]
        public async Task<IActionResult> GetMyRedemptions([FromHeader(Name = "X-Customer-Token")] string? customerToken)
        {
            var customerId = ValidateCustomerToken(customerToken);
            if (!customerId.HasValue) return Unauthorized();
            var records = await _redemptionService.GetCustomerRedemptionsAsync(customerId.Value);
            return Ok(records.Select(MapRedemptionRecordDto).ToList());
        }

        [HttpGet("my/vouchers")]
        [AllowAnonymous]
        public async Task<IActionResult> GetMyVouchers([FromHeader(Name = "X-Customer-Token")] string? customerToken)
        {
            var customerId = ValidateCustomerToken(customerToken);
            if (!customerId.HasValue) return Unauthorized();
            var vouchers = await _redemptionService.GetCustomerVouchersAsync(customerId.Value);
            return Ok(vouchers.Select(MapVoucherDto).ToList());
        }

        [HttpPost("redeem")]
        [AllowAnonymous]
        public async Task<IActionResult> Redeem([FromHeader(Name = "X-Customer-Token")] string? customerToken, [FromBody] RedeemCatalogRequest request)
        {
            var customerId = ValidateCustomerToken(customerToken);
            if (!customerId.HasValue) return Unauthorized();

            var result = await _redemptionService.RedeemAsync(customerId.Value, request.CatalogItemId);
            if (!result.Success)
            {
                return BadRequest(new { error = result.Error });
            }

            return Ok(new RedeemCatalogResponse
            {
                Success = true,
                PointsSpent = result.PointsSpent,
                NewPointBalance = result.NewPointBalance,
                VoucherCode = result.Voucher?.VoucherCode,
                QrCodeData = result.Voucher?.QRCodeData,
                ExpiresAt = result.Voucher?.ExpiresAt
            });
        }

        // === Helpers ===

        private Guid? ValidateCustomerToken(string? authHeader)
        {
            if (string.IsNullOrEmpty(authHeader)) return null;
            string token = authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
                ? authHeader["Bearer ".Length..]
                : authHeader;
            return _customerTokenService.ValidateToken(token);
        }

        private static CatalogItemDto MapCatalogItemDto(RedemptionCatalogItem item) => new()
        {
            Id = item.Id,
            ProductName = item.ProductName,
            Description = item.Description,
            ImageUrl = item.ImageUrl,
            PointsRequired = item.PointsRequired,
            IsActive = item.IsActive,
            StockCount = item.StockCount,
            ValidFrom = item.ValidFrom,
            ValidTo = item.ValidTo,
            VoucherExpiryDays = item.VoucherExpiryDays,
            IsAvailable = item.IsAvailable
        };

        private static RedemptionRecordDto MapRedemptionRecordDto(RedemptionRecord r) => new()
        {
            Id = r.Id,
            CustomerId = r.CustomerId,
            CatalogItemId = r.CatalogItemId,
            VoucherId = r.VoucherId,
            PointsSpent = r.PointsSpent,
            Status = r.Status,
            RedeemedAt = r.RedeemedAt,
            FulfilledAt = r.FulfilledAt,
            CancelledAt = r.CancelledAt,
            Notes = r.Notes
        };

        private static VoucherDto MapVoucherDto(Voucher v) => new()
        {
            Id = v.Id,
            VoucherCode = v.VoucherCode,
            QrCodeData = v.QRCodeData,
            Status = v.Status,
            IssuedAt = v.IssuedAt,
            UsedAt = v.UsedAt,
            ExpiresAt = v.ExpiresAt,
            IsValid = v.IsValid
        };
    }

    // === DTOs ===

    public class CatalogItemDto
    {
        public Guid Id { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? ImageUrl { get; set; }
        public int PointsRequired { get; set; }
        public bool IsActive { get; set; }
        public int? StockCount { get; set; }
        public DateTime ValidFrom { get; set; }
        public DateTime? ValidTo { get; set; }
        public int VoucherExpiryDays { get; set; }
        public bool IsAvailable { get; set; }
    }

    public class CreateCatalogItemRequest
    {
        public string ProductName { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? ImageUrl { get; set; }
        public int PointsRequired { get; set; }
        public int? StockCount { get; set; }
        public DateTime? ValidTo { get; set; }
        public int VoucherExpiryDays { get; set; } = 30;
    }

    public class UpdateCatalogItemRequest : CreateCatalogItemRequest { }

    public class FulfillVoucherRequest
    {
        public string VoucherCode { get; set; } = string.Empty;
        public string? Notes { get; set; }
    }

    public class CancelRequest
    {
        public string? Notes { get; set; }
    }

    public class RedeemCatalogRequest
    {
        public Guid CatalogItemId { get; set; }
        /// <summary>Loyalty Alliance Phase 3B: optional tenantId for cross-tenant redeem (Alliance mode).
        /// If null, uses the current tenant context (ITenantProvider). If provided, routes to
        /// the specified tenant's catalog — future use (multi-VPS routing).</summary>
        public Guid? TenantId { get; set; }
    }

    public class RedeemCatalogResponse
    {
        public bool Success { get; set; }
        public int PointsSpent { get; set; }
        public int NewPointBalance { get; set; }
        public string? VoucherCode { get; set; }
        public string? QrCodeData { get; set; }
        public DateTime? ExpiresAt { get; set; }
    }

    public class RedemptionRecordDto
    {
        public Guid Id { get; set; }
        public Guid CustomerId { get; set; }
        public Guid CatalogItemId { get; set; }
        public Guid? VoucherId { get; set; }
        public int PointsSpent { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTime RedeemedAt { get; set; }
        public DateTime? FulfilledAt { get; set; }
        public DateTime? CancelledAt { get; set; }
        public string? Notes { get; set; }
    }

    public class VoucherDto
    {
        public Guid Id { get; set; }
        public string VoucherCode { get; set; } = string.Empty;
        public string? QrCodeData { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTime IssuedAt { get; set; }
        public DateTime? UsedAt { get; set; }
        public DateTime ExpiresAt { get; set; }
        public bool IsValid { get; set; }
    }
}
