using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using VanAn.CoreHub.Infrastructure;
using VanAn.CoreHub.Services.DomainRegistrar;
using VanAn.Shared.Domain.Aggregates.DomainResellerAggregate;

namespace VanAn.CoreHub.Services
{
    /// <summary>
    /// Service for managing TenantDomain records — Domain Reseller R1.
    /// CRUD operations on the TenantDomains table + integration with IDomainRegistrarService
    /// for DNS A record automation.
    ///
    /// Pattern follows KhachLinkInstanceService:
    /// - Uses VanAnDbContext directly (platform-level entity, no tenant filter)
    /// - AsNoTracking for read queries
    /// - ILogger optional (NullLogger-friendly for tests)
    /// </summary>
    public class TenantDomainService : ITenantDomainService
    {
        private readonly IVanAnDbContext _dbContext;
        private readonly IDomainRegistrarService _registrarService;
        private readonly ILogger<TenantDomainService>? _logger;

        public TenantDomainService(
            IVanAnDbContext dbContext,
            IDomainRegistrarService registrarService,
            ILogger<TenantDomainService>? logger = null)
        {
            _dbContext = dbContext;
            _registrarService = registrarService;
            _logger = logger;
        }

        public async Task<TenantDomain?> GetByIdAsync(Guid id, CancellationToken ct = default)
        {
            return await _dbContext.TenantDomains
                .AsNoTracking()
                .FirstOrDefaultAsync(d => d.Id == id, ct);
        }

        public async Task<TenantDomain?> GetByDomainAsync(string domain, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(domain))
                return null;

            var normalized = domain.ToLowerInvariant();
            return await _dbContext.TenantDomains
                .AsNoTracking()
                .FirstOrDefaultAsync(d => d.Domain == normalized, ct);
        }

        public async Task<List<TenantDomain>> GetAllAsync(CancellationToken ct = default)
        {
            return await _dbContext.TenantDomains
                .AsNoTracking()
                .OrderByDescending(d => d.CreatedAt)
                .ToListAsync(ct);
        }

        public async Task<List<TenantDomain>> GetByOwnerTenantAsync(Guid ownerTenantId, CancellationToken ct = default)
        {
            return await _dbContext.TenantDomains
                .AsNoTracking()
                .Where(d => d.OwnerTenantId == ownerTenantId)
                .OrderByDescending(d => d.CreatedAt)
                .ToListAsync(ct);
        }

        public async Task<TenantDomain> CreateAsync(
            string domain,
            Guid ownerTenantId,
            string registrantEmail,
            RegistrarProvider registrar = RegistrarProvider.GoDaddy,
            DateTime? expiresAt = null,
            CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(domain))
                throw new ArgumentException("Domain cannot be empty.", nameof(domain));
            if (ownerTenantId == Guid.Empty)
                throw new ArgumentException("OwnerTenantId cannot be Guid.Empty.", nameof(ownerTenantId));

            var normalizedDomain = domain.ToLowerInvariant();

            // Unique Domain check
            bool duplicate = await _dbContext.TenantDomains
                .AnyAsync(d => d.Domain == normalizedDomain, ct);
            if (duplicate)
                throw new InvalidOperationException($"A TenantDomain with Domain '{normalizedDomain}' already exists.");

            var tenantDomain = new TenantDomain(normalizedDomain, ownerTenantId, registrantEmail, registrar, expiresAt);
            await _dbContext.TenantDomains.AddAsync(tenantDomain, ct);
            await _dbContext.SaveChangesAsync(ct);

