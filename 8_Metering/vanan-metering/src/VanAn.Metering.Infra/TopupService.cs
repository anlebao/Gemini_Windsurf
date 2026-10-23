using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using VanAn.Metering.Contracts;
using VanAn.Metering.Domain;

namespace VanAn.Metering.Infra;

public sealed record TopupOpResult(TopupRequest? Request, string? ErrorCode, string? ErrorMessage)
{
    public static TopupOpResult Ok(TopupRequest r) => new(r, null, null);
    public static TopupOpResult Fail(string code, string message) => new(null, code, message);
}

/// <summary>
/// P7 D6–D9 — manual VietQR top-ups: create (idempotent per caller key) → user
/// transfers with ReferenceCode in the note → admin confirms → wallet Grant.
/// Lazy expiry (D9): Pending rows past TTL are marked Expired on read — no job.
/// </summary>
public sealed class TopupService(MeteringDbContext db, WalletService wallet, TimeSpan? topupTtl = null)
{
    // Unambiguous alphabet — no 0/O, 1/I/L — safe to retype into a bank note.
    private const string RefAlphabet = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";
    private const int RefLength = 8;

    private TimeSpan Ttl => topupTtl ?? TimeSpan.FromHours(72);

    /// <summary>D6 — POST /v1/topups. Dedupe (CallerId, Idempotency-Key) → replay same request.</summary>
    public async Task<TopupOpResult> CreateAsync(
        string callerId, string? idemKey, TopupCreateRequest req, DateTime nowUtc, CancellationToken ct = default)
    {
        if (req.TenantId == Guid.Empty)
            return TopupOpResult.Fail("invalid_tenantId", "tenantId must not be empty.");
        if (string.IsNullOrWhiteSpace(idemKey))
            return TopupOpResult.Fail("missing_idempotency_key", "Idempotency-Key is required.");
        if (idemKey.Length > 128)
            return TopupOpResult.Fail("idempotency_key_too_long", "Idempotency-Key must be <= 128 characters.");
        if (req.AmountVnd is < 0)
            return TopupOpResult.Fail("invalid_amountVnd", "amountVnd must be >= 0.");

        var existing = await db.TopupRequests.AsNoTracking()
            .FirstOrDefaultAsync(t => t.CallerId == callerId && t.IdempotencyKey == idemKey, ct);
        if (existing != null) return TopupOpResult.Ok(existing);

        // Card §5-e: referenceCode collision (astronomically rare) → regenerate once.
        for (var attempt = 0; attempt < 2; attempt++)
        {
            var referenceCode = "TOPUP-" + RandomReference();
            var request = TopupRequest.Create(req.TenantId, referenceCode, req.AmountVnd,
                callerId, idemKey, nowUtc, Ttl);
            db.TopupRequests.Add(request);
            try
            {
                await db.SaveChangesAsync(ct);
                return TopupOpResult.Ok(request);
            }
            catch (DbUpdateException)
            {
                db.Entry(request).State = EntityState.Detached;
                // Same idempotency key raced in — return the winner (card §5-b path).
                var winner = await db.TopupRequests.AsNoTracking()
                    .FirstOrDefaultAsync(t => t.CallerId == callerId && t.IdempotencyKey == idemKey, ct);
                if (winner != null) return TopupOpResult.Ok(winner);
            }
        }
        return TopupOpResult.Fail("reference_collision", "Could not allocate a unique referenceCode — retry.");
    }

    /// <summary>GET /v1/topups — list with lazy expiry (D9).</summary>
    public async Task<TopupListResponse> ListAsync(string? status, Guid? tenantId, DateTime nowUtc, CancellationToken ct = default)
    {
        var q = db.TopupRequests.AsQueryable();
        if (tenantId.HasValue) q = q.Where(t => t.TenantId == tenantId.Value);
        if (!string.IsNullOrWhiteSpace(status))
            q = Enum.TryParse<TopupStatus>(status, ignoreCase: true, out var st)
                ? q.Where(t => t.Status == st)
                : q.Where(t => false);

        var items = await q.OrderByDescending(t => t.CreatedAtUtc).Take(200).ToListAsync(ct);
        await LazyExpireAsync(items, nowUtc, ct);
        var cfg = await db.TopupConfigs.AsNoTracking().FirstOrDefaultAsync(ct);
        return new TopupListResponse(items.Select(t => ToDto(t, cfg)).ToList());
    }

    /// <summary>D6 — admin confirm: Pending → Grant credits + reset alert cycle. Idempotent on repeat.</summary>
    public async Task<TopupOpResult> ConfirmAsync(
        Guid id, TopupConfirmRequest req, string adminActor, DateTime nowUtc, CancellationToken ct = default)
    {
        if (req.Credits <= 0)
            return TopupOpResult.Fail("invalid_credits", "credits must be > 0.");

        var request = await db.TopupRequests.FindAsync([id], ct);
        if (request == null) return TopupOpResult.Fail("not_found", "Topup request not found.");
        await LazyExpireAsync([request], nowUtc, ct);

        if (request.Status == TopupStatus.Confirmed)
            return TopupOpResult.Ok(request); // replay — no double-grant (card §5-b)
        if (request.Status != TopupStatus.Pending)
            return TopupOpResult.Fail("topup_not_pending", $"Cannot confirm a {StatusName(request.Status)} topup request.");

        // Same context save: status flip + ledger Grant + wallet update = 1 commit.
        request.Confirm(req.Credits, adminActor, req.Note, nowUtc);
        try
        {
            await wallet.GrantAsync(request.TenantId, req.Credits,
                $"topup:{request.ReferenceCode}", adminActor, nowUtc, topupRequestId: request.Id, ct);
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // T25 — concurrent confirm lost the race on the ledger's unique
            // TopupRequestId index: reload and return the winner (no double-grant).
            db.Entry(request).State = EntityState.Detached;
            var winner = await db.TopupRequests.FindAsync([id], ct);
            if (winner?.Status == TopupStatus.Confirmed) return TopupOpResult.Ok(winner);
            return TopupOpResult.Fail("confirm_conflict", "Concurrent confirm failed — retry.");
        }
        return TopupOpResult.Ok(request);
    }

