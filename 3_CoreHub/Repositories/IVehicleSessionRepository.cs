using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Repositories
{
    /// <summary>
    /// Repository interface for VehicleSession entities (Issue #126 — Guard QR Verify).
    /// </summary>
    public interface IVehicleSessionRepository
    {
        Task<VehicleSession?> GetByIdAsync(Guid id, Guid tenantId, CancellationToken ct = default);
        /// <summary>
        /// #130-fix2 (2026-08-18): Lookup session by Id WITHOUT tenant filter.
        /// Used by GuardController.Claim to resolve tenantId from {sc,sid} QR payloads
        /// (PrintTicket format) where tenantId is not embedded in the payload.
        /// Returns session.TenantId via the entity. Caller must verify tenant context
        /// before acting on the session (ClaimAsync still filters by tenantId).
        /// </summary>
        Task<VehicleSession?> GetByIdWithoutTenantFilterAsync(Guid id, CancellationToken ct = default);
        Task<VehicleSession?> GetByQrTokenHashAsync(string qrTokenHash, Guid tenantId, CancellationToken ct = default);
        Task<VehicleSession?> GetByShortCodeAsync(string shortCode, Guid tenantId, CancellationToken ct = default);
        /// <summary>
        /// Issue #147: Lookup session by short code WITHOUT tenant filter (today only).
        /// Used by GuardController.Claim to resolve tenantId when customer enters short code
        /// (no QR payload → no tenantId embedded). Short codes are unique per tenant per day,
        /// but may collide across tenants. Caller must handle multiple matches.
        /// </summary>
        Task<List<VehicleSession>> GetByShortCodeWithoutTenantFilterAsync(string shortCode, CancellationToken ct = default);
        Task<(List<VehicleSession> Items, int Total)> GetTodaySessionsAsync(Guid tenantId, VehicleSessionStatus? status, int page, int pageSize, CancellationToken ct = default);
        Task<(int CheckInCount, int CheckOutCount, int InLotCount)> GetTodayStatsAsync(Guid tenantId, CancellationToken ct = default);
        Task<List<VehicleSession>> GetActiveByCustomerIdAsync(Guid customerId, Guid tenantId, CancellationToken ct = default);
        Task<List<VehicleSession>> GetByIdsForCustomerAsync(Guid customerId, List<Guid> sessionIds, CancellationToken ct = default);
        Task AddAsync(VehicleSession session, CancellationToken ct = default);
        Task SaveChangesAsync(CancellationToken ct = default);
    }
}
