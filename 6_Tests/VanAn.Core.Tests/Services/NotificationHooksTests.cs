using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System.Text.Json;
using VanAn.CoreHub.Domain.Repositories;
using VanAn.CoreHub.Infrastructure;
using VanAn.CoreHub.Services;
using VanAn.CoreHub.Tests.TestInfrastructure;
using VanAn.Shared.Domain;
using Xunit;

namespace VanAn.Core.Tests.Services;

/// <summary>
/// Notification Hub (2026-10-15): hooks fail-safe — chat message từ khách → thông báo owner,
/// order status event → thông báo owner. Không làm hỏng luồng nghiệp vụ khi notification lỗi.
/// </summary>
public class NotificationHooksTests
{
    private static IConfiguration PushConfig() => new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["PushNotifications:VapidPrivateKey"] = "test-vapid-private-key",
            ["PushNotifications:VapidPublicKey"] = "test-vapid-public-key",
            ["PushNotifications:VapidSubject"] = "mailto:test@vanan.com"
        })
        .Build();

    private static IConfiguration NatsConfig() => new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?> { ["Nats:Url"] = "nats://localhost:4222" })
        .Build();

    [Fact(DisplayName = "HOOK-1: chat message từ KHÁCH (Shop subject) → owner notification Chat")]
    public async Task ChatHook_CustomerMessage_CreatesOwnerNotification()
    {
        using TestContextScope scope = VanAnDbContextTestFactory.Create();
        VanAnDbContext db = scope.Context;
        var tenantId = new TenantId(Guid.NewGuid());
        var customerId = Guid.NewGuid();

        var notificationCenter = new NotificationCenterService(db, NullLogger<NotificationCenterService>.Instance);
        var messaging = new RealtimeMessagingService(db, NullLogger<RealtimeMessagingService>.Instance, notificationCenter);

        var conversation = await messaging.EnsureConversationAsync(
            tenantId, RealtimeSubjectType.Shop, tenantId.Value, customerId, tenantId.Value,
            RealtimeParticipantRole.Customer, RealtimeParticipantRole.Shop);

        await messaging.SendMessageAsync(conversation!.Id, customerId, "Chào shop, mình muốn hỏi về đơn hàng");

        var notifications = await db.TenantNotifications.IgnoreQueryFilters().ToListAsync();
        var chat = Assert.Single(notifications);
        Assert.Equal(NotificationCategory.Chat, chat.Category);
        Assert.Equal(tenantId, chat.TenantId);
        Assert.Contains("Chào shop", chat.Body);
    }

    [Fact(DisplayName = "HOOK-2: chat message từ SHOP → KHÔNG tạo thông báo owner")]
    public async Task ChatHook_ShopMessage_NoOwnerNotification()
    {
        using TestContextScope scope = VanAnDbContextTestFactory.Create();
        VanAnDbContext db = scope.Context;
        var tenantId = new TenantId(Guid.NewGuid());
        var shopStaffId = Guid.NewGuid();

        var notificationCenter = new NotificationCenterService(db, NullLogger<NotificationCenterService>.Instance);
        var messaging = new RealtimeMessagingService(db, NullLogger<RealtimeMessagingService>.Instance, notificationCenter);

        var conversation = await messaging.EnsureConversationAsync(
            tenantId, RealtimeSubjectType.Shop, tenantId.Value, shopStaffId, tenantId.Value,
            RealtimeParticipantRole.Shop, RealtimeParticipantRole.Customer);

        await messaging.SendMessageAsync(conversation!.Id, shopStaffId, "Shop trả lời khách");

        Assert.Equal(0, await db.TenantNotifications.IgnoreQueryFilters().CountAsync());
    }

    [Fact(DisplayName = "HOOK-3: order.status.changed (completed) → owner notification OrderStatus")]
    public async Task OrderEvent_CreatesOwnerNotification()
    {
        using TestContextScope scope = VanAnDbContextTestFactory.Create();
        VanAnDbContext db = scope.Context;
        var tenantId = new TenantId(Guid.NewGuid());
        var orderId = Guid.NewGuid();

        var pushMock = new Mock<PushNotificationService>(
            PushConfig(), NullLogger<PushNotificationService>.Instance,
            Mock.Of<IPushSubscriptionRepository>(), null, null);
        pushMock.Setup(p => p.SendOrderStatusNotificationAsync(
                It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string?>()))
            .ReturnsAsync(1);

        var services = new ServiceCollection()
            .AddSingleton(pushMock.Object)
            .AddSingleton<INotificationCenterService>(new NotificationCenterService(db, NullLogger<NotificationCenterService>.Instance))
            .BuildServiceProvider();

        var sut = new PushNotificationBackgroundService(services, NatsConfig(), NullLogger<PushNotificationBackgroundService>.Instance);

        await sut.HandleEventAsync(JsonSerializer.SerializeToUtf8Bytes(new
        {
            orderId,
            tenantId = tenantId.Value,
            customerId = Guid.NewGuid(),
            newStatus = "completed"
        }), CancellationToken.None);

        var notifications = await db.TenantNotifications.IgnoreQueryFilters().ToListAsync();
        var n = Assert.Single(notifications);
        Assert.Equal(NotificationCategory.OrderStatus, n.Category);
        Assert.Equal(tenantId, n.TenantId);
        Assert.Equal(orderId, n.ReferenceId);
    }

    [Fact(DisplayName = "HOOK-4: hook lỗi (notification fail) KHÔNG làm hỏng luồng — fail-safe")]
    public async Task Hook_NotificationFails_DoesNotBreakFlow()
    {
        using TestContextScope scope = VanAnDbContextTestFactory.Create();
        VanAnDbContext db = scope.Context;
        var tenantId = new TenantId(Guid.NewGuid());

        // NotificationCenterService ném — hook phải catch + không rethrow (không làm hỏng gửi tin).
        var throwingCenter = new Mock<INotificationCenterService>();
        throwingCenter.Setup(c => c.CreateForOwnerAsync(
                It.IsAny<TenantId>(), It.IsAny<NotificationCategory>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<Guid?>(), It.IsAny<TenantNotificationSeverity>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("notification db down"));

        var messaging = new RealtimeMessagingService(db, NullLogger<RealtimeMessagingService>.Instance, throwingCenter.Object);
        var customerId = Guid.NewGuid();
        var conversation = await messaging.EnsureConversationAsync(
            tenantId, RealtimeSubjectType.Shop, tenantId.Value, customerId, tenantId.Value,
            RealtimeParticipantRole.Customer, RealtimeParticipantRole.Shop);

        // SendMessageAsync phải vẫn thành công dù notification hook lỗi.
        var msg = await messaging.SendMessageAsync(conversation!.Id, customerId, "Test message");
        Assert.NotNull(msg);
    }
}
