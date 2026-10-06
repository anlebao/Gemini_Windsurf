using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using VanAn.CoreHub.Commands;
using VanAn.CoreHub.Infrastructure;
using VanAn.CoreHub.Services;
using VanAn.CoreHub.Services.Booking;
using VanAn.CoreHub.Tests.TestInfrastructure;
using VanAn.Shared.Domain;
using Xunit;
using BookingEntity = VanAn.Shared.Domain.Booking;
using OrderEntity = VanAn.Shared.Domain.Order;

namespace VanAn.Core.Tests.BookingScheduling
{
    /// <summary>
    /// P3.5 Booking→Order hook (D2, Q1 = COMPLETED): CreateOrderCommand từ offering/add-on snapshots,
    /// path CreateOrderFromCommandAsync (giữ HTX tag — lesson P6c), Booking.OrderId, idempotent R7
    /// (không double-create), recovery khi crash giữa create/attach (TrackingCode lookup).
    /// </summary>
    public class BookingOrderHookTests
    {
        private static BookingService BuildBookingService(VanAnDbContext ctx, FakeOrderService orderSvc)
            => new(ctx, new AvailabilityService(ctx, NullLogger<AvailabilityService>.Instance),
                NullLogger<BookingService>.Instance, orderSvc, financialService: null);

        private static async Task<BookingEntity> BookThroughToServiceAsync(VanAnDbContext ctx, BookingService svc)
        {
            await BookingTestData.EnsureConfigAsync(ctx, BookingTestData.TenantId, enabled: true);
            var (staffId, offeringId) = await BookingTestData.SeedStaffOfferingAsync(ctx, BookingTestData.TenantId);
            // Test schema (EnsureCreated) còn FK OrderItems→Products → seed product stub (pattern sync subscriber).
            await BookingTestData.SeedProductStubAsync(ctx, BookingTestData.TenantId, offeringId, "Massage 60m", 300_000m);
            var booking = await svc.CreateBookingAsync(BookingTestData.TenantId,
                new CreateBookingCommand(OfferingId: offeringId, StartAt: BookingTestData.At(14, 0), StaffId: null, CustomerId: null, CustomerDeviceId: "device-oh", CustomerNote: "Note khách", AddOns: [], AttributionId: null),
                "key-oh");
            await svc.ConfirmAsync(BookingTestData.TenantId, booking.Id);
            await svc.AssignStaffAsync(BookingTestData.TenantId, booking.Id, staffId);
            await svc.CheckInAsync(BookingTestData.TenantId, booking.Id);
            await svc.StartServiceAsync(BookingTestData.TenantId, booking.Id);
            return booking;
        }

        [Fact]
        public async Task Complete_CreatesOrderFromSnapshots_AndAttaches()
        {
            using var scope = VanAnDbContextTestFactory.Create();
            scope.TenantProvider!.SetTenant(BookingTestData.TenantId.Value);
            var ctx = scope.Context;
            var orderSvc = new FakeOrderService(ctx);
            var svc = BuildBookingService(ctx, orderSvc);
            var booking = await BookThroughToServiceAsync(ctx, svc);

            var completed = await svc.CompleteAsync(BookingTestData.TenantId, booking.Id, 300_000m);

            Assert.Equal(BookingStatus.Completed, completed.Status);
            Assert.NotNull(completed.OrderId);
            Assert.Equal(1, orderSvc.CreatedOrders);

            var order = await ctx.Orders.IgnoreQueryFilters().FirstAsync(o => o.Id == completed.OrderId.Value);
            Assert.Equal(booking.PublicBookingCode, order.TrackingCode);     // recovery key
            Assert.Equal(booking.CustomerNote, order.CustomerNotes);
            Assert.Equal(300_000m, order.SubTotal);

            // Items: 1 offering line snapshot name/price.
            var item = Assert.Single(order.Items);
            Assert.Equal(booking.OfferingNameSnapshot, item.ProductName);
            Assert.Equal(300_000m, item.UnitPrice);
            Assert.Equal(booking.OfferingId, item.ProductId);
        }

