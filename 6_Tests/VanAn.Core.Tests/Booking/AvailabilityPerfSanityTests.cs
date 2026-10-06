using System.Diagnostics;
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
    /// P6.4 — Perf sanity §28: availability p95 (reconcile với baseline, KHÔNG tạo SLA riêng).
    /// Benchmark đơn giản trên test context (SQLite in-memory): seed staff + schedule 7 ngày,
    /// đo p95 GetAvailableSlotsAsync với dữ liệu vừa phải. Mục đích bắt N+1 / missing-index
    /// regression — KHÔNG phải SLA production (PG p95 ≤ 500ms xác nhận tại RV P7 L3).
    /// </summary>
    public class AvailabilityPerfSanityTests
    {
        [Fact]
        public async Task GetAvailableSlots_P95_WithinLooseBound_NoRegression()
        {
            using var scope = VanAnDbContextTestFactory.Create();
            scope.TenantProvider!.SetTenant(BookingTestData.TenantId.Value);
            var ctx = scope.Context;
            var svc = new AvailabilityService(ctx, NullLogger<AvailabilityService>.Instance);

            await BookingTestData.EnsureConfigAsync(ctx, BookingTestData.TenantId, enabled: true);
            Guid offeringId = Guid.Empty;
            // Seed 10 staff × schedule 7 ngày (Monday-Sunday 09:00-18:00) + 2 override (1 leave/1 break).
            for (int s = 0; s < 10; s++)
            {
                var (staffId, offId) = await BookingTestData.SeedStaffOfferingAsync(ctx, BookingTestData.TenantId,
                    durationMinutes: 45, price: 200_000m, staffName: $"Staff {s}", withSchedule: false);
                offeringId = offId;
                for (int wd = 0; wd < 7; wd++)
                {
                    ctx.StaffWorkingSchedules.Add(new StaffWorkingSchedule(
                        BookingTestData.TenantId, staffId, wd,
                        new TimeSpan(9, 0, 0), new TimeSpan(18, 0, 0),
                        new TimeSpan(12, 0, 0), new TimeSpan(12, 30, 0)));
                }
                if (s % 3 == 0)
                {
                    ctx.StaffScheduleOverrides.Add(new StaffScheduleOverride(
                        BookingTestData.TenantId, staffId, BookingTestData.TestMonday.ToDateTime(TimeOnly.MinValue),
                        StaffScheduleOverrideType.Leave, new TimeSpan(9, 0, 0), new TimeSpan(18, 0, 0), "Leave"));
                }
            }
            await ctx.SaveChangesAsync();

            // Warm-up (EF query plan + tracking).
            for (int i = 0; i < 3; i++)
                _ = await svc.GetAvailableSlotsAsync(BookingTestData.TenantId, offeringId, BookingTestData.TestMonday);

            // Đo 50 lần — p95 (loose bound 1500ms trên SQLite test; PG production đo tại RV).
            var times = new List<double>(50);
            for (int i = 0; i < 50; i++)
            {
                var sw = Stopwatch.StartNew();
                var slots = await svc.GetAvailableSlotsAsync(BookingTestData.TenantId, offeringId, BookingTestData.TestMonday);
                sw.Stop();
                times.Add(sw.Elapsed.TotalMilliseconds);
                Assert.NotNull(slots);
            }

            times.Sort();
            double p95 = times[(int)Math.Ceiling(times.Count * 0.95) - 1];
            Assert.True(p95 < 1500, $"Availability p95 = {p95:F0}ms vượt loose bound 1500ms (test context SQLite).");

            // Baseline log: SQLite test context p95 — PG production so sánh tại RV.
            _ = p95;
        }
    }
}
