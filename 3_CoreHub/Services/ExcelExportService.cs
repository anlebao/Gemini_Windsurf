using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using VanAn.CoreHub.Domain.Repositories;
using VanAn.CoreHub.Infrastructure;
using VanAn.CoreHub.Services.Reports;
using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Services
{
    /// <summary>
    /// Wave 3: Excel export service implementation.
    /// Generates .xlsx reports for Revenue, Inventory, and Customer data.
    /// </summary>
    public class ExcelExportService(
        IOrderService orderService,
        ICustomerRepository customerRepository,
        IVanAnDbContext context,
        ILogger<ExcelExportService> logger) : IExcelExportService
    {
        private const int MaxRows = 10_000;

        private readonly IOrderService _orderService = orderService;
        private readonly ICustomerRepository _customerRepository = customerRepository;
        private readonly IVanAnDbContext _context = context;
        private readonly ILogger<ExcelExportService> _logger = logger;

        public async Task<byte[]> ExportRevenueAsync(Guid tenantId, DateTime from, DateTime to, CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Exporting revenue report for tenant {TenantId} from {From} to {To}", tenantId, from, to);

            List<Order> orders = [.. (await _orderService.GetOrdersByDateRangeAsync(tenantId, from, to))
                .OrderBy(o => o.OrderDate)
                .Take(MaxRows)];

            if (orders.Count >= MaxRows)
            {
                _logger.LogWarning("Revenue export hit max rows cap {MaxRows} for tenant {TenantId}", MaxRows, tenantId);
            }

            return await RevenueExcelReport.GenerateAsync(orders, from, to);
        }

        public async Task<byte[]> ExportInventoryAsync(Guid tenantId, CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Exporting inventory report for tenant {TenantId}", tenantId);

            List<Inventory> inventories = await _context.Inventories
                .AsNoTracking()
                .OrderBy(i => i.IngredientId)
                .Take(MaxRows)
                .ToListAsync(cancellationToken);

            if (inventories.Count >= MaxRows)
            {
                _logger.LogWarning("Inventory export hit max rows cap {MaxRows} for tenant {TenantId}", MaxRows, tenantId);
            }

            List<Ingredient> ingredients = await _context.Ingredients
                .AsNoTracking()
                .ToListAsync(cancellationToken);

            Dictionary<Guid, Ingredient> ingredientMap = ingredients.ToDictionary(i => i.IngredientId.Value);
            return await InventoryExcelReport.GenerateAsync(inventories, ingredientMap);
        }

        public async Task<byte[]> ExportCustomerAsync(Guid tenantId, DateTime from, DateTime to, CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Exporting customer report for tenant {TenantId} from {From} to {To}", tenantId, from, to);

            IReadOnlyList<Customer> customers = await _customerRepository.GetAllActiveAsync();
            List<Customer> filteredCustomers = customers
                .Where(c => c.LastOrderDate == null || (c.LastOrderDate >= from && c.LastOrderDate <= to))
                .OrderByDescending(c => c.TotalSpent)
                .Take(MaxRows)
                .ToList();

            if (filteredCustomers.Count >= MaxRows)
            {
                _logger.LogWarning("Customer export hit max rows cap {MaxRows} for tenant {TenantId}", MaxRows, tenantId);
            }

            return await CustomerExcelReport.GenerateAsync(filteredCustomers);
        }
    }
}
