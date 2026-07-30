using VanAn.Shared.Domain.Common;

namespace VanAn.Shared.Domain.Aggregates.SystemSettingAggregate
{
    /// <summary>
    /// SystemSetting — key-value config entity for global/platform-level settings — Sprint 7.
    /// TenantId nullable: global settings have TenantId = null (or Guid.Empty).
    /// Keys: GlobalCommerceMode, DefaultPlatformFeeRate, DefaultCommunityFundRate, DefaultDeliveryFee.
    /// Runtime toggle (no restart) — admin UI reads/writes via CommerceModeService.
    /// </summary>
    public class SystemSetting : BaseEntity, IMustHaveTenant
    {
        public string Key { get; protected set; } = string.Empty;
        public string Value { get; protected set; } = string.Empty;
        public new DateTime? UpdatedAt { get; protected set; }
        public new Guid? UpdatedBy { get; protected set; }

        protected SystemSetting() { }

        public SystemSetting(TenantId tenantId, string key, string value, Guid? updatedBy = null)
            : base(tenantId)
        {
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException("Key cannot be empty", nameof(key));
            if (key.Length > 100)
                throw new ArgumentOutOfRangeException(nameof(key), "Key max 100 chars");
            if (value == null)
                throw new ArgumentNullException(nameof(value));
            if (value.Length > 500)
                throw new ArgumentOutOfRangeException(nameof(value), "Value max 500 chars");

            Key = key;
            Value = value;
            UpdatedAt = DateTime.UtcNow;
            UpdatedBy = updatedBy;
        }

        public void Update(string value, Guid? updatedBy = null)
        {
            if (value == null)
                throw new ArgumentNullException(nameof(value));
            if (value.Length > 500)
                throw new ArgumentOutOfRangeException(nameof(value), "Value max 500 chars");

            Value = value;
            UpdatedAt = DateTime.UtcNow;
            UpdatedBy = updatedBy;
            UpdateAudit();
        }
    }
}
