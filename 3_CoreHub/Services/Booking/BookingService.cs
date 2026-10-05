using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using VanAn.CoreHub.Infrastructure;
using VanAn.Shared.Domain;
// Namespace "Booking" xung đột type Booking (lesson P1 — CS0118) → alias cho domain entity.
using BookingEntity = VanAn.Shared.Domain.Booking;

namespace VanAn.CoreHub.Services.Booking
{
    /// <summary>
    /// BookingService — create (Idempotency-Key §21.1) + state machine 9-state (§9.3) + BookingEvent audit (§23-24)
    /// + double-booking prevention (SRS §12, AC-C04).
    ///
    /// Design note create-with-staff: customer chọn staff cụ thể → slot phải được LOCK ngay tại create
    /// (SRS §12 "atomic conflict check tại create"; AC-C04 "tối đa 1 request thành công"). Domain không có
    /// trường "preferred staff" (P1) → staff được assign ngay (Status = StaffAssigned) khi khách chọn staff;
    /// "Bất kỳ ai" → PendingConfirmation, tenant assign sau (re-check conflict server-side §13).
    ///
    /// Double-booking: PG — advisory lock per (tenant, staff) + SELECT ... FOR UPDATE re-check trong transaction
    /// (precedent WalletService HR-SCALE-3) + unique index (TenantId, StaffId, StartAt) là defense-in-depth;
    /// SQLite (tests) — LINQ fallback + unique index backstop. Tối đa 1 request thành công; còn lại 409
    /// BookingConflictException với message thân thiện (§6.5).
    /// </summary>
    public sealed class BookingService(
        VanAnDbContext context,
        IAvailabilityService availabilityService,
        ILogger<BookingService> logger) : IBookingService
    {
        /// <summary>Message conflict customer-friendly (SRS §6.5) — KHÔNG hiển thị mã kỹ thuật.</summary>
        public const string ConflictMessage = "Khung giờ này vừa có người đặt. Vui lòng chọn khung giờ khác.";

        private static readonly BookingStatus[] ActiveStatuses =
        [
            BookingStatus.PendingConfirmation,
            BookingStatus.Confirmed,
            BookingStatus.StaffAssigned,
            BookingStatus.CheckedIn,
            BookingStatus.InService
        ];

        private readonly VanAnDbContext _context = context;
        private readonly IAvailabilityService _availabilityService = availabilityService;
        private readonly ILogger<BookingService> _logger = logger;

        // ── Create (Idempotency-Key §21.1 + double-booking AC-C04) ──────────

        public async Task<BookingEntity> CreateBookingAsync(
            TenantId tenantId, CreateBookingCommand command, string idempotencyKey, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(idempotencyKey))
                throw new ValidationException("Idempotency-Key là bắt buộc cho create booking.");

            // Q5 feature-flag (D5): chỉ tenant enabled mới tạo booking.
            BookingTenantConfig config = await GetConfigAsync(tenantId, ct);
            if (!config.IsEnabled)
                throw new ValidationException("Tenant chưa bật tính năng đặt lịch hẹn.");

            // §21.1 — retry cùng key → trả về booking gốc (không tạo mới).
            BookingEntity? existingByKey = await FindByIdempotencyKeyAsync(idempotencyKey, ct);
            if (existingByKey is not null)
            {
                _logger.LogInformation("Booking idempotent create hit: key={Key} booking={BookingId}", idempotencyKey, existingByKey.Id);
                return existingByKey;
            }

            // Validate offering (tenant-scoped, active) — snapshot fields.
            AppointmentOffering offering = await _context.AppointmentOfferings.IgnoreQueryFilters()
                .FirstOrDefaultAsync(o => o.TenantId == tenantId && o.Id == command.OfferingId && o.IsActive, ct)
                ?? throw new NotFoundException("Dịch vụ không tồn tại hoặc không hoạt động.");

            if (command.StartAt.Kind == DateTimeKind.Unspecified)
                command = command with { StartAt = DateTime.SpecifyKind(command.StartAt, DateTimeKind.Utc) };
            DateTime startAt = command.StartAt.ToUniversalTime();
            DateTime endAt = startAt.AddMinutes(offering.DurationMinutes);
            if (startAt <= DateTime.UtcNow)
                throw new ValidationException("Thời gian đặt phải ở tương lai.");

            // Staff cụ thể → validate skill + slot server-side TRƯỚC transaction (message thân thiện).
            if (command.StaffId is not null)
            {
                AvailabilityCheckResult check = await _availabilityService.ValidateSlotAsync(tenantId, offering.Id, command.StaffId.Value, startAt, ct);
                if (!check.IsAvailable)
                    throw new BookingConflictException(check.UnavailableReason ?? ConflictMessage);
            }

            // Add-ons — non-scheduling, active, tenant-scoped.
            List<AddOn> addOns = [];
            foreach (AddOnLine line in command.AddOns ?? [])
            {
                AddOn? addOn = await _context.AddOns.IgnoreQueryFilters()
                    .FirstOrDefaultAsync(a => a.TenantId == tenantId && a.Id == line.AddOnId && a.IsActive, ct)
                    ?? throw new ValidationException($"Add-on {line.AddOnId} không tồn tại hoặc không hoạt động.");
                addOns.Add(addOn);
            }
            decimal estimatedTotal = offering.Price + addOns.Sum(a => a.Price);

            BookingEntity booking = null!;
            await _context.ExecuteAtomicAsync(async () =>
            {
                await using var tx = await _context.BeginTransactionAsync(ct);
                try
                {
                    // PG: serialize tạo booking cùng (tenant, staff) — sau lock re-check conflict là authoritative.
                    if (command.StaffId is not null)
                        await AcquireStaffLockAsync(tenantId, command.StaffId.Value, ct);

                    if (command.StaffId is not null)
                    {
                        BookingEntity? conflict = await FindConflictAsync(tenantId, command.StaffId.Value, startAt, endAt, excludeBookingId: null, ct);
                        if (conflict is not null)
                            throw new BookingConflictException(ConflictMessage);
                    }

                    booking = new BookingEntity(
                        tenantId, GeneratePublicCode(), offering.Id, offering.DisplayName,
                        offering.DurationMinutes, offering.Price, startAt, endAt, estimatedTotal,
                        command.CustomerId, command.CustomerDeviceId, command.AttributionId);

                    booking.SetCustomerNote(command.CustomerNote);

                    // Deposit policy từ BookingTenantConfig (§16.1 Screen 3 — "Đặt cọc số tiền đã cấu hình").
                    // Phân loại tiền: SECURITY_DEPOSIT (đặt cọc/bảo đảm thực hiện §16.2) — P3 financial refine theo tax profile.
                    if (config.DepositPolicy == DepositPolicy.Fixed && config.DepositFixedAmount is > 0)
                    {
                        booking.SetDepositRequirement(true, MoneyNatureType.SecurityDeposit, config.DepositFixedAmount);
                    }
                    else if (config.DepositPolicy == DepositPolicy.Percentage && config.DepositPercentage is > 0)
                    {
                        decimal amount = Math.Round(estimatedTotal * config.DepositPercentage.Value / 100m, 0, MidpointRounding.AwayFromZero);
                        booking.SetDepositRequirement(true, MoneyNatureType.SecurityDeposit, amount);
                    }

                    // Khách chọn staff cụ thể → assign ngay (slot locked — design note ở header).
                    if (command.StaffId is not null)
                    {
                        booking.AssignStaff(command.StaffId.Value);
                        _context.BookingStaffAssignments.Add(new BookingStaffAssignment(
                            tenantId, booking.Id, command.StaffId.Value, assignedBy: null, "ASSIGN"));
                        _context.BookingEvents.Add(new BookingEvent(
                            tenantId, booking.Id, "BookingStaffAssigned", "CUSTOMER", command.CustomerId));
                    }

                    _context.Bookings.Add(booking);
                    _context.BookingItems.Add(new BookingItem(
                        tenantId, booking.Id, "OFFERING", offering.DisplayName, 1,
                        offering.Price, offering.DurationMinutes, offering.Id));
                    foreach (AddOn addOn in addOns)
                    {
                        _context.BookingItems.Add(new BookingItem(
                            tenantId, booking.Id, "ADD_ON", addOn.Name, 1, addOn.Price, 0, addOnId: addOn.Id));
                    }
                    _context.BookingEvents.Add(new BookingEvent(
                        tenantId, booking.Id, "BookingCreated", "CUSTOMER", command.CustomerId));
                    _context.BookingIdempotencyRecords.Add(new BookingIdempotencyRecord(idempotencyKey, tenantId.Value, booking.Id));

                    _ = await _context.SaveChangesAsync(ct);
                    await tx.CommitAsync();
                }
                catch (DbUpdateException ex) when (IsUniqueViolation(ex))
                {
                    await tx.RollbackAsync();
                    // Concurrent retry cùng Idempotency-Key → trả booking gốc; còn lại = conflict guard (unique index).
                    BookingEntity? byKey = await FindByIdempotencyKeyAsync(idempotencyKey, ct);
                    if (byKey is not null)
                    {
                        booking = byKey;
                        return;
                    }
                    throw new BookingConflictException(ConflictMessage, ex);
                }
                catch (DbUpdateConcurrencyException ex)
                {
                    await tx.RollbackAsync();
                    throw new BookingConflictException(ConflictMessage, ex);
                }
                catch
                {
                    await tx.RollbackAsync();
                    throw;
                }
            });

            _logger.LogInformation("Booking created: {BookingId} code={Code} tenant={TenantId} staff={StaffId}",
                booking.Id, booking.PublicBookingCode, tenantId.Value, command.StaffId);
            return booking;
        }

