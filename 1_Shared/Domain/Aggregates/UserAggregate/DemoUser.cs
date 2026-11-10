using VanAn.Shared.Domain.Common;

namespace VanAn.Shared.Domain.Aggregates.UserAggregate
{
    /// <summary>
    /// DemoUser Aggregate Root â€” Rich Domain Model.
    /// Replaces the anemic <see cref="VanAn.Shared.Domain.DemoUser"/> class (marked [Obsolete] in Domain.cs).
    /// Wave 6: God File split + lifecycle management.
    /// </summary>
    public class DemoUser : AggregateRoot
    {
        // â”€â”€ Identity & Profile â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        public string Username { get; private set; } = string.Empty;
        public string PasswordHash { get; private set; } = string.Empty;
        public string DisplayName { get; private set; } = string.Empty;
        public string? Email { get; private set; }
        public UserRole Role { get; private set; } = UserRole.Staff;
        public bool IsActive { get; private set; } = true;

        // ── ID-P1 (2026-11-10, user duyệt): Trust/contact fields ────────────
        // PhoneNumber plaintext trong domain — EF encrypt at rest (EncryptedStringConverter).
        public string? PhoneNumber { get; private set; }
        public DateTime? PhoneVerifiedAt { get; private set; }
        // Provenance (REVIEW B7): "email" | "zns" | "sms" | "otp" | "manual" — PG-only, không sync SQLite.
        public string? VerifiedChannel { get; private set; }
        public UserTrustLevel TrustLevel { get; private set; } = UserTrustLevel.None;

        // ── MFA-1 (2026-11-10, user duyệt): Admin MFA fields ─────────────────
        public bool MfaEnabled { get; private set; }
        public string? MfaSecret { get; private set; }   // TOTP secret — EF encrypt at rest
        public string? TrustedDeviceHash { get; private set; } // SHA-256 token hash — 1 device MVP

        // EF Core requires parameterless constructor
        private DemoUser() { }

        public DemoUser(TenantId tenantId, string username, string passwordHash, string displayName, UserRole role)
            : base(tenantId)
        {
            if (tenantId is null || tenantId.Value == Guid.Empty)
                throw new ArgumentException("TenantId cannot be empty.", nameof(tenantId));
            if (string.IsNullOrWhiteSpace(username))
                throw new ArgumentException("Username cannot be empty.", nameof(username));
            if (string.IsNullOrWhiteSpace(passwordHash))
                throw new ArgumentException("PasswordHash cannot be empty.", nameof(passwordHash));
            if (string.IsNullOrWhiteSpace(displayName))
                throw new ArgumentException("DisplayName cannot be empty.", nameof(displayName));

            Username = username;
            PasswordHash = passwordHash;
            DisplayName = displayName;
            Role = role;
            IsActive = true;
        }

        // â”€â”€ Factory â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        /// <summary>Creates a new user and raises <see cref="UserCreatedEvent"/>.</summary>
        public static DemoUser Create(TenantId tenantId, string username, string passwordHash, string displayName, UserRole role)
        {
            var user = new DemoUser(tenantId, username, passwordHash, displayName, role);
            user.AddDomainEvent(new UserCreatedEvent(
                user.Id,
                tenantId.Value,
                username,
                displayName,
                role,
                DateTime.UtcNow));
            return user;
        }

        // â”€â”€ Domain Methods â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        /// <summary>Deactivate this user. Domain-level guard only checks current state.</summary>
        public void Deactivate()
        {
            if (!IsActive)
                throw new InvalidOperationException("User is already deactivated.");

            IsActive = false;
            AddDomainEvent(new UserDeactivatedEvent(Id, TenantId.Value, DateTime.UtcNow));
        }

        /// <summary>Reactivate a deactivated user.</summary>
        public void Reactivate()
        {
            if (IsActive)
                throw new InvalidOperationException("User is already active.");

            IsActive = true;
        }

        /// <summary>Change password hash. The hash must already be computed by the service layer.</summary>
        public void ChangePassword(string newBcryptHash)
        {
            if (string.IsNullOrWhiteSpace(newBcryptHash))
                throw new ArgumentException("Password hash cannot be empty.", nameof(newBcryptHash));

            PasswordHash = newBcryptHash;
            AddDomainEvent(new UserPasswordChangedEvent(Id, TenantId.Value, DateTime.UtcNow));
        }

