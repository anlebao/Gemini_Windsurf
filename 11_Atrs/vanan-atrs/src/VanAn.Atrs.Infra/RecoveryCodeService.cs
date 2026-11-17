using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using VanAn.Atrs.Contracts;
using VanAn.Atrs.Domain;

namespace VanAn.Atrs.Infra;

/// <summary>
/// Recovery codes (FR-008): generate → hiển thị 1 lần → lưu hash (BCrypt) →
/// verify single-use + rate limit/lockout → regenerate (step-up do caller enforce).
/// KHÔNG dùng Random (non-CSPRNG) — bắt buộc RandomNumberGenerator.
/// KHÔNG log code/hash plaintext.
/// </summary>
public interface IRecoveryCodeService
{
    /// <summary>Generate bộ codes mới (bộ cũ bị REVOKED). Plaintext trả về đúng 1 lần.</summary>
    Task<GenerateRecoveryCodesResult> GenerateSetAsync(Guid trustSubjectId, CancellationToken ct = default);

    /// <summary>Verify + consume 1 code. Rate limit + lockout progressive theo subject.</summary>
    Task<VerifyRecoveryCodeResult> VerifyAndConsumeAsync(Guid trustSubjectId, string code, Guid? deviceId, CancellationToken ct = default);

    /// <summary>Trạng thái cho UI (không trả code).</summary>
    Task<RecoveryCodesStatusDto> GetStatusAsync(Guid trustSubjectId, CancellationToken ct = default);
}

public sealed class RecoveryCodeOptions
{
    public int CodeCount { get; set; } = 10;
    public int AttemptLimit { get; set; } = 5;
    public int LockoutMinutes { get; set; } = 15;
    public int WorkFactor { get; set; } = 11;
}

