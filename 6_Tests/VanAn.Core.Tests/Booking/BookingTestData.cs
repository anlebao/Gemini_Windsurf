using Microsoft.EntityFrameworkCore;
using VanAn.CoreHub.Infrastructure;
using VanAn.Shared.Domain;

namespace VanAn.Core.Tests.BookingScheduling
{
    /// <summary>
    /// Test data helpers cho Booking & Staff Scheduling services (SRS v1.1 MVP — P2).
    /// TenantId/OtherTenantId cố định; ngày cố định 2026-10-12 (Monday) — weekday schedule khớp.
    /// </summary>
    internal static class BookingTestData
    {
        public static readonly TenantId TenantId = new(Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"));
        public static readonly TenantId OtherTenantId = new(Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc"));

        /// <summary>Monday 2026-10-12 — các test dùng schedule weekday khớp.</summary>
        public static readonly DateOnly TestMonday = new(2026, 10, 12);

        public static DateTime At(int hour, int minute = 0, int day = 12)
            => new(2026, 10, day, hour, minute, 0, DateTimeKind.Utc);

        public static async Task<BookingTenantConfig> EnsureConfigAsync(VanAnDbContext ctx, TenantId tenantId, bool enabled = true, CancellationToken ct = default)
        {
            BookingTenantConfig? config = await ctx.BookingTenantConfigs
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(c => c.TenantId == tenantId, ct);
            if (config is null)
            {
                config = new BookingTenantConfig(tenantId);
                ctx.BookingTenantConfigs.Add(config);
            }
            if (enabled)
                config.Enable();
            else
                config.Disable();
            await ctx.SaveChangesAsync(ct);
            return config;
        }

        public static async Task<BookingTenantConfig> EnsureConfigWithDepositAsync(
            VanAnDbContext ctx, TenantId tenantId, DepositPolicy policy, decimal? fixedAmount = null, decimal? percentage = null, CancellationToken ct = default)
        {
            BookingTenantConfig config = await EnsureConfigAsync(ctx, tenantId, enabled: true, ct);
            config.UpdateDepositPolicy(policy, fixedAmount, percentage);
            await ctx.SaveChangesAsync(ct);
            return config;
        }

        /// <summary>Seed staff + offering + skill junction + weekday schedule (Monday 09:00-17:00, break 12:00-13:00).</summary>
        public static async Task<(Guid StaffId, Guid OfferingId)> SeedStaffOfferingAsync(
            VanAnDbContext ctx, TenantId tenantId, int durationMinutes = 60, decimal price = 300_000m,
            string staffName = "KTV A", bool withSchedule = true, CancellationToken ct = default)
        {
            var staff = new Staff(tenantId, staffName, "Technician");
            ctx.Staffs.Add(staff);
            var offering = new AppointmentOffering(tenantId, "Massage 60m", OfferingType.Service, durationMinutes, price);
            ctx.AppointmentOfferings.Add(offering);
            await ctx.SaveChangesAsync(ct);

            if (withSchedule)
            {
                ctx.StaffWorkingSchedules.Add(new StaffWorkingSchedule(
                    tenantId, staff.Id, (int)DayOfWeek.Monday,
                    new TimeSpan(9, 0, 0), new TimeSpan(17, 0, 0),
                    new TimeSpan(12, 0, 0), new TimeSpan(13, 0, 0)));
            }
            ctx.StaffServices.Add(new StaffService(tenantId, staff.Id, offering.Id));
            await ctx.SaveChangesAsync(ct);
            return (staff.Id, offering.Id);
        }

        /// <summary>Seed staff KHÔNG có skill (để test skill-match exclusion).</summary>
        public static async Task<Guid> SeedStaffNoSkillAsync(VanAnDbContext ctx, TenantId tenantId, string staffName = "KTV Không Skill", CancellationToken ct = default)
        {
            var staff = new Staff(tenantId, staffName, "Technician");
            ctx.Staffs.Add(staff);
            await ctx.SaveChangesAsync(ct);
            return staff.Id;
        }
    }
}
