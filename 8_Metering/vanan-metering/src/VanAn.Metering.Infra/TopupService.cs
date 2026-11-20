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

/// <summary>P8 — result of minting a Tingee checkout link for a pending top-up.</summary>
public sealed record TopupPayLinkResult(
    string? CheckoutUrl, string? OrderId, decimal? AmountVnd, int ExpiresInMinute,
    string? ErrorCode = null, string? ErrorMessage = null)
{
    public static TopupPayLinkResult Fail(string code, string msg)
        => new(null, null, null, 0, code, msg);
}

/// <summary>P9 — result of minting a Tingee dynamic QR for a pending top-up.</summary>
public sealed record TopupQrResult(
    string? QrPayload, string? BillId, string? QrAccount, int ExpiresInMinute,
    string? ErrorCode = null, string? ErrorMessage = null)
{
    public static TopupQrResult Fail(string code, string msg)
        => new(null, null, null, 0, code, msg);
}

/// <summary>
/// P7 D6–D9 — manual VietQR top-ups: create (idempotent per caller key) → user
/// transfers with ReferenceCode in the note → admin confirms → wallet Grant.
/// Lazy expiry (D9): Pending rows past TTL are marked Expired on read — no job.
/// </summary>
public sealed class TopupService(
    MeteringDbContext db, WalletService wallet, TimeSpan? topupTtl = null,
    TingeePaymentClient? tingee = null, PackageService? packages = null)
{
    // Unambiguous alphabet — no 0/O, 1/I/L — safe to retype into a bank note.
    private const string RefAlphabet = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";
    private const int RefLength = 8;

    // P13 — public top-up guardrail (storefront callers are anonymous): bound
    // QR-spam by capping live pending requests and creation rate per tenant.
    private const int MaxPendingPerTenant = 5;
    private const int MaxCreationsPerHour = 20;

    private TimeSpan Ttl => topupTtl ?? TimeSpan.FromHours(72);
    private readonly TingeePaymentClient? _tingee = tingee;
    private readonly PackageService? _packages = packages;

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

        // P11 — package selection: price/credits/validity come from the catalog.
        CreditPackage? package = null;
        if (!string.IsNullOrWhiteSpace(req.PackageCode))
        {
            if (_packages == null)
                return TopupOpResult.Fail("packages_unavailable", "Package catalog is not configured.");
            package = await _packages.GetByCodeAsync(req.PackageCode, activeOnly: true, ct);
            if (package == null)
            {
                var any = await _packages.GetByCodeAsync(req.PackageCode, activeOnly: false, ct);
                return TopupOpResult.Fail(any == null ? "package_not_found" : "package_unavailable",
                    any == null ? "Unknown package code." : "Package is not active.");
            }
        }

        var existing = await db.TopupRequests.AsNoTracking()
            .FirstOrDefaultAsync(t => t.CallerId == callerId && t.IdempotencyKey == idemKey, ct);
        if (existing != null) return TopupOpResult.Ok(existing);

        // P13 — rate-limit before insert (idempotent replays already returned above).
        // Pending cap chỉ đếm lệnh CHƯA hết hạn — row quá TTL mà chưa lazy-expire
        // (chưa ai đọc) không được chặn tenant mãi mãi.
        var pendingCount = await db.TopupRequests
            .CountAsync(t => t.TenantId == req.TenantId
                && t.Status == TopupStatus.Pending && t.ExpiresAtUtc > nowUtc, ct);
        if (pendingCount >= MaxPendingPerTenant)
            return TopupOpResult.Fail("too_many_pending_topups",
                $"Too many pending top-up requests ({MaxPendingPerTenant}) — finish or let them expire first.");
        var hourlyCount = await db.TopupRequests
            .CountAsync(t => t.TenantId == req.TenantId && t.CreatedAtUtc >= nowUtc.AddHours(-1), ct);
        if (hourlyCount >= MaxCreationsPerHour)
            return TopupOpResult.Fail("topup_rate_limited",
                $"Too many top-up requests this hour ({MaxCreationsPerHour}) — try again later.");

        // Card §5-e: referenceCode collision (astronomically rare) → regenerate once.
        for (var attempt = 0; attempt < 2; attempt++)
        {
            var referenceCode = "TOPUP-" + RandomReference();
            var request = TopupRequest.Create(req.TenantId, referenceCode,
                package?.PriceVnd ?? req.AmountVnd,
                callerId, idemKey, nowUtc, Ttl, package?.Code);
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
        return await ConfirmCoreAsync(request, req.Credits, adminActor, req.Note, nowUtc, ct);
    }

    /// <summary>Shared confirm core — flips status + Grant + single SaveChanges commit.</summary>
    private async Task<TopupOpResult> ConfirmCoreAsync(
        TopupRequest request, decimal credits, string actor, string? note,
        DateTime nowUtc, CancellationToken ct)
    {
        // P11 — package top-ups grant an expiring lot (validity from confirm time).
        DateTime? lotExpiresAt = null;
        if (request.PackageCode != null && _packages != null)
        {
            var pkg = await _packages.GetByCodeAsync(request.PackageCode, activeOnly: false, ct);
            if (pkg != null) lotExpiresAt = nowUtc.AddDays(pkg.ValidityDays);
        }

        // Same context save: status flip + ledger Grant + wallet update = 1 commit.
        request.Confirm(credits, actor, note, nowUtc);
        try
        {
            await wallet.GrantAsync(request.TenantId, credits,
                $"topup:{request.ReferenceCode}", actor, nowUtc,
                topupRequestId: request.Id, expiresAtUtc: lotExpiresAt, ct: ct);
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // T25 — concurrent confirm lost the race on the ledger's unique
            // TopupRequestId index: reload and return the winner (no double-grant).
            db.Entry(request).State = EntityState.Detached;
            var winner = await db.TopupRequests.FindAsync([request.Id], ct);
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

    // ---- P8: Tingee online payment ----

    /// <summary>
    /// POST /v1/topups/{id}/pay — mint a FRESH Tingee checkout link for a Pending
    /// top-up. Links expire ≤30 min while TopupRequest lives 72h, so links are
    /// never persisted — every call mints a new one (orderId = ReferenceCode).
    /// </summary>
    public async Task<TopupPayLinkResult> CreatePayLinkAsync(
        Guid id, DateTime nowUtc, CancellationToken ct = default)
    {
        var request = await db.TopupRequests.FindAsync([id], ct);
        if (request == null)
            return TopupPayLinkResult.Fail("not_found", "Topup request not found.");
        await LazyExpireAsync([request], nowUtc, ct);
        if (request.Status != TopupStatus.Pending)
            return TopupPayLinkResult.Fail("topup_not_pending",
                $"Cannot pay a {StatusName(request.Status)} topup request.");
        if (request.AmountVnd is null or <= 0)
            return TopupPayLinkResult.Fail("amount_required",
                "Topup request has no amountVnd — cannot create a payment link.");
        if (_tingee?.IsEnabled != true)
            return TopupPayLinkResult.Fail("tingee_disabled", "Tingee payment is not configured.");

        var link = await _tingee.CreatePaymentLinkAsync(
            request.ReferenceCode, request.AmountVnd.Value,
            $"Nạp phí nền tảng {request.ReferenceCode}", ct);
        if (link.CheckoutUrl == null)
            return TopupPayLinkResult.Fail(link.ErrorCode ?? "tingee_error",
                link.ErrorMessage ?? "Tingee API error");
        return new TopupPayLinkResult(link.CheckoutUrl, request.ReferenceCode,
            request.AmountVnd, 30);
    }

    // ---- P9: Tingee dynamic QR ----

    /// <summary>
    /// POST /v1/topups/{id}/qr — mint a FRESH one-time dynamic VietQR (VA transfer,
    /// no checkout redirect). billId is persisted on the request so the Payment
    /// Callback webhook can match the incoming bank transaction to this top-up.
    /// Every call overwrites QrBillId — only the LATEST QR's billId stays valid.
    /// </summary>
    public async Task<TopupQrResult> CreateQrAsync(
        Guid id, DateTime nowUtc, CancellationToken ct = default)
    {
        var request = await db.TopupRequests.FindAsync([id], ct);
        if (request == null)
            return TopupQrResult.Fail("not_found", "Topup request not found.");
        await LazyExpireAsync([request], nowUtc, ct);
        if (request.Status != TopupStatus.Pending)
            return TopupQrResult.Fail("topup_not_pending",
                $"Cannot create QR for a {StatusName(request.Status)} topup request.");
        if (request.AmountVnd is null or <= 0)
            return TopupQrResult.Fail("amount_required",
                "Topup request has no amountVnd — cannot create a QR.");
        if (_tingee?.IsQrEnabled != true)
            return TopupQrResult.Fail(_tingee?.IsEnabled == true ? "qr_disabled" : "tingee_disabled",
                "Tingee dynamic QR is not configured.");

        var qr = await _tingee.GenerateDynamicQrAsync(
            request.AmountVnd.Value, request.ReferenceCode, ct);
        if (qr.QrPayload == null || qr.BillId == null)
            return TopupQrResult.Fail(qr.ErrorCode ?? "tingee_error",
                qr.ErrorMessage ?? "Tingee QR error");

        request.AttachQrBill(qr.BillId);
        await db.SaveChangesAsync(ct);
        var expiresMin = Math.Max(1, (int)(request.ExpiresAtUtc - nowUtc).TotalMinutes);
        return new TopupQrResult(qr.QrPayload, qr.BillId, qr.QrAccount, expiresMin);
    }

    /// <summary>
    /// P8 — webhook success path: find by ReferenceCode (= Tingee orderId) → attach
    /// provider payment facts → auto-confirm credits = paidAmountVnd / VndPerCredit.
    /// Partial payments confirm the ACTUAL amount received (user decision).
    /// VndPerCredit = 0 → record payment only, keep Pending for manual admin confirm.
    /// Idempotent: Confirmed requests return Ok without re-granting.
    /// </summary>
    public async Task<TopupOpResult> ConfirmPaidByReferenceAsync(
        string referenceCode, decimal paidAmountVnd, string? providerTxnId,
        DateTime nowUtc, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(referenceCode))
            return TopupOpResult.Fail("not_found", "Topup request not found.");

        var probe = await db.TopupRequests.AsNoTracking()
            .Where(t => t.ReferenceCode == referenceCode)
            .Select(t => t.Id).FirstOrDefaultAsync(ct);
        if (probe == Guid.Empty)
            return TopupOpResult.Fail("not_found", "Topup request not found.");

        return await ConfirmPaidCoreAsync(probe, paidAmountVnd, providerTxnId, nowUtc, ct);
    }

    /// <summary>
    /// P9 — Payment Callback path: find by QrBillId (= Tingee dynamic-QR billId)
    /// → same attach + auto-confirm core as the P8 reference lookup.
    /// </summary>
    public async Task<TopupOpResult> ConfirmPaidByQrBillIdAsync(
        string billId, decimal paidAmountVnd, string? providerTxnId,
        DateTime nowUtc, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(billId))
            return TopupOpResult.Fail("not_found", "Topup request not found.");

        var probe = await db.TopupRequests.AsNoTracking()
            .Where(t => t.QrBillId == billId)
            .Select(t => t.Id).FirstOrDefaultAsync(ct);
        if (probe == Guid.Empty)
            return TopupOpResult.Fail("not_found", "Topup request not found.");

        return await ConfirmPaidCoreAsync(probe, paidAmountVnd, providerTxnId, nowUtc, ct);
    }

    /// <summary>Shared paid-confirm core — attach provider facts + grant by VndPerCredit.</summary>
    private async Task<TopupOpResult> ConfirmPaidCoreAsync(
        Guid requestId, decimal paidAmountVnd, string? providerTxnId,
        DateTime nowUtc, CancellationToken ct)
    {
        var request = await db.TopupRequests.FindAsync([requestId], ct);
        if (request == null) return TopupOpResult.Fail("not_found", "Topup request not found.");
        await LazyExpireAsync([request], nowUtc, ct);

        if (request.Status == TopupStatus.Confirmed)
            return TopupOpResult.Ok(request); // webhook replay — no double-grant
        if (request.Status != TopupStatus.Pending)
            return TopupOpResult.Fail("topup_not_pending",
                $"Cannot confirm a {StatusName(request.Status)} topup request.");

        request.AttachProviderPayment("tingee", providerTxnId, paidAmountVnd);

        var cfg = await db.TopupConfigs.AsNoTracking().FirstOrDefaultAsync(ct);
        var vndPerCredit = cfg?.VndPerCredit ?? 0;
        if (vndPerCredit <= 0)
        {
            // Auto-confirm OFF — payment recorded, Pending kept for manual confirm (P8 D7).
            await db.SaveChangesAsync(ct);
            return TopupOpResult.Ok(request);
        }

        // P11 — package top-up: must pay the FULL package price; credits come
        // from the catalog (not VndPerCredit). Partial stays Pending with the
        // payment fact recorded for manual handling.
        if (request.PackageCode != null && _packages != null)
        {
            var pkg = await _packages.GetByCodeAsync(request.PackageCode, activeOnly: false, ct);
            if (pkg != null)
            {
                if (paidAmountVnd < pkg.PriceVnd)
                {
                    await db.SaveChangesAsync(ct); // payment fact persisted, stays Pending
                    return TopupOpResult.Ok(request);
                }
                var pkgNote = $"tingee:{providerTxnId ?? request.ReferenceCode} package={pkg.Code} paid={paidAmountVnd}";
                return await ConfirmCoreAsync(request, pkg.Credits, "tingee:webhook", pkgNote, nowUtc, ct);
            }
        }

        var credits = paidAmountVnd / vndPerCredit;
        if (credits <= 0)
            return TopupOpResult.Fail("invalid_credits", "Computed credits <= 0.");
        var note = $"tingee:{providerTxnId ?? request.ReferenceCode} paid={paidAmountVnd}";
        return await ConfirmCoreAsync(request, credits, "tingee:webhook", note, nowUtc, ct);
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
                req.AccountNumber.Trim(), req.AccountName.Trim(), adminActor, nowUtc,
                req.VndPerCredit ?? 0,
                req.VaPollEnabled ?? true,
                req.VaPollIntervalMinutes is > 0 ? req.VaPollIntervalMinutes.Value : 2);
            db.TopupConfigs.Add(cfg);
        }
        else
        {
            cfg.Update(req.BankCode.Trim(), req.BankName.Trim(),
                req.AccountNumber.Trim(), req.AccountName.Trim(), adminActor, nowUtc,
                req.VndPerCredit ?? cfg.VndPerCredit,
                req.VaPollEnabled ?? cfg.VaPollEnabled,
                req.VaPollIntervalMinutes is > 0 ? req.VaPollIntervalMinutes.Value : cfg.VaPollIntervalMinutes);
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

    /// <summary>P13b — physical purge of stale top-up rows (lazy expiry only marks
    /// status; rows would otherwise accumulate forever). Deletes Expired/Rejected
    /// and stale Pending rows older than <paramref name="retentionDays"/>.
    /// Confirmed rows are NEVER deleted — they are the payment fact behind ledger
    /// grants (TopupRequestId references, provider transaction id, paid amount).</summary>
    public async Task<int> PurgeStaleAsync(DateTime nowUtc, int retentionDays = 90, CancellationToken ct = default)
    {
        var cutoff = nowUtc.AddDays(-retentionDays);
        var stale = await db.TopupRequests
            .Where(t => t.CreatedAtUtc < cutoff
                && t.Status != TopupStatus.Confirmed
                && (t.Status != TopupStatus.Pending || t.ExpiresAtUtc < nowUtc))
            .ToListAsync(ct);
        if (stale.Count == 0) return 0;
        db.TopupRequests.RemoveRange(stale);
        await db.SaveChangesAsync(ct);
        return stale.Count;
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
        t.PackageCode,
        t.Note, t.ConfirmedBy, t.ConfirmedAtUtc, t.CreatedAtUtc, t.ExpiresAtUtc);

    private static TopupConfigDto ToDto(TopupConfig c) => new(
        c.BankCode, c.BankName, c.AccountNumber, c.AccountName, c.UpdatedBy, c.UpdatedAtUtc,
        c.VndPerCredit, c.VaPollEnabled, c.VaPollIntervalMinutes);
}
