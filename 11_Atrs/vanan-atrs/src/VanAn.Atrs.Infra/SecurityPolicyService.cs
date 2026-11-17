using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using VanAn.Atrs.Contracts;
using VanAn.Atrs.Domain;

namespace VanAn.Atrs.Infra;

/// <summary>
/// ATRS-10 A21 — security policy versioned + approval (maker-checker).
/// Bảng security_policies tự là audit trail (proposer/approver/timestamps).
/// Value JSON; service parse an toàn + fallback mặc định (không crash nếu thiếu/hỏng).
/// </summary>
public interface ISecurityPolicyService
{
    /// <summary>Toàn bộ version (effective + proposed + rejected + superseded) — policy.manage.</summary>
    Task<IReadOnlyList<SecurityPolicyDto>> ListAsync(CancellationToken ct = default);
    /// <summary>Effective hiện tại của key (có cache ngắn).</summary>
    Task<SecurityPolicyDto?> GetEffectiveAsync(string policyKey, CancellationToken ct = default);
    /// <summary>Đọc số (double) từ effective policy — fallback khi thiếu/hỏng. Vd recovery.sla_hours.</summary>
    Task<double> GetEffectiveNumberAsync(string policyKey, string jsonField, double fallback, CancellationToken ct = default);
    /// <summary>Đọc bool từ effective policy — fallback khi thiếu/hỏng. Vd dispute.maker_checker_required.</summary>
    Task<bool> GetEffectiveBoolAsync(string policyKey, string jsonField, bool fallback, CancellationToken ct = default);
    /// <summary>Propose thay đổi (policy.manage) — version+1, PROPOSED, chờ approve.</summary>
    Task<SecurityPolicyDto> ProposeAsync(string policyKey, string policyGroup, string jsonValue, Guid proposerId, string? changeNote, CancellationToken ct = default);
    /// <summary>Approve (policy.approve) — maker-checker: approver ≠ proposer; version cũ → SUPERSEDED.</summary>
    Task<SecurityPolicyDto> ApproveAsync(Guid policyId, Guid approverId, CancellationToken ct = default);
    /// <summary>Reject (policy.approve) — giữ row REJECTED cho audit.</summary>
    Task<SecurityPolicyDto> RejectAsync(Guid policyId, Guid approverId, CancellationToken ct = default);
}

