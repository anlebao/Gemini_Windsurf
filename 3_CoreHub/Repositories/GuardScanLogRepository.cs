using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using VanAn.CoreHub.Infrastructure;
using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Repositories
{
    /// <summary>
    /// Repository implementation for GuardScanLog entities (Issue #126 — Guard QR Verify).
    /// </summary>
    public class GuardScanLogRepository(IVanAnDbContext context, ILogger<GuardScanLogRepository> logger) : IGuardScanLogRepository
    {
        private readonly IVanAnDbContext _context = context;
        private readonly ILogger<GuardScanLogRepository> _logger = logger;

        public async Task AddAsync(GuardScanLog log, CancellationToken ct = default)
        {
            _ = await _context.GuardScanLogs.AddAsync(log, ct);
        }

        public async Task<List<GuardScanLog>> GetBySessionAsync(Guid sessionId, Guid tenantId, CancellationToken ct = default)
        {
            try
            {
                return await _context.GuardScanLogs
                    .Where(l => l.VehicleSessionId == sessionId && l.TenantId == new TenantId(tenantId))
                    .OrderByDescending(l => l.ScannedAt)
                    .ToListAsync(ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting scan logs for session {SessionId}", sessionId);
                return new List<GuardScanLog>();
            }
        }

        public async Task SaveChangesAsync(CancellationToken ct = default)
        {
            await _context.SaveChangesAsync(ct);
        }
    }
}