            _logger?.LogInformation("Created TenantDomain {Id} '{Domain}' owner={OwnerTenantId} registrar={Registrar}",
                tenantDomain.Id, tenantDomain.Domain, tenantDomain.OwnerTenantId, tenantDomain.Registrar);
            return tenantDomain;
        }

        /// <summary>
        /// Link a TenantDomain to a KhachLinkInstance + auto-create A record at the registrar.
        /// This is the integration point between Domain Reseller and KhachLink Multi-Profile.
        /// </summary>
        public async Task<bool> LinkToKhachLinkInstanceAsync(
            Guid tenantDomainId,
            Guid khachLinkInstanceId,
            string vpsIpAddress,
            CancellationToken ct = default)
        {
            var tenantDomain = await _dbContext.TenantDomains
                .FirstOrDefaultAsync(d => d.Id == tenantDomainId, ct);
            if (tenantDomain is null)
                return false;

            // 1. Update entity link
            tenantDomain.LinkToKhachLinkInstance(khachLinkInstanceId);

            // 2. Auto-create A record at registrar (apex domain → VPS IP)
            bool dnsSuccess = await _registrarService.SetARecordAsync(
                tenantDomain.Domain, "@", vpsIpAddress, ttl: 600, ct);

            if (!dnsSuccess)
            {
                _logger?.LogWarning("LinkToKhachLinkInstance: A record creation failed for {Domain} → {IP}",
                    tenantDomain.Domain, vpsIpAddress);
                // Still save the entity link — admin can retry DNS separately
            }

            await _dbContext.SaveChangesAsync(ct);

            _logger?.LogInformation("Linked TenantDomain {Domain} → KhachLinkInstance {KliId} (DNS: {DnsStatus})",
                tenantDomain.Domain, khachLinkInstanceId, dnsSuccess ? "OK" : "FAILED");
            return true;
        }

        public async Task<bool> UnlinkFromKhachLinkInstanceAsync(Guid tenantDomainId, CancellationToken ct = default)
        {
            var tenantDomain = await _dbContext.TenantDomains
                .FirstOrDefaultAsync(d => d.Id == tenantDomainId, ct);
            if (tenantDomain is null)
                return false;

            tenantDomain.UnlinkFromKhachLinkInstance();
            await _dbContext.SaveChangesAsync(ct);

            _logger?.LogInformation("Unlinked TenantDomain {Domain} from KhachLinkInstance", tenantDomain.Domain);
            return true;
        }

        public async Task<bool> MarkRegisteredAsync(Guid id, DateTime expiresAt, string? operationId = null, CancellationToken ct = default)
        {
            var tenantDomain = await _dbContext.TenantDomains
                .FirstOrDefaultAsync(d => d.Id == id, ct);
            if (tenantDomain is null)
                return false;

            tenantDomain.MarkRegistered(expiresAt, operationId);
            await _dbContext.SaveChangesAsync(ct);
            return true;
        }

        public async Task<bool> MarkFailedAsync(Guid id, string errorMessage, string? operationId = null, CancellationToken ct = default)
        {
            var tenantDomain = await _dbContext.TenantDomains
                .FirstOrDefaultAsync(d => d.Id == id, ct);
            if (tenantDomain is null)
                return false;

            tenantDomain.MarkFailed(errorMessage, operationId);
            await _dbContext.SaveChangesAsync(ct);
            return true;
        }

        public async Task<bool> RenewAsync(Guid id, DateTime newExpiresAt, CancellationToken ct = default)
        {
            var tenantDomain = await _dbContext.TenantDomains
                .FirstOrDefaultAsync(d => d.Id == id, ct);
            if (tenantDomain is null)
                return false;

            tenantDomain.Renew(newExpiresAt);
            await _dbContext.SaveChangesAsync(ct);

            _logger?.LogInformation("Renewed TenantDomain {Domain} → new expiry {ExpiresAt}",
                tenantDomain.Domain, newExpiresAt);
            return true;
        }

        /// <summary>Cron helper: mark domains past their expiry as Expired.</summary>
        public async Task<int> MarkExpiredDomainsAsync(CancellationToken ct = default)
        {
            var now = DateTime.UtcNow;
            var expired = await _dbContext.TenantDomains
                .Where(d => d.Status == DomainStatus.Active && d.ExpiresAt < now)
                .ToListAsync(ct);

            foreach (var domain in expired)
            {
                domain.MarkExpired();
                _logger?.LogWarning("TenantDomain {Domain} expired (was {ExpiresAt})",
                    domain.Domain, domain.ExpiresAt);
            }

            if (expired.Count > 0)
                await _dbContext.SaveChangesAsync(ct);

            return expired.Count;
        }
    }

    /// <summary>Interface for TenantDomainService — Domain Reseller R1.</summary>
    public interface ITenantDomainService
    {
        Task<TenantDomain?> GetByIdAsync(Guid id, CancellationToken ct = default);
        Task<TenantDomain?> GetByDomainAsync(string domain, CancellationToken ct = default);
        Task<List<TenantDomain>> GetAllAsync(CancellationToken ct = default);
        Task<List<TenantDomain>> GetByOwnerTenantAsync(Guid ownerTenantId, CancellationToken ct = default);
        Task<TenantDomain> CreateAsync(string domain, Guid ownerTenantId, string registrantEmail,
            RegistrarProvider registrar = RegistrarProvider.GoDaddy, DateTime? expiresAt = null, CancellationToken ct = default);
        Task<bool> LinkToKhachLinkInstanceAsync(Guid tenantDomainId, Guid khachLinkInstanceId, string vpsIpAddress, CancellationToken ct = default);
        Task<bool> UnlinkFromKhachLinkInstanceAsync(Guid tenantDomainId, CancellationToken ct = default);
        Task<bool> MarkRegisteredAsync(Guid id, DateTime expiresAt, string? operationId = null, CancellationToken ct = default);
        Task<bool> MarkFailedAsync(Guid id, string errorMessage, string? operationId = null, CancellationToken ct = default);
        Task<bool> RenewAsync(Guid id, DateTime newExpiresAt, CancellationToken ct = default);
        Task<int> MarkExpiredDomainsAsync(CancellationToken ct = default);
    }
}
