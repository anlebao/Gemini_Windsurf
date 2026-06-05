using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VanAn.CoreHub.Services;
using VanAn.Shared.Domain.Audit;

namespace VanAn.Gateway.Controllers
{
    /// <summary>
    /// API Controller for Audit Trail - Phase 2.9.4
    /// Provides read-only access to audit logs for compliance and debugging
    /// Admin-only access for security
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Produces("application/json")]
    [Authorize(Roles = "Admin")] // Only administrators can access audit logs
    public class AuditTrailController : ControllerBase
    {
        private readonly IAuditTrailService _auditTrailService;
        private readonly ILogger<AuditTrailController> _logger;

        public AuditTrailController(
            IAuditTrailService auditTrailService,
            ILogger<AuditTrailController> logger)
        {
            _auditTrailService = auditTrailService;
            _logger = logger;
        }

        /// <summary>
        /// Query audit logs with filters
        /// </summary>
        [HttpGet("query")]
        public async Task<ActionResult<AuditLogPagedResult>> Query(
            [FromQuery] AuditLogQueryRequest request,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var query = new AuditLogQuery
                {
                    FromDate = request.FromDate,
                    ToDate = request.ToDate,
                    Action = request.Action,
                    EntityType = request.EntityType,
                    EntityId = request.EntityId,
                    UserId = request.UserId,
                    CorrelationId = request.CorrelationId,
                    SearchTerm = request.SearchTerm,
                    PageNumber = request.PageNumber ?? 1,
                    PageSize = request.PageSize ?? 50
                };

                var result = await _auditTrailService.QueryAsync(query, cancellationToken);

                _logger.LogInformation(
                    "Audit trail queried: {TotalCount} records found for page {PageNumber}",
                    result.TotalCount, result.PageNumber);

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error querying audit trail");
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// Get audit history for a specific entity
        /// </summary>
        [HttpGet("entity/{entityType}/{entityId}")]
        public async Task<ActionResult<IReadOnlyList<AuditLog>>> GetEntityHistory(
            AuditableEntityType entityType,
            Guid entityId,
            [FromQuery] int maxResults = 100,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var history = await _auditTrailService.GetEntityHistoryAsync(
                    entityType, entityId, maxResults, cancellationToken);

                _logger.LogInformation(
                    "Entity audit history retrieved: {EntityType} {EntityId}, {Count} records",
                    entityType, entityId, history.Count);

                return Ok(history);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving entity audit history for {EntityType} {EntityId}",
                    entityType, entityId);
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// Get recent audit logs for dashboard
        /// </summary>
        [HttpGet("recent")]
        public async Task<ActionResult<IReadOnlyList<AuditLog>>> GetRecent(
            [FromQuery] int count = 50,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var recent = await _auditTrailService.GetRecentAsync(count, cancellationToken);

                _logger.LogInformation("Recent audit logs retrieved: {Count} records", recent.Count);

                return Ok(recent);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving recent audit logs");
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// Get audit logs by correlation ID (for tracking related operations)
        /// </summary>
        [HttpGet("correlation/{correlationId}")]
        public async Task<ActionResult<IReadOnlyList<AuditLog>>> GetByCorrelationId(
            string correlationId,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var logs = await _auditTrailService.GetByCorrelationIdAsync(correlationId, cancellationToken);

                _logger.LogInformation(
                    "Audit logs by correlation ID retrieved: {CorrelationId}, {Count} records",
                    correlationId, logs.Count);

                return Ok(logs);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving audit logs by correlation ID {CorrelationId}",
                    correlationId);
                return StatusCode(500, "Internal server error");
            }
        }
    }

    /// <summary>
    /// Request model for querying audit logs
    /// </summary>
    public class AuditLogQueryRequest
    {
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }
        public AuditActionType? Action { get; set; }
        public AuditableEntityType? EntityType { get; set; }
        public Guid? EntityId { get; set; }
        public string? UserId { get; set; }
        public string? CorrelationId { get; set; }
        public string? SearchTerm { get; set; }
        public int? PageNumber { get; set; } = 1;
        public int? PageSize { get; set; } = 50;
    }
}
