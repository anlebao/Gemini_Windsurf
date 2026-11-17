using Microsoft.EntityFrameworkCore;
using VanAn.Atrs.Contracts;
using VanAn.Atrs.Domain;

namespace VanAn.Atrs.Infra;

/// <summary>Viết notification in-app (ATRS-13). Email channel = gap (P4 Brevo) — ghi card.</summary>
public interface IAtrsNotificationWriter
{
    Task NotifyAsync(Guid trustSubjectId, string eventType, string title, string body, string? link = null, CancellationToken ct = default);
}

/// <summary>Đọc notification của chính subject (ownership enforce ở service).</summary>
public interface IAtrsNotificationQueryService
{
    Task<NotificationPageDto> ListAsync(Guid trustSubjectId, int skip, int take, CancellationToken ct = default);
    Task<bool> MarkReadAsync(Guid notificationId, Guid trustSubjectId, CancellationToken ct = default);
}

public sealed class AtrsNotificationService(AtrsDbContext db) : IAtrsNotificationWriter, IAtrsNotificationQueryService
{
    public async Task NotifyAsync(Guid trustSubjectId, string eventType, string title, string body, string? link = null, CancellationToken ct = default)
    {
        // Best-effort: không throw — notification không được làm hỏng flow chính.
        try
        {
            db.AccountNotifications.Add(new AccountNotification(Guid.NewGuid(), trustSubjectId, eventType, title, body, link));
            await db.SaveChangesAsync(ct);
        }
        catch (Exception)
        {
            // swallow — best-effort (không log secret/body, chỉ eventType)
        }
    }

    public async Task<NotificationPageDto> ListAsync(Guid trustSubjectId, int skip, int take, CancellationToken ct = default)
    {
        var q = db.AccountNotifications.AsNoTracking()
            .Where(n => n.TrustSubjectId == trustSubjectId);
        var total = await q.CountAsync(ct);
        var unread = await q.CountAsync(n => n.ReadAt == null, ct);
        var items = await q.OrderByDescending(n => n.ReadAt == null).ThenByDescending(n => n.CreatedAt)
            .Skip(Math.Max(0, skip)).Take(Math.Clamp(take, 1, 100))
            .ToListAsync(ct);
        return new NotificationPageDto(
            items.Select(n => new AccountNotificationDto(n.Id, n.EventType, n.Title, n.Body, n.Link, n.CreatedAt, n.ReadAt != null)).ToList(),
            total, unread, Math.Max(0, skip), Math.Clamp(take, 1, 100));
    }

    public async Task<bool> MarkReadAsync(Guid notificationId, Guid trustSubjectId, CancellationToken ct = default)
    {
        var n = await db.AccountNotifications
            .FirstOrDefaultAsync(x => x.Id == notificationId && x.TrustSubjectId == trustSubjectId, ct);
        if (n is null)
            return false; // ownership: không lộ sự tồn tại của notification người khác
        n.MarkRead();
        await db.SaveChangesAsync(ct);
        return true;
    }
}