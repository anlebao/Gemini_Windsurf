using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using VanAn.CoreHub.Infrastructure;
using VanAn.Shared.Domain;
using VanAn.Shared.Domain.Aggregates.SystemSettingAggregate;
using VanAn.Shared.Domain.Common;

namespace VanAn.CoreHub.Services.Onboarding;

// ── Platform Onboarding Flags (2026-11-09, SRS FR-22) ────────────────────────
// SysAdmin toggle on/off các entry-point self-serve onboarding — lưu
// SystemSetting global keys (pattern CommerceModeService), runtime không restart.
// Default TRUE khi key chưa tồn tại (kênh mở sẵn — kill-switch chủ động).

/// <summary>4 toggles độc lập — mỗi cái tắt không ảnh hưởng cái khác.</summary>
public record OnboardingFlagsDto(
    bool SelfServeEnabled,     // master: submit endpoint 503 + /onboard trang đóng
    bool DirectoryCta,         // nút CTA trên timlathay.com (Directory đọc public)
    bool QrEntry,              // card QR generator trên ShopERP admin
    bool DeeplinkPrefill);     // /onboard honor query params prefill

/// <summary>PUT body — full state (không partial).</summary>
public record UpdateOnboardingFlagsRequest(
    bool SelfServeEnabled,
    bool DirectoryCta,
    bool QrEntry,
    bool DeeplinkPrefill);

public interface IPlatformOnboardingFlagsService
{
    /// <summary>Đọc 4 flags (cache ~30s). Default true khi key chưa có.</summary>
    Task<OnboardingFlagsDto> GetFlagsAsync(CancellationToken ct = default);

    /// <summary>Ghi cả 4 flags (upsert SystemSetting, UpdatedBy=sysadmin).</summary>
    Task SetFlagsAsync(UpdateOnboardingFlagsRequest flags, Guid updatedBy, CancellationToken ct = default);

    /// <summary>Convenience cho submit path — master switch.</summary>
    Task<bool> IsSelfServeEnabledAsync(CancellationToken ct = default);
}

public class PlatformOnboardingFlagsService(
    IVanAnDbContext dbContext,
    ILogger<PlatformOnboardingFlagsService> logger) : IPlatformOnboardingFlagsService
{
    internal const string KeySelfServe = "Onboarding.SelfServeEnabled";
    internal const string KeyDirectoryCta = "Onboarding.DirectoryCtaEnabled";
    internal const string KeyQrEntry = "Onboarding.QrEntryEnabled";
    internal const string KeyDeeplinkPrefill = "Onboarding.DeeplinkPrefillEnabled";

    private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(30);
    private static readonly TenantId GlobalTenant = new(Guid.Empty);

    private readonly SemaphoreSlim _cacheLock = new(1, 1);
    private OnboardingFlagsDto? _cached;
    private DateTime _cachedAt = DateTime.MinValue;

    public async Task<OnboardingFlagsDto> GetFlagsAsync(CancellationToken ct = default)
    {
        if (_cached is not null && DateTime.UtcNow - _cachedAt < CacheTtl)
            return _cached;

        await _cacheLock.WaitAsync(ct);
        try
        {
            if (_cached is not null && DateTime.UtcNow - _cachedAt < CacheTtl)
                return _cached;

            var keys = new[] { KeySelfServe, KeyDirectoryCta, KeyQrEntry, KeyDeeplinkPrefill };
            var rows = await dbContext.SystemSettings
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(s => keys.Contains(s.Key))
                .Select(s => new { s.Key, s.Value })
                .ToListAsync(ct);

            var map = rows.ToDictionary(r => r.Key, r => r.Value, StringComparer.Ordinal);
            _cached = new OnboardingFlagsDto(
                SelfServeEnabled: ParseBool(map.GetValueOrDefault(KeySelfServe)),
                DirectoryCta: ParseBool(map.GetValueOrDefault(KeyDirectoryCta)),
                QrEntry: ParseBool(map.GetValueOrDefault(KeyQrEntry)),
                DeeplinkPrefill: ParseBool(map.GetValueOrDefault(KeyDeeplinkPrefill)));
            _cachedAt = DateTime.UtcNow;
            return _cached;
        }
        finally
        {
            _cacheLock.Release();
        }
    }

    public async Task SetFlagsAsync(UpdateOnboardingFlagsRequest flags, Guid updatedBy, CancellationToken ct = default)
    {
        var dto = new OnboardingFlagsDto(flags.SelfServeEnabled, flags.DirectoryCta, flags.QrEntry, flags.DeeplinkPrefill);
        await UpsertAsync(KeySelfServe, dto.SelfServeEnabled.ToString().ToLowerInvariant(), updatedBy, ct);
        await UpsertAsync(KeyDirectoryCta, dto.DirectoryCta.ToString().ToLowerInvariant(), updatedBy, ct);
        await UpsertAsync(KeyQrEntry, dto.QrEntry.ToString().ToLowerInvariant(), updatedBy, ct);
        await UpsertAsync(KeyDeeplinkPrefill, dto.DeeplinkPrefill.ToString().ToLowerInvariant(), updatedBy, ct);
        await dbContext.SaveChangesAsync(ct);

        // Invalidate cache ngay — admin toggle phải có hiệu lực tức thì phía đọc
        _cached = null;
        _cachedAt = DateTime.MinValue;

        logger.LogInformation(
            "Onboarding flags updated by {UpdatedBy}: selfServe={SS} directoryCta={DC} qrEntry={QR} deeplink={DL}",
            updatedBy, dto.SelfServeEnabled, dto.DirectoryCta, dto.QrEntry, dto.DeeplinkPrefill);
    }

    public async Task<bool> IsSelfServeEnabledAsync(CancellationToken ct = default)
        => (await GetFlagsAsync(ct)).SelfServeEnabled;

    private async Task UpsertAsync(string key, string value, Guid updatedBy, CancellationToken ct)
    {
        var setting = await dbContext.SystemSettings
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(s => s.Key == key, ct);

        if (setting is null)
        {
            dbContext.SystemSettings.Add(new SystemSetting(GlobalTenant, key, value, updatedBy));
        }
        else
        {
            setting.Update(value, updatedBy);
        }
    }

    /// <summary>Default TRUE khi key thiếu hoặc giá trị không parse được — kênh mở sẵn.</summary>
    private static bool ParseBool(string? value)
        => value is null || !bool.TryParse(value, out var b) || b;
}
