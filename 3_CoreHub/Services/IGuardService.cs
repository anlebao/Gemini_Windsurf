using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Services
{
    /// <summary>
    /// Guard QR Verification service (Issue #126).
    /// Business logic for vehicle session lifecycle: issue, claim, verify, checkout, flag, void.
    /// Photos stored in Cloudflare R2 via IR2StorageService (presigned URL pattern).
    /// </summary>
    public interface IGuardService
    {
        /// <summary>Generate presigned PUT URLs for photo upload (plate + customer).</summary>
        Task<PresignUploadResult> PresignUploadAsync(Guid tenantId, string contentType);

        /// <summary>Issue a new QR session (guard creates QR with plate + customer photos).</summary>
        Task<IssueResult> IssueAsync(Guid tenantId, Guid guardId, IssueRequest req);

        /// <summary>Claim QR session by customer (Channel A/B/C→A migration).</summary>
        Task<ClaimResult> ClaimAsync(Guid tenantId, Guid customerId, ClaimRequest req);

        /// <summary>Verify scanned QR (guard scans QR from KhachLink screen or paper ticket).</summary>
        Task<VerifyResult> VerifyAsync(Guid tenantId, Guid guardId, string scannedQrPayload);

        /// <summary>Check-out session (guard confirms match).</summary>
        Task<CheckoutResult> CheckoutAsync(Guid tenantId, Guid guardId, Guid sessionId);

        /// <summary>Flag session as suspicious (guard detects mismatch).</summary>
        Task<FlagResult> FlagAsync(Guid tenantId, Guid guardId, Guid sessionId, string reason);

        /// <summary>Void session (cancelled/expired).</summary>
        Task<VoidResult> VoidAsync(Guid tenantId, Guid guardId, Guid sessionId);

        /// <summary>Get today's sessions (paginated, optional status filter).</summary>
        Task<TodaySessionsResult> GetTodaySessionsAsync(Guid tenantId, VehicleSessionStatus? status, int page, int pageSize);

        /// <summary>Get session detail by ID (with presigned photo URLs).</summary>
        Task<SessionDetailResult> GetSessionAsync(Guid tenantId, Guid sessionId);

        /// <summary>Get session statuses for a customer's claimed sessions (cross-tenant, for KhachLink wallet sync).</summary>
        Task<List<SessionStatusResult>> GetSessionStatusesAsync(Guid customerId, List<Guid> sessionIds);
    }

    // === Result DTOs ===

    public record PresignUploadResult(string PlatePhotoKey, string PlatePhotoUploadUrl, string CustomerPhotoKey, string CustomerPhotoUploadUrl);

    public record IssueResult(Guid SessionId, string QrPayload, string ShortCode);

    public record ClaimResult(Guid SessionId, string PlateNumber, string PlatePhotoUrl, string CustomerPhotoUrl, DateTime IssuedAt, VehicleSessionStatus Status);

    public record VerifyResult(Guid SessionId, string PlateNumber, string PlatePhotoUrl, string CustomerPhotoUrl, DateTime IssuedAt, VehicleSessionStatus Status, Guid? CustomerId);

    public record CheckoutResult(Guid SessionId, VehicleSessionStatus Status, DateTime CheckedOutAt);

    public record FlagResult(Guid SessionId, VehicleSessionStatus Status, string FlagReason);

    public record VoidResult(Guid SessionId, VehicleSessionStatus Status);

    public record TodaySessionsResult(int Total, int CheckInCount, int CheckOutCount, int InLotCount, List<SessionSummary> Items);

    public record SessionSummary(Guid SessionId, string PlateNumber, string ShortCode, VehicleSessionStatus Status, DateTime IssuedAt, DateTime? CheckedOutAt, Guid? CustomerId);

    public record SessionDetailResult(Guid SessionId, string PlateNumber, string ShortCode, VehicleSessionStatus Status, DateTime IssuedAt, DateTime? ClaimedAt, DateTime? CheckedOutAt, string? FlagReason, Guid? CustomerId, string PlatePhotoUrl, string CustomerPhotoUrl);

    /// <summary>Status sync result for KhachLink QR wallet (R2 Sprint 4).</summary>
    public record SessionStatusResult(Guid SessionId, string PlateNumber, string ShortCode, VehicleSessionStatus Status, DateTime IssuedAt, DateTime? CheckedOutAt, Guid? CustomerId, Guid TenantId);

    // === Request DTOs ===

    public record IssueRequest(string PlateNumber, string PlatePhotoKey, string CustomerPhotoKey, string? CustomerPhone);

    public record ClaimRequest(string? QrPayload, string? ShortCode);
}