    /// <summary>Admin reject — Pending only; reason recorded.</summary>
    public async Task<TopupOpResult> RejectAsync(
        Guid id, TopupRejectRequest req, string adminActor, DateTime nowUtc, CancellationToken ct = default)
    {
        var request = await db.TopupRequests.FindAsync([id], ct);
        if (request == null) return TopupOpResult.Fail("not_found", "Topup request not found.");
        await LazyExpireAsync([request], nowUtc, ct);

        if (request.Status != TopupStatus.Pending)
            return TopupOpResult.Fail("topup_not_pending", $"Cannot reject a {StatusName(request.Status)} topup request.");

        request.Reject(adminActor, req.Reason, nowUtc);
        await db.SaveChangesAsync(ct);
        return TopupOpResult.Ok(request);
    }

    // ---- TopupConfig singleton ----

    public async Task<TopupConfigDto?> GetConfigAsync(CancellationToken ct = default)
    {
        var cfg = await db.TopupConfigs.AsNoTracking().FirstOrDefaultAsync(ct);
        return cfg == null ? null : ToDto(cfg);
    }

    /// <summary>Entity form for QR payload building at the API layer.</summary>
    public Task<TopupConfig?> GetConfigEntityAsync(CancellationToken ct = default)
        => db.TopupConfigs.AsNoTracking().FirstOrDefaultAsync(ct);

    public async Task<TopupConfigDto> PutConfigAsync(
        TopupConfigPutRequest req, string adminActor, DateTime nowUtc, CancellationToken ct = default)
    {
        var cfg = await db.TopupConfigs.FindAsync([TopupConfig.SingletonId], ct);
        if (cfg == null)
        {
            cfg = TopupConfig.Create(req.BankCode.Trim(), req.BankName.Trim(),
                req.AccountNumber.Trim(), req.AccountName.Trim(), adminActor, nowUtc);
            db.TopupConfigs.Add(cfg);
        }
        else
        {
            cfg.Update(req.BankCode.Trim(), req.BankName.Trim(),
                req.AccountNumber.Trim(), req.AccountName.Trim(), adminActor, nowUtc);
        }
        db.AuditLogs.Add(AuditLog.Create(adminActor, "upsert", nameof(TopupConfig), cfg.Id,
            oldJson: null, newJson: null, atUtc: nowUtc));
        await db.SaveChangesAsync(ct);
        return ToDto(cfg);
    }

    // ---- helpers ----

    private async Task LazyExpireAsync(IReadOnlyCollection<TopupRequest> items, DateTime nowUtc, CancellationToken ct)
    {
        var stale = items.Where(t => t.Status == TopupStatus.Pending && nowUtc >= t.ExpiresAtUtc).ToList();
        if (stale.Count == 0) return;
        // Detached items from a list query — load tracked rows before mutating.
        var ids = stale.Select(t => t.Id).ToList();
        var tracked = await db.TopupRequests.Where(t => ids.Contains(t.Id)).ToListAsync(ct);
        var changed = false;
        foreach (var t in tracked) changed |= t.ExpireIfStale(nowUtc);
        if (changed) await db.SaveChangesAsync(ct);
        foreach (var s in stale) s.ExpireIfStale(nowUtc); // keep returned DTOs consistent
    }

    private static string StatusName(TopupStatus s) => s switch
    {
        TopupStatus.Confirmed => "confirmed",
        TopupStatus.Rejected => "rejected",
        TopupStatus.Expired => "expired",
        _ => "pending",
    };

    private static string RandomReference()
    {
        Span<byte> bytes = stackalloc byte[RefLength];
        RandomNumberGenerator.Fill(bytes);
        return string.Create(RefLength, bytes.ToArray(), static (chars, buf) =>
        {
            for (var i = 0; i < chars.Length; i++) chars[i] = RefAlphabet[buf[i] % RefAlphabet.Length];
        });
    }

    private static TopupDto ToDto(TopupRequest t, TopupConfig? cfg) => new(
        t.Id, t.TenantId, t.ReferenceCode, t.AmountVnd, t.CreditsGranted,
        StatusName(t.Status),
        cfg != null ? VietQrPayloadBuilder.Build(cfg, t.ReferenceCode, t.AmountVnd) : null,
        t.Note, t.ConfirmedBy, t.ConfirmedAtUtc, t.CreatedAtUtc, t.ExpiresAtUtc);

    private static TopupConfigDto ToDto(TopupConfig c) => new(
        c.BankCode, c.BankName, c.AccountNumber, c.AccountName, c.UpdatedBy, c.UpdatedAtUtc);
}
