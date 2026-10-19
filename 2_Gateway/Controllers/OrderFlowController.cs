using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VanAn.Shared.Services;

namespace VanAn.Gateway.Controllers
{
    /// <summary>
    /// Issue #195 (2026-10-19): tenant-facing order-flow resolution.
    /// ShopERP OrderFlowApiClient + OrderWorkflowService (HTTP scope) đọc flow hiệu lực
    /// cho tenant từ JWT claim — PG là source of truth.
    /// </summary>
    [ApiController]
    [Route("api/v1/orderflow")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public class OrderFlowController(IOrderFlowService orderFlowService) : ControllerBase
    {
        private readonly IOrderFlowService _orderFlowService = orderFlowService;

        /// <summary>
        /// GET /api/v1/orderflow/resolve?sourceType=booking
        /// Trả OrderFlowResolution (steps + allowed transitions) cho tenant trong JWT.
        /// </summary>
        [HttpGet("resolve")]
        public async Task<ActionResult<OrderFlowResolution>> Resolve([FromQuery] string? sourceType, CancellationToken ct)
        {
            Guid tenantId = GetTenantId();
            if (tenantId == Guid.Empty)
                return Unauthorized(new { error = "Tenant ID required in JWT claim" });

            OrderFlowResolution resolution = await _orderFlowService.ResolveFlowAsync(tenantId, sourceType, ct);
            return Ok(resolution);
        }

        /// <summary>
        /// GET /api/v1/orderflow/next?current=pending&sourceType=booking
        /// Danh sách trạng thái tiếp theo hợp lệ từ trạng thái hiện tại (UI action buttons).
        /// </summary>
        [HttpGet("next")]
        public async Task<ActionResult<IReadOnlyList<string>>> GetNext([FromQuery] string current, [FromQuery] string? sourceType, CancellationToken ct)
        {
            Guid tenantId = GetTenantId();
            if (tenantId == Guid.Empty)
                return Unauthorized(new { error = "Tenant ID required in JWT claim" });

            if (string.IsNullOrWhiteSpace(current))
                return BadRequest(new { error = "current status is required" });

            IReadOnlyList<string> next = await _orderFlowService.GetNextStatusesAsync(tenantId, sourceType, current, ct);
            return Ok(next);
        }

        private Guid GetTenantId()
        {
            string? claim = User.FindFirst("TenantId")?.Value ?? User.FindFirst("tenant_id")?.Value;
            return Guid.TryParse(claim, out Guid id) ? id : Guid.Empty;
        }
    }

    /// <summary>
    /// Issue #195: SystemAdmin CRUD order-flow profiles (system → tenant → category scope).
    /// </summary>
    [ApiController]
    [Route("api/v1/admin/orderflow")]
    [Authorize(Policy = "SystemAdmin", AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public class AdminOrderFlowController(
        IOrderFlowAdminService adminService,
        ILogger<AdminOrderFlowController> logger) : ControllerBase
    {
        private readonly IOrderFlowAdminService _adminService = adminService;
        private readonly ILogger<AdminOrderFlowController> _logger = logger;

        /// <summary>GET /api/v1/admin/orderflow?tenantId= — tất cả profiles (hoặc theo tenant).</summary>
        [HttpGet]
        public async Task<ActionResult<List<OrderFlowProfileDto>>> GetProfiles([FromQuery] Guid? tenantId, CancellationToken ct)
        {
            List<OrderFlowProfileDto> profiles = await _adminService.GetProfilesAsync(tenantId, ct);
            return Ok(profiles);
        }

        /// <summary>POST /api/v1/admin/orderflow — upsert profile + steps.</summary>
        [HttpPost]
        public async Task<ActionResult<OrderFlowProfileDto>> Upsert([FromBody] UpsertOrderFlowRequest request, CancellationToken ct)
        {
            try
            {
                string actor = User.Identity?.Name ?? "sysadmin";
                OrderFlowProfileDto dto = await _adminService.UpsertProfileAsync(
                    request.TenantId, request.SourceType, request.CategoryKey, request.Name,
                    request.Steps.Select(s => new OrderFlowStepInput(
                        s.Code, s.DisplayName, s.SortOrder, s.IsTerminal, s.AllowedNextCodes)).ToList(),
                    actor, ct);
                return Ok(dto);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Upsert order flow profile failed");
                return StatusCode(500, new { error = "Internal server error" });
            }
        }

        /// <summary>DELETE /api/v1/admin/orderflow/{id} — deactivate profile.</summary>
        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> Deactivate(Guid id, CancellationToken ct)
        {
            try
            {
                string actor = User.Identity?.Name ?? "sysadmin";
                await _adminService.DeactivateProfileAsync(id, actor, ct);
                return NoContent();
            }
            catch (InvalidOperationException ex)
            {
                return NotFound(new { error = ex.Message });
            }
        }
    }

    public sealed class UpsertOrderFlowRequest
    {
        public Guid? TenantId { get; set; }
        public string? SourceType { get; set; }
        public string? CategoryKey { get; set; }
        public string Name { get; set; } = string.Empty;
        public List<UpsertOrderFlowStep> Steps { get; set; } = new();
    }

    public sealed class UpsertOrderFlowStep
    {
        public string Code { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public int SortOrder { get; set; }
        public bool IsTerminal { get; set; }
        public List<string> AllowedNextCodes { get; set; } = new();
    }
}