        // ── State machine transitions (idempotent §21.2) ────────────────────

        public Task<BookingEntity> ConfirmAsync(TenantId tenantId, Guid bookingId, Guid? actorId = null, CancellationToken ct = default)
            => TransitionAsync(tenantId, bookingId, BookingStatus.Confirmed, "BookingConfirmed", "TENANT", actorId, b => b.Confirm(), ct);

        public Task<BookingEntity> RejectAsync(TenantId tenantId, Guid bookingId, string? reason, Guid? actorId = null, CancellationToken ct = default)
            => TransitionAsync(tenantId, bookingId, BookingStatus.Rejected, "BookingRejected", "TENANT", actorId, b => b.Reject(reason), ct);

        public Task<BookingEntity> CancelAsync(TenantId tenantId, Guid bookingId, string? reason, Guid? actorId = null, CancellationToken ct = default)
            => TransitionAsync(tenantId, bookingId, BookingStatus.Cancelled, "BookingCancelled", "CUSTOMER", actorId, b => b.Cancel(reason), ct);

        public Task<BookingEntity> CheckInAsync(TenantId tenantId, Guid bookingId, Guid? actorId = null, CancellationToken ct = default)
            => TransitionAsync(tenantId, bookingId, BookingStatus.CheckedIn, "BookingCheckedIn", "TENANT", actorId, b => b.CheckIn(), ct);