public sealed class RecoveryCodeService(
    AtrsDbContext db,
    IMemoryCache cache,
    IOptions<RecoveryCodeOptions> options,
    IAtrsNotificationWriter notifications,
    AtrsMetrics metrics) : IRecoveryCodeService
{
    // Alphabet loại ký tự gây nhầm lẫn: 0/O, 1/I.
    private const string Alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
    private const int CodeGroups = 2;
    private const int GroupLength = 5;

    public async Task<GenerateRecoveryCodesResult> GenerateSetAsync(Guid trustSubjectId, CancellationToken ct = default)
    {
        var opt = options.Value;
        var setId = Guid.NewGuid();

        // Bộ ACTIVE cũ → REVOKED (regenerate invalidates).
        var activeSets = await db.RecoveryCodeSets
            .Where(s => s.TrustSubjectId == trustSubjectId && s.Status == RecoveryCodeSetStatus.ACTIVE)
            .ToListAsync(ct);
        foreach (var oldSet in activeSets)
            oldSet.Revoke();

        var set = new RecoveryCodeSet(setId, trustSubjectId);
        db.RecoveryCodeSets.Add(set);

        var codes = new List<string>(opt.CodeCount);
        foreach (var _ in Enumerable.Range(0, opt.CodeCount))
        {
            var plain = GenerateCode();
            codes.Add(plain);
            // Hash từ NORMALIZED string (bỏ '-'/space, uppercase) — VerifyAndConsume cũng normalize.
            db.RecoveryCodes.Add(new RecoveryCode(
                Guid.NewGuid(), setId, BCrypt.Net.BCrypt.HashPassword(Normalize(plain), opt.WorkFactor)));
        }

        await db.SaveChangesAsync(ct);
        ResetAttempts(trustSubjectId);
        metrics.Record("recovery_codes.regenerated");
        // KHÔNG chứa mã trong notification (rule ATRS-13).
        await notifications.NotifyAsync(trustSubjectId, "security.recovery_codes_regenerated",
            "Mã khôi phục mới đã được tạo",
            "Bộ mã khôi phục mới đã được tạo — mã cũ không còn hiệu lực. Lưu mã ở nơi an toàn.", ct: ct);
        return new GenerateRecoveryCodesResult(setId, codes, set.GeneratedAt);
    }

    public async Task<VerifyRecoveryCodeResult> VerifyAndConsumeAsync(Guid trustSubjectId, string code, Guid? deviceId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(code))
            return new VerifyRecoveryCodeResult(false, "InvalidCode");

        var opt = options.Value;
        if (IsLocked(trustSubjectId))
            return new VerifyRecoveryCodeResult(false, "Locked");

        var normalized = Normalize(code);
        var activeSetId = await db.RecoveryCodeSets
            .Where(s => s.TrustSubjectId == trustSubjectId && s.Status == RecoveryCodeSetStatus.ACTIVE)
            .Select(s => s.Id)
            .FirstOrDefaultAsync(ct);
        if (activeSetId == Guid.Empty)
            return new VerifyRecoveryCodeResult(false, "InvalidCode");

        var codes = await db.RecoveryCodes.Where(c => c.SetId == activeSetId).ToListAsync(ct);

        // Mọi fail dùng reason generic "InvalidCode" (anti-enumeration, FR-008/§9.3).
        foreach (var candidate in codes)
        {
            if (!BCrypt.Net.BCrypt.Verify(normalized, candidate.CodeHash))
                continue;

            if (candidate.UsedAt.HasValue)
            {
                RecordFailure(trustSubjectId, opt);
                return new VerifyRecoveryCodeResult(false, "InvalidCode");
            }

            candidate.MarkUsed(deviceId);
            await db.SaveChangesAsync(ct);
            ResetAttempts(trustSubjectId);
            return new VerifyRecoveryCodeResult(true, "Success");
        }

        RecordFailure(trustSubjectId, opt);
        return IsLocked(trustSubjectId)
            ? new VerifyRecoveryCodeResult(false, "Locked")
            : new VerifyRecoveryCodeResult(false, "InvalidCode");
    }

    public async Task<RecoveryCodesStatusDto> GetStatusAsync(Guid trustSubjectId, CancellationToken ct = default)
    {
        var activeSet = await db.RecoveryCodeSets
            .FirstOrDefaultAsync(s => s.TrustSubjectId == trustSubjectId && s.Status == RecoveryCodeSetStatus.ACTIVE, ct);
        if (activeSet is null)
            return new RecoveryCodesStatusDto(false, 0, null, null);

        var remaining = await db.RecoveryCodes.CountAsync(c => c.SetId == activeSet.Id && c.UsedAt == null, ct);
        return new RecoveryCodesStatusDto(true, remaining, activeSet.GeneratedAt, activeSet.RegeneratedAt);
    }

    private static string GenerateCode()
    {
        var random = System.Security.Cryptography.RandomNumberGenerator.GetBytes(CodeGroups * GroupLength);
        var chars = new char[CodeGroups * GroupLength];
        for (var i = 0; i < chars.Length; i++)
            chars[i] = Alphabet[random[i] % Alphabet.Length];
        // Grouping "XXXXX-XXXXX"
        return string.Join('-', Enumerable.Range(0, CodeGroups)
            .Select(g => new string(chars, g * GroupLength, GroupLength)));
    }

    private static string Normalize(string code)
        => code.Replace(" ", "", StringComparison.Ordinal).Replace("-", "", StringComparison.Ordinal).ToUpperInvariant();

    private bool IsLocked(Guid subjectId) => cache.TryGetValue(LockKey(subjectId), out _);

    private void RecordFailure(Guid subjectId, RecoveryCodeOptions opt)
    {
        var key = AttemptKey(subjectId);
        var attempts = cache.TryGetValue(key, out int value) ? value + 1 : 1;
        cache.Set(key, attempts, TimeSpan.FromMinutes(opt.LockoutMinutes));
        if (attempts >= opt.AttemptLimit)
            cache.Set(LockKey(subjectId), true, TimeSpan.FromMinutes(opt.LockoutMinutes));
    }

    private void ResetAttempts(Guid subjectId)
    {
        cache.Remove(AttemptKey(subjectId));
        cache.Remove(LockKey(subjectId));
    }

    private static string AttemptKey(Guid subjectId) => $"rc-attempt:{subjectId}";
    private static string LockKey(Guid subjectId) => $"rc-lock:{subjectId}";
}