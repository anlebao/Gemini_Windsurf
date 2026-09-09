using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using VanAn.CoreHub.Infrastructure;
using VanAn.CoreHub.Infrastructure.Entities;
using VanAn.Shared.Domain.Audit;  // Sprint 3 P3.3 — AuditActionType
using VanAn.Shared.Domain.Common;

namespace VanAn.CoreHub.Services;

public class PlatformUserLoginService : IPlatformUserLoginService
{
    private readonly IVanAnDbContext _db;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly IAuditTrailService _auditTrailService;  // Sprint 3 P3.3
    private readonly IHttpContextAccessor _httpContextAccessor;  // Sprint 3 P3.3 — IP capture

    public PlatformUserLoginService(
        IVanAnDbContext db,
        IJwtTokenService jwtTokenService,
        IAuditTrailService auditTrailService,
        IHttpContextAccessor httpContextAccessor)
    {
        _db = db;
        _jwtTokenService = jwtTokenService;
        _auditTrailService = auditTrailService;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task<PlatformLoginResult?> LoginAsync(string username, string password, CancellationToken ct = default)
    {
        var user = await _db.PlatformUsers
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.Username == username, ct);

        var clientIp = _httpContextAccessor.HttpContext?.Connection?.RemoteIpAddress?.ToString() ?? "unknown";
        var userAgent = _httpContextAccessor.HttpContext?.Request?.Headers?.UserAgent.ToString() ?? "unknown";

        if (user == null)
        {
            // Sprint 3 P3.3: Log failed login — user not found
            await LogSecurityEventAsync(
                AuditActionType.FailedLogin,
                $"Failed login: username '{username}' not found from IP {clientIp}",
                clientIp, userAgent, ct);
            return null;
        }

        if (!BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
        {
            // Sprint 3 P3.3: Log failed login — wrong password
            await LogSecurityEventAsync(
                AuditActionType.FailedLogin,
                $"Failed login: wrong password for '{username}' from IP {clientIp}",
                clientIp, userAgent, ct);
            return null;
        }

        if (!user.IsActive)
        {
            // Sprint 3 P3.3: Log failed login — inactive user
            await LogSecurityEventAsync(
                AuditActionType.FailedLogin,
                $"Failed login: inactive user '{username}' from IP {clientIp}",
                clientIp, userAgent, ct);
            return null;
        }

        var token = _jwtTokenService.GenerateToken(
            userId: user.Id,
            email: user.Email ?? user.Username,
            role: PlatformRole.SystemAdmin.ToString(),
            tenantId: Guid.Empty);

        return new PlatformLoginResult(user.Id, user.Email ?? user.Username, PlatformRole.SystemAdmin.ToString(), token);
    }

    /// <summary>Sprint 3 P3.3: Best-effort security event logging for failed logins.</summary>
    private async Task LogSecurityEventAsync(
        AuditActionType actionType,
        string description,
        string clientIp,
        string userAgent,
        CancellationToken ct)
    {
        try
        {
            await _auditTrailService.LogSecurityEventAsync(
                actionType,
                description,
                correlationId: clientIp,  // IP as correlation — track repeated attempts from same IP
                ipAddress: clientIp,      // Sprint 3 EXPANDED — structured IP field
                userAgent: userAgent,     // Sprint 3 EXPANDED — structured UserAgent field
                ct);
        }
        catch
        {
            // Best-effort — don't fail login flow if audit fails
        }
    }
}
