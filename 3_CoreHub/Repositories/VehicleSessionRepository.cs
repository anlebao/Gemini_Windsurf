using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using VanAn.CoreHub.Infrastructure;
using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Repositories
{
    /// <summary>
    /// Repository implementation for VehicleSession entities (Issue #126 — Guard QR Verify).
    /// Multi-tenancy enforced via TenantId filter on every query.
    /// </summary>
    public class VehicleSessionRepository(IVanAnDbContext context, ILogger<VehicleSessionRepository> logger) : IVehicleSessionRepository
    {
        private readonly IVanAnDbContext _context = context;
        private readonly ILogger<VehicleSessionRepository> _logger = logger;

        /// <summary>
        /// #130-fix: Calculate "today" in Vietnam timezone (UTC+7) for date filtering.
        /// IssuedAt is stored in UTC. Vietnam is UTC+7, so:
        /// - Vietnam midnight = UTC 17:00 of the previous day
        /// - Returns (startUtc, endUtc) representing the Vietnam-local "today" range in UTC.
        /// </summary>
        private static (DateTime StartUtc, DateTime EndUtc) GetVietnamTodayRange()
        {
            var vietnamToday = DateTime.UtcNow.AddHours(7).Date;
            var startUtc = vietnamToday.AddHours(-7);
            var endUtc = startUtc.AddDays(1);
            return (startUtc, endUtc);
        }

        public async Task<VehicleSession?> GetByIdAsync(Guid id, Guid tenantId, CancellationToken ct = default)
        {
            try
            {
                return await _context.VehicleSessions
                    .FirstOrDefaultAsync(s => s.Id == id && s.TenantId.Value == tenantId, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting VehicleSession {SessionId} for tenant {TenantId}", id, tenantId);
                return null;
            }
        }

        public async Task<VehicleSession?> GetByQrTokenHashAsync(string qrTokenHash, Guid tenantId, CancellationToken ct = default)
        {
            try
            {
                return await _context.VehicleSessions
                    .FirstOrDefaultAsync(s => s.QrTokenHash == qrTokenHash && s.TenantId.Value == tenantId, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting VehicleSession by QR token hash for tenant {TenantId}", tenantId);
                return null;
            }
        }

        public async Task<VehicleSession?> GetByShortCodeAsync(string shortCode, Guid tenantId, CancellationToken ct = default)
        {
            try
            {
                var (startUtc, endUtc) = GetVietnamTodayRange();
                return await _context.VehicleSessions
                    .Where(s => s.ShortCode == shortCode && s.TenantId.Value == tenantId && s.IssuedAt >= startUtc && s.IssuedAt < endUtc)
                    .OrderByDescending(s => s.IssuedAt)
                    .FirstOrDefaultAsync(ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting VehicleSession by short code {ShortCode} for tenant {TenantId}", shortCode, tenantId);
                return null;
            }
        }

        public async Task<(List<VehicleSession> Items, int Total)> GetTodaySessionsAsync(Guid tenantId, VehicleSessionStatus? status, int page, int pageSize, CancellationToken ct = default)
        {
            try
            {
                var (startUtc, endUtc) = GetVietnamTodayRange();
                var query = _context.VehicleSessions
                    .Where(s => s.TenantId.Value == tenantId && s.IssuedAt >= startUtc && s.IssuedAt < endUtc);

                if (status.HasValue)
                    query = query.Where(s => s.Status == status.Value);

                var total = await query.CountAsync(ct);
                var items = await query
                    .OrderByDescending(s => s.IssuedAt)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync(ct);

                return (items, total);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting today sessions for tenant {TenantId}", tenantId);
                return (new List<VehicleSession>(), 0);
            }
        }

        public async Task<(int CheckInCount, int CheckOutCount, int InLotCount)> GetTodayStatsAsync(Guid tenantId, CancellationToken ct = default)
        {
            try
            {
                var (startUtc, endUtc) = GetVietnamTodayRange();
                var sessions = await _context.VehicleSessions
                    .Where(s => s.TenantId.Value == tenantId && s.IssuedAt >= startUtc && s.IssuedAt < endUtc)
                    .ToListAsync(ct);

                var checkInCount = sessions.Count;
                var checkOutCount = sessions.Count(s => s.Status == VehicleSessionStatus.CheckedOut);
                var inLotCount = sessions.Count(s => s.Status is VehicleSessionStatus.Issued or VehicleSessionStatus.Claimed);

                return (checkInCount, checkOutCount, inLotCount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting today stats for tenant {TenantId}", tenantId);
                return (0, 0, 0);
            }
        }

        public async Task<List<VehicleSession>> GetActiveByCustomerIdAsync(Guid customerId, Guid tenantId, CancellationToken ct = default)
        {
            try
            {
                return await _context.VehicleSessions
                    .Where(s => s.CustomerId == customerId && s.TenantId.Value == tenantId
                        && s.Status == VehicleSessionStatus.Claimed)
                    .OrderByDescending(s => s.ClaimedAt)
                    .ToListAsync(ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting active sessions for customer {CustomerId}", customerId);
                return new List<VehicleSession>();
            }
        }

        public async Task<List<VehicleSession>> GetByIdsForCustomerAsync(Guid customerId, List<Guid> sessionIds, CancellationToken ct = default)
        {
            try
            {
                if (sessionIds == null || sessionIds.Count == 0)
                    return new List<VehicleSession>();

                return await _context.VehicleSessions
                    .Where(s => s.CustomerId == customerId && sessionIds.Contains(s.Id))
                    .OrderByDescending(s => s.IssuedAt)
                    .ToListAsync(ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting sessions by IDs for customer {CustomerId}", customerId);
                return new List<VehicleSession>();
            }
        }

        public async Task AddAsync(VehicleSession session, CancellationToken ct = default)
        {
            _ = await _context.VehicleSessions.AddAsync(session, ct);
        }

        public async Task SaveChangesAsync(CancellationToken ct = default)
        {
            await _context.SaveChangesAsync(ct);
        }
    }
}