        public Task<BookingEntity> StartServiceAsync(TenantId tenantId, Guid bookingId, Guid? actorId = null, CancellationToken ct = default)
            => TransitionAsync(tenantId, bookingId, BookingStatus.InService, "BookingStarted", "TENANT", actorId, b => b.StartService(), ct);

        public Task<BookingEntity> CompleteAsync(TenantId tenantId, Guid bookingId, decimal actualTotal, Guid? actorId = null, CancellationToken ct = default)
            => TransitionAsync(tenantId, bookingId, BookingStatus.Completed, "BookingCompleted", "TENANT", actorId, b => b.Complete(actualTotal), ct);

        public Task<BookingEntity> MarkNoShowAsync(TenantId tenantId, Guid bookingId, string? reason, Guid? actorId = null, CancellationToken ct = default)
            => TransitionAsync(tenantId, bookingId, BookingStatus.NoShow, "BookingNoShow", "TENANT", actorId, b => b.MarkNoShow(reason), ct);

        // ── Assign / change staff (§21.3 idempotent + §13 re-check) ─────────

        public async Task<BookingEntity> AssignStaffAsync(TenantId tenantId, Guid bookingId, Guid staffId, Guid? actorId = null, CancellationToken ct = default)
            => await AssignOrChangeStaffAsync(tenantId, bookingId, staffId, actorId, ct);

