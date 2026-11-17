using Microsoft.EntityFrameworkCore;
using VanAn.Atrs.Contracts;
using VanAn.Atrs.Domain;

namespace VanAn.Atrs.Infra;

/// <summary>
/// Device registry (SRS §7.2, FR-003) — list/rename/revoke/mark-lost. U07/U08 backend.
/// DeviceId = server-issued risk signal, KHÔNG phải proof-of-identity badge.
/// Revoke device → revoke toàn bộ credential của device (ATRS-3 step-up gate do caller enforce).
/// </summary>
public interface IDeviceService
{
    Task<IReadOnlyList<UserDeviceSummaryDto>> ListAsync(Guid trustSubjectId, CancellationToken ct = default);
    Task<UserDeviceDetailDto?> GetAsync(Guid trustSubjectId, Guid deviceId, CancellationToken ct = default);
    Task<UserDeviceSummaryDto?> RenameAsync(Guid trustSubjectId, Guid deviceId, string friendlyName, CancellationToken ct = default);
    Task<bool> RevokeAsync(Guid trustSubjectId, Guid deviceId, string? reasonCode, CancellationToken ct = default);
    Task<bool> MarkLostAsync(Guid trustSubjectId, Guid deviceId, CancellationToken ct = default);
    Task<Guid> EnsureDeviceAsync(Guid trustSubjectId, string deviceId, RegistrationMethod method,
        DeviceType deviceType, string? platform = null, string? friendlyName = null, CancellationToken ct = default);
}

public sealed class DeviceService(AtrsDbContext db, IAtrsNotificationWriter notifications, AtrsMetrics metrics) : IDeviceService
{
    public async Task<IReadOnlyList<UserDeviceSummaryDto>> ListAsync(Guid trustSubjectId, CancellationToken ct = default)
    {
        var devices = await db.UserDevices
            .AsNoTracking()
            .Where(d => d.TrustSubjectId == trustSubjectId)
            .OrderByDescending(d => d.LastSeenAt)
            .ToListAsync(ct);
        var credentialCounts = await db.DeviceCredentials
            .AsNoTracking()
            .Where(c => c.TrustSubjectId == trustSubjectId && c.Status == CredentialStatus.ACTIVE
                && c.DeviceRecordId != null)
            .GroupBy(c => c.DeviceRecordId)
            .Select(g => new { DeviceId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.DeviceId!.Value, x => x.Count, ct);
        return devices.Select(d => ToSummary(d, credentialCounts.GetValueOrDefault(d.Id))).ToList();
    }

    public async Task<UserDeviceDetailDto?> GetAsync(Guid trustSubjectId, Guid deviceId, CancellationToken ct = default)
    {
        var device = await db.UserDevices
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == deviceId && d.TrustSubjectId == trustSubjectId, ct);
        if (device is null)
            return null;

        var creds = await db.DeviceCredentials
            .AsNoTracking()
            .Where(c => c.DeviceRecordId == deviceId)
            .OrderByDescending(c => c.CreatedAt)
            .ToListAsync(ct);
        return new UserDeviceDetailDto(
            ToSummary(device, creds.Count(c => c.Status == CredentialStatus.ACTIVE)),
            creds.Select(ToCredentialSummary).ToList());
    }

    public async Task<UserDeviceSummaryDto?> RenameAsync(Guid trustSubjectId, Guid deviceId, string friendlyName, CancellationToken ct = default)
    {
        var device = await db.UserDevices
            .FirstOrDefaultAsync(d => d.Id == deviceId && d.TrustSubjectId == trustSubjectId, ct);
        if (device is null)
            return null;
        device.Rename(friendlyName);
        await db.SaveChangesAsync(ct);
        var count = await db.DeviceCredentials.CountAsync(
            c => c.DeviceRecordId == deviceId && c.Status == CredentialStatus.ACTIVE, ct);
        return ToSummary(device, count);
    }

