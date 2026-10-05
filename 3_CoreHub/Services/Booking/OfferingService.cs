using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using VanAn.CoreHub.Infrastructure;
using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Services.Booking
{
    /// <summary>
    /// OfferingService — catalog booking (SRS §8.2-8.3): category / offering / package / add-on.
    /// Booking lưu SNAPSHOT (name/duration/price) tại booking time → đổi catalog không ảnh hưởng booking cũ (§8.4).
    /// Multi-tenancy: mọi SELECT IgnoreQueryFilters + WHERE TenantId.
    /// </summary>
    public sealed class OfferingService(VanAnDbContext context, ILogger<OfferingService> logger) : IOfferingService
    {
        private readonly VanAnDbContext _context = context;
        private readonly ILogger<OfferingService> _logger = logger;

        public async Task<ServiceCategory> CreateCategoryAsync(TenantId tenantId, string name, int displayOrder = 0, CancellationToken ct = default)
        {
            var category = new ServiceCategory(tenantId, name, displayOrder);
            _context.ServiceCategories.Add(category);
            _ = await _context.SaveChangesAsync(ct);
            return category;
        }

        public async Task<ServiceCategory> UpdateCategoryAsync(TenantId tenantId, Guid categoryId, string name, int displayOrder, bool isActive, CancellationToken ct = default)
        {
            ServiceCategory category = await _context.ServiceCategories
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(c => c.TenantId == tenantId && c.Id == categoryId, ct)
                ?? throw new NotFoundException("Danh mục dịch vụ không tồn tại.");
            category.Update(name, displayOrder, isActive);
            _ = await _context.SaveChangesAsync(ct);
            return category;
        }

        public async Task<IReadOnlyList<ServiceCategory>> ListCategoriesAsync(TenantId tenantId, bool activeOnly = false, CancellationToken ct = default)
        {
            IQueryable<ServiceCategory> query = _context.ServiceCategories
                .IgnoreQueryFilters()
                .Where(c => c.TenantId == tenantId);
            if (activeOnly)
                query = query.Where(c => c.IsActive);
            return await query.OrderBy(c => c.DisplayOrder).ThenBy(c => c.Name).ToListAsync(ct);
        }

        public async Task<AppointmentOffering> CreateOfferingAsync(TenantId tenantId, CreateOfferingCommand command, CancellationToken ct = default)
        {
            await ValidateCategoryAsync(tenantId, command.CategoryId, ct);

            var offering = new AppointmentOffering(
                tenantId, command.DisplayName, command.OfferingType, command.DurationMinutes,
                command.Price, command.CategoryId, command.RequiredSkillCode, command.Description);
            _context.AppointmentOfferings.Add(offering);
            _ = await _context.SaveChangesAsync(ct);
            _logger.LogInformation("Offering created: {OfferingId} type={Type} tenant={TenantId}", offering.Id, command.OfferingType, tenantId.Value);
            return offering;
        }

        public async Task<AppointmentOffering> UpdateOfferingAsync(TenantId tenantId, Guid offeringId, CreateOfferingCommand command, bool isActive = true, CancellationToken ct = default)
        {
            AppointmentOffering offering = await _context.AppointmentOfferings
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(o => o.TenantId == tenantId && o.Id == offeringId, ct)
                ?? throw new NotFoundException("Dịch vụ không tồn tại.");
            await ValidateCategoryAsync(tenantId, command.CategoryId, ct);

            offering.Update(command.DisplayName, command.OfferingType, command.DurationMinutes,
                command.Price, command.CategoryId, command.RequiredSkillCode, command.Description, isActive);
            _ = await _context.SaveChangesAsync(ct);
            return offering;
        }

        public async Task<AppointmentOffering?> GetOfferingAsync(TenantId tenantId, Guid offeringId, CancellationToken ct = default)
            => await _context.AppointmentOfferings
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(o => o.TenantId == tenantId && o.Id == offeringId, ct);

        public async Task<IReadOnlyList<AppointmentOffering>> ListOfferingsAsync(TenantId tenantId, Guid? categoryId = null, bool activeOnly = false, CancellationToken ct = default)
        {
            IQueryable<AppointmentOffering> query = _context.AppointmentOfferings
                .IgnoreQueryFilters()
                .Where(o => o.TenantId == tenantId);
            if (categoryId is not null)
                query = query.Where(o => o.CategoryId == categoryId);
            if (activeOnly)
                query = query.Where(o => o.IsActive);
            return await query.OrderBy(o => o.DisplayName).ToListAsync(ct);
        }

        public async Task<IReadOnlyList<AppointmentOfferingItem>> SetPackageItemsAsync(
            TenantId tenantId, Guid packageOfferingId, IReadOnlyList<PackageItemCommand> items, CancellationToken ct = default)
        {
            AppointmentOffering package = await _context.AppointmentOfferings
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(o => o.TenantId == tenantId && o.Id == packageOfferingId, ct)
                ?? throw new NotFoundException("Package không tồn tại.");
            if (package.OfferingType != OfferingType.Package)
                throw new ValidationException("Chỉ OfferingType.Package mới có package items.");

            List<AppointmentOfferingItem> existing = await _context.AppointmentOfferingItems
                .IgnoreQueryFilters()
                .Where(i => i.TenantId == tenantId && i.OfferingId == packageOfferingId)
                .ToListAsync(ct);
            _context.AppointmentOfferingItems.RemoveRange(existing);

            foreach (PackageItemCommand item in items)
            {
                _context.AppointmentOfferingItems.Add(new AppointmentOfferingItem(
                    tenantId, packageOfferingId, item.ChildName, item.Quantity,
                    item.UnitPriceSnapshot, item.ChildOfferingId));
            }

            _ = await _context.SaveChangesAsync(ct);
            return await ListPackageItemsAsync(tenantId, packageOfferingId, ct);
        }

        public async Task<IReadOnlyList<AppointmentOfferingItem>> ListPackageItemsAsync(TenantId tenantId, Guid packageOfferingId, CancellationToken ct = default)
            => await _context.AppointmentOfferingItems
                .IgnoreQueryFilters()
                .Where(i => i.TenantId == tenantId && i.OfferingId == packageOfferingId)
                .OrderBy(i => i.ChildName)
                .ToListAsync(ct);

        public async Task<AddOn> CreateAddOnAsync(TenantId tenantId, string name, decimal price, CancellationToken ct = default)
        {
            var addOn = new AddOn(tenantId, name, price);
            _context.AddOns.Add(addOn);
            _ = await _context.SaveChangesAsync(ct);
            return addOn;
        }

        public async Task<AddOn> UpdateAddOnAsync(TenantId tenantId, Guid addOnId, string name, decimal price, bool isActive, CancellationToken ct = default)
        {
            AddOn addOn = await _context.AddOns
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(a => a.TenantId == tenantId && a.Id == addOnId, ct)
                ?? throw new NotFoundException("Add-on không tồn tại.");
            addOn.Update(name, price, isActive);
            _ = await _context.SaveChangesAsync(ct);
            return addOn;
        }

        public async Task<IReadOnlyList<AddOn>> ListAddOnsAsync(TenantId tenantId, bool activeOnly = false, CancellationToken ct = default)
        {
            IQueryable<AddOn> query = _context.AddOns
                .IgnoreQueryFilters()
                .Where(a => a.TenantId == tenantId);
            if (activeOnly)
                query = query.Where(a => a.IsActive);
            return await query.OrderBy(a => a.Name).ToListAsync(ct);
        }

        private async Task ValidateCategoryAsync(TenantId tenantId, Guid? categoryId, CancellationToken ct)
        {
            if (categoryId is null)
                return;
            bool exists = await _context.ServiceCategories
                .IgnoreQueryFilters()
                .AnyAsync(c => c.TenantId == tenantId && c.Id == categoryId, ct);
            if (!exists)
                throw new ValidationException("Danh mục dịch vụ không tồn tại trong tenant này.");
        }
    }
}