        /// <summary>Assign a new role to the user.</summary>
        public void AssignRole(UserRole newRole)
        {
            var previousRole = Role;
            Role = newRole;
            AddDomainEvent(new UserRoleChangedEvent(Id, TenantId.Value, previousRole, newRole, DateTime.UtcNow));
        }

        /// <summary>Update display profile only.</summary>
        public void UpdateProfile(string displayName)
        {
            if (string.IsNullOrWhiteSpace(displayName))
                throw new ArgumentException("DisplayName cannot be empty.", nameof(displayName));

            DisplayName = displayName;
        }

        // ── ID-P1: Trust/contact methods (2026-11-10) ────────────────────────

        /// <summary>
        /// Set contact info (owner verified). Normalize phone digits (+84/84 → 0).
        /// Phone đổi → reset PhoneVerifiedAt + VerifiedChannel (liên hệ cũ không còn giá trị).
        /// Email set chỉ khi khác null — không clear email hiện có bằng null.
        /// </summary>
        public void SetContactInfo(string? phone, string? email)
        {
            if (!string.IsNullOrWhiteSpace(phone))
            {
                var normalized = NormalizePhone(phone);
                if (normalized != PhoneNumber)
                {
                    PhoneNumber = normalized;
                    PhoneVerifiedAt = null;
                    VerifiedChannel = null;
                }
            }
            if (!string.IsNullOrWhiteSpace(email))
                Email = email;
        }

        /// <summary>Mark phone as verified — timestamp + channel + nâng trust lên ít nhất ContactVerified (không downgrade).</summary>
        public void MarkPhoneVerified(string? channel = null)
        {
            if (string.IsNullOrWhiteSpace(PhoneNumber))
                throw new InvalidOperationException("PhoneNumber must be set before marking verified.");

            PhoneVerifiedAt = DateTime.UtcNow;
            if (!string.IsNullOrWhiteSpace(channel))
                VerifiedChannel = channel;
            ApplyTrustLevel(UserTrustLevel.ContactVerified);
        }

        /// <summary>Tenant Active = chủ thể kinh doanh thật (L3). Idempotent.</summary>
        public void MarkBusinessVerified()
        {
            TrustLevel = UserTrustLevel.BusinessVerified;
        }

        /// <summary>
        /// REVIEW B3 (2026-11-10, user duyệt): tenant mất hiệu lực (reject/deactivate/timeout)
        /// → BusinessVerified → ContactVerified (KHÔNG xuống None — liên hệ vẫn đã verify).
        /// No-op nếu đang None/ContactVerified.
        /// </summary>
        public void RevokeBusinessVerification()
        {
            if (TrustLevel == UserTrustLevel.BusinessVerified)
                TrustLevel = UserTrustLevel.ContactVerified;
        }

        /// <summary>Chỉ tăng hoặc giữ — level thấp hơn hiện tại → no-op (dùng cho sync subscriber).</summary>
        public void ApplyTrustLevel(UserTrustLevel level)
        {
            if (level > TrustLevel)
                TrustLevel = level;
        }

        // ── MFA-1: Admin MFA methods (2026-11-10) ────────────────────────────

        /// <summary>Bật MFA — lưu TOTP secret (đã base32 từ service). Chỉ bật 1 lần.</summary>
        public void EnableMfa(string secret)
        {
            if (string.IsNullOrWhiteSpace(secret))
                throw new ArgumentException("MFA secret cannot be empty.", nameof(secret));
            if (MfaEnabled)
                throw new InvalidOperationException("MFA is already enabled.");

            MfaSecret = secret;
            MfaEnabled = true;
            UpdateAudit();
        }

        /// <summary>Tắt MFA — clear secret. Idempotent.</summary>
        public void DisableMfa()
        {
            MfaSecret = null;
            MfaEnabled = false;
            UpdateAudit();
        }

        /// <summary>Lưu hash token trusted-device (1 device MVP — MFA-1).</summary>
        public void SetTrustedDevice(string tokenHash)
        {
            TrustedDeviceHash = tokenHash;
        }

        /// <summary>Xóa trusted device (logout hoặc "quên thiết bị").</summary>
        public void ClearTrustedDevice()
        {
            TrustedDeviceHash = null;
        }

        private static string NormalizePhone(string phone)
        {
            var digits = new string(phone.Where(char.IsDigit).ToArray());
            if (digits.StartsWith("84", StringComparison.Ordinal) && digits.Length == 11)
                digits = "0" + digits[2..];
            return digits;
        }
    }
}
