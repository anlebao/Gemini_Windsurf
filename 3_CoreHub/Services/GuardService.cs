using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using VanAn.CoreHub.Repositories;
using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Services
{
    /// <summary>
    /// Guard QR Verification service implementation (Issue #126).
    /// QR payload format: JSON {"sid":"<sessionId>","t":"<qrToken>","tn":"<tenantId>"}
    /// QR token hash: SHA256(qrPayload) — stored in VehicleSession.QrTokenHash for lookup.
    /// Short code: 6-digit random, unique per tenant per day.
    /// </summary>
    public class GuardService : IGuardService
    {
        private readonly IVehicleSessionRepository _sessionRepo;
        private readonly IGuardScanLogRepository _scanLogRepo;
        private readonly IR2StorageService _r2Storage;
        private readonly ILogger<GuardService> _logger;
        private static readonly Random _random = new();

        public GuardService(
            IVehicleSessionRepository sessionRepo,
            IGuardScanLogRepository scanLogRepo,
            IR2StorageService r2Storage,
            ILogger<GuardService> logger)
        {
            _sessionRepo = sessionRepo;
            _scanLogRepo = scanLogRepo;
            _r2Storage = r2Storage;
            _logger = logger;
        }

        public async Task<PresignUploadResult> PresignUploadAsync(Guid tenantId, string contentType)
        {
            var ct = contentType ?? "image/jpeg";
            var plateKey = _r2Storage.GenerateKey("plates", tenantId);
            var customerKey = _r2Storage.GenerateKey("customers", tenantId);
            var plateUrl = _r2Storage.GetPresignedUploadUrl(plateKey, ct, 15);
            var customerUrl = _r2Storage.GetPresignedUploadUrl(customerKey, ct, 15);
            return new PresignUploadResult(plateKey, plateUrl, customerKey, customerUrl);
        }

        /// <summary>#130: Generate a photo key for server-side upload.</summary>
        public string GeneratePhotoKey(Guid tenantId, string slot)
        {
            var prefix = slot == "plate" ? "plates" : "customers";
            return _r2Storage.GenerateKey(prefix, tenantId);
        }

        /// <summary>#130: Upload photo to R2 server-side (Gateway → R2, no CORS needed).</summary>
        public Task<bool> UploadPhotoAsync(string key, string base64Data, string contentType)
        {
            return _r2Storage.UploadObjectAsync(key, base64Data, contentType);
        }

        public async Task<IssueResult> IssueAsync(Guid tenantId, Guid guardId, IssueRequest req)
        {
            // 1. Generate QR token (random 256-bit hex string)
            var qrToken = GenerateQrToken();

            // 2. Create QR payload (JSON) — sessionId will be set after entity creation
            //    We use a placeholder sessionId here, then update the payload with the real Id.
            //    Actually, we generate the session first, then build the payload with the real Id.
            //    But QrTokenHash must be set at construction time...
            //    Solution: Build payload with a pre-generated sessionId (Guid.NewGuid()),
            //    then use that as the entity Id. But BaseEntity.Id is set by constructor.
            //    Alternative: Use the QR token itself as the lookup key (not sessionId in payload).
            //    Payload = {"t":"<qrToken>","tn":"<tenantId>"} — no sessionId needed in payload.
            //    QrTokenHash = SHA256(payload) — unique per session.

            var payload = JsonSerializer.Serialize(new { t = qrToken, tn = tenantId });
            var qrTokenHash = HashPayload(payload);

            // 3. Generate unique short code (6-digit, unique per tenant per day)
            var shortCode = await GenerateUniqueShortCodeAsync(tenantId);

            // 4. Create VehicleSession
            var session = new VehicleSession(
                new TenantId(tenantId),
                req.PlateNumber,
                req.PlatePhotoKey,
                req.CustomerPhotoKey,
                guardId,
                qrTokenHash,
                shortCode,
                req.CustomerPhone);

            await _sessionRepo.AddAsync(session);
            await _sessionRepo.SaveChangesAsync();

            _logger.LogInformation("Guard {GuardId} issued QR session {SessionId} for plate {PlateNumber} (tenant {TenantId})",
                guardId, session.Id, req.PlateNumber, tenantId);

            return new IssueResult(session.Id, payload, shortCode);
        }

        public async Task<ClaimResult> ClaimAsync(Guid tenantId, Guid customerId, ClaimRequest req)
        {
            VehicleSession? session = null;

            // Lookup by QR payload hash OR short code
            if (!string.IsNullOrWhiteSpace(req.QrPayload))
            {
                var hash = HashPayload(req.QrPayload);
                session = await _sessionRepo.GetByQrTokenHashAsync(hash, tenantId);

                // #130-fix: Fallback — if hash lookup fails, try parsing {sc, sid} payload format
                // (used by PrintTicket.razor). Without this, KhachLink app cannot claim QR codes
                // from printed tickets — the printed QR uses {sc,sid} format, not the original
                // {sid,t,tn} format, so the hash doesn't match.
                if (session == null)
                {
                    session = await TryLookupByAlternativePayloadAsync(req.QrPayload, tenantId);
                }
            }
            else if (!string.IsNullOrWhiteSpace(req.ShortCode))
            {
                session = await _sessionRepo.GetByShortCodeAsync(req.ShortCode, tenantId);
            }

            if (session == null)
                throw new KeyNotFoundException("QR session not found. Please check your QR code or short code.");

            if (session.Status != VehicleSessionStatus.Issued)
                throw new InvalidOperationException($"Cannot claim session: current status is {session.Status}. Expected Issued.");

            // Claim the session
            session.Claim(customerId);
            await _sessionRepo.SaveChangesAsync();

            var plateUrl = _r2Storage.GetPresignedDownloadUrl(session.PlatePhotoKey, 60);
            // #130: Ảnh khách tùy chọn — trả null nếu không có (tránh R2Storage throw trên null key)
            var customerUrl = string.IsNullOrWhiteSpace(session.CustomerPhotoKey)
                ? null
                : _r2Storage.GetPresignedDownloadUrl(session.CustomerPhotoKey, 60);

            _logger.LogInformation("Customer {CustomerId} claimed QR session {SessionId} (tenant {TenantId})",
                customerId, session.Id, tenantId);

            return new ClaimResult(session.Id, session.PlateNumber, plateUrl, customerUrl, session.IssuedAt, session.Status);
        }

        public async Task<VerifyResult> VerifyAsync(Guid tenantId, Guid guardId, string scannedQrPayload)
        {
            if (string.IsNullOrWhiteSpace(scannedQrPayload))
                throw new ArgumentException("Scanned QR payload is required.", nameof(scannedQrPayload));

            var hash = HashPayload(scannedQrPayload);
            var session = await _sessionRepo.GetByQrTokenHashAsync(hash, tenantId);

            // #130-fix: Fallback — if hash lookup fails, try parsing {sc, sid} payload format
            // (used by PrintTicket.razor). Look up by sessionId or shortCode.
            if (session == null)
            {
                session = await TryLookupByAlternativePayloadAsync(scannedQrPayload, tenantId);
            }

            if (session == null)
            {
                // Log mismatch scan
                await _scanLogRepo.AddAsync(new GuardScanLog(
                    new TenantId(tenantId), Guid.Empty, hash, GuardScanResult.Mismatch, guardId, "QR not found"));
                await _scanLogRepo.SaveChangesAsync();
                throw new KeyNotFoundException("QR session not found. The QR code may be invalid or from another location.");
            }

            if (session.Status == VehicleSessionStatus.Voided)
                throw new InvalidOperationException("This QR session has been voided.");

            // Log match scan
            await _scanLogRepo.AddAsync(new GuardScanLog(
                new TenantId(tenantId), session.Id, hash, GuardScanResult.Match, guardId));
            await _scanLogRepo.SaveChangesAsync();

            var plateUrl = _r2Storage.GetPresignedDownloadUrl(session.PlatePhotoKey, 60);
            // #130: Ảnh khách tùy chọn — trả null nếu không có
            var customerUrl = string.IsNullOrWhiteSpace(session.CustomerPhotoKey)
                ? null
                : _r2Storage.GetPresignedDownloadUrl(session.CustomerPhotoKey, 60);

            _logger.LogInformation("Guard {GuardId} verified QR session {SessionId} (plate {PlateNumber})",
                guardId, session.Id, session.PlateNumber);

            return new VerifyResult(session.Id, session.PlateNumber, plateUrl, customerUrl,
                session.IssuedAt, session.Status, session.CustomerId);
        }

        public async Task<CheckoutResult> CheckoutAsync(Guid tenantId, Guid guardId, Guid sessionId)
        {
            var session = await _sessionRepo.GetByIdAsync(sessionId, tenantId)
                ?? throw new KeyNotFoundException("QR session not found.");

            session.Checkout(guardId);
            await _sessionRepo.SaveChangesAsync();

            _logger.LogInformation("Guard {GuardId} checked out QR session {SessionId} (plate {PlateNumber})",
                guardId, session.Id, session.PlateNumber);

            return new CheckoutResult(session.Id, session.Status, session.CheckedOutAt!.Value);
        }

        public async Task<FlagResult> FlagAsync(Guid tenantId, Guid guardId, Guid sessionId, string reason)
        {
            if (string.IsNullOrWhiteSpace(reason))
                throw new ArgumentException("Flag reason is required.", nameof(reason));

            var session = await _sessionRepo.GetByIdAsync(sessionId, tenantId)
                ?? throw new KeyNotFoundException("QR session not found.");

            session.Flag(reason, guardId);
            await _sessionRepo.SaveChangesAsync();

            // Log flagged scan
            await _scanLogRepo.AddAsync(new GuardScanLog(
                new TenantId(tenantId), session.Id, session.QrTokenHash, GuardScanResult.Flagged, guardId, reason));
            await _scanLogRepo.SaveChangesAsync();

            _logger.LogWarning("Guard {GuardId} flagged QR session {SessionId}: {Reason}",
                guardId, session.Id, reason);

            return new FlagResult(session.Id, session.Status, session.FlagReason!);
        }

        public async Task<VoidResult> VoidAsync(Guid tenantId, Guid guardId, Guid sessionId)
        {
            var session = await _sessionRepo.GetByIdAsync(sessionId, tenantId)
                ?? throw new KeyNotFoundException("QR session not found.");

            session.Void();
            await _sessionRepo.SaveChangesAsync();

            _logger.LogInformation("Guard {GuardId} voided QR session {SessionId}", guardId, session.Id);

            return new VoidResult(session.Id, session.Status);
        }

        public async Task<TodaySessionsResult> GetTodaySessionsAsync(Guid tenantId, VehicleSessionStatus? status, int page, int pageSize)
        {
            if (page < 1) page = 1;
            if (pageSize < 1 || pageSize > 100) pageSize = 20;

            var (items, total) = await _sessionRepo.GetTodaySessionsAsync(tenantId, status, page, pageSize);
            var (checkInCount, checkOutCount, inLotCount) = await _sessionRepo.GetTodayStatsAsync(tenantId);

            var summaries = items.Select(s => new SessionSummary(
                s.Id, s.PlateNumber, s.ShortCode, s.Status, s.IssuedAt, s.CheckedOutAt, s.CustomerId)).ToList();

            return new TodaySessionsResult(total, checkInCount, checkOutCount, inLotCount, summaries);
        }

        public async Task<SessionDetailResult> GetSessionAsync(Guid tenantId, Guid sessionId)
        {
            var session = await _sessionRepo.GetByIdAsync(sessionId, tenantId)
                ?? throw new KeyNotFoundException("QR session not found.");

            var plateUrl = _r2Storage.GetPresignedDownloadUrl(session.PlatePhotoKey, 60);
            // #130: Ảnh khách tùy chọn — trả null nếu không có
            var customerUrl = string.IsNullOrWhiteSpace(session.CustomerPhotoKey)
                ? null
                : _r2Storage.GetPresignedDownloadUrl(session.CustomerPhotoKey, 60);

            return new SessionDetailResult(
                session.Id, session.PlateNumber, session.ShortCode, session.Status,
                session.IssuedAt, session.ClaimedAt, session.CheckedOutAt, session.FlagReason,
                session.CustomerId, plateUrl, customerUrl);
        }

        public async Task<List<SessionStatusResult>> GetSessionStatusesAsync(Guid customerId, List<Guid> sessionIds)
        {
            if (sessionIds == null || sessionIds.Count == 0)
                return new List<SessionStatusResult>();

            var sessions = await _sessionRepo.GetByIdsForCustomerAsync(customerId, sessionIds);

            return sessions.Select(s => new SessionStatusResult(
                s.Id, s.PlateNumber, s.ShortCode, s.Status,
                s.IssuedAt, s.CheckedOutAt, s.CustomerId, s.TenantId.Value)).ToList();
        }

        // === Private helpers ===

        /// <summary>
        /// #130-fix: Fallback lookup for QR payloads in {sc, sid} format (used by PrintTicket).
        /// Parses the JSON payload and tries to find the session by sessionId or shortCode.
        /// Returns null if the payload doesn't match this format or the session isn't found.
        /// </summary>
        private async Task<VehicleSession?> TryLookupByAlternativePayloadAsync(string payload, Guid tenantId)
        {
            try
            {
                using var doc = JsonDocument.Parse(payload);
                var root = doc.RootElement;

                // Try sessionId first (most reliable)
                if (root.TryGetProperty("sid", out var sidEl) && sidEl.TryGetGuid(out var sessionId))
                {
                    var session = await _sessionRepo.GetByIdAsync(sessionId, tenantId);
                    if (session != null)
                    {
                        _logger.LogInformation("Verify fallback: found session {SessionId} by sid payload", sessionId);
                        return session;
                    }
                }

                // Try shortCode as fallback
                if (root.TryGetProperty("sc", out var scEl))
                {
                    var shortCode = scEl.GetString();
                    if (!string.IsNullOrWhiteSpace(shortCode))
                    {
                        var session = await _sessionRepo.GetByShortCodeAsync(shortCode, tenantId);
                        if (session != null)
                        {
                            _logger.LogInformation("Verify fallback: found session {SessionId} by short code {ShortCode}", session.Id, shortCode);
                            return session;
                        }
                    }
                }
            }
            catch (JsonException)
            {
                // Not JSON — payload is in the original {t, tn} format but hash didn't match
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Verify fallback: error parsing alternative payload");
            }

            return null;
        }

        private static string GenerateQrToken()
        {
            var bytes = RandomNumberGenerator.GetBytes(32);
            return Convert.ToHexString(bytes).ToLowerInvariant();
        }

        private static string HashPayload(string payload)
        {
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(payload));
            return Convert.ToHexString(bytes).ToLowerInvariant();
        }

        private async Task<string> GenerateUniqueShortCodeAsync(Guid tenantId)
        {
            // Try up to 10 times to generate a unique 6-digit code per tenant per day
            for (int i = 0; i < 10; i++)
            {
                var code = _random.Next(100000, 999999).ToString();
                var existing = await _sessionRepo.GetByShortCodeAsync(code, tenantId);
                if (existing == null)
                    return code;
            }
            // Fallback: use timestamp-based code (very unlikely to collide)
            return DateTime.UtcNow.Ticks.ToString()[^6..];
        }
    }
}
