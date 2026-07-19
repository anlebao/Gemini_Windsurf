using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using VanAn.CoreHub.Infrastructure;
using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Services
{
    /// <summary>
    /// Phase 2 (Multi-VPS Checkout): Service for managing ShopERP hosting instances.
    /// Platform-level CRUD + health check. Used by Gateway Admin API (Phase 2) and
    /// Gateway router (Phase 3) to resolve tenant → ShopInstance → BaseUrl.
    ///
    /// ShopInstance is a platform-level entity (TenantId = Guid.Empty sentinel).
    /// Queries use IgnoreQueryFilters() to bypass the multi-tenancy query filter.
    /// </summary>
    public class ShopInstanceService : IShopInstanceService
    {
        private readonly IVanAnDbContext _dbContext;
        private readonly HttpClient _httpClient;
        private readonly ILogger<ShopInstanceService>? _logger;

        public ShopInstanceService(IVanAnDbContext dbContext, HttpClient httpClient, ILogger<ShopInstanceService>? logger = null)
        {
            _dbContext = dbContext;
            _httpClient = httpClient;
            _logger = logger;
        }

        public async Task<ShopInstance> CreateAsync(
            string baseUrl,
            string label,
            int maxTenants = 50,
            string? healthCheckUrl = null,
            CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(baseUrl))
                throw new ArgumentException("BaseUrl cannot be empty.", nameof(baseUrl));
            if (string.IsNullOrWhiteSpace(label))
                throw new ArgumentException("Label cannot be empty.", nameof(label));
            if (maxTenants < 0)
                throw new ArgumentException("MaxTenants cannot be negative.", nameof(maxTenants));
            if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out _))
                throw new ArgumentException("BaseUrl must be a valid absolute URL.", nameof(baseUrl));
            if (!string.IsNullOrWhiteSpace(healthCheckUrl) && !Uri.TryCreate(healthCheckUrl, UriKind.Absolute, out _))
                throw new ArgumentException("HealthCheckUrl must be a valid absolute URL.", nameof(healthCheckUrl));

            // Unique BaseUrl check (bypass tenant filter — platform entity)
            bool duplicate = await _dbContext.ShopInstances
                .IgnoreQueryFilters()
                .AnyAsync(s => s.BaseUrl == baseUrl, ct);
            if (duplicate)
                throw new InvalidOperationException($"A ShopInstance with BaseUrl '{baseUrl}' already exists.");

            var instance = new ShopInstance(baseUrl, label, maxTenants, healthCheckUrl);
            await _dbContext.ShopInstances.AddAsync(instance, ct);
            await _dbContext.SaveChangesAsync(ct);

            _logger?.LogInformation("Created ShopInstance {Id} '{Label}' at {BaseUrl}", instance.Id, instance.Label, instance.BaseUrl);
            return instance;
        }

        public async Task<ShopInstance?> GetByIdAsync(Guid id, CancellationToken ct = default)
        {
            return await _dbContext.ShopInstances
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(s => s.Id == id, ct);
        }

        public async Task<List<ShopInstance>> GetAllAsync(CancellationToken ct = default)
        {
            return await _dbContext.ShopInstances
                .IgnoreQueryFilters()
                .AsNoTracking()
                .ToListAsync(ct);
        }

        public async Task<List<ShopInstance>> GetActiveAsync(CancellationToken ct = default)
        {
            return await _dbContext.ShopInstances
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(s => s.IsActive)
                .ToListAsync(ct);
        }

        public async Task<bool> UpdateAsync(Guid id, string label, int maxTenants, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(label))
                throw new ArgumentException("Label cannot be empty.", nameof(label));
            if (maxTenants < 0)
                throw new ArgumentException("MaxTenants cannot be negative.", nameof(maxTenants));

            var instance = await _dbContext.ShopInstances
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(s => s.Id == id, ct);
            if (instance is null)
                return false;

            instance.UpdateLabel(label);
            instance.UpdateMaxTenants(maxTenants);
            await _dbContext.SaveChangesAsync(ct);

            _logger?.LogInformation("Updated ShopInstance {Id} label='{Label}' maxTenants={MaxTenants}", id, label, maxTenants);
            return true;
        }

        public async Task<bool> SetActiveAsync(Guid id, bool isActive, CancellationToken ct = default)
        {
            var instance = await _dbContext.ShopInstances
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(s => s.Id == id, ct);
            if (instance is null)
                return false;

            if (isActive)
                instance.Activate();
            else
                instance.Deactivate();

            await _dbContext.SaveChangesAsync(ct);

            _logger?.LogInformation("Set ShopInstance {Id} IsActive={IsActive}", id, isActive);
            return true;
        }

        public async Task<ShopInstanceHealthResult> CheckHealthAsync(Guid id, CancellationToken ct = default)
        {
            var instance = await _dbContext.ShopInstances
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(s => s.Id == id, ct);
            if (instance is null)
                throw new InvalidOperationException($"ShopInstance with Id '{id}' not found.");

            string healthUrl = !string.IsNullOrWhiteSpace(instance.HealthCheckUrl)
                ? instance.HealthCheckUrl!
                : $"{instance.BaseUrl.TrimEnd('/')}/health";

            var sw = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                using var response = await _httpClient.GetAsync(healthUrl, ct);
                sw.Stop();

                if (response.IsSuccessStatusCode)
                {
                    instance.UpdateHealth("Healthy");
                    await _dbContext.SaveChangesAsync(ct);
                    _logger?.LogInformation("Health check ShopInstance {Id}: Healthy ({Ms}ms)", id, sw.ElapsedMilliseconds);
                    return ShopInstanceHealthResult.Healthy(sw.ElapsedMilliseconds);
                }
                else
                {
                    string error = $"HTTP {(int)response.StatusCode} {response.ReasonPhrase}";
                    instance.UpdateHealth("Down");
                    await _dbContext.SaveChangesAsync(ct);
                    _logger?.LogWarning("Health check ShopInstance {Id}: Down — {Error}", id, error);
                    return ShopInstanceHealthResult.Down(error, sw.ElapsedMilliseconds);
                }
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or OperationCanceledException)
            {
                sw.Stop();
                instance.UpdateHealth("Down");
                await _dbContext.SaveChangesAsync(ct);
                _logger?.LogWarning(ex, "Health check ShopInstance {Id}: Down — unreachable", id);
                return ShopInstanceHealthResult.Down(ex.Message, sw.ElapsedMilliseconds);
            }
        }

        public async Task<int> CountTenantsAsync(Guid shopInstanceId, CancellationToken ct = default)
        {
            return await _dbContext.Tenants
                .IgnoreQueryFilters()
                .CountAsync(t => t.ShopInstanceId == shopInstanceId, ct);
        }
    }
}