        [Fact]
        public async Task Complete_Twice_NoDoubleCreate()
        {
            using var scope = VanAnDbContextTestFactory.Create();
            scope.TenantProvider!.SetTenant(BookingTestData.TenantId.Value);
            var ctx = scope.Context;
            var orderSvc = new FakeOrderService(ctx);
            var svc = BuildBookingService(ctx, orderSvc);
            var booking = await BookThroughToServiceAsync(ctx, svc);

            _ = await svc.CompleteAsync(BookingTestData.TenantId, booking.Id, 300_000m);
            var second = await svc.CompleteAsync(BookingTestData.TenantId, booking.Id, 300_000m);

            Assert.Equal(1, orderSvc.CreatedOrders);           // R7 — không double-create
            Assert.Equal(1, await ctx.Orders.IgnoreQueryFilters().CountAsync());
            Assert.Equal(second.OrderId, booking.OrderId);
        }

        [Fact]
        public async Task Complete_Recovery_OrderExistsByTrackingCode_AttachesWithoutCreate()
        {
            using var scope = VanAnDbContextTestFactory.Create();
            scope.TenantProvider!.SetTenant(BookingTestData.TenantId.Value);
            var ctx = scope.Context;
            var orderSvc = new FakeOrderService(ctx);
            var svc = BuildBookingService(ctx, orderSvc);
            var booking = await BookThroughToServiceAsync(ctx, svc);

            // Giả lập crash: order đã tạo (TrackingCode = booking code) nhưng AttachOrder chưa kịp lưu.
            var orphanOrder = orderSvc.CreateOrderForBooking(ctx, booking, 300_000m);

            var completed = await svc.CompleteAsync(BookingTestData.TenantId, booking.Id, 300_000m);

            Assert.Equal(orphanOrder.Id, completed.OrderId);   // attach existing — không tạo mới
            Assert.Equal(0, orderSvc.CreatedOrders);           // hook KHÔNG gọi CreateOrderFromCommandAsync (recovery)
            Assert.Equal(1, await ctx.Orders.IgnoreQueryFilters().CountAsync());
        }

        [Fact]
        public async Task Complete_OrderServiceThrows_TransitionStillSucceeds()
        {
            using var scope = VanAnDbContextTestFactory.Create();
            scope.TenantProvider!.SetTenant(BookingTestData.TenantId.Value);
            var ctx = scope.Context;
            var orderSvc = new FakeOrderService(ctx) { ThrowOnCreate = true };
            var svc = BuildBookingService(ctx, orderSvc);
            var booking = await BookThroughToServiceAsync(ctx, svc);

            // Fail-safe: lỗi order hook KHÔNG fail transition COMPLETED.
            var completed = await svc.CompleteAsync(BookingTestData.TenantId, booking.Id, 300_000m);

            Assert.Equal(BookingStatus.Completed, completed.Status);
            Assert.Null(completed.OrderId);
            Assert.Equal(0, await ctx.Orders.IgnoreQueryFilters().CountAsync());
        }

        /// <summary>Fake IOrderService — chỉ CreateOrderFromCommandAsync hoạt động (ghi Order vào ctx test).</summary>
        private sealed class FakeOrderService(VanAnDbContext ctx) : IOrderService
        {
            public int CreatedOrders { get; private set; }
            public bool ThrowOnCreate { get; init; }

            public Task<OrderEntity> CreateOrderFromCommandAsync(CreateOrderCommand command, Guid tenantId, string? routingKey = null)
            {
                if (ThrowOnCreate)
                    throw new InvalidOperationException("simulated order service failure");
                OrderEntity order = CreateOrderForBooking(ctx, command, new TenantId(tenantId));
                CreatedOrders++;
                return Task.FromResult(order);
            }

            /// <summary>Build + persist Order (tracking = booking code) — dùng cho hook + recovery test.</summary>
            public OrderEntity CreateOrderForBooking(VanAnDbContext db, BookingEntity booking, decimal total)
            {
                var tenantIdObj = new TenantId(booking.TenantId.Value);
                return CreateOrderForBooking(db, new CreateOrderCommand
                {
                    CustomerId = booking.CustomerId,
                    CustomerDeviceId = Guid.Empty,
                    CustomerNotes = booking.CustomerNote,
                    TrackingCode = booking.PublicBookingCode,
                    Items = [new OrderItemRequest
                    {
                        ProductId = booking.OfferingId,
                        Quantity = 1,
                        UnitPrice = booking.OfferingPriceSnapshot,
                        TenantId = booking.TenantId.Value,
                        ProductName = booking.OfferingNameSnapshot,
                        VatRate = 0.10m
                    }]
                }, tenantIdObj);
            }

