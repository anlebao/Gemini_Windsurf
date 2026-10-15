using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using VanAn.CoreHub.Infrastructure;
using VanAn.CoreHub.Services;
using VanAn.CoreHub.Tests.TestInfrastructure;
using VanAn.Shared.Domain;
using Xunit;

namespace VanAn.Core.Tests.Services;

/// <summary>
/// Notification Hub (2026-10-15): NotificationCenterService — owner (TenantNotification)
/// + khách (PushNotificationDelivery inbox) + idempotency + read state.
/// </summary>
public class NotificationCenterServiceTests
{
    private static (VanAnDbContext db, NotificationCenterService svc) Create()
    {
        VanAnDbContext db = VanAnDbContextTestFactory.Create().Context;
        return (db, new NotificationCenterService(db, NullLogger<NotificationCenterService>.Instance));
    }

    // ── Owner ──────────────────────────────────────────────────────────────

    [Fact(DisplayName = "NC-1: CreateForOwner → unread count + list + mark read")]
    public async Task Owner_CreateListMarkRead_Works()
    {
        var (db, svc) = Create();
        var tenantId = new TenantId(Guid.NewGuid());

        await svc.CreateForOwnerAsync(tenantId, NotificationCategory.OrderStatus, "Đơn mới", "Khách đặt đơn #1", "/orders/1", "Order", Guid.NewGuid());
        await svc.CreateForOwnerAsync(tenantId, NotificationCategory.System, "Cảnh báo ca", "STOCK_LOW", "/inventory/alert-center", "ShiftAlert", Guid.NewGuid());

        Assert.Equal(2, await svc.UnreadCountForOwnerAsync(tenantId));

        var list = await svc.ListForOwnerAsync(tenantId);
        Assert.Equal(2, list.Count);
        Assert.Equal(NotificationCategory.System, list[0].Category); // mới nhất trước

        var filtered = await svc.ListForOwnerAsync(tenantId, NotificationCategory.OrderStatus);
        Assert.Single(filtered);

        await svc.MarkOwnerReadAsync(list[0].Id, tenantId);
        Assert.Equal(1, await svc.UnreadCountForOwnerAsync(tenantId));

        await svc.MarkAllOwnerReadAsync(tenantId);
        Assert.Equal(0, await svc.UnreadCountForOwnerAsync(tenantId));
    }

    [Fact(DisplayName = "NC-2: CreateForOwner idempotent theo ReferenceType+ReferenceId")]
    public async Task Owner_CreateDuplicateReference_Idempotent()
    {
        var (db, svc) = Create();
        var tenantId = new TenantId(Guid.NewGuid());
        var refId = Guid.NewGuid();

        await svc.CreateForOwnerAsync(tenantId, NotificationCategory.Booking, "Đặt lịch", "Booking A", "/booking/queue", "Booking", refId);
        await svc.CreateForOwnerAsync(tenantId, NotificationCategory.Booking, "Đặt lịch", "Booking A (lặp)", "/booking/queue", "Booking", refId);

        Assert.Equal(1, await svc.UnreadCountForOwnerAsync(tenantId));
    }

    [Fact(DisplayName = "NC-3: MarkOwnerRead sai tenant → không đổi")]
    public async Task Owner_MarkRead_WrongTenant_Noop()
    {
        var (db, svc) = Create();
        var tenantId = new TenantId(Guid.NewGuid());
        await svc.CreateForOwnerAsync(tenantId, NotificationCategory.Security, "Claim", "Claim mới", "/admin/claims", "TenantClaim", Guid.NewGuid());

        var item = (await svc.ListForOwnerAsync(tenantId)).Single();
        await svc.MarkOwnerReadAsync(item.Id, new TenantId(Guid.NewGuid())); // tenant khác

        Assert.Equal(1, await svc.UnreadCountForOwnerAsync(tenantId));
    }

    // ── Khách ──────────────────────────────────────────────────────────────

    [Fact(DisplayName = "NC-4: CreateForCustomer → unread + list (dedupe theo NotificationId) + mark read")]
    public async Task Customer_CreateListMarkRead_Works()
    {
        var (db, svc) = Create();
        var customerId = Guid.NewGuid();

        await svc.CreateForCustomerAsync(customerId, NotificationCategory.Loyalty, "Điểm thưởng", "+100 điểm", "/my-loyalty", "Loyalty", Guid.NewGuid());
        await svc.CreateForCustomerAsync(customerId, NotificationCategory.Promotion, "Khuyến mãi", "Giảm 20%", "/campaigns", "Campaign", Guid.NewGuid());

        Assert.Equal(2, await svc.UnreadCountForCustomerAsync(customerId));

        var list = await svc.ListForCustomerAsync(customerId);
        Assert.Equal(2, list.Count);

        await svc.MarkCustomerReadAsync(list[0].NotificationId, customerId);
        Assert.Equal(1, await svc.UnreadCountForCustomerAsync(customerId));

        await svc.MarkAllCustomerReadAsync(customerId);
        Assert.Equal(0, await svc.UnreadCountForCustomerAsync(customerId));
    }

    [Fact(DisplayName = "NC-5: ListForCustomer dedupe — nhiều delivery cùng NotificationId → 1 item")]
    public async Task Customer_List_DedupesByNotificationId()
    {
        var (db, svc) = Create();
        var customerId = Guid.NewGuid();

        // 2 delivery cùng NotificationId (giả lập nhiều subscription khi web push)
        db.PushNotificationDeliveries.Add(new PushNotificationDelivery(
            new TenantId(Guid.Empty), customerId, null, "/order-tracking/1",
            NotificationCategory.OrderStatus, "Đơn hàng", "Hoàn thành", "Order", Guid.NewGuid()));
        db.PushNotificationDeliveries.Add(new PushNotificationDelivery(
            new TenantId(Guid.Empty), customerId, null, "/order-tracking/1",
            NotificationCategory.OrderStatus, "Đơn hàng", "Hoàn thành", "Order", Guid.NewGuid()));
        await db.SaveChangesAsync();

        // Gộp NotificationId giống nhau (như PushNotificationService tạo per-subscription)
        await db.Database.ExecuteSqlRawAsync(
            "UPDATE \"PushNotificationDeliveries\" SET \"NotificationId\" = {0}", Guid.NewGuid());

        var list = await svc.ListForCustomerAsync(customerId);
        Assert.Single(list);
        Assert.Equal(1, await svc.UnreadCountForCustomerAsync(customerId));
    }

    // ── Domain guards ──────────────────────────────────────────────────────

    [Fact(DisplayName = "NC-6: TenantNotification guard — title/body rỗng throw")]
    public void Domain_TenantNotification_EmptyTitle_Throws()
    {
        Assert.Throws<ArgumentException>(() => new TenantNotification(
            new TenantId(Guid.NewGuid()), NotificationCategory.System, "", "body"));
        Assert.Throws<ArgumentException>(() => new TenantNotification(
            new TenantId(Guid.NewGuid()), NotificationCategory.System, "title", " "));
    }

    [Fact(DisplayName = "NC-7: MarkAsRead idempotent (owner + customer)")]
    public void Domain_MarkAsRead_Idempotent()
    {
        var n = new TenantNotification(new TenantId(Guid.NewGuid()), NotificationCategory.System, "t", "b");
        n.MarkAsRead();
        DateTime? readAt = n.ReadAt;
        n.MarkAsRead(); // lần 2 không đổi
        Assert.True(n.IsRead);
        Assert.Equal(readAt, n.ReadAt);
    }
}
