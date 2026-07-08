using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VanAn.CoreHub.Services;
using VanAn.Shared.Domain.Common;

namespace VanAn.ShopERP.Controllers
{
    /// <summary>
    /// Wave 14: API Key management CRUD. Restricted to Admin role (per-tenant).
    ///
    /// POST   /api/apikeys           → create key (returns raw secret once)
    /// GET    /api/apikeys           → list keys for current tenant
    /// DELETE /api/apikeys/{id}      → revoke key
    ///
    /// Endpoints intentionally do NOT expose SecretHash — raw secret is returned only at
    /// creation time (like GitHub PAT). Subsequent GETs show masked metadata only.
    /// </summary>
    [ApiController]
    [Route("api/apikeys")]
    [Authorize(Roles = "Admin,Owner,SystemAdmin")]
    public class ApiKeyController(
        IApiKeyManagementService apiKeyService,
        ITenantProvider tenantProvider,
        ILogger<ApiKeyController> logger) : ControllerBase
    {
        private readonly IApiKeyManagementService _apiKeyService = apiKeyService;
        private readonly ITenantProvider _tenantProvider = tenantProvider;
        private readonly ILogger<ApiKeyController> _logger = logger;

        // ── Create ────────────────────────────────────────────────────────────

        /// <summary>Creates a new API Key. Returns raw secret one time only.</summary>
        [HttpPost]
        public async Task<IActionResult> CreateKey(
            [FromBody] CreateApiKeyRequest request,
            CancellationToken ct = default)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var tenantId = _tenantProvider.TenantId;
            if (tenantId == Guid.Empty)
                return Unauthorized("Tenant context required");

            var (key, rawSecret) = await _apiKeyService.CreateKeyAsync(
                tenantId, request.Name, request.ExpirationDays, ct);

            _logger.LogInformation("API: Key created {KeyId} for tenant {TenantId}", key.Id, tenantId);

            return CreatedAtAction(nameof(ListKeys), null, new ApiKeyCreatedDto(
                key.Id,
                key.Name,
                RawSecret: rawSecret, // One-time exposure
                key.CreatedAt,
                key.ExpiresAt));
        }

        // ── List ──────────────────────────────────────────────────────────────

        [HttpGet]
        public async Task<IActionResult> ListKeys(CancellationToken ct = default)
        {
            var tenantId = _tenantProvider.TenantId;
            if (tenantId == Guid.Empty)
                return Unauthorized("Tenant context required");

            var keys = await _apiKeyService.ListKeysAsync(tenantId, ct);
            var dtos = keys.Select(k => new ApiKeyDto(
                k.Id, k.Name, k.IsActive, k.CreatedAt, k.ExpiresAt, k.LastUsedAt, k.RevokedAt));

            return Ok(dtos);
        }

        // ── Revoke ────────────────────────────────────────────────────────────

        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> RevokeKey(Guid id, CancellationToken ct = default)
        {
            var tenantId = _tenantProvider.TenantId;
            if (tenantId == Guid.Empty)
                return Unauthorized("Tenant context required");

            var key = await _apiKeyService.RevokeKeyAsync(id, tenantId, ct);
            _logger.LogInformation("API: Key revoked {KeyId} for tenant {TenantId}", id, tenantId);
            return Ok(new ApiKeyDto(key.Id, key.Name, key.IsActive, key.CreatedAt, key.ExpiresAt, key.LastUsedAt, key.RevokedAt));
        }
    }

    // ── Request / Response DTOs ───────────────────────────────────────────────

    public sealed record CreateApiKeyRequest(
        string Name,
        int ExpirationDays = 90);

    /// <summary>Returned only on creation — includes raw secret (one-time).</summary>
    public sealed record ApiKeyCreatedDto(
        Guid Id,
        string Name,
        string RawSecret,
        DateTime CreatedAt,
        DateTime ExpiresAt);

    /// <summary>Returned on list / revoke — never exposes secret.</summary>
    public sealed record ApiKeyDto(
        Guid Id,
        string Name,
        bool IsActive,
        DateTime CreatedAt,
        DateTime ExpiresAt,
        DateTime? LastUsedAt,
        DateTime? RevokedAt);
}