    public async Task<bool> RevokeAsync(Guid trustSubjectId, Guid deviceId, string? reasonCode, CancellationToken ct = default)
    {
        var device = await db.UserDevices
            .FirstOrDefaultAsync(d => d.Id == deviceId && d.TrustSubjectId == trustSubjectId, ct);
        if (device is null)
            return false;
        device.Revoke(reasonCode);

        var creds = await db.DeviceCredentials
            .Where(c => c.DeviceRecordId == deviceId && c.Status == CredentialStatus.ACTIVE)
            .ToListAsync(ct);
        foreach (var c in creds)
            c.Revoke();
        await db.SaveChangesAsync(ct);
        metrics.Record("device.revoked");
        await notifications.NotifyAsync(trustSubjectId, "device.revoked",
            "Thiết bị đã bị thu hồi",
            $"Thiết bị {(string.IsNullOrWhiteSpace(device.FriendlyName) ? device.DeviceId[..Math.Min(8, device.DeviceId.Length)] : device.FriendlyName)} đã bị thu hồi khỏi tài khoản của bạn.", ct: ct);
        return true;
    }

    public async Task<bool> MarkLostAsync(Guid trustSubjectId, Guid deviceId, CancellationToken ct = default)
    {
        var device = await db.UserDevices
            .FirstOrDefaultAsync(d => d.Id == deviceId && d.TrustSubjectId == trustSubjectId, ct);
        if (device is null)
            return false;
        device.MarkLost();
        await db.SaveChangesAsync(ct);
        metrics.Record("device.lost");
        await notifications.NotifyAsync(trustSubjectId, "device.lost",
            "Thiết bị bị đánh dấu thất lạc",
            $"Thiết bị {(string.IsNullOrWhiteSpace(device.FriendlyName) ? device.DeviceId[..Math.Min(8, device.DeviceId.Length)] : device.FriendlyName)} đã được đánh dấu thất lạc.", ct: ct);
        return true;
    }

    public async Task<Guid> EnsureDeviceAsync(Guid trustSubjectId, string deviceId, RegistrationMethod method,
        DeviceType deviceType, string? platform = null, string? friendlyName = null, CancellationToken ct = default)
    {
        var existing = await db.UserDevices
            .FirstOrDefaultAsync(d => d.TrustSubjectId == trustSubjectId && d.DeviceId == deviceId, ct);
        if (existing is not null)
        {
            existing.Touch(platform);
            await db.SaveChangesAsync(ct);
            return existing.Id;
        }

        var device = new UserDevice(Guid.NewGuid(), trustSubjectId, deviceId, method, deviceType, platform, friendlyName);
        db.UserDevices.Add(device);
        await db.SaveChangesAsync(ct);
        metrics.Record("device.registered");
        await notifications.NotifyAsync(trustSubjectId, "device.registered",
            "Thiết bị mới đã đăng ký",
            "Một thiết bị mới đã được liên kết với tài khoản của bạn.", ct: ct);
        return device.Id;
    }

    private static UserDeviceSummaryDto ToSummary(UserDevice d, int activeCredentialCount)
        => new(
            d.Id,
            d.DeviceId,
            d.DeviceType.ToString(),
            d.Platform,
            d.FriendlyName,
            d.RegistrationMethod.ToString(),
            d.Status.ToString(),
            d.FirstSeenAt,
            d.LastSeenAt,
            d.RevokedAt,
            activeCredentialCount);

    private static DeviceCredentialSummaryDto ToCredentialSummary(DeviceCredential c)
        => new(
            c.Id,
            c.CredentialId,
            c.RpId,
            c.TransportsJson is null ? null : System.Text.Json.JsonSerializer.Deserialize<string[]>(c.TransportsJson),
            c.BackupEligible,
            c.BackupState,
            c.Status.ToString(),
            c.CreatedAt,
            c.LastUsedAt);
}