public sealed class SecurityPolicyService(
    AtrsDbContext db,
    IMemoryCache cache,
    TimeProvider timeProvider) : ISecurityPolicyService
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(30);

    public async Task<IReadOnlyList<SecurityPolicyDto>> ListAsync(CancellationToken ct = default)
    {
        var rows = await db.SecurityPolicies.AsNoTracking()
            .OrderBy(p => p.PolicyKey).ThenByDescending(p => p.Version)
            .ToListAsync(ct);
        return rows.Select(ToDto).ToList();
    }

    public async Task<SecurityPolicyDto?> GetEffectiveAsync(string policyKey, CancellationToken ct = default)
    {
        var cacheKey = $"atrs-policy:{policyKey}";
        if (cache.TryGetValue(cacheKey, out SecurityPolicyDto? cached) && cached is not null)
            return cached;
        var row = await db.SecurityPolicies.AsNoTracking()
            .Where(p => p.PolicyKey == policyKey && p.Status == SecurityPolicyStatus.EFFECTIVE)
            .OrderByDescending(p => p.Version)
            .FirstOrDefaultAsync(ct);
        if (row is null)
            return null;
        var dto = ToDto(row);
        cache.Set(cacheKey, dto, timeProvider.GetUtcNow().UtcDateTime.Add(CacheTtl));
        return dto;
    }

    public async Task<double> GetEffectiveNumberAsync(string policyKey, string jsonField, double fallback, CancellationToken ct = default)
    {
        var policy = await GetEffectiveAsync(policyKey, ct);
        if (policy is null)
            return fallback;
        try
        {
            using var doc = JsonDocument.Parse(policy.JsonValue);
            if (doc.RootElement.TryGetProperty(jsonField, out var el) && el.TryGetDouble(out var value))
                return value;
        }
        catch (JsonException) { /* fallback */ }
        return fallback;
    }

    public async Task<bool> GetEffectiveBoolAsync(string policyKey, string jsonField, bool fallback, CancellationToken ct = default)
    {
        var policy = await GetEffectiveAsync(policyKey, ct);
        if (policy is null)
            return fallback;
        try
        {
            using var doc = JsonDocument.Parse(policy.JsonValue);
            if (doc.RootElement.TryGetProperty(jsonField, out var el)
                && el.ValueKind is JsonValueKind.True or JsonValueKind.False)
                return el.GetBoolean();
        }
        catch (JsonException) { /* fallback */ }
        return fallback;
    }

    public async Task<SecurityPolicyDto> ProposeAsync(string policyKey, string policyGroup, string jsonValue, Guid proposerId, string? changeNote, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(policyKey) || string.IsNullOrWhiteSpace(policyGroup) || string.IsNullOrWhiteSpace(jsonValue))
            throw new ArgumentException("policyKey/policyGroup/jsonValue là bắt buộc.");
        // Validate JSON trước khi lưu — JsonException → ArgumentException (controller trả 400, không 500).
        try
        {
            using (JsonDocument.Parse(jsonValue)) { }
        }
        catch (JsonException ex)
        {
            throw new ArgumentException("JsonValue không phải JSON hợp lệ.", ex);
        }

        // Đã có PROPOSED pending cho cùng key → chặn (một proposal mỗi lần).
        var pending = await db.SecurityPolicies.AnyAsync(
            p => p.PolicyKey == policyKey && p.Status == SecurityPolicyStatus.PROPOSED, ct);
        if (pending)
            throw new InvalidOperationException($"Đã có proposal đang chờ cho {policyKey} — approve/reject trước.");

        var currentVersion = await db.SecurityPolicies.AsNoTracking()
            .Where(p => p.PolicyKey == policyKey)
            .OrderByDescending(p => p.Version)
            .Select(p => (int?)p.Version)
            .FirstOrDefaultAsync(ct);
        var version = (currentVersion ?? 0) + 1;

        var policy = new SecurityPolicy(Guid.NewGuid(), policyKey, policyGroup, jsonValue, version, proposerId, changeNote ?? "");
        db.SecurityPolicies.Add(policy);
        await db.SaveChangesAsync(ct);
        cache.Remove($"atrs-policy:{policyKey}");
        return ToDto(policy);
    }

    public async Task<SecurityPolicyDto> ApproveAsync(Guid policyId, Guid approverId, CancellationToken ct = default)
    {
        var policy = await db.SecurityPolicies.FirstOrDefaultAsync(p => p.Id == policyId, ct)
            ?? throw new InvalidOperationException($"SecurityPolicy {policyId} not found.");
        if (policy.Status != SecurityPolicyStatus.PROPOSED)
            throw new InvalidOperationException($"Cannot approve policy in status {policy.Status}.");
        if (policy.ProposedBy == approverId)
            throw new InvalidOperationException("Approver cannot be the proposer (maker-checker).");

        // Supersede version effective hiện tại (nếu có).
        var current = await db.SecurityPolicies
            .Where(p => p.PolicyKey == policy.PolicyKey && p.Status == SecurityPolicyStatus.EFFECTIVE)
            .ToListAsync(ct);
        foreach (var row in current)
            row.Supersede();

        policy.Approve(approverId);
        await db.SaveChangesAsync(ct);
        cache.Remove($"atrs-policy:{policy.PolicyKey}");
        return ToDto(policy);
    }

    public async Task<SecurityPolicyDto> RejectAsync(Guid policyId, Guid approverId, CancellationToken ct = default)
    {
        var policy = await db.SecurityPolicies.FirstOrDefaultAsync(p => p.Id == policyId, ct)
            ?? throw new InvalidOperationException($"SecurityPolicy {policyId} not found.");
        if (policy.Status != SecurityPolicyStatus.PROPOSED)
            throw new InvalidOperationException($"Cannot reject policy in status {policy.Status}.");
        if (policy.ProposedBy == approverId)
            throw new InvalidOperationException("Approver cannot be the proposer (maker-checker).");
        policy.Reject(approverId);
        await db.SaveChangesAsync(ct);
        cache.Remove($"atrs-policy:{policy.PolicyKey}");
        return ToDto(policy);
    }

    private static SecurityPolicyDto ToDto(SecurityPolicy p)
        => new(p.Id, p.PolicyKey, p.PolicyGroup, p.JsonValue, p.Version, p.Status.ToString(),
            p.ProposedBy, p.ApprovedBy, p.ProposedAt, p.EffectiveAt, p.RejectedAt, p.ChangeNote);
}