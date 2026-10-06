using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Infrastructure.Configurations
{
    /// <summary>
    /// EF Core configurations — Booking & Staff Scheduling (SRS v1.1 MVP, 2026-10-05).
    /// Data lives in Gateway PG ONLY (D1) — Không replica ShopERP SQLite.
    /// Single-Identity Pattern 100%: Id = PK, business key VO Ignore, constructor sync Id = XxxId.Value.
    /// Auto-applied via ApplyConfigurationsFromAssembly (VanAnDbContext). ShopERPDbContext không có DbSet
    /// booking → các config này không ảnh hưởng model SQLite.
    /// </summary>
    public class BookingTenantConfigConfiguration : IEntityTypeConfiguration<BookingTenantConfig>, IEntityConfiguration
    {
        public void Configure(EntityTypeBuilder<BookingTenantConfig> builder)
        {
            _ = builder.HasKey(e => e.Id);
            _ = builder.Ignore(e => e.BookingTenantConfigId);
            _ = builder.Property(e => e.IsEnabled).HasDefaultValue(false);
            _ = builder.Property(e => e.DepositFixedAmount).HasPrecision(18, 2);
            _ = builder.Property(e => e.DepositPercentage).HasPrecision(5, 2);
            _ = builder.Property(e => e.CancelReschedulePolicy).HasMaxLength(2000);
            _ = builder.Property(e => e.EinvoiceMode).HasMaxLength(64);
            _ = builder.HasIndex(e => e.TenantId).IsUnique(); // 1 row / tenant
        }
    }

    public class ServiceCategoryConfiguration : IEntityTypeConfiguration<ServiceCategory>, IEntityConfiguration
    {
        public void Configure(EntityTypeBuilder<ServiceCategory> builder)
        {
            _ = builder.HasKey(e => e.Id);
            _ = builder.Ignore(e => e.ServiceCategoryId);
            _ = builder.Property(e => e.Name).HasMaxLength(200).IsRequired();
            _ = builder.HasIndex(e => new { e.TenantId, e.Name }).IsUnique();
        }
    }

    public class AppointmentOfferingConfiguration : IEntityTypeConfiguration<AppointmentOffering>, IEntityConfiguration
    {
        public void Configure(EntityTypeBuilder<AppointmentOffering> builder)
        {
            _ = builder.HasKey(e => e.Id);
            _ = builder.Ignore(e => e.AppointmentOfferingId);
            _ = builder.Property(e => e.DisplayName).HasMaxLength(300).IsRequired();
            _ = builder.Property(e => e.DurationMinutes).IsRequired();
            _ = builder.Property(e => e.Price).HasPrecision(18, 2).IsRequired();
            _ = builder.Property(e => e.RequiredSkillCode).HasMaxLength(64);
            _ = builder.Property(e => e.Description).HasMaxLength(1000);
            _ = builder.HasIndex(e => new { e.TenantId, e.IsActive });
            _ = builder.HasIndex(e => e.CategoryId);
        }
    }

    public class AppointmentOfferingItemConfiguration : IEntityTypeConfiguration<AppointmentOfferingItem>, IEntityConfiguration
    {
        public void Configure(EntityTypeBuilder<AppointmentOfferingItem> builder)
        {
            _ = builder.HasKey(e => e.Id);
            _ = builder.Ignore(e => e.AppointmentOfferingItemId);
            _ = builder.Property(e => e.ChildName).HasMaxLength(300).IsRequired();
            _ = builder.Property(e => e.UnitPriceSnapshot).HasPrecision(18, 2);
            _ = builder.HasIndex(e => new { e.TenantId, e.OfferingId });
        }
    }

    public class AddOnConfiguration : IEntityTypeConfiguration<AddOn>, IEntityConfiguration
    {
        public void Configure(EntityTypeBuilder<AddOn> builder)
        {
            _ = builder.HasKey(e => e.Id);
            _ = builder.Ignore(e => e.AddOnId);
            _ = builder.Property(e => e.Name).HasMaxLength(200).IsRequired();
            _ = builder.Property(e => e.Price).HasPrecision(18, 2).IsRequired();
        }
    }

    public class StaffConfiguration : IEntityTypeConfiguration<Staff>, IEntityConfiguration
    {
        public void Configure(EntityTypeBuilder<Staff> builder)
        {
            _ = builder.HasKey(e => e.Id);
            _ = builder.Ignore(e => e.StaffId);
            _ = builder.Property(e => e.DisplayName).HasMaxLength(200).IsRequired();
            _ = builder.Property(e => e.Role).HasMaxLength(64);
            _ = builder.Property(e => e.AvatarUrl).HasMaxLength(500);
            _ = builder.HasIndex(e => e.StaffUserId);
            _ = builder.HasIndex(e => new { e.TenantId, e.IsActive });
        }
    }

    public class StaffServiceConfiguration : IEntityTypeConfiguration<StaffService>, IEntityConfiguration
    {
        public void Configure(EntityTypeBuilder<StaffService> builder)
        {
            _ = builder.HasKey(e => e.Id);
            _ = builder.Ignore(e => e.StaffServiceId);
            _ = builder.HasIndex(e => new { e.StaffId, e.OfferingId }).IsUnique(); // eligibility unique
            _ = builder.HasIndex(e => e.OfferingId);
        }
    }

    public class StaffWorkingScheduleConfiguration : IEntityTypeConfiguration<StaffWorkingSchedule>, IEntityConfiguration
    {
        public void Configure(EntityTypeBuilder<StaffWorkingSchedule> builder)
        {
            _ = builder.HasKey(e => e.Id);
            _ = builder.Ignore(e => e.StaffWorkingScheduleId);
            _ = builder.HasIndex(e => new { e.StaffId, e.Weekday });
        }
    }

    public class StaffScheduleOverrideConfiguration : IEntityTypeConfiguration<StaffScheduleOverride>, IEntityConfiguration
    {
        public void Configure(EntityTypeBuilder<StaffScheduleOverride> builder)
        {
            _ = builder.HasKey(e => e.Id);
            _ = builder.Ignore(e => e.StaffScheduleOverrideId);
            _ = builder.Property(e => e.Note).HasMaxLength(500);
            _ = builder.HasIndex(e => new { e.StaffId, e.Date });
        }
    }

    public class QRChannelConfiguration : IEntityTypeConfiguration<QRChannel>, IEntityConfiguration
    {
        public void Configure(EntityTypeBuilder<QRChannel> builder)
        {
            _ = builder.HasKey(e => e.Id);
            _ = builder.Ignore(e => e.QRChannelId);
            _ = builder.Property(e => e.QrTokenHash).HasMaxLength(128).IsRequired();
            _ = builder.HasIndex(e => e.QrTokenHash).IsUnique(); // unguessable token hash
            _ = builder.HasIndex(e => new { e.TenantId, e.IsActive });
        }
    }

    public class AttributionSessionConfiguration : IEntityTypeConfiguration<AttributionSession>, IEntityConfiguration
    {
        public void Configure(EntityTypeBuilder<AttributionSession> builder)
        {
            _ = builder.HasKey(e => e.Id);
            _ = builder.Ignore(e => e.AttributionSessionId);
            _ = builder.Property(e => e.AnonymousSessionId).HasMaxLength(128).IsRequired();
            // §26.3 rapid-scan guard: 1 session / (tenant, qr, anonymous session) — get-or-create không tạo vô hạn.
            _ = builder.HasIndex(e => new { e.TenantId, e.QrId, e.AnonymousSessionId }).IsUnique();
            _ = builder.HasIndex(e => e.SalesmanId);
        }
    }

    public class BookingConfiguration : IEntityTypeConfiguration<Booking>, IEntityConfiguration
    {
        public void Configure(EntityTypeBuilder<Booking> builder)
        {
            _ = builder.HasKey(e => e.Id);
            _ = builder.Ignore(e => e.BookingId);
            _ = builder.Property(e => e.PublicBookingCode).HasMaxLength(32).IsRequired();
            _ = builder.HasIndex(e => e.PublicBookingCode).IsUnique(); // opaque, non-sequential
            _ = builder.Property(e => e.CustomerDeviceId).HasMaxLength(128);
            _ = builder.Property(e => e.OfferingNameSnapshot).HasMaxLength(300).IsRequired();
            _ = builder.Property(e => e.OfferingPriceSnapshot).HasPrecision(18, 2);
            _ = builder.Property(e => e.DepositAmount).HasPrecision(18, 2);
            _ = builder.Property(e => e.EstimatedTotal).HasPrecision(18, 2).IsRequired();
            _ = builder.Property(e => e.ActualTotal).HasPrecision(18, 2);
            _ = builder.Property(e => e.CustomerNote).HasMaxLength(2000);
            _ = builder.Property(e => e.CancellationReason).HasMaxLength(1000);
            _ = builder.HasIndex(e => new { e.TenantId, e.Status });
            _ = builder.HasIndex(e => new { e.TenantId, e.StartAt });
            _ = builder.HasIndex(e => e.StaffId);
            _ = builder.HasIndex(e => new { e.TenantId, e.OrderId });
            // AC-C04 double-booking guard — final line of defense: 1 staff không thể có 2 booking
            // cùng StartAt (NULL StaffId = "bất kỳ ai" → multiple NULLs allowed).
            // RV P6 fix: filtered index CHỈ trên trạng thái ACTIVE (1-5) — booking Completed/Cancelled/
            // Rejected/NoShow không được chặn vĩnh viễn việc đặt lại slot (đúng §12 — conflict chỉ tính
            // active). Unique index đầy đủ (không filter) khiến slot staff đã xong không thể đặt lại.
            _ = builder.HasIndex(e => new { e.TenantId, e.StaffId, e.StartAt })
                .IsUnique()
                .HasDatabaseName("IX_Bookings_TenantId_StaffId_StartAt")
                .HasFilter("\"Status\" IN (1, 2, 3, 4, 5)");
            // Optimistic concurrency (§19.1) — mỗi transition bump Version; EF so WHERE Version = X.
            _ = builder.Property(e => e.Version).IsConcurrencyToken();
        }
    }

    public class BookingItemConfiguration : IEntityTypeConfiguration<BookingItem>, IEntityConfiguration
    {
        public void Configure(EntityTypeBuilder<BookingItem> builder)
        {
            _ = builder.HasKey(e => e.Id);
            _ = builder.Ignore(e => e.BookingItemId);
            _ = builder.Property(e => e.ItemType).HasMaxLength(16).IsRequired();
            _ = builder.Property(e => e.Name).HasMaxLength(300).IsRequired();
            _ = builder.Property(e => e.UnitPrice).HasPrecision(18, 2).IsRequired();
            _ = builder.HasIndex(e => new { e.TenantId, e.BookingId });
        }
    }

    public class BookingStaffAssignmentConfiguration : IEntityTypeConfiguration<BookingStaffAssignment>, IEntityConfiguration
    {
        public void Configure(EntityTypeBuilder<BookingStaffAssignment> builder)
        {
            _ = builder.HasKey(e => e.Id);
            _ = builder.Ignore(e => e.BookingStaffAssignmentId);
            _ = builder.Property(e => e.AssignmentType).HasMaxLength(16).IsRequired();
            _ = builder.HasIndex(e => new { e.TenantId, e.BookingId });
            _ = builder.HasIndex(e => new { e.TenantId, e.StaffId });
        }
    }

    public class BookingEventConfiguration : IEntityTypeConfiguration<BookingEvent>, IEntityConfiguration
    {
        public void Configure(EntityTypeBuilder<BookingEvent> builder)
        {
            _ = builder.HasKey(e => e.Id);
            _ = builder.Ignore(e => e.BookingEventId);
            _ = builder.Property(e => e.EventType).HasMaxLength(64).IsRequired();
            _ = builder.Property(e => e.ActorType).HasMaxLength(16).IsRequired();
            _ = builder.Property(e => e.Metadata).HasMaxLength(4000);
            _ = builder.HasIndex(e => new { e.TenantId, e.BookingId, e.OccurredAt });
        }
    }

    public class PaymentTransactionConfiguration : IEntityTypeConfiguration<PaymentTransaction>, IEntityConfiguration
    {
        public void Configure(EntityTypeBuilder<PaymentTransaction> builder)
        {
            _ = builder.HasKey(e => e.Id);
            _ = builder.Ignore(e => e.PaymentTransactionId);
            _ = builder.Property(e => e.Amount).HasPrecision(18, 2).IsRequired();
            _ = builder.Property(e => e.Method).HasMaxLength(16).IsRequired();
            _ = builder.Property(e => e.ProviderRef).HasMaxLength(128);
            _ = builder.HasIndex(e => new { e.TenantId, e.BookingId });
        }
    }

    public class InvoiceIntegrationRecordConfiguration : IEntityTypeConfiguration<InvoiceIntegrationRecord>, IEntityConfiguration
    {
        public void Configure(EntityTypeBuilder<InvoiceIntegrationRecord> builder)
        {
            _ = builder.HasKey(e => e.Id);
            _ = builder.Ignore(e => e.InvoiceIntegrationRecordId);
            _ = builder.Property(e => e.Provider).HasMaxLength(64);
            _ = builder.Property(e => e.Reference).HasMaxLength(128);
            _ = builder.Property(e => e.ErrorCode).HasMaxLength(128);
            _ = builder.HasIndex(e => new { e.TenantId, e.BookingId });
        }
    }

    public class CommissionRuleConfiguration : IEntityTypeConfiguration<CommissionRule>, IEntityConfiguration
    {
        public void Configure(EntityTypeBuilder<CommissionRule> builder)
        {
            _ = builder.HasKey(e => e.Id);
            _ = builder.Ignore(e => e.CommissionRuleId);
            _ = builder.Property(e => e.CommissionValue).HasPrecision(18, 2).IsRequired();
            _ = builder.HasIndex(e => new { e.TenantId, e.IsActive, e.OfferingId });
        }
    }

    public class CommissionLedgerEntryConfiguration : IEntityTypeConfiguration<CommissionLedgerEntry>, IEntityConfiguration
    {
        public void Configure(EntityTypeBuilder<CommissionLedgerEntry> builder)
        {
            _ = builder.HasKey(e => e.Id);
            _ = builder.Ignore(e => e.CommissionLedgerEntryId);
            _ = builder.Property(e => e.RuleSnapshotJson).HasMaxLength(4000).IsRequired();
            _ = builder.Property(e => e.BaseAmount).HasPrecision(18, 2).IsRequired();
            _ = builder.Property(e => e.GrossCommissionAmount).HasPrecision(18, 2).IsRequired();
            _ = builder.Property(e => e.TaxWithheldAmount).HasPrecision(18, 2).IsRequired();
            _ = builder.Property(e => e.NetCommissionAmount).HasPrecision(18, 2).IsRequired();
            _ = builder.Property(e => e.Currency).HasMaxLength(8).IsRequired();
            _ = builder.Property(e => e.TaxRuleVersion).HasMaxLength(32);
            _ = builder.Property(e => e.WithholdingReasonCode).HasMaxLength(64);
            _ = builder.HasIndex(e => new { e.TenantId, e.SourceType, e.BookingId });
            _ = builder.HasIndex(e => new { e.TenantId, e.SourceType, e.OrderId });
            _ = builder.HasIndex(e => new { e.TenantId, e.SalesmanId, e.State });
        }
    }
}