        public async Task<BookingEntity> ChangeStaffAsync(TenantId tenantId, Guid bookingId, Guid staffId, Guid? actorId = null, CancellationToken ct = default)
            => await AssignOrChangeStaffAsync(tenantId, bookingId, staffId, actorId, ct);

        // ── Reads ───────────────────────────────────────────────────────────

        public async Task<BookingStatusDto?> GetPublicStatusAsync(string publicBookingCode, CancellationToken ct = default)
        {
            BookingEntity? booking = await _context.Bookings.IgnoreQueryFilters()
                .FirstOrDefaultAsync(b => b.PublicBookingCode == publicBookingCode, ct);
            return booking is null ? null : ToStatusDto(booking);
        }

        public async Task<IReadOnlyList<BookingEntity>> GetQueueAsync(
            TenantId tenantId, BookingStatus? status = null, DateTime? from = null, DateTime? to = null, CancellationToken ct = default)
        {
            IQueryable<BookingEntity> query = _context.Bookings.IgnoreQueryFilters().Where(b => b.TenantId == tenantId);
            if (status is not null)
                query = query.Where(b => b.Status == status);
            if (from is not null)
                query = query.Where(b => b.StartAt >= from);
            if (to is not null)
                query = query.Where(b => b.StartAt <= to);
            return await query.OrderBy(b => b.StartAt).ToListAsync(ct);
        }

        public async Task<BookingEntity?> GetBookingAsync(TenantId tenantId, Guid bookingId, CancellationToken ct = default)
            => await _context.Bookings.IgnoreQueryFilters()
                .FirstOrDefaultAsync(b => b.TenantId == tenantId && b.Id == bookingId, ct);

        // ── BookingTenantConfig (Q5 feature-flag + deposit policy) ──────────

        public async Task<BookingTenantConfig> GetConfigAsync(TenantId tenantId, CancellationToken ct = default)
        {
            BookingTenantConfig? config = await _context.BookingTenantConfigs.IgnoreQueryFilters()
                .FirstOrDefaultAsync(c => c.TenantId == tenantId, ct);
            if (config is null)
            {
                // Get-or-create với defaults (pattern VaIIeTenantConfig) — IsEnabled default FALSE (Q5).
                config = new BookingTenantConfig(tenantId);
                _context.BookingTenantConfigs.Add(config);
                _ = await _context.SaveChangesAsync(ct);
            }
            return config;
        }

        public async Task<BookingTenantConfig> UpdateConfigAsync(
            TenantId tenantId, bool isEnabled, DepositPolicy depositPolicy,
            decimal? depositFixedAmount, decimal? depositPercentage,
            string? cancelReschedulePolicy, string? einvoiceMode, CancellationToken ct = default)
        {
            BookingTenantConfig config = await GetConfigAsync(tenantId, ct);
            if (isEnabled && !config.IsEnabled)
                config.Enable();
            else if (!isEnabled && config.IsEnabled)
                config.Disable();
            config.UpdateDepositPolicy(depositPolicy, depositFixedAmount, depositPercentage);
            config.UpdatePolicyText(cancelReschedulePolicy, einvoiceMode);
            _ = await _context.SaveChangesAsync(ct);
            _logger.LogInformation("BookingTenantConfig updated: tenant={TenantId} enabled={Enabled} deposit={Policy}",
                tenantId.Value, config.IsEnabled, config.DepositPolicy);
            return config;
        }

        // ── Helpers ─────────────────────────────────────────────────────────

