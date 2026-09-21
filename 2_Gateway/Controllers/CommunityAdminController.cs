using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VanAn.CoreHub.Infrastructure;
using VanAn.CoreHub.Infrastructure.DataProtection;
using VanAn.CoreHub.Services;
using VanAn.Shared.Domain;

namespace VanAn.Gateway.Controllers
{
    /// <summary>
    /// CC-S6 (Sprint 6): Community admin endpoints - eligible customer list, activate/deactivate roles.
    /// Auth: SystemAdmin Bearer JWT (platform-level, cross-tenant).
    /// </summary>
    [ApiController]
    [Route("api/admin/community")]
    public class CommunityAdminController(
        ICommunityAdminService communityAdminService,
        VanAnDbContext dbContext,
        ILogger<CommunityAdminController> logger) : ControllerBase
    {
        private readonly ICommunityAdminService _communityAdminService = communityAdminService;
        private readonly VanAnDbContext _dbContext = dbContext;
        private readonly ILogger<CommunityAdminController> _logger = logger;

        /// <summary>
        /// GET /api/admin/community/eligible?page=1&amp;pageSize=20
        /// Returns customers eligible for community role activation.
        /// </summary>
        [HttpGet("eligible")]
        [Authorize(Policy = "SystemAdmin", AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public async Task<IActionResult> GetEligible([FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] bool includeIneligible = false)
        {
            var result = await _communityAdminService.GetEligibleCustomersAsync(page, pageSize, includeIneligible);
            return Ok(result);
        }

        /// <summary>
        /// POST /api/admin/community/{customerId}/activate-role
        /// Activate a community role (Shipper or Salesman) for a customer.
        /// </summary>
        [HttpPost("{customerId}/activate-role")]
        [Authorize(Policy = "SystemAdmin", AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public async Task<IActionResult> ActivateRole(Guid customerId, [FromBody] ActivateRoleRequest request)
        {
            if (!Enum.TryParse<CommunityRoleType>(request.Role, ignoreCase: true, out var roleType))
                return BadRequest(new { error = $"Invalid role: {request.Role}. Must be 'Shipper' or 'Salesman'." });

            try
            {
                // Get admin user ID from JWT claims
                var adminId = GetAdminUserId();
                var role = await _communityAdminService.ActivateRoleAsync(customerId, roleType, adminId, request.BypassEligibility);

                _logger.LogInformation("ActivateRole: {Role} activated for customer {CustomerId} by admin {AdminId}",
                    roleType, customerId, adminId);

                return Ok(new
                {
                    communityRoleId = role.Id,
                    roleType = role.RoleType.ToString(),
                    activatedAt = role.ActivatedAt
                });
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning("ActivateRole failed: {Message}", ex.Message);
                if (ex.Message.Contains("already has an active"))
                    return Conflict(new { error = ex.Message });
                if (ex.Message.Contains("does not meet eligibility") || ex.Message.Contains("not found") || ex.Message.Contains("not active"))
                    return BadRequest(new { error = ex.Message });
                return BadRequest(new { error = ex.Message });
            }
        }

        /// <summary>
        /// POST /api/admin/community/{customerId}/deactivate-role
        /// Deactivate an active community role for a customer.
        /// </summary>
        [HttpPost("{customerId}/deactivate-role")]
        [Authorize(Policy = "SystemAdmin", AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public async Task<IActionResult> DeactivateRole(Guid customerId, [FromBody] ActivateRoleRequest request)
        {
            if (!Enum.TryParse<CommunityRoleType>(request.Role, ignoreCase: true, out var roleType))
                return BadRequest(new { error = $"Invalid role: {request.Role}. Must be 'Shipper' or 'Salesman'." });

            try
            {
                await _communityAdminService.DeactivateRoleAsync(customerId, roleType);

                _logger.LogInformation("DeactivateRole: {Role} deactivated for customer {CustomerId}",
                    roleType, customerId);

                return Ok(new { deactivatedAt = DateTime.UtcNow });
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning("DeactivateRole failed: {Message}", ex.Message);
                if (ex.Message.Contains("No active"))
                    return NotFound(new { error = ex.Message });
                return BadRequest(new { error = ex.Message });
            }
        }

        /// <summary>
        /// 2026-09-21 (admin panel 500): One-time PII repair for Customers whose PhoneNumber/Email
        /// were encrypted with a LOST ephemeral Data Protection key (Gateway ran without a persistent
        /// key ring until this fix). Reads the RAW column values (SqlQueryRaw bypasses the EF value
        /// converter — no decrypt attempt), tries to decrypt with the CURRENT ring, and re-encrypts
        /// any row that fails (recovering the phone from Orders.CustomerPhone where available, else
        /// empty). Idempotent + rerunnable. After this, every Customer row is decryptable by the
        /// persistent ring.
        /// </summary>
        [HttpPost("pii-repair")]
        [Authorize(Policy = "SystemAdmin", AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public async Task<IActionResult> RepairCustomerPii(CancellationToken ct = default)
        {
            var phoneProtector = DataProtectionProviderAccessor.CreateProtector("Customer.PhoneNumber");
            var emailProtector = DataProtectionProviderAccessor.CreateProtector("Customer.Email");

            // Raw projection bypasses EncryptedStringConverter → returns stored bytes as-is
            // (the converter would throw on the first unreadable row).
            var rows = await _dbContext.Database
                .SqlQueryRaw<CustomerPiiRawRow>("SELECT \"Id\" AS Id, \"PhoneNumber\" AS PhoneNumber, \"Email\" AS Email FROM \"Customers\"")
                .ToListAsync(ct);

            int repaired = 0, kept = 0, recoveredFromOrders = 0;
            var unrecoverable = new List<string>();

            foreach (var row in rows)
            {
                string? newPhone = null, newEmail = null;
                bool needsPhoneWrite = false, needsEmailWrite = false;

                if (!string.IsNullOrEmpty(row.PhoneNumber))
                {
                    if (TryUnprotect(phoneProtector, row.PhoneNumber, out var phonePlain))
                    {
                        kept++;
                    }
                    else
                    {
                        // Plaintext phone (written before encryption) or lost-key ciphertext.
                        var plain = LooksLikePhone(row.PhoneNumber) ? row.PhoneNumber : await TryRecoverPhoneFromOrdersAsync(row.Id, ct);
                        if (plain is null)
                        {
                            unrecoverable.Add($"{row.Id}: phone");
                        }
                        else
                        {
                            if (!plain.Equals(row.PhoneNumber, StringComparison.Ordinal)) recoveredFromOrders++;
                            newPhone = plain;
                            needsPhoneWrite = true;
                        }
                    }
                }

                if (!string.IsNullOrEmpty(row.Email))
                {
                    if (TryUnprotect(emailProtector, row.Email, out _))
                    {
                        kept++;
                    }
                    else if (LooksLikeEmail(row.Email))
                    {
                        // Plaintext email → re-encrypt with current ring
                        newEmail = row.Email;
                        needsEmailWrite = true;
                    }
                    else
                    {
                        unrecoverable.Add($"{row.Id}: email");
                    }
                }

                if (needsPhoneWrite || needsEmailWrite)
                {
                    // Write the re-encrypted value directly (bypasses the converter on write too —
                    // loading the entity to modify it would trigger the decrypt-on-read and throw).
                    var sql = "UPDATE \"Customers\" SET "
                        + (needsPhoneWrite ? "\"PhoneNumber\" = {0} " : "\"PhoneNumber\" = \"PhoneNumber\" ")
                        + (needsEmailWrite ? ",\"Email\" = {1} " : "")
                        + "WHERE \"Id\" = {2}";
                    var p0 = needsPhoneWrite ? phoneProtector.Protect(newPhone ?? "") : "";
                    var p1 = needsEmailWrite ? emailProtector.Protect(newEmail ?? "") : "";
                    // Use the (sql, IEnumerable<object?>, CancellationToken) overload — the
                    // params object?[] overload would treat `ct` as a SQL parameter and EF
                    // throws "no store type mapping for properties of type 'CancellationToken'".
                    _ = await _dbContext.Database.ExecuteSqlRawAsync(sql, new object?[] { p0, p1, row.Id }, ct);
                    repaired++;
                }
            }

            _logger.LogInformation(
                "Customer PII repair: {Repaired} repaired, {Kept} already decryptable, {Recovered} recovered from orders, {Unrecoverable} unrecoverable",
                repaired, kept, recoveredFromOrders, unrecoverable.Count);

            return Ok(new
            {
                repaired,
                kept,
                recoveredFromOrders,
                unrecoverable = unrecoverable.Take(20),
                unrecoverableCount = unrecoverable.Count
            });
        }

        private Guid GetAdminUserId()
        {
            var userIdClaim = User.FindFirst("sub")?.Value
                ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                ?? User.FindFirst("userId")?.Value;
            return Guid.TryParse(userIdClaim, out var id) ? id : Guid.Empty;
        }

        private static bool TryUnprotect(IDataProtector protector, string value, out string plain)
        {
            try
            {
                plain = protector.Unprotect(value);
                return true;
            }
            catch (System.Security.Cryptography.CryptographicException)
            {
                plain = "";
                return false;
            }
        }

        private async Task<string?> TryRecoverPhoneFromOrdersAsync(Guid customerId, CancellationToken ct)
        {
            // Order.CustomerInfo is OwnsOne → column is "CustomerInfo_PhoneNumber" (plaintext snapshot).
            // EF Core scalar SqlQueryRaw<T> wraps the SQL as SELECT "t"."Value" FROM (...) AS "t" —
            // the alias MUST be quoted "Value" (unquoted → PG lowercases it → 42703 column t.Value).
            var phone = await _dbContext.Database
                .SqlQueryRaw<string?>("SELECT \"CustomerInfo_PhoneNumber\" AS \"Value\" FROM \"Orders\" WHERE \"CustomerId\" = {0} AND \"CustomerInfo_PhoneNumber\" IS NOT NULL AND \"CustomerInfo_PhoneNumber\" <> '' ORDER BY \"CreatedAt\" DESC LIMIT 1", customerId)
                .FirstOrDefaultAsync(ct);
            return string.IsNullOrEmpty(phone) ? null : phone;
        }

        private static bool LooksLikePhone(string value) =>
            value.Length >= 8 && value.Length <= 15 && value.All(char.IsDigit) || value.StartsWith("+") && value.Length >= 9 && value[1..].All(char.IsDigit);

        private static bool LooksLikeEmail(string value) => value.Contains('@') && value.Contains('.');

        private class CustomerPiiRawRow
        {
            public Guid Id { get; set; }
            public string? PhoneNumber { get; set; }
            public string? Email { get; set; }
        }
    }

    public class ActivateRoleRequest
    {
        public string Role { get; set; } = string.Empty;
        /// <summary>
        /// Owner override: when true, skip the IdentityLevel + LoyaltyPoints eligibility check.
        /// Used by TenantCommunityAdminController (owner-scoped) to upgrade freshly-onboarded
        /// Google-login customers who don't yet meet the standard criteria.
        /// </summary>
        public bool BypassEligibility { get; set; }
    }
}
