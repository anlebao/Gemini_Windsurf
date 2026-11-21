using Npgsql;

namespace VanAn.Voice.Api.Services;

/// <summary>
/// Resolves a human-readable display name for a voice user id — shown as caller
/// identity on IncomingCall. Read-only against Gateway PostgreSQL; FAIL-OPEN:
/// missing config or any error returns null and callers fall back to the raw id.
/// </summary>
public interface IVoiceDisplayNameResolver
{
    Task<string?> ResolveAsync(Guid userId, CancellationToken ct = default);
}

/// <summary>
/// SQL is admin config (<c>Voice:DisplayNameSql</c>, param <c>@userId</c>) — same
/// trusted-config pattern as <see cref="PostgresContextVerifier"/>. Expected to
/// COALESCE customer names (Customers."FullName") with staff names (Users."DisplayName").
/// </summary>
public sealed class PostgresDisplayNameResolver(
    IConfiguration configuration,
    ILogger<PostgresDisplayNameResolver> logger) : IVoiceDisplayNameResolver
{
    private readonly string? _connectionString = configuration["Voice:GatewayDb"];

    public async Task<string?> ResolveAsync(Guid userId, CancellationToken ct = default)
    {
        var sql = configuration["Voice:DisplayNameSql"];
        if (string.IsNullOrWhiteSpace(_connectionString) || string.IsNullOrWhiteSpace(sql) || userId == Guid.Empty)
            return null;

        try
        {
#pragma warning disable CA2100
            await using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync(ct);
            await using var cmd = new NpgsqlCommand(sql, conn);
#pragma warning restore CA2100
            cmd.Parameters.AddWithValue("userId", userId);
            var name = await cmd.ExecuteScalarAsync(ct) as string;
            if (string.IsNullOrWhiteSpace(name)) return null;
            logger.LogDebug("Display name resolved for {UserId}: {Name}", userId, name);
            return name.Trim();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Display-name resolution failed for {UserId} — falling back to id.", userId);
            return null;
        }
    }
}

/// <summary>Fallback when GatewayDb is not configured — always falls back to id.</summary>
public sealed class NullDisplayNameResolver : IVoiceDisplayNameResolver
{
    public Task<string?> ResolveAsync(Guid userId, CancellationToken ct = default)
        => Task.FromResult<string?>(null);
}
