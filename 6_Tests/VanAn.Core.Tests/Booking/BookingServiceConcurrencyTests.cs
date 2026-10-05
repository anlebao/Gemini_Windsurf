using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using VanAn.CoreHub.Infrastructure;
using VanAn.CoreHub.Services.Booking;
using VanAn.CoreHub.Tests.TestInfrastructure;
using VanAn.Shared.Domain;
using Xunit;

namespace VanAn.Core.Tests.BookingScheduling
{
    /// <summary>
    /// Double-booking prevention (SRS §12, AC-C04) — 2 requests race cùng staff/slot:
    /// tối đa 1 request thành công; request còn lại nhận business conflict (409).
    /// - SQLite (test env): unique index (TenantId, StaffId, StartAt) là backstop — writer serialize ở DB level.
    /// - Production PG: advisory lock per (tenant, staff) + SELECT ... FOR UPDATE re-check trong transaction
    ///   (BookingService.AcquireStaffLockAsync/FindConflictAsync — precedent WalletService HR-SCALE-3),
    ///   chặn CẢ conflict chồng nhau khác StartAt (covered sequentially: BookingServiceTests.Create_OverlappingBooking).
    /// </summary>
    public class BookingServiceConcurrencyTests
    {
        private static BookingService BuildService(VanAnDbContext ctx)
            => new(ctx, new AvailabilityService(ctx, NullLogger<AvailabilityService>.Instance), NullLogger<BookingService>.Instance);

        private static async Task<(VanAn.Shared.Domain.Booking? Booking, BookingConflictException? Conflict)> RunAsync(
            Task<VanAn.Shared.Domain.Booking> task)
        {
            try
            {
                return (await task, null);
            }
            catch (BookingConflictException ex)
            {
                return (null, ex);
            }
        }

        /// <summary>2 contexts trên CÙNG 1 in-memory SQLite DB (Cache=Shared), mỗi context 1 connection riêng.</summary>
        private sealed class SharedBookingDb : IDisposable
        {
            private readonly SqliteConnection _connection1;
            private readonly SqliteConnection _connection2;

            public VanAnDbContext Context1 { get; }
            public VanAnDbContext Context2 { get; }

            public static SharedBookingDb Create()
            {
                string dbName = $"race_{Guid.NewGuid():N}";
                var conn1 = new SqliteConnection($"DataSource={dbName};Mode=Memory;Cache=Shared");
                var conn2 = new SqliteConnection($"DataSource={dbName};Mode=Memory;Cache=Shared");
                conn1.Open();
                conn2.Open();

                var tenant1 = new TestTenantProvider();
                var tenant2 = new TestTenantProvider();

                var ctx1 = new VanAnDbContext(
                    new DbContextOptionsBuilder<VanAnDbContext>().UseSqlite(conn1).EnableSensitiveDataLogging().Options, tenant1);
                var ctx2 = new VanAnDbContext(
                    new DbContextOptionsBuilder<VanAnDbContext>().UseSqlite(conn2).Options, tenant2);

                ctx1.Database.EnsureCreated();

                tenant1.SetTenant(BookingTestData.TenantId.Value);
                tenant2.SetTenant(BookingTestData.TenantId.Value);

                return new SharedBookingDb(conn1, conn2, ctx1, ctx2);
            }

            private SharedBookingDb(SqliteConnection connection1, SqliteConnection connection2, VanAnDbContext context1, VanAnDbContext context2)
            {
                _connection1 = connection1;
                _connection2 = connection2;
                Context1 = context1;
                Context2 = context2;
            }

            public void Dispose()
            {
                Context1.Dispose();
                Context2.Dispose();
                _connection1.Dispose();
                _connection2.Dispose();
            }
        }

        [Fact]
        public async Task Race_TwoRequests_SameStaffSlot_OnlyOneSucceeds_OneGetsBusinessConflict()
        {
            using var db = SharedBookingDb.Create();
            var ctx = db.Context1;

            // Seed: config enabled + offering + staff + skill + schedule.
            await BookingTestData.EnsureConfigAsync(ctx, BookingTestData.TenantId, enabled: true);
            var (staffId, offeringId) = await BookingTestData.SeedStaffOfferingAsync(ctx, BookingTestData.TenantId);

            var svc1 = BuildService(db.Context1);
            var svc2 = BuildService(db.Context2);

            var command = new CreateBookingCommand(
                offeringId, BookingTestData.At(14, 0), staffId,
                null, "device-race-1", null, []);

            // 2 requests gần như đồng thời (Task.WhenAll — race thật, AC-C04).
            Task<VanAn.Shared.Domain.Booking> task1 = svc1.CreateBookingAsync(BookingTestData.TenantId, command, "race-key-1");
            Task<VanAn.Shared.Domain.Booking> task2 = svc2.CreateBookingAsync(BookingTestData.TenantId, command, "race-key-2");

            (VanAn.Shared.Domain.Booking? Booking, BookingConflictException? Conflict)[] results =
                await Task.WhenAll(RunAsync(task1), RunAsync(task2));

            VanAn.Shared.Domain.Booking? success = results.FirstOrDefault(r => r.Booking is not null).Booking;
            BookingConflictException? conflict = results.FirstOrDefault(r => r.Conflict is not null).Conflict;

            // Tối đa 1 request thành công (AC-C04) + request còn lại nhận business conflict.
            Assert.NotNull(success);
            Assert.NotNull(conflict);
            Assert.Contains("khung giờ", conflict!.Message);

            // Chỉ 1 booking tồn tại trong DB.
            Assert.Equal(1, await ctx.Bookings.IgnoreQueryFilters().CountAsync());
            var persisted = await ctx.Bookings.IgnoreQueryFilters().FirstAsync();
            Assert.Equal(success!.Id, persisted.Id);
            Assert.Equal(BookingStatus.StaffAssigned, persisted.Status);
            Assert.Equal(staffId, persisted.StaffId);
        }

        [Fact]
        public async Task IdempotentCreate_ConcurrentRetry_SameKey_ReturnsSameBooking()
        {
            using var db = SharedBookingDb.Create();
            var ctx = db.Context1;

            await BookingTestData.EnsureConfigAsync(ctx, BookingTestData.TenantId, enabled: true);
            var (staffId, offeringId) = await BookingTestData.SeedStaffOfferingAsync(ctx, BookingTestData.TenantId);

            var svc1 = BuildService(db.Context1);
            var svc2 = BuildService(db.Context2);

            var command = new CreateBookingCommand(offeringId, BookingTestData.At(14, 0), staffId, null, "d-retry", null, []);

            // Cùng Idempotency-Key từ 2 request (client retry song song — §21.1) → 1 booking duy nhất.
            Task<VanAn.Shared.Domain.Booking> task1 = svc1.CreateBookingAsync(BookingTestData.TenantId, command, "same-key");
            Task<VanAn.Shared.Domain.Booking> task2 = svc2.CreateBookingAsync(BookingTestData.TenantId, command, "same-key");

            (VanAn.Shared.Domain.Booking? Booking, BookingConflictException? Conflict)[] results =
                await Task.WhenAll(RunAsync(task1), RunAsync(task2));

            VanAn.Shared.Domain.Booking? success = results.FirstOrDefault(r => r.Booking is not null).Booking;
            Assert.NotNull(success);

            // 1 booking duy nhất + 1 idempotency record (không duplicate).
            Assert.Equal(1, await ctx.Bookings.IgnoreQueryFilters().CountAsync());
            Assert.Equal(1, await ctx.BookingIdempotencyRecords.IgnoreQueryFilters().CountAsync(r => r.IdempotencyKey == "same-key"));
        }
    }
}
