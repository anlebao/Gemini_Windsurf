using VanAn.Shared.Domain;
using Xunit;

namespace VanAn.Core.Tests.BookingScheduling
{
    /// <summary>
    /// Booking & Staff Scheduling (SRS v1.1 MVP — P1) — domain lifecycle tests.
    /// Pure domain — KHÔNG cần DB (Single-Identity + state machine + validation + immutable ledger).
    /// </summary>
    public class BookingDomainTests
    {
        private static readonly TenantId TestTenantId = new(Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"));
        private static readonly Guid StaffA = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        private static readonly Guid StaffB = Guid.Parse("bbbbbbbb-bbbb-0000-bbbb-bbbbbbbbbbbb");
        private static readonly Guid SalesmanId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");

        // ── Booking: create + Single-Identity ────────────────────────────────────

        [Fact]
        public void Create_ShouldSetPendingStatusAndSyncIdentity()
        {
            var start = new DateTime(2026, 10, 10, 14, 0, 0, DateTimeKind.Utc);
            var booking = NewBooking(start);

            Assert.Equal(TestTenantId, booking.TenantId);
            Assert.Equal(BookingStatus.PendingConfirmation, booking.Status);
            Assert.Equal(start, booking.StartAt);
            Assert.Equal(start.AddMinutes(60), booking.EndAt);
            Assert.Equal("Massage 60m", booking.OfferingNameSnapshot);
            Assert.Equal(300_000m, booking.EstimatedTotal);
            Assert.Equal(0, booking.Version);

            // SINGLE-IDENTITY: Id (PK) == BookingId.Value (business key)
            Assert.Equal(booking.Id, booking.BookingId.Value);
        }

        [Fact]
        public void Create_InvalidTimeRange_ShouldThrow()
        {
            var start = new DateTime(2026, 10, 10, 14, 0, 0, DateTimeKind.Utc);
            Assert.Throws<ArgumentException>(() => NewBooking(start, end: start));
        }

        // ── Booking: state machine (SRS §9.3 allowed transitions) ───────────────

        [Fact]
        public void StateMachine_HappyPath_Confirm_Assign_CheckIn_Start_Complete()
        {
            var booking = NewBooking();

            booking.Confirm();
            Assert.Equal(BookingStatus.Confirmed, booking.Status);

            booking.AssignStaff(StaffA);
            Assert.Equal(BookingStatus.StaffAssigned, booking.Status);
            Assert.Equal(StaffA, booking.StaffId);

            booking.CheckIn();
            Assert.Equal(BookingStatus.CheckedIn, booking.Status);
            Assert.NotNull(booking.CheckedInAt);

            booking.StartService();
            Assert.Equal(BookingStatus.InService, booking.Status);

            booking.Complete(320_000m);
            Assert.Equal(BookingStatus.Completed, booking.Status);
            Assert.Equal(320_000m, booking.ActualTotal);
            Assert.NotNull(booking.CompletedAt);

            // Version bump mỗi transition
            Assert.Equal(5, booking.Version);
        }

        [Fact]
        public void StateMachine_Reject_OnlyFromPending()
        {
            var booking = NewBooking();
            booking.Reject("Hết chỗ");

            Assert.Equal(BookingStatus.Rejected, booking.Status);
            Assert.Equal("Hết chỗ", booking.CancellationReason);

            Assert.Throws<InvalidOperationException>(() => booking.Reject());      // Rejected → Rejected
            Assert.Throws<InvalidOperationException>(() => booking.Confirm());     // Rejected → Confirmed
            Assert.Throws<InvalidOperationException>(() => booking.AssignStaff(StaffA));
        }

        [Fact]
        public void StateMachine_Cancel_FromPendingOrConfirmed()
        {
            var booking = NewBooking();
            booking.Cancel("Khách hủy");
            Assert.Equal(BookingStatus.Cancelled, booking.Status);

            var confirmed = NewBooking();
            confirmed.Confirm();
            confirmed.Cancel();
            Assert.Equal(BookingStatus.Cancelled, confirmed.Status);

            // RV P6 decision (user approve 2026-10-06): STAFF_ASSIGNED → CANCELLED hợp lệ —
            // create-with-staff (khách chọn staff) phải hủy được.
            var assigned = NewBooking();
            assigned.Confirm();
            assigned.AssignStaff(StaffA);
            assigned.Cancel("Khách đổi ý");
            Assert.Equal(BookingStatus.Cancelled, assigned.Status);

            // CheckedIn/InService vẫn KHÔNG hủy (đã bắt đầu phục vụ).
            var checkedIn = NewBooking();
            checkedIn.Confirm();
            checkedIn.AssignStaff(StaffA);
            checkedIn.CheckIn();
            Assert.Throws<InvalidOperationException>(() => checkedIn.Cancel());
        }

        [Fact]
        public void StateMachine_NoShow_OnlyFromStaffAssigned()
        {
            var booking = NewBooking();
            Assert.Throws<InvalidOperationException>(() => booking.MarkNoShow());

            booking.Confirm();
            Assert.Throws<InvalidOperationException>(() => booking.MarkNoShow());

            booking.AssignStaff(StaffA);
            booking.MarkNoShow("Khách không đến");
            Assert.Equal(BookingStatus.NoShow, booking.Status);
        }

        [Fact]
        public void StateMachine_CheckIn_RequiresStaffAssigned()
        {
            var booking = NewBooking();
            Assert.Throws<InvalidOperationException>(() => booking.CheckIn());
        }

        [Fact]
        public void StateMachine_ChangeStaff_FromConfirmedOrStaffAssigned()
        {
            var booking = NewBooking();
            Assert.Throws<InvalidOperationException>(() => booking.ChangeStaff(StaffB)); // Pending → không đổi được

            booking.Confirm();
            booking.ChangeStaff(StaffB);
            Assert.Equal(StaffB, booking.StaffId);
            Assert.Equal(BookingStatus.StaffAssigned, booking.Status);

            booking.ChangeStaff(StaffA); // reassign vẫn hợp lệ
            Assert.Equal(StaffA, booking.StaffId);
        }

        // ── Booking: snapshot + deposit + order link ─────────────────────────────

        [Fact]
        public void Complete_NegativeTotal_ShouldThrow()
        {
            var booking = NewBooking();
            booking.Confirm();
            booking.AssignStaff(StaffA);
            booking.CheckIn();
            booking.StartService();
            Assert.Throws<ArgumentException>(() => booking.Complete(-1m));
        }

        [Fact]
        public void SetDepositRequirement_ValidatesPolicy()
        {
            var booking = NewBooking();
            Assert.Throws<ArgumentException>(() => booking.SetDepositRequirement(true, MoneyNatureType.SecurityDeposit, null));
            Assert.Throws<ArgumentException>(() => booking.SetDepositRequirement(true, null, 100_000m));

            booking.SetDepositRequirement(true, MoneyNatureType.SecurityDeposit, 100_000m);
            Assert.True(booking.DepositRequired);
            Assert.Equal(PaymentStatus.Pending, booking.PaymentStatus);

            booking.RecordDepositPaid();
            Assert.Equal(PaymentStatus.Paid, booking.PaymentStatus);
            Assert.Equal(BookingSubState.DepositPaid, booking.SubState);
        }

        [Fact]
        public void AttachOrder_OnceOnly()
        {
            var booking = NewBooking();
            var orderId = Guid.NewGuid();

            booking.AttachOrder(orderId);
            Assert.Equal(orderId, booking.OrderId);

            Assert.Throws<InvalidOperationException>(() => booking.AttachOrder(Guid.NewGuid()));
        }

        // ── BookingTenantConfig (Q5 feature-flag + deposit policy) ───────────────

        [Fact]
        public void BookingTenantConfig_DefaultDisabled_SyncIdentity()
        {
            var config = new BookingTenantConfig(TestTenantId);

            Assert.False(config.IsEnabled);
            Assert.Equal(config.Id, config.BookingTenantConfigId.Value);

            config.Enable();
            Assert.True(config.IsEnabled);

            config.Disable();
            Assert.False(config.IsEnabled);
        }

        [Fact]
        public void BookingTenantConfig_DepositPolicyValidation()
        {
            var config = new BookingTenantConfig(TestTenantId);

            Assert.Throws<ArgumentException>(() => config.UpdateDepositPolicy(DepositPolicy.Fixed, null, null));
            Assert.Throws<ArgumentException>(() => config.UpdateDepositPolicy(DepositPolicy.Percentage, null, 150m));

            config.UpdateDepositPolicy(DepositPolicy.Fixed, 100_000m, null);
            Assert.Equal(100_000m, config.DepositFixedAmount);

            config.UpdateDepositPolicy(DepositPolicy.None, 50_000m, 10m);
            Assert.Null(config.DepositFixedAmount);
            Assert.Null(config.DepositPercentage);
        }

        // ── Staff + schedule + eligibility ───────────────────────────────────────

        [Fact]
        public void Staff_Create_SyncIdentity()
        {
            var staff = new Staff(TestTenantId, "Nguyễn Văn A", "Technician");

            Assert.Equal(staff.Id, staff.StaffId.Value);
            Assert.Equal("Nguyễn Văn A", staff.DisplayName);
            Assert.True(staff.IsActive);

            staff.Deactivate();
            Assert.False(staff.IsActive);
        }

        [Fact]
        public void StaffWorkingSchedule_ValidatesRange()
        {
            var staffId = Guid.NewGuid();
            var schedule = new StaffWorkingSchedule(TestTenantId, staffId, (int)DayOfWeek.Monday, new TimeSpan(8, 0, 0), new TimeSpan(17, 0, 0));

            Assert.Equal(schedule.Id, schedule.StaffWorkingScheduleId.Value);
            Assert.Equal(new TimeSpan(8, 0, 0), schedule.StartTime);

            Assert.Throws<ArgumentException>(() => new StaffWorkingSchedule(TestTenantId, staffId, 7, new TimeSpan(8, 0, 0), new TimeSpan(17, 0, 0)));
            Assert.Throws<ArgumentException>(() => new StaffWorkingSchedule(TestTenantId, staffId, (int)DayOfWeek.Monday, new TimeSpan(17, 0, 0), new TimeSpan(8, 0, 0)));
        }

        [Fact]
        public void StaffScheduleOverride_NormalizesDate()
        {
            var staffId = Guid.NewGuid();
            var raw = new DateTime(2026, 10, 10, 23, 59, 59, DateTimeKind.Utc);
            var overrideEntry = new StaffScheduleOverride(TestTenantId, staffId, raw, StaffScheduleOverrideType.Leave);

            Assert.Equal(raw.Date, overrideEntry.Date);
            Assert.Equal(StaffScheduleOverrideType.Leave, overrideEntry.OverrideType);
        }

        [Fact]
        public void StaffService_EligibilityJunction_SyncIdentity()
        {
            var staffId = Guid.NewGuid();
            var offeringId = Guid.NewGuid();
            var junction = new StaffService(TestTenantId, staffId, offeringId);

            Assert.Equal(junction.Id, junction.StaffServiceId.Value);
            Assert.Equal(staffId, junction.StaffId);
            Assert.Equal(offeringId, junction.OfferingId);
        }

        // ── Offering + AddOn ─────────────────────────────────────────────────────

        [Fact]
        public void AppointmentOffering_Create_SyncIdentity()
        {
            var offering = new AppointmentOffering(TestTenantId, "Combo Massage + Foot Care", OfferingType.Package, 90, 450_000m);

            Assert.Equal(offering.Id, offering.AppointmentOfferingId.Value);
            Assert.Equal(90, offering.DurationMinutes);
            Assert.True(offering.IsActive);

            offering.Deactivate();
            Assert.False(offering.IsActive);

            Assert.Throws<ArgumentException>(() => new AppointmentOffering(TestTenantId, "", OfferingType.Service, 30, 100m));
            Assert.Throws<ArgumentException>(() => new AppointmentOffering(TestTenantId, "X", OfferingType.Service, 0, 100m));
            Assert.Throws<ArgumentException>(() => new AppointmentOffering(TestTenantId, "X", OfferingType.Service, 30, -1m));
        }

        [Fact]
        public void AddOn_Create_SyncIdentity()
        {
            var addOn = new AddOn(TestTenantId, "Nước uống", 20_000m);

            Assert.Equal(addOn.Id, addOn.AddOnId.Value);
            Assert.Equal(20_000m, addOn.Price);
        }

        // ── QR + Attribution (SRS §7) ────────────────────────────────────────────

        [Fact]
        public void QRChannel_Revoke_IsOneWay()
        {
            var qr = new QRChannel(TestTenantId, "hash-token-abc", SalesmanId);

            Assert.Equal(qr.Id, qr.QRChannelId.Value);
            Assert.True(qr.IsActive);
            Assert.Null(qr.RevokedAt);

            qr.Revoke();
            Assert.False(qr.IsActive);
            Assert.NotNull(qr.RevokedAt);

            qr.Revoke(); // idempotent — không throw
            Assert.False(qr.IsActive);
        }

        [Fact]
        public void AttributionSession_Refresh_KeepsSalesman()
        {
            var session = new AttributionSession(TestTenantId, Guid.NewGuid(), "anon-session-1", SalesmanId);

            Assert.Equal(session.Id, session.AttributionSessionId.Value);
            Assert.Equal(SalesmanId, session.SalesmanId);
            Assert.False(session.IsQualified);

            session.Refresh();
            session.Refresh();
            Assert.Equal(SalesmanId, session.SalesmanId);   // refresh không đổi salesman (§7.5)
            Assert.Equal("anon-session-1", session.AnonymousSessionId);

            session.MarkQualified();
            Assert.True(session.IsQualified);
        }

        // ── Commission ledger (D3 — immutable + reversal, SRS §17.4-17.5) ───────

        [Fact]
        public void CommissionLedgerEntry_FinalizeFlow()
        {
            var entry = NewLedgerEntry();

            Assert.Equal(entry.Id, entry.CommissionLedgerEntryId.Value);
            Assert.Equal(CommissionLedgerState.Pending, entry.State);
            Assert.Equal(1_000_000m, entry.BaseAmount);
            Assert.Equal(100_000m, entry.GrossCommissionAmount);
            Assert.Equal(10_000m, entry.TaxWithheldAmount);
            Assert.Equal(90_000m, entry.NetCommissionAmount);

            entry.MarkEarned();
            Assert.Equal(CommissionLedgerState.Earned, entry.State);
            Assert.NotNull(entry.FinalizedAt);

            entry.MarkPaid();
            Assert.Equal(CommissionLedgerState.Paid, entry.State);
            Assert.NotNull(entry.PaidAt);

            // Immutable: không thể finalize lại / paid lại
            Assert.Throws<InvalidOperationException>(() => entry.MarkEarned());
            Assert.Throws<InvalidOperationException>(() => entry.MarkPaid());
        }

        [Fact]
        public void CommissionLedgerEntry_Void_OnlyBeforePaid()
        {
            var entry = NewLedgerEntry();
            entry.Void();
            Assert.Equal(CommissionLedgerState.Voided, entry.State);

            var paid = NewLedgerEntry();
            paid.MarkEarned();
            paid.MarkPaid();
            Assert.Throws<InvalidOperationException>(() => paid.Void());   // đã paid → reversal entry mới, không void
        }

        [Fact]
        public void CommissionLedgerEntry_Validation()
        {
            Assert.Throws<ArgumentException>(() => new CommissionLedgerEntry(
                TestTenantId, CommissionSourceType.Booking, Guid.Empty, "{}", 100m, 10m, 1m, 9m));
            Assert.Throws<ArgumentException>(() => new CommissionLedgerEntry(
                TestTenantId, CommissionSourceType.Booking, SalesmanId, "{}", 100m, -10m, 1m, 9m));
        }

        // ── Payment + Invoice facts (SRS §16) ────────────────────────────────────

        [Fact]
        public void PaymentTransaction_Lifecycle()
        {
            var bookingId = Guid.NewGuid();
            var txn = new PaymentTransaction(TestTenantId, bookingId, MoneyNatureType.SecurityDeposit, 100_000m);

            Assert.Equal(txn.Id, txn.PaymentTransactionId.Value);
            Assert.Equal(PaymentStatus.Pending, txn.Status);
            Assert.Equal(MoneyNatureType.SecurityDeposit, txn.Type);

            txn.MarkPaid("vietqr-ref-1");
            Assert.Equal(PaymentStatus.Paid, txn.Status);
            Assert.NotNull(txn.CapturedAt);

            txn.MarkRefunded();
            Assert.Equal(PaymentStatus.Refunded, txn.Status);

            Assert.Throws<ArgumentException>(() => new PaymentTransaction(TestTenantId, bookingId, MoneyNatureType.SecurityDeposit, 0m));
        }

        [Fact]
        public void InvoiceIntegrationRecord_TriggerSnapshot()
        {
            var bookingId = Guid.NewGuid();
            var record = new InvoiceIntegrationRecord(TestTenantId, bookingId, InvoiceTrigger.OnCompletion);

            Assert.Equal(record.Id, record.InvoiceIntegrationRecordId.Value);
            Assert.Equal(InvoiceTrigger.OnCompletion, record.InvoiceTrigger);
            Assert.Equal(BookingInvoiceStatus.NotRequired, record.InvoiceStatus);

            record.UpdateStatus(BookingInvoiceStatus.Issued, provider: "M-invoice", reference: "INV-001");
            Assert.Equal(BookingInvoiceStatus.Issued, record.InvoiceStatus);
            Assert.NotNull(record.IssuedAt);
        }

        // ── Helpers ──────────────────────────────────────────────────────────────

        private static Booking NewBooking(DateTime? start = null, DateTime? end = null)
        {
            var s = start ?? new DateTime(2026, 10, 10, 14, 0, 0, DateTimeKind.Utc);
            var e = end ?? s.AddMinutes(60);
            return new Booking(
                TestTenantId, "VA-8F23", Guid.NewGuid(), "Massage 60m", 60, 300_000m,
                s, e, 300_000m);
        }

        private static CommissionLedgerEntry NewLedgerEntry() => new(
            TestTenantId, CommissionSourceType.Booking, SalesmanId,
            """{"ruleId":"r1","type":"Percentage","value":0.1}""",
            1_000_000m, 100_000m, 10_000m, 90_000m,
            bookingId: Guid.NewGuid(), qrId: Guid.NewGuid(),
            taxRuleVersion: "ND253-2026-07", withholdingReasonCode: "CONTRACT_LT_3M");
    }
}
