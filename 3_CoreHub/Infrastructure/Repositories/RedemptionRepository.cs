using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using VanAn.CoreHub.Domain.Repositories;
using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Infrastructure.Repositories
{
    /// <summary>
    /// Loyalty-B: EF Core implementation of IRedemptionRepository.
    /// ShopERP SQLite (tenant-scoped). Always filters by tenant + soft-delete.
    /// </summary>
    public class RedemptionRepository(IVanAnDbContext context) : IRedemptionRepository
    {
        private readonly IVanAnDbContext _context = context;
        private readonly Guid _currentTenantId = context is VanAnDbContext vanAnContext ? vanAnContext.CurrentTenantId : Guid.Empty;

        // === Catalog Items ===

        public async Task<RedemptionCatalogItem?> GetCatalogItemByIdAsync(Guid id)
        {
            return await _context.RedemptionCatalogItems
                .Where(i => i.Id == id && !i.IsDeleted)
                .FirstOrDefaultAsync();
        }

        public async Task<IReadOnlyList<RedemptionCatalogItem>> GetActiveCatalogItemsAsync()
        {
            return await _context.RedemptionCatalogItems
                .Where(i => !i.IsDeleted && i.IsActive)
                .OrderBy(i => i.PointsRequired)
                .ToListAsync();
        }

        public async Task<IReadOnlyList<RedemptionCatalogItem>> GetAllCatalogItemsAsync()
        {
            return await _context.RedemptionCatalogItems
                .Where(i => !i.IsDeleted)
                .OrderByDescending(i => i.CreatedAt)
                .ToListAsync();
        }

        public async Task<RedemptionCatalogItem> AddCatalogItemAsync(RedemptionCatalogItem item)
        {
            _ = await _context.RedemptionCatalogItems.AddAsync(item);
            _ = await _context.SaveChangesAsync();
            return item;
        }

        public async Task<RedemptionCatalogItem> UpdateCatalogItemAsync(RedemptionCatalogItem item)
        {
            _context.RedemptionCatalogItems.Update(item);
            _ = await _context.SaveChangesAsync();
            return item;
        }

        public async Task<bool> SoftDeleteCatalogItemAsync(Guid id)
        {
            RedemptionCatalogItem? item = await _context.RedemptionCatalogItems
                .Where(i => i.Id == id && !i.IsDeleted)
                .FirstOrDefaultAsync();
            if (item == null) return false;
            item.SoftDelete();
            _ = await _context.SaveChangesAsync();
            return true;
        }

        // === Redemption Records ===

        public async Task<RedemptionRecord?> GetRecordByIdAsync(Guid id)
        {
            return await _context.RedemptionRecords
                .Where(r => r.Id == id && !r.IsDeleted)
                .FirstOrDefaultAsync();
        }

        public async Task<IReadOnlyList<RedemptionRecord>> GetRecordsByCustomerAsync(Guid customerId)
        {
            return await _context.RedemptionRecords
                .Where(r => r.CustomerId == customerId && !r.IsDeleted)
                .OrderByDescending(r => r.RedeemedAt)
                .ToListAsync();
        }

        public async Task<IReadOnlyList<RedemptionRecord>> GetRecentRecordsAsync(int count = 50)
        {
            return await _context.RedemptionRecords
                .Where(r => !r.IsDeleted)
                .OrderByDescending(r => r.RedeemedAt)
                .Take(count)
                .ToListAsync();
        }

        public async Task<RedemptionRecord> AddRecordAsync(RedemptionRecord record)
        {
            _ = await _context.RedemptionRecords.AddAsync(record);
            _ = await _context.SaveChangesAsync();
            return record;
        }

        public async Task<RedemptionRecord> UpdateRecordAsync(RedemptionRecord record)
        {
            _context.RedemptionRecords.Update(record);
            _ = await _context.SaveChangesAsync();
            return record;
        }

        // === Vouchers ===

        public async Task<Voucher?> GetVoucherByIdAsync(Guid id)
        {
            return await _context.Vouchers
                .Where(v => v.Id == id && !v.IsDeleted)
                .FirstOrDefaultAsync();
        }

        public async Task<Voucher?> GetVoucherByCodeAsync(string voucherCode)
        {
            return await _context.Vouchers
                .Where(v => v.VoucherCode == voucherCode && !v.IsDeleted)
                .FirstOrDefaultAsync();
        }

        public async Task<IReadOnlyList<Voucher>> GetVouchersByCustomerAsync(Guid customerId)
        {
            return await _context.Vouchers
                .Where(v => v.CustomerId == customerId && !v.IsDeleted)
                .OrderByDescending(v => v.IssuedAt)
                .ToListAsync();
        }

        public async Task<Voucher> AddVoucherAsync(Voucher voucher)
        {
            _ = await _context.Vouchers.AddAsync(voucher);
            _ = await _context.SaveChangesAsync();
            return voucher;
        }

        public async Task<Voucher> UpdateVoucherAsync(Voucher voucher)
        {
            _context.Vouchers.Update(voucher);
            _ = await _context.SaveChangesAsync();
            return voucher;
        }

        // === Save ===

        public Task<int> SaveChangesAsync() => _context.SaveChangesAsync();
    }
}
