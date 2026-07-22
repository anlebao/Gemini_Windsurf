using VanAn.Shared.Domain.Common;

namespace VanAn.Shared.Domain
{
    /// <summary>
    /// Phase 1 (Multi-VPS Checkout): Represents a ShopERP hosting instance.
    /// Platform-level routing entity â€” maps tenants to the VPS that hosts their ShopERP.
    /// Not a business aggregate; no business key VO. Follows Single-Identity Pattern (Id = PK only).
    /// TenantId is set to a platform sentinel (Guid.Empty) â€” this entity is NOT tenant-scoped.
    /// Query with <c>IgnoreQueryFilters()</c> to bypass the multi-tenancy query filter.
    /// </summary>
    public class ShopInstance : BaseEntity
    {
        /// <summary>Base URL of the ShopERP instance (e.g., "http://shoperp:5003").</summary>
        public string BaseUrl { get; private set; } = string.Empty;

        /// <summary>Human-readable label for admin UI (e.g., "VPS-1 HCM").</summary>
        public string Label { get; private set; } = string.Empty;

        /// <summary>Maximum tenants this instance can host. Default: 50.</summary>
        public int MaxTenants { get; private set; } = 50;

        /// <summary>Whether this instance is active and accepting tenant assignments.</summary>
        public bool IsActive { get; private set; } = true;

        /// <summary>Optional health check endpoint (if different from BaseUrl/health).</summary>
        public string? HealthCheckUrl { get; private set; }

        /// <summary>Timestamp of the last health check.</summary>
        public DateTime? LastHealthCheck { get; private set; }

        /// <summary>Health status: "Healthy", "Degraded", "Unhealthy", "Unknown" (default).</summary>
        public string HealthStatus { get; private set; } = "Unknown";

        // EF Core materialization
        private ShopInstance() { }

        /// <summary>Factory: create a new ShopInstance.</summary>
        public ShopInstance(string baseUrl, string label, int maxTenants = 50, string? healthCheckUrl = null)
            : base(new TenantId(Guid.Empty)) // platform-level entity, not tenant-scoped
        {
            if (string.IsNullOrWhiteSpace(baseUrl))
                throw new ArgumentException("BaseUrl cannot be empty.", nameof(baseUrl));
            if (string.IsNullOrWhiteSpace(label))
                throw new ArgumentException("Label cannot be empty.", nameof(label));
            if (maxTenants < 0)
                throw new ArgumentException("MaxTenants cannot be negative.", nameof(maxTenants));

            BaseUrl = baseUrl;
            Label = label;
            MaxTenants = maxTenants;
            HealthCheckUrl = healthCheckUrl;
            IsActive = true;
            HealthStatus = "Unknown";
        }

        /// <summary>Update health status + timestamp.</summary>
        public void UpdateHealth(string status, DateTime? checkedAt = null)
        {
            if (string.IsNullOrWhiteSpace(status))
                throw new ArgumentException("Health status cannot be empty.", nameof(status));
            HealthStatus = status;
            LastHealthCheck = checkedAt ?? DateTime.UtcNow;
            UpdateAudit();
        }

        /// <summary>Activate this instance (accept new tenant assignments).</summary>
        public void Activate()
        {
            IsActive = true;
            UpdateAudit();
        }

        /// <summary>Deactivate this instance (no new tenant assignments; existing tenants stay).</summary>
        public void Deactivate()
        {
            IsActive = false;
            UpdateAudit();
        }

        /// <summary>Update the display label.</summary>
        public void UpdateLabel(string label)
        {
            if (string.IsNullOrWhiteSpace(label))
                throw new ArgumentException("Label cannot be empty.", nameof(label));
            Label = label;
            UpdateAudit();
        }

        /// <summary>Update the maximum tenant capacity.</summary>
        public void UpdateMaxTenants(int max)
        {
            if (max < 0)
                throw new ArgumentException("MaxTenants cannot be negative.", nameof(max));
            MaxTenants = max;
            UpdateAudit();
        }
    }
}
