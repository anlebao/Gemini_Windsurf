using VanAn.Shared.Domain;
using VanAn.CoreHub.Services.Cache;
using VanAn.CoreHub.Infrastructure;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;

namespace VanAn.CoreHub.Services.Template
{
    /// <summary>
    /// HKD Book Generation Service - Orchestrates template creation and book generation
    /// Main service for generating HKD books from templates
    /// </summary>
    public class HKDBookGenerationService : IHKDBookGenerationService
    {
        private readonly VanAnDbContext _context;
        private readonly TemplateFactory _templateFactory;
        private readonly IBookResultCache _cache;
        private readonly ILogger<HKDBookGenerationService> _logger;
        
        public HKDBookGenerationService(
            VanAnDbContext context,
            TemplateFactory templateFactory,
            IBookResultCache cache,
            ILogger<HKDBookGenerationService> logger)
        {
            _context = context;
            _templateFactory = templateFactory;
            _cache = cache;
            _logger = logger;
        }
        
        /// <summary>
        /// Generate HKD book for a specific template and period
        /// </summary>
        public async Task<GenericHKDBook> GenerateBookAsync(
            TenantId tenantId,
            AccountingPeriod period,
            string templateCode)
        {
            var cacheKey = GetCacheKey(tenantId, period, templateCode);
            
            // Check cache first
            var cachedBook = await _cache.GetBookAsync(cacheKey);
            if (cachedBook != null)
            {
                _logger.LogDebug("Using cached HKD book for tenant {TenantId}, template {TemplateCode}, period {Period}", 
                    tenantId.Value, templateCode, period);
                return cachedBook;
            }
            
            _logger.LogInformation("Generating HKD book for tenant {TenantId}, template {TemplateCode}, period {Period}", 
                tenantId.Value, templateCode, period);
            
            // Get tenant information to determine HKD group
            var tenant = await _context.Tenants
                .FirstOrDefaultAsync(t => t.Id == tenantId);
            
            if (tenant == null)
            {
                throw new ArgumentException($"Tenant {tenantId.Value} not found");
            }
            
            // Get journal entries for the period
            var entries = await GetJournalEntriesAsync(tenantId, period);
            
            // Create template using factory
            var template = _templateFactory.CreateTemplate(tenant.HKDGroup ?? HKDGroup.Group1, templateCode);
            
            // Generate book using template
            var book = await template.CreateBookAsync(tenantId, period, entries);
            
            // Cache the result
            await _cache.SetBookAsync(cacheKey, book, TimeSpan.FromHours(1));
            
            _logger.LogInformation("HKD book generated successfully: {TemplateCode} with {ValueCount} values", 
                templateCode, book.NumericValues.Count);
            
            return book;
        }
        
        /// <summary>
        /// Generate all HKD books for a tenant and period
        /// </summary>
        public async Task<List<GenericHKDBook>> GenerateAllBooksAsync(
            TenantId tenantId,
            AccountingPeriod period)
        {
            _logger.LogInformation("Generating all HKD books for tenant {TenantId}, period {Period}", 
                tenantId.Value, period);
            
            // Get tenant information
            var tenant = await _context.Tenants
                .FirstOrDefaultAsync(t => t.Id == tenantId);
            
            if (tenant == null)
            {
                throw new ArgumentException($"Tenant {tenantId.Value} not found");
            }
            
            // Get all templates for the tenant's group
            var templates = _templateFactory.GetTemplatesForGroup(tenant.HKDGroup ?? HKDGroup.Group1);
            var books = new List<GenericHKDBook>();
            
            // Get journal entries once for all templates
            var entries = await GetJournalEntriesAsync(tenantId, period);
            
            // Generate each book
            foreach (var template in templates)
            {
                try
                {
                    var book = await template.CreateBookAsync(tenantId, period, entries);
                    books.Add(book);
                    
                    // Cache individual book
                    var cacheKey = GetCacheKey(tenantId, period, template.TemplateCode);
                    await _cache.SetBookAsync(cacheKey, book, TimeSpan.FromHours(1));
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error generating HKD book {TemplateCode} for tenant {TenantId}", 
                        template.TemplateCode, tenantId.Value);
                    // Continue with other templates
                }
            }
            
