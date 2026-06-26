namespace VanAn.Shared.Domain.Aggregates.ApiKeyAggregate
{
    /// <summary>
    /// Wave 14: API Key entity for HMAC request signing.
    /// Supports per-tenant API keys with HMAC-SHA256 shared secret.
    /// Secret is stored as BCrypt hash; raw secret returned only at creation time.
    /// </summary>
    public class ApiKey
    {
        public Guid Id { get; private set; }
        public Guid TenantId { get; private set; }
        public string Name { get; private set; }
        /// <summary>BCrypt hash of the shared secret (HMAC key).</summary>
        public string SecretHash { get; private set; }
        public bool IsActive { get; private set; }
        public DateTime CreatedAt { get; private set; }
        public DateTime ExpiresAt { get; private set; }
        public DateTime? LastUsedAt { get; private set; }
        public DateTime? RevokedAt { get; private set; }

        // EF Core constructor
        private ApiKey() { Name = string.Empty; SecretHash = string.Empty; }

        public ApiKey(Guid tenantId, string name, string secretHash, int expirationDays = 90)
        {
            Id = Guid.NewGuid();
            TenantId = tenantId;
            Name = name;
            SecretHash = secretHash;
            IsActive = true;
            CreatedAt = DateTime.UtcNow;
            ExpiresAt = DateTime.UtcNow.AddDays(expirationDays);
        }

        public void Revoke()
        {
            IsActive = false;
            RevokedAt = DateTime.UtcNow;
        }

        public void RecordUsage()
        {
            LastUsedAt = DateTime.UtcNow;
        }

        public bool IsExpired() => DateTime.UtcNow > ExpiresAt;
        public bool IsValid() => IsActive && !IsExpired();
    }
}