        private async Task<BookingEntity> TransitionAsync(
            TenantId tenantId, Guid bookingId, BookingStatus targetStatus, string eventType,
            string actorType, Guid? actorId, Action<BookingEntity> apply, CancellationToken ct)
        {
            BookingEntity booking = await GetBookingOrThrowAsync(tenantId, bookingId, ct);

            // Idempotent (§21.2): đã ở trạng thái đích → trả về hiện tại, không tạo event mới.
            if (booking.Status == targetStatus)
                return booking;

            try
            {
                apply(booking);
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning("Invalid booking transition {Event} booking={BookingId} status={Status}: {Message}",
                    eventType, bookingId, booking.Status, ex.Message);
                throw new ValidationException(ex.Message);
            }

            _context.BookingEvents.Add(new BookingEvent(tenantId, bookingId, eventType, actorType, actorId));
            try
            {
                _ = await _context.SaveChangesAsync(ct);
            }
            catch (DbUpdateConcurrencyException)
            {
                // Optimistic concurrency (§19.1): ai đó đã transition trước — reload + đánh giá idempotent.
                BookingEntity fresh = await GetBookingOrThrowAsync(tenantId, bookingId, ct);
                if (fresh.Status == targetStatus)
                    return fresh;
                throw new BookingConflictException("Booking vừa được cập nhật bởi yêu cầu khác. Vui lòng thử lại.");
            }

            _logger.LogInformation("Booking transition {Event}: booking={BookingId} tenant={TenantId}", eventType, bookingId, tenantId.Value);
            return booking;
        }

        private async Task<BookingEntity> AssignOrChangeStaffAsync(
            TenantId tenantId, Guid bookingId, Guid staffId, Guid? actorId, CancellationToken ct)
        {
            BookingEntity booking = await GetBookingOrThrowAsync(tenantId, bookingId, ct);

            // §21.3 idempotent: assign cùng staff đã gán → không tạo assignment record trùng.
            if (booking.StaffId == staffId && booking.Status == BookingStatus.StaffAssigned)
                return booking;

            // §13: manual override luôn re-check conflict server-side (message thân thiện trước).
            AvailabilityCheckResult check = await _availabilityService.ValidateSlotAsync(
                tenantId, booking.OfferingId, staffId, booking.StartAt, ct);
            if (!check.IsAvailable)
                throw new BookingConflictException(check.UnavailableReason ?? ConflictMessage);

            BookingEntity result = null!;
            await _context.ExecuteAtomicAsync(async () =>
            {
                await using var tx = await _context.BeginTransactionAsync(ct);
                try
                {
                    await AcquireStaffLockAsync(tenantId, staffId, ct);

                    // Re-check conflict authoritative trong transaction (loại booking hiện tại).
                    BookingEntity? conflict = await FindConflictAsync(tenantId, staffId, booking.StartAt, booking.EndAt, booking.Id, ct);
                    if (conflict is not null)
                        throw new BookingConflictException(ConflictMessage);

                    // Reload fresh sau lock (tránh stale) rồi apply.
                    BookingEntity fresh = await GetBookingOrThrowAsync(tenantId, bookingId, ct);
                    if (fresh.StaffId == staffId && fresh.Status == BookingStatus.StaffAssigned)
                    {
                        result = fresh;
                        await tx.CommitAsync();
                        return;
                    }

                    bool isReassign = fresh.Status == BookingStatus.StaffAssigned;
                    if (isReassign)
                        fresh.ChangeStaff(staffId);
                    else
                        fresh.AssignStaff(staffId);

                    _context.BookingStaffAssignments.Add(new BookingStaffAssignment(
                        tenantId, bookingId, staffId, actorId, isReassign ? "REASSIGN" : "ASSIGN"));
                    _context.BookingEvents.Add(new BookingEvent(
                        tenantId, bookingId, isReassign ? "BookingStaffReassigned" : "BookingStaffAssigned", "TENANT", actorId));

                    _ = await _context.SaveChangesAsync(ct);
                    await tx.CommitAsync();
                    result = fresh;
                }
                catch
                {
                    await tx.RollbackAsync();
                    throw;
                }
            });

            _logger.LogInformation("Booking staff {Action}: booking={BookingId} staff={StaffId}", result.StaffId == staffId ? "assigned" : "reassigned", bookingId, staffId);
            return result;
        }

        private async Task<BookingEntity> GetBookingOrThrowAsync(TenantId tenantId, Guid bookingId, CancellationToken ct)
            => await GetBookingAsync(tenantId, bookingId, ct)
               ?? throw new NotFoundException("Booking không tồn tại trong tenant này.");