            _logger.LogInformation("Generated {SuccessCount}/{TotalCount} HKD books for tenant {TenantId}", 
                books.Count, templates.Count, tenantId.Value);
            
            return books;
        }
        
        /// <summary>
        /// Get available templates for a tenant
        /// </summary>
        public async Task<List<HKDBookTemplate>> GetAvailableTemplatesAsync(TenantId tenantId)
        {
            var tenant = await _context.Tenants
                .FirstOrDefaultAsync(t => t.Id == tenantId);
            
            if (tenant == null)
            {
                throw new ArgumentException($"Tenant {tenantId.Value} not found");
            }
            
            var templates = _templateFactory.GetTemplatesForGroup(tenant.HKDGroup ?? HKDGroup.Group1);
            
            _logger.LogDebug("Found {TemplateCount} templates for tenant {TenantId} (Group: {Group})", 
                templates.Count, tenantId.Value, tenant.HKDGroup);
            
            return await Task.FromResult(templates);
        }
        
        /// <summary>
        /// Validate template before generation
        /// </summary>
        public async Task<List<string>> ValidateTemplateAsync(string templateCode)
        {
            // Create a temporary template instance for validation
            var template = _templateFactory.CreateTemplate(HKDGroup.Group1, templateCode);
            
            // Use the template's validation method
            if (template is BaseHKDBookTemplate baseTemplate)
            {
                return await baseTemplate.ValidateTemplateAsync();
            }
            
            // For other template types, return empty validation
            return await Task.FromResult(new List<string>());
        }
        
        /// <summary>
        /// Get book generation status
        /// </summary>
        public async Task<BookGenerationStatus> GetGenerationStatusAsync(
            TenantId tenantId,
            AccountingPeriod period)
        {
            var templates = await GetAvailableTemplatesAsync(tenantId);
            var status = new BookGenerationStatus
            {
                TenantId = tenantId,
                Period = period,
                TotalTemplates = templates.Count,
                GeneratedBooks = 0,
                FailedBooks = 0
            };
            
            foreach (var template in templates)
            {
                var cacheKey = GetCacheKey(tenantId, period, template.TemplateCode);
                var cachedBook = await _cache.GetBookAsync(cacheKey);
                
                if (cachedBook != null)
                {
                    status.GeneratedBooks++;
                }
            }
            
            return status;
        }
        
        private async Task<List<JournalEntry>> GetJournalEntriesAsync(TenantId tenantId, AccountingPeriod period)
        {
            return await _context.JournalEntries
                .Where(e => EF.Property<Guid>(e, "TenantId") == tenantId.Value &&
                           e.Period.Year == period.Year &&
                           e.Period.Month == period.Month)
                .OrderBy(e => e.EntryDate)
                .ToListAsync();
        }
        
        private string GetCacheKey(TenantId tenantId, AccountingPeriod period, string templateCode)
        {
            return $"hkd_book_{tenantId.Value}_{period.Year}_{period.Month:D2}_{templateCode}";
        }
    }
    
    /// <summary>
    /// Service interface for HKD book generation
    /// </summary>
    public interface IHKDBookGenerationService
    {
        Task<GenericHKDBook> GenerateBookAsync(TenantId tenantId, AccountingPeriod period, string templateCode);
        Task<List<GenericHKDBook>> GenerateAllBooksAsync(TenantId tenantId, AccountingPeriod period);
        Task<List<HKDBookTemplate>> GetAvailableTemplatesAsync(TenantId tenantId);
        Task<List<string>> ValidateTemplateAsync(string templateCode);
        Task<BookGenerationStatus> GetGenerationStatusAsync(TenantId tenantId, AccountingPeriod period);
    }
    
    /// <summary>
    /// Book generation status
    /// </summary>
    public record BookGenerationStatus
    {
        public TenantId TenantId { get; set; }
        public AccountingPeriod Period { get; set; }
        public int TotalTemplates { get; set; }
        public int GeneratedBooks { get; set; }
        public int FailedBooks { get; set; }
        public bool IsComplete => GeneratedBooks + FailedBooks >= TotalTemplates;
        public decimal ProgressPercentage => TotalTemplates > 0 ? (decimal)GeneratedBooks / TotalTemplates * 100 : 0;
    }
}
