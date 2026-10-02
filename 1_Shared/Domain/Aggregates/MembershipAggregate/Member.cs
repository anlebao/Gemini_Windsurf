using VanAn.Shared.Domain.Common;

namespace VanAn.Shared.Domain.Aggregates.MembershipAggregate
{
    /// <summary>
    /// Membership Infrastructure (2026-09-29): Thành viên HTX (Sổ đăng ký thành viên điện tử — SRS §17).
    /// Được tạo từ MembershipApplication khi HTX duyệt (service layer — D2).
    ///
    /// Polymorphic party (Data Integrity Contract — master_plan mục 6):
    /// - MemberCustomerId (FK Customers.Id) — cá nhân (customer/salesman/shipper...).
    /// - MemberTenantId (FK Tenants.Id) — HKD/DN với tư cách pháp nhân.
    /// - CHECK constraint (exactly-one) ở EF config Phase 2; guard ở factory.
    ///
    /// Lifecycle: Active → Suspended ⇄ Active · Active → Resigned | Terminated.
    /// CẤM xóa member record vật lý — giữ lịch sử pháp lý/audit (SRS §7).
    /// </summary>
    public class Member : AggregateRoot
    {
        // ── Party (polymorphic — exactly-one, CHECK constraint ở EF config) ──
        public Guid? MemberCustomerId { get; private set; }   // FK Customers.Id — cá nhân
        // FK Tenants.Id (PK là TenantId VO — pattern Tenant.PredecessorTenantId) — HKD/DN
        public TenantId? MemberTenantId { get; private set; }

        public MembershipType MembershipType { get; private set; }
        public string MemberNumber { get; private set; } = string.Empty;
        public MemberStatus Status { get; private set; } = MemberStatus.Active;

        // Góp vốn cam kết (snapshot từ MembershipApplication — user directive 2026-10-02).
        // HTX xác nhận đã thu qua CapitalFeeStatus (MVP chỉ theo dõi status — SRS §16).
        public decimal? CapitalContributionAmount { get; private set; }

        // ── Registry timestamps (SRS §17.2) ───────────────────────────────────
        public DateTime JoinedAt { get; private set; }
        public DateTime? ApprovedAt { get; private set; }
        public DateTime? EffectiveAt { get; private set; }
        public DateTime? TerminatedAt { get; private set; }
        public string? StatusReason { get; private set; }

        // EF Core requires parameterless constructor
        private Member() { }

        /// <summary>
        /// Factory: kích hoạt thành viên chính thức (status=Active).
        /// Guard: exactly-one party (customer XOR tenant) + self-membership cấm.
        /// Raise MemberActivatedEvent.
        /// </summary>
        /// <param name="htxTenantId">HTX chủ quản (BaseEntity.TenantId — FK Tenants.Id).</param>
        /// <param name="memberNumber">VD: MEM-HTX-Q1-000182 (SRS §18) — sinh ở service layer.</param>
        /// <param name="memberCustomerId">FK Customers.Id — nếu member là cá nhân.</param>
        /// <param name="memberTenantId">FK Tenants.Id — nếu member là HKD/DN.</param>
        public static Member CreateActive(
            TenantId htxTenantId,
            string memberNumber,
            MembershipType membershipType,
            Guid? memberCustomerId = null,
            TenantId? memberTenantId = null,
            decimal? capitalContributionAmount = null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(memberNumber);

            bool hasCustomer = memberCustomerId is not null && memberCustomerId.Value != Guid.Empty;
            bool hasTenant = memberTenantId is not null && memberTenantId.Value != Guid.Empty;
            if (hasCustomer == hasTenant)
                throw new InvalidOperationException(
                    "Member must reference exactly one party: either a customer (individual) or a tenant (business), not both or neither.");
            if (hasTenant && memberTenantId == htxTenantId)
                throw new InvalidOperationException("A tenant cannot be a member of its own HTX (self-membership).");

            var member = new Member
            {
                MemberCustomerId = hasCustomer ? memberCustomerId : null,
                MemberTenantId = hasTenant ? memberTenantId : null,
                MembershipType = membershipType,
                MemberNumber = memberNumber,
                Status = MemberStatus.Active,
                JoinedAt = DateTime.UtcNow,
                ApprovedAt = DateTime.UtcNow,
                EffectiveAt = DateTime.UtcNow,
                CapitalContributionAmount = capitalContributionAmount
            };
            member.SetTenantId(htxTenantId);
            member.AddDomainEvent(new MemberActivatedEvent(
                htxTenantId.Value, member.Id, memberNumber,
                hasCustomer ? memberCustomerId : null,
                membershipType, DateTime.UtcNow));
            return member;
        }

        // ── Lifecycle transitions (guard-based) ───────────────────────────────

        /// <summary>Active → Suspended (tạm đình chỉ). Chỉ từ Active.</summary>
        public void Suspend(string reason)
        {
            if (Status != MemberStatus.Active)
                throw new InvalidOperationException($"Cannot suspend member in status {Status}. Only Active members can be suspended.");
            ArgumentException.ThrowIfNullOrWhiteSpace(reason);

            ChangeStatus(MemberStatus.Suspended, reason);
        }

        /// <summary>Suspended → Active (khôi phục). Chỉ từ Suspended.</summary>
        public void Reactivate()
        {
            if (Status != MemberStatus.Suspended)
                throw new InvalidOperationException($"Cannot reactivate member in status {Status}. Only Suspended members can be reactivated.");

            ChangeStatus(MemberStatus.Active, null);
        }

        /// <summary>Active/Suspended → Resigned (tự rút theo Điều lệ).</summary>
        public void Resign(string reason)
        {
            if (Status is not (MemberStatus.Active or MemberStatus.Suspended))
                throw new InvalidOperationException($"Cannot resign member in status {Status}. Only Active or Suspended members can resign.");

            ChangeStatus(MemberStatus.Resigned, reason);
        }

        /// <summary>Active/Suspended → Terminated (chấm dứt tư cách theo Điều lệ).</summary>
        public void Terminate(string reason)
        {
            if (Status is not (MemberStatus.Active or MemberStatus.Suspended))
                throw new InvalidOperationException($"Cannot terminate member in status {Status}. Only Active or Suspended members can be terminated.");

            ChangeStatus(MemberStatus.Terminated, reason);
        }

        private void ChangeStatus(MemberStatus newStatus, string? reason)
        {
            var oldStatus = Status;
            Status = newStatus;
            StatusReason = reason;
            if (newStatus == MemberStatus.Terminated)
                TerminatedAt = DateTime.UtcNow;
            UpdateAudit();
            AddDomainEvent(new MemberStatusChangedEvent(
                TenantId.Value, Id, oldStatus, newStatus, reason, DateTime.UtcNow));
        }

        // ── Query helpers ─────────────────────────────────────────────────────
        public bool IsActive() => Status == MemberStatus.Active;
        public bool IsSuspended() => Status == MemberStatus.Suspended;
        public bool IsResigned() => Status == MemberStatus.Resigned;
        public bool IsTerminated() => Status == MemberStatus.Terminated;
    }
}