        private async Task<BookingEntity?> FindByIdempotencyKeyAsync(string idempotencyKey, CancellationToken ct)
        {
            BookingIdempotencyRecord? record = await _context.BookingIdempotencyRecords
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(r => r.IdempotencyKey == idempotencyKey, ct);
            if (record is null)
                return null;
            return await _context.Bookings.IgnoreQueryFilters()
                .FirstOrDefaultAsync(b => b.Id == record.BookingId, ct);
        }

        /// <summary>PG: advisory lock per (tenant, staff) — serialize create/assign cùng staff (pattern WalletService).</summary>
        private async Task AcquireStaffLockAsync(TenantId tenantId, Guid staffId, CancellationToken ct)
        {
            bool isPostgres = _context.ProviderName.Contains("PostgreSQL") || _context.ProviderName.Contains("Npgsql");
            if (isPostgres)
            {
                // CTE-wrapped: DO blocks không nhận bind parameters (lesson WalletService).
                _ = await _context.Database.ExecuteSqlRawAsync(
                    "WITH l AS (SELECT pg_advisory_xact_lock(hashtextextended({0}, 0))) SELECT 1",
                    $"{tenantId.Value}:{staffId}", ct);
            }
        }

        /// <summary>
        /// Conflict query trong transaction — PG: SELECT ... FOR UPDATE (locks rows cho concurrent-safety);
        /// SQLite (tests): LINQ fallback (database-level lock) + unique index backstop.
        /// Active statuses: PendingConfirmation..InService (Completed/Cancelled/Rejected/NoShow không chặn).
        /// </summary>
        private async Task<BookingEntity?> FindConflictAsync(
            TenantId tenantId, Guid staffId, DateTime startAt, DateTime endAt, Guid? excludeBookingId, CancellationToken ct)
        {
            bool isPostgres = _context.ProviderName.Contains("PostgreSQL") || _context.ProviderName.Contains("Npgsql");

            IQueryable<BookingEntity> query;
            if (isPostgres)
            {
                query = _context.Bookings
                    .FromSqlRaw(
                        "SELECT * FROM \"Bookings\" WHERE \"TenantId\" = {0} AND \"StaffId\" = {1} " +
                        "AND \"StartAt\" < {2} AND \"EndAt\" > {3} AND \"Status\" IN (1,2,3,4,5) FOR UPDATE",
                        tenantId.Value, staffId, endAt, startAt)
                    .IgnoreQueryFilters();
            }
            else
            {
                query = _context.Bookings.IgnoreQueryFilters()
                    .Where(b => b.TenantId == tenantId
                                && b.StaffId == staffId
                                && ActiveStatuses.Contains(b.Status)
                                && b.StartAt < endAt
                                && b.EndAt > startAt);
            }

            if (excludeBookingId is not null)
                query = query.Where(b => b.Id != excludeBookingId);

            return await query.FirstOrDefaultAsync(ct);
        }

        private static bool IsUniqueViolation(DbUpdateException ex)
        {
            // SQLite: SQLITE_CONSTRAINT (19); PostgreSQL: unique_violation (23505). Kiểm tra chuỗi inner.
            for (Exception? e = ex; e is not null; e = e.InnerException)
            {
                string msg = e.Message;
                if (msg.Contains("UNIQUE constraint failed", StringComparison.OrdinalIgnoreCase)
                    || msg.Contains("duplicate key", StringComparison.OrdinalIgnoreCase)
                    || msg.Contains("23505", StringComparison.Ordinal)
                    || msg.Contains("SQLite Error 19", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;
        }

        private static string GeneratePublicCode()
            => Guid.NewGuid().ToString("N")[..16].ToUpperInvariant();

        private static BookingStatusDto ToStatusDto(BookingEntity booking)
            => new(
                booking.Id,
                booking.PublicBookingCode,
                booking.Status,
                booking.SubState,
                booking.StaffId,
                booking.OfferingNameSnapshot,
                booking.StartAt,
                booking.EndAt,
                booking.EstimatedTotal,
                booking.PaymentStatus,
                booking.InvoiceStatus,
                booking.Version,
                booking.CompletedAt,
                booking.OrderId);
    }
}
