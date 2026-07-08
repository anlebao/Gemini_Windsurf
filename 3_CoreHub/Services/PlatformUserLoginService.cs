using Microsoft.EntityFrameworkCore;
using VanAn.CoreHub.Infrastructure;
using VanAn.CoreHub.Infrastructure.Entities;
using VanAn.Shared.Domain.Common;

namespace VanAn.CoreHub.Services;

public class PlatformUserLoginService : IPlatformUserLoginService
{
    private readonly IVanAnDbContext _db;
    private readonly IJwtTokenService _jwtTokenService;

    public PlatformUserLoginService(IVanAnDbContext db, IJwtTokenService jwtTokenService)
    {
        _db = db;
        _jwtTokenService = jwtTokenService;
    }

    public async Task<PlatformLoginResult?> LoginAsync(string username, string password, CancellationToken ct = default)
    {
        var user = await _db.PlatformUsers
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.Username == username, ct);

        if (user == null)
            return null;

        if (!BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
            return null;

        if (!user.IsActive)
            return null;

        var token = _jwtTokenService.GenerateToken(
            userId: user.Id,
            email: user.Email ?? user.Username,
            role: PlatformRole.SystemAdmin.ToString(),
            tenantId: Guid.Empty);

        return new PlatformLoginResult(user.Id, user.Email ?? user.Username, PlatformRole.SystemAdmin.ToString(), token);
    }
}
