using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VanAn.CoreHub.Services;
using VanAn.Shared.Domain.Aggregates.WalletAggregate;

namespace VanAn.Gateway.Controllers
{
    /// <summary>
    /// Settlement Batch-3 (TC-09): Withdrawal/payout admin endpoints.
    /// List → approve/reject → pay (manual bank transfer, admin enters bank ref — Q4b).
    /// Auth: SystemAdmin Bearer JWT (platform-level, cross-tenant).
    /// </summary>
    [ApiController]
    [Authorize(Policy = "SystemAdmin", AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    [Route("api/admin/withdrawals")]
    public class WithdrawalAdminController(
        IWalletService walletService,
        ILogger<WithdrawalAdminController> logger) : ControllerBase
    {
        private readonly IWalletService _walletService = walletService;
        private readonly ILogger<WithdrawalAdminController> _logger = logger;

        /// <summary>GET /api/admin/withdrawals?status=pending&amp;page=1&amp;pageSize=20</summary>
        [HttpGet]
        public async Task<IActionResult> List(
            [FromQuery] string? status,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
            if (page < 1) page = 1;
            if (pageSize < 1 || pageSize > 100) pageSize = 20;

            WithdrawalStatus? statusFilter = null;
            if (!string.IsNullOrWhiteSpace(status))
            {
                if (!Enum.TryParse<WithdrawalStatus>(status, ignoreCase: true, out var parsed))
                    return BadRequest(new { error = $"Status '{status}' không hợp lệ." });
                statusFilter = parsed;
            }

            var result = await _walletService.GetWithdrawalRequestsAsync(statusFilter, page, pageSize);
            return Ok(result);
        }

        /// <summary>POST /api/admin/withdrawals/{id}/approve — Pending → Approved.</summary>
        [HttpPost("{id:guid}/approve")]
        public async Task<IActionResult> Approve(Guid id)
        {
            try
            {
                await _walletService.ApproveWithdrawalAsync(id, GetAdminUserId());
                return Ok(new { requestId = id, status = "Approved" });
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(new { error = ex.Message });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error approving withdrawal {RequestId}", id);
                return StatusCode(500, new { error = "Lỗi server." });
            }
        }

        /// <summary>POST /api/admin/withdrawals/{id}/reject — Pending/Approved → Rejected.</summary>
        [HttpPost("{id:guid}/reject")]
        public async Task<IActionResult> Reject(Guid id, [FromBody] RejectWithdrawalBody body)
        {
            if (body == null || string.IsNullOrWhiteSpace(body.Reason))
                return BadRequest(new { error = "Reason không được để trống." });

            try
            {
                await _walletService.RejectWithdrawalAsync(id, GetAdminUserId(), body.Reason);
                return Ok(new { requestId = id, status = "Rejected" });
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(new { error = ex.Message });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error rejecting withdrawal {RequestId}", id);
                return StatusCode(500, new { error = "Lỗi server." });
            }
        }

        /// <summary>
        /// POST /api/admin/withdrawals/{id}/pay — Approved → Paid.
        /// Admin supplies the bank transfer reference after completing the manual payout (Q4b).
        /// Creates the Withdrawal wallet tx exactly once.
        /// </summary>
        [HttpPost("{id:guid}/pay")]
        public async Task<IActionResult> Pay(Guid id, [FromBody] PayWithdrawalBody body)
        {
            if (body == null || string.IsNullOrWhiteSpace(body.BankReference))
                return BadRequest(new { error = "BankReference không được để trống." });

            try
            {
                var request = await _walletService.MarkWithdrawalPaidAsync(id, GetAdminUserId(), body.BankReference);
                return Ok(new
                {
                    requestId = request.Id,
                    status = request.Status.ToString(),
                    walletTransactionId = request.WalletTransactionId,
                    bankReference = request.BankReference
                });
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(new { error = ex.Message });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error paying withdrawal {RequestId}", id);
                return StatusCode(500, new { error = "Lỗi server." });
            }
        }

        private Guid GetAdminUserId()
        {
            var userIdClaim = User.FindFirst("sub")?.Value
                ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                ?? User.FindFirst("userId")?.Value;
            return Guid.TryParse(userIdClaim, out var id) ? id : Guid.Empty;
        }
    }

    public class RejectWithdrawalBody
    {
        public string Reason { get; set; } = string.Empty;
    }

    public class PayWithdrawalBody
    {
        public string BankReference { get; set; } = string.Empty;
    }
}
