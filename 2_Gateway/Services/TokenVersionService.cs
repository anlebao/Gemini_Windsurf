using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using VanAn.CoreHub.Infrastructure;

namespace VanAn.Gateway.Services;

/// <summary>
/// ATRS-3 (D3): token version service — source of truth là Gateway PG (Users/Customers).
/// Cache TTL 60s + write-through khi bump. Claim `tv` trong JWT so với version hiện tại;
/// lệch → từ chối token (revocation). Hệ thống/service tokens (không phải DemoUser) → null → bỏ qua.
/// </summary>
public interface ITokenVersionService
{
    /// <summary>Version hiện tại của user; null khi không phải DemoUser (không enforce).</summary>
    Task<int?> GetVersionAsync(Guid userId, CancellationToken ct = default);
    Task InvalidateAsync(Guid userId);
}

public sealed class TokenVersionService(IMemoryCache cache, IVanAnDbContext db) : ITokenVersionService
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(60);
    private const string KeyPrefix = "token-version:";

    public async Task<int?> GetVersionAsync(Guid userId, CancellationToken ct = default)
    {
        var key = KeyPrefix + userId;
        if (cache.TryGetValue(key, out int cached))
            return cached;

        try
        {
            var version = await db.Users
                .Where(u => u.Id == userId)
                .Select(u => (int?)u.TokenVersion)
                .FirstOrDefaultAsync(ct);
            if (!version.HasValue)
                return null; // không phải DemoUser (PlatformUser/system token) — không enforce

            cache.Set(key, version.Value, CacheTtl);
            return version;
        }
        catch (Exception)
        {
            // Fail-open: lỗi hạ tầng không được làm sập auth (quyết định an toàn: cho qua,
            // version check chỉ hoạt động khi PG khỏe).
            return null;
        }
    }

    public Task InvalidateAsync(Guid userId)
    {
        cache.Remove(KeyPrefix + userId);
        return Task.CompletedTask;
    }
}