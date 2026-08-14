using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Repositories
{
    /// <summary>
    /// Repository interface for GuardScanLog entities (Issue #126 — Guard QR Verify).
    /// </summary>
    public interface IGuardScanLogRepository
    {
        Task AddAsync(GuardScanLog log, CancellationToken ct = default);
        Task<List<GuardScanLog>> GetBySessionAsync(Guid sessionId, Guid tenantId, CancellationToken ct = default);
        Task SaveChangesAsync(CancellationToken ct = default);
    }
}
