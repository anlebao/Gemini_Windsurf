using VanAn.Shared.Domain;
using Xunit;

namespace VanAn.Core.Tests.InventoryIntelligence
{
    /// <summary>
    /// VA-IIE Sprint B (P1) — Shift lifecycle (Draft → Submitted → Acknowledged → Closed).
    /// Pure domain tests — KHÔNG cần DB (Single-Identity + state machine + validation).
    /// </summary>
    public class ShiftLifecycleTests
    {
        private static readonly TenantId TestTenantId = new(Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"));
        private static readonly Guid StaffUserId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        private static readonly Guid ManagerUserId = Guid.Parse("22222222-2222-2222-2222-222222222222");

        // ── Open ca (constructor) ─────────────────────────────────────────────────

        [Fact]
        public void Create_ShouldSetDraftStatusAndSyncIdentity()
        {
            var shift = new Shift(TestTenantId, ShiftType.Morning, StaffUserId);

            Assert.Equal(TestTenantId, shift.TenantId);
            Assert.Equal(ShiftType.Morning, shift.ShiftType);
            Assert.Equal(StaffUserId, shift.StaffUserId);
            Assert.Equal(ShiftStatus.Draft, shift.Status);
            Assert.NotNull(shift.StartTime);
            Assert.Null(shift.EndTime);
            Assert.Null(shift.CashCount);
            Assert.Null(shift.PosCashTotal);

            // SINGLE-IDENTITY: Id (PK) == ShiftId.Value (business key)
            Assert.Equal(shift.Id, shift.ShiftId.Value);
        }

        // ── Đóng ca + nộp bàn giao (Submit) ──────────────────────────────────────

        [Fact]
        public void Submit_ShouldSetSubmittedWithCashAndNotes()
        {
            var shift = new Shift(TestTenantId, ShiftType.Evening, StaffUserId);

            shift.Submit("Bàn giao ca tối", 1_250_000m, 1_240_000m, endTime: shift.StartTime.AddHours(8));

            Assert.Equal(ShiftStatus.Submitted, shift.Status);
            Assert.NotNull(shift.EndTime);
            Assert.Equal(1_250_000m, shift.CashCount);
            Assert.Equal(1_240_000m, shift.PosCashTotal);
            Assert.Equal("Bàn giao ca tối", shift.HandoverNotes);
        }

        [Fact]
        public void Submit_NegativeCash_ShouldThrow()
        {
            var shift = new Shift(TestTenantId, ShiftType.Morning, StaffUserId);

            Assert.Throws<ArgumentOutOfRangeException>(() => shift.Submit(null, -1m, 0m));
        }

        [Fact]
        public void Submit_EndTimeBeforeStartTime_ShouldThrow()
        {
            var shift = new Shift(TestTenantId, ShiftType.Morning, StaffUserId);

            Assert.Throws<ArgumentOutOfRangeException>(() =>
                shift.Submit(null, 100m, 100m, endTime: shift.StartTime.AddHours(-1)));
        }

        [Fact]
        public void Submit_Twice_ShouldThrow()
        {
            var shift = new Shift(TestTenantId, ShiftType.Full, StaffUserId);
            shift.Submit(null, 100m, 100m);

            Assert.Throws<InvalidOperationException>(() => shift.Submit(null, 100m, 100m));
        }

        // ── Bàn giao ca (Acknowledge) ────────────────────────────────────────────

        [Fact]
        public void Acknowledge_ShouldSetAcknowledgedByAndAt()
        {
            var shift = new Shift(TestTenantId, ShiftType.Morning, StaffUserId);
            shift.Submit("OK", 500_000m, 510_000m);

            shift.Acknowledge(ManagerUserId);

            Assert.Equal(ShiftStatus.Acknowledged, shift.Status);
            Assert.Equal(ManagerUserId, shift.AcknowledgedBy);
            Assert.NotNull(shift.AcknowledgedAt);
        }

        [Fact]
        public void Acknowledge_FromDraft_ShouldThrow()
        {
            var shift = new Shift(TestTenantId, ShiftType.Morning, StaffUserId);

            Assert.Throws<InvalidOperationException>(() => shift.Acknowledge(ManagerUserId));
        }

        // ── Chốt ca (Close) ──────────────────────────────────────────────────────

        [Fact]
        public void Close_AfterAcknowledge_ShouldSetClosed()
        {
            var shift = new Shift(TestTenantId, ShiftType.Morning, StaffUserId);
            shift.Submit(null, 100m, 100m);
            shift.Acknowledge(ManagerUserId);

            shift.Close();

            Assert.Equal(ShiftStatus.Closed, shift.Status);
        }

        [Fact]
        public void Close_FromSubmitted_ShouldThrow()
        {
            var shift = new Shift(TestTenantId, ShiftType.Morning, StaffUserId);
            shift.Submit(null, 100m, 100m);

            Assert.Throws<InvalidOperationException>(() => shift.Close());
        }

        // ── InventoryCount ───────────────────────────────────────────────────────

        [Fact]
        public void InventoryCount_Create_ShouldSyncIdentity()
        {
            var count = new InventoryCount(TestTenantId, Guid.NewGuid(), Guid.NewGuid(), CountType.Opening, 100_000m, "g");

            Assert.Equal(CountType.Opening, count.CountType);
            Assert.Equal(100_000m, count.Quantity);
            Assert.Equal("g", count.Unit);
            Assert.Null(count.MidShiftStockIn);
            Assert.Equal(count.Id, count.InventoryCountId.Value);
        }

        // ── ShiftAlert ───────────────────────────────────────────────────────────

        [Fact]
        public void ShiftAlert_CreateAndResolve_ShouldWork()
        {
            var alert = new ShiftAlert(
                TestTenantId, Guid.NewGuid(), "ING_VARIANCE_HIGH",
                AlertSeverity.Warning, "Hao hụt cao", Guid.NewGuid(), 500m, 12.5m);

            Assert.False(alert.IsResolved);
            Assert.Equal(AlertSeverity.Warning, alert.Severity);
            Assert.Equal("ING_VARIANCE_HIGH", alert.AlertCode);
            Assert.Equal(alert.Id, alert.ShiftAlertId.Value);

            alert.Resolve(ManagerUserId, "Đã kiểm lại kho");

            Assert.True(alert.IsResolved);
            Assert.Equal(ManagerUserId, alert.ResolvedBy);
            Assert.NotNull(alert.ResolvedAt);
            Assert.Equal("Đã kiểm lại kho", alert.ResolutionNote);
        }

        [Fact]
        public void ShiftAlert_ResolveTwice_ShouldThrow()
        {
            var alert = new ShiftAlert(TestTenantId, Guid.NewGuid(), "STOCK_LOW", AlertSeverity.Critical, "Hết hàng");
            alert.Resolve(ManagerUserId, "Nhập thêm");

            Assert.Throws<InvalidOperationException>(() => alert.Resolve(ManagerUserId, "Lần 2"));
        }

        // ── TheoreticalConsumption ───────────────────────────────────────────────

        [Fact]
        public void TheoreticalConsumption_ShouldComputeVariance()
        {
            // Theoretical 4000g, Actual 5000g → Variance +1000g, +25%
            var consumption = new TheoreticalConsumption(TestTenantId, Guid.NewGuid(), Guid.NewGuid(), 4_000m, 5_000m);

            Assert.Equal(4_000m, consumption.TheoreticalQuantity);
            Assert.Equal(5_000m, consumption.ActualQuantity);
            Assert.Equal(1_000m, consumption.Variance);
            Assert.Equal(25m, consumption.VariancePercent);
            Assert.Equal(consumption.Id, consumption.TheoreticalConsumptionId.Value);
        }

        [Fact]
        public void TheoreticalConsumption_ZeroTheoretical_ShouldNotDivideByZero()
        {
            var consumption = new TheoreticalConsumption(TestTenantId, Guid.NewGuid(), Guid.NewGuid(), 0m, 10m);

            Assert.Equal(0m, consumption.VariancePercent);
            Assert.Equal(10m, consumption.Variance);
        }
    }
}
