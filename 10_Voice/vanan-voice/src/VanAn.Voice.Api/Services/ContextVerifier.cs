using Npgsql;

namespace VanAn.Voice.Api.Services;

public sealed record ContextVerification(bool Verified, Guid? TenantId, string? Label);

/// <summary>
/// Verifies a call's business context (SRS v1.1 O6): the referenced record must exist and
/// BOTH users must be participants. For <c>chat</c> the tenant of the call is the conversation
/// tenant — the only trustworthy tenant source for customers (no tenant claim in token).
/// </summary>
public interface IContextVerifier
{
    Task<ContextVerification?> VerifyAsync(string contextType, string contextReferenceId, Guid callerUserId, Guid targetUserId, CancellationToken ct = default);

    /// <summary>
    /// Chat-first UX (SRS §9.2, P4): when the caller does not know the counterpart's user id,
    /// the server resolves the OTHER active participants of the conversation — ordered so the
    /// opposite role comes first. Returns ALL candidates (shop threads have many staff/guest
    /// participants); the caller picks the first reachable one.
    /// </summary>
    Task<IReadOnlyList<Guid>> ResolveCounterpartCandidatesAsync(string contextType, string contextReferenceId, Guid callerUserId, CancellationToken ct = default);
}

/// <summary>
/// Read-only verification against the Gateway PostgreSQL (dedicated connection; the module
/// never writes to Gateway data). SQL per context type is configurable via
/// <c>Voice:ContextVerifySql:&lt;type&gt;</c> — pinned to the real schema during P4 chat
/// integration. Unconfigured types fail CLOSED (Verified=false).
/// </summary>
public sealed class PostgresContextVerifier(
    IConfiguration configuration,
    ILogger<PostgresContextVerifier> logger) : IContextVerifier
{
    private readonly string? _connectionString = configuration["Voice:GatewayDb"];

    public Task<ContextVerification?> VerifyAsync(string contextType, string contextReferenceId, Guid callerUserId, Guid targetUserId, CancellationToken ct = default)
        => VerifyInternalAsync(contextType, contextReferenceId, callerUserId, targetUserId, ct);

    public async Task<IReadOnlyList<Guid>> ResolveCounterpartCandidatesAsync(string contextType, string contextReferenceId, Guid callerUserId, CancellationToken ct = default)
    {
        var sql = configuration[$"Voice:ContextVerifySql:{contextType}:counterpart"];
        if (string.IsNullOrWhiteSpace(_connectionString) || string.IsNullOrWhiteSpace(sql))
        {
            logger.LogWarning("Counterpart resolver not configured for '{ContextType}' — returning empty.", contextType);
            return Array.Empty<Guid>();
        }

        try
        {
            var candidates = new List<Guid>();
#pragma warning disable CA2100
            await using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync(ct);
            await using var cmd = new NpgsqlCommand(sql, conn);
#pragma warning restore CA2100
            cmd.Parameters.AddWithValue("contextRef", contextReferenceId);
            cmd.Parameters.AddWithValue("caller", callerUserId);
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                if (!reader.IsDBNull(0)) candidates.Add(reader.GetGuid(0));
            }
            logger.LogInformation("Counterpart resolve {ContextType} {ContextRef}: caller={Caller} → [{Result}]",
                contextType, contextReferenceId, callerUserId, string.Join(", ", candidates));
            return candidates;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Counterpart resolution failed for {ContextType} — returning empty.", contextType);
            return Array.Empty<Guid>();
        }
    }

    private async Task<ContextVerification?> VerifyInternalAsync(string contextType, string contextReferenceId, Guid callerUserId, Guid targetUserId, CancellationToken ct)
    {
        var sql = configuration[$"Voice:ContextVerifySql:{contextType}"];
        if (string.IsNullOrWhiteSpace(_connectionString) || string.IsNullOrWhiteSpace(sql))
        {
            logger.LogWarning("Context verifier not configured for '{ContextType}' — failing closed.", contextType);
            return new ContextVerification(false, null, null);
        }

        try
        {
            // SQL comes from trusted admin config (Voice:ContextVerifySql:*), never from callers;
            // all values are passed as parameters.
#pragma warning disable CA2100
            await using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync(ct);
            await using var cmd = new NpgsqlCommand(sql, conn);
#pragma warning restore CA2100
            cmd.Parameters.AddWithValue("contextRef", contextReferenceId);
            cmd.Parameters.AddWithValue("caller", callerUserId);
            cmd.Parameters.AddWithValue("target", targetUserId);
            var tenant = await cmd.ExecuteScalarAsync(ct) as Guid?;
            logger.LogInformation("Context verify {ContextType} {ContextRef}: caller={Caller} target={Target} → tenant={Tenant}",
                contextType, contextReferenceId, callerUserId, targetUserId, tenant);
            return new ContextVerification(tenant is not null, tenant, null);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Context verification failed for {ContextType} — failing closed.", contextType);
            return new ContextVerification(false, null, null);
        }
    }
}

/// <summary>Test/fallback verifier — denies everything (fail closed).</summary>
public sealed class NullContextVerifier(ILogger<NullContextVerifier> logger) : IContextVerifier
{
    public Task<ContextVerification?> VerifyAsync(string contextType, string contextReferenceId, Guid callerUserId, Guid targetUserId, CancellationToken ct = default)
    {
        logger.LogWarning("No context verifier registered — denying context call {ContextType}/{ContextRef}.", contextType, contextReferenceId);
        return Task.FromResult<ContextVerification?>(new ContextVerification(false, null, null));
    }

    public Task<IReadOnlyList<Guid>> ResolveCounterpartCandidatesAsync(string contextType, string contextReferenceId, Guid callerUserId, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<Guid>>(Array.Empty<Guid>());
}