            private OrderEntity CreateOrderForBooking(VanAnDbContext db, CreateOrderCommand command, TenantId tenantIdObj)
            {
                Guid orderId = Guid.NewGuid();
                var orderItems = command.Items.Select(i =>
                    OrderItem.Create(Guid.NewGuid(), tenantIdObj, orderId, i.ProductId, i.Quantity, i.UnitPrice, i.ProductName, i.VatRate)).ToList();
                var order = OrderEntity.Create(orderId, tenantIdObj, command.CustomerId, orderItems);
                order.SetCustomerDeviceId(command.CustomerDeviceId.ToString());
                if (!string.IsNullOrWhiteSpace(command.TrackingCode))
                    order.SetTrackingCode(command.TrackingCode.Trim());
                if (!string.IsNullOrWhiteSpace(command.CustomerNotes))
                    order.SetCustomerNotes(command.CustomerNotes.Trim());
                db.Orders.Add(order);
                db.SaveChanges();
                return order;
            }

            public Task<int> GetTodayOrderCountAsync(Guid tenantId) => throw new NotImplementedException();
            public Task<IEnumerable<OrderEntity>> GetOrdersByDateRangeAsync(Guid tenantId, DateTime startDate, DateTime endDate) => throw new NotImplementedException();
            public Task<IEnumerable<OrderEntity>> GetAllOrdersByDateRangeAsync(DateTime startDate, DateTime endDate) => throw new NotImplementedException();
            public Task<List<OrderEntity>> GetAllOrdersByStatusAsync(OrderStatusId status) => throw new NotImplementedException();
            public Task<OrderEntity?> GetOrderByIdAsync(Guid orderId, Guid tenantId) => throw new NotImplementedException();
            public Task<OrderEntity> CreateOrderAsync(OrderEntity order, Guid tenantId) => throw new NotImplementedException();
            public Task<bool> UpdateOrderStatusAsync(Guid orderId, string newStatus, Guid tenantId) => throw new NotImplementedException();
            public Task<bool> UpdateOrderVoiceNoteAsync(Guid orderId, string voiceNoteText, Guid tenantId) => throw new NotImplementedException();
            public Task<OrderEntity> CreateOrderWithQueueAsync(OrderEntity order, Guid tenantId) => throw new NotImplementedException();
            public Task<List<OrderEntity>> GetQueuedOrdersAsync(Guid tenantId) => throw new NotImplementedException();
            public Task<bool> IsTransitionValidAsync(OrderStatusId currentStatus, OrderStatusId newStatus) => throw new NotImplementedException();
            public Task<List<OrderEntity>> GetOrdersByStatusAsync(OrderStatusId status, Guid tenantId) => throw new NotImplementedException();
            public Task<OrderDashboardData> GetDashboardDataAsync(Guid tenantId) => throw new NotImplementedException();
            public Task<OrderSummary> GetOrderSummaryAsync(Guid orderId, Guid tenantId) => throw new NotImplementedException();
            public Task<List<AccountingEntry>> GetEntriesByOrderAsync(Guid orderId, TenantId tenantId) => throw new NotImplementedException();
            public Task ConfirmPaymentAsync(Guid orderId, Guid tenantId, string transactionId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
            public Task MarkPaidAsync(Guid orderId, Guid tenantId, string transactionId, bool enqueuePaymentConfirmedEvent = false, CancellationToken cancellationToken = default) => throw new NotImplementedException();
            public Task GenerateAccountingEntriesAsync(OrderEntity order, TenantId tenantId) => throw new NotImplementedException();
            public Task<OrderEntity?> GetOrderByIdForPublicTrackingAsync(Guid orderId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        }
    }
}
