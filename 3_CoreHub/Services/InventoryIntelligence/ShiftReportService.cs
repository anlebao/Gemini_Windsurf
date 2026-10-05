using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using VanAn.CoreHub.Infrastructure;
using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Services.InventoryIntelligence
{
    /// <summary>
    /// VA-IIE (Sprint B, P2.1): orchestrator — mở/đóng ca + kiểm kê + bàn giao.
    /// SubmitShiftAsync = "Tính &amp; Đóng ca": theoretical → variance → alerts → persist (1 transaction).
    /// </summary>
    public sealed class ShiftReportService(
        IVanAnDbContext context,
        ITheoreticalConsumptionService theoreticalConsumptionService,
        IVarianceAnalysisService varianceAnalysisService,
        IAlertEngine alertEngine,
        ILogger<ShiftReportService> logger) : IShiftReportService
    {
        private readonly IVanAnDbContext _context = context;
        private readonly ITheoreticalConsumptionService _theoreticalConsumptionService = theoreticalConsumptionService;
        private readonly IVarianceAnalysisService _varianceAnalysisService = varianceAnalysisService;
        private readonly IAlertEngine _alertEngine = alertEngine;
        private readonly ILogger<ShiftReportService> _logger = logger;

        public async Task<Shift> OpenShiftAsync(TenantId tenantId, ShiftType shiftType, Guid staffUserId, DateTime? startTime = null, CancellationToken ct = default)
        {
            Shift shift = new(tenantId, shiftType, staffUserId, startTime);
            _context.Shifts.Add(shift);
            _ = await _context.SaveChangesAsync(ct);
            _logger.LogInformation("Shift opened {ShiftId} ({ShiftType}) by {StaffUserId}", shift.Id, shiftType, staffUserId);
            return shift;
        }

        public async Task<InventoryCount> AddInventoryCountAsync(Guid shiftId, Guid ingredientId, CountType countType, decimal quantity, string unit, decimal? midShiftStockIn = null, CancellationToken ct = default)
        {
            Shift shift = await GetShiftAsync(shiftId, ct);
            EnsureEditable(shift);

            InventoryCount? existing = await _context.InventoryCounts
                .FirstOrDefaultAsync(c => c.ShiftId == shiftId && c.IngredientId == ingredientId && c.CountType == countType, ct);

            if (existing is null)
            {
                existing = new InventoryCount(shift.TenantId, shiftId, ingredientId, countType, quantity, unit, midShiftStockIn);
                _context.InventoryCounts.Add(existing);
            }
            else
            {
                existing.Update(quantity, unit, midShiftStockIn);
            }

            _ = await _context.SaveChangesAsync(ct);
            return existing;
        }

        public async Task<InventoryCount> AddRestockAsync(Guid shiftId, Guid ingredientId, decimal quantity, string unit, CancellationToken ct = default)
        {
            if (quantity <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(quantity), "Restock quantity phải > 0");
            }

            Shift shift = await GetShiftAsync(shiftId, ct);
            EnsureEditable(shift);

            // Restock cộng dồn vào MidShiftStockIn của opening count row (SRS §3.1.2: Tồn đầu + Nhập thêm).
            InventoryCount? opening = await _context.InventoryCounts
                .FirstOrDefaultAsync(c => c.ShiftId == shiftId && c.IngredientId == ingredientId && c.CountType == CountType.Opening, ct);

            if (opening is null)
            {
                opening = new InventoryCount(shift.TenantId, shiftId, ingredientId, CountType.Opening, 0m, unit, quantity);
                _context.InventoryCounts.Add(opening);
            }
            else
            {
                opening.AddRestock(quantity);
            }

            _ = await _context.SaveChangesAsync(ct);
            return opening;
        }

        public async Task<Shift> SubmitShiftAsync(Guid shiftId, Guid staffUserId, decimal cashCount, decimal posCashTotal, string? handoverNotes = null, DateTime? endTime = null, CancellationToken ct = default)
        {
            Shift shift = await GetShiftAsync(shiftId, ct);

            // Domain submit — validate Draft + cash/endTime; set in-memory (engine cần CashCount/Status).
            shift.Submit(handoverNotes, cashCount, posCashTotal, endTime);

            IReadOnlyList<InventoryCount> counts = await _context.InventoryCounts
                .Where(c => c.ShiftId == shiftId)
                .ToListAsync(ct);

            await using Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction transaction = await _context.BeginTransactionAsync(ct);
            try
            {
                // 1. Tiêu hao lý thuyết: POS OrderItems × Recipe (version active tại thời điểm bán) × (1+WasteFactor)
                IReadOnlyDictionary<Guid, decimal> theoretical = await _theoreticalConsumptionService.CalculateForShiftAsync(shift, ct);

                // 2. Variance: Actual = Opening + MidShift − Closing → persist TheoreticalConsumption rows (cache, SRS §7.3)
                IReadOnlyList<TheoreticalConsumption> consumptions = await _varianceAnalysisService.AnalyzeShiftAsync(shift, counts, theoretical, ct);

                // 3. Alert engine (10 rules) — persist ShiftAlerts
                ShiftAlertContext alertContext = await BuildAlertContextAsync(shift, counts, consumptions, ct);
                IReadOnlyList<ShiftAlert> alerts = await _alertEngine.EvaluateAsync(alertContext, thresholds: null, ct);
                if (alerts.Count > 0)
                {
                    _context.ShiftAlerts.AddRange(alerts);
                }

                // 4. Persist shift (status → Submitted + cash/notes) trong cùng transaction
                _ = await _context.SaveChangesAsync(ct);
                await transaction.CommitAsync(ct);

                _logger.LogInformation("Shift {ShiftId} submitted — {ConsumptionCount} consumptions, {AlertCount} alerts",
                    shiftId, consumptions.Count, alerts.Count);
                return shift;
            }
            catch
            {
                await transaction.RollbackAsync(CancellationToken.None);
                throw;
            }
        }

        public async Task<Shift> AcknowledgeShiftAsync(Guid shiftId, Guid acknowledgedBy, CancellationToken ct = default)
        {
            Shift shift = await GetShiftAsync(shiftId, ct);
            shift.Acknowledge(acknowledgedBy);
            _ = await _context.SaveChangesAsync(ct);
            _logger.LogInformation("Shift {ShiftId} acknowledged by {UserId}", shiftId, acknowledgedBy);
            return shift;
        }

        public async Task<Shift> CloseShiftAsync(Guid shiftId, Guid acknowledgedBy, CancellationToken ct = default)
        {
            Shift shift = await GetShiftAsync(shiftId, ct);
            shift.Close();
            _ = await _context.SaveChangesAsync(ct);
            _logger.LogInformation("Shift {ShiftId} closed", shiftId);
            return shift;
        }

        public async Task<Dtos.ShiftReportDto> GetShiftReportAsync(Guid shiftId, CancellationToken ct = default)
        {
            Shift shift = await GetShiftAsync(shiftId, ct);

            IReadOnlyList<InventoryCount> counts = await _context.InventoryCounts
                .Where(c => c.ShiftId == shiftId)
                .ToListAsync(ct);
            IReadOnlyList<TheoreticalConsumption> consumptions = await _context.TheoreticalConsumptions
                .Where(t => t.ShiftId == shiftId)
                .ToListAsync(ct);
            IReadOnlyList<ShiftAlert> alerts = await _context.ShiftAlerts
                .Where(a => a.ShiftId == shiftId)
                .ToListAsync(ct);

            IReadOnlyList<Guid> ingredientIds = counts.Select(c => c.IngredientId)
                .Concat(consumptions.Select(c => c.IngredientId))
                .Distinct()
                .ToList();
            Dictionary<Guid, Ingredient> ingredients = await _context.Ingredients
                .Where(i => ingredientIds.Contains(i.Id))
                .ToDictionaryAsync(i => i.Id, ct);

            DateTime end = shift.EndTime ?? DateTime.UtcNow;
            List<Order> orders = await _context.Orders
                .Include(o => o.Items)
                .Where(o => o.CreatedAt >= shift.StartTime && o.CreatedAt <= end && o.TenantId == shift.TenantId)
                .OrderByDescending(o => o.CreatedAt)
                .ToListAsync(ct);

            string IngredientName(Guid id) => ingredients.TryGetValue(id, out Ingredient? i) ? i.Name : id.ToString();

            var opening = counts.Where(c => c.CountType == CountType.Opening)
                .Select(c => new Dtos.InventoryCountDto(c.IngredientId, IngredientName(c.IngredientId), c.Unit, c.CountType, c.Quantity, c.MidShiftStockIn))
                .ToList();
            var closing = counts.Where(c => c.CountType == CountType.Closing)
                .Select(c => new Dtos.InventoryCountDto(c.IngredientId, IngredientName(c.IngredientId), c.Unit, c.CountType, c.Quantity, c.MidShiftStockIn))
                .ToList();

            var consumptionDtos = consumptions
                .Select(c => new Dtos.ConsumptionDto(
                    c.IngredientId,
                    IngredientName(c.IngredientId),
                    ingredients.TryGetValue(c.IngredientId, out Ingredient? ing) ? ing.Unit : string.Empty,
                    c.TheoreticalQuantity, c.ActualQuantity, c.Variance, c.VariancePercent,
                    IVarianceAnalysisService.Classify(c.Variance)))
                .ToList();

            var alertDtos = alerts
                .Select(a => new Dtos.ShiftAlertDto(a.Id, a.AlertCode, a.Severity, a.Message, a.IngredientId, a.VarianceValue, a.VariancePercent, a.IsResolved))
                .ToList();

            var orderDtos = orders
                .Select(o => new Dtos.OrderSummaryDto(o.Id, o.OrderType, o.TotalPrice, o.CreatedAt, o.Items.Count))
                .ToList();

            decimal? diff = shift.CashCount.HasValue && shift.PosCashTotal.HasValue ? shift.CashCount - shift.PosCashTotal : null;

            return new Dtos.ShiftReportDto
            {
                Shift = new Dtos.ShiftDto(shift.Id, shift.ShiftType, shift.Status, shift.StartTime, shift.EndTime,
                    shift.StaffUserId, shift.AcknowledgedBy, shift.AcknowledgedAt, shift.HandoverNotes),
                OpeningCounts = opening,
                ClosingCounts = closing,
                Consumptions = consumptionDtos,
                Cash = new Dtos.CashSectionDto(shift.CashCount, shift.PosCashTotal, diff, diff is null or 0m),
                Alerts = alertDtos,
                Orders = orderDtos
            };
        }

        public async Task<IReadOnlyList<Shift>> ListShiftsAsync(TenantId tenantId, DateTime? from = null, DateTime? to = null, CancellationToken ct = default)
        {
            IQueryable<Shift> query = _context.Shifts.Where(s => s.TenantId == tenantId);
            if (from is not null)
            {
                query = query.Where(s => s.StartTime >= from);
            }
            if (to is not null)
            {
                query = query.Where(s => s.StartTime <= to);
            }
            return await query.OrderByDescending(s => s.StartTime).ToListAsync(ct);
        }

        public async Task<IReadOnlyList<Ingredient>> GetIngredientsAsync(TenantId tenantId, CancellationToken ct = default)
        {
            // Multi-tenancy: catalog chung 1 SQLite nhiều tenant — filter theo TenantId (RV 2026-10-04).
            return await _context.Ingredients
                .Where(i => i.TenantId == tenantId)
                .OrderBy(i => i.Name)
                .ToListAsync(ct);
        }

        // ── Helpers ──────────────────────────────────────────────────────────────

        private async Task<Shift> GetShiftAsync(Guid shiftId, CancellationToken ct)
        {
            return await _context.Shifts.FirstOrDefaultAsync(s => s.Id == shiftId, ct)
                ?? throw new InvalidOperationException($"Shift {shiftId} không tồn tại");
        }

        private static void EnsureEditable(Shift shift)
        {
            if (shift.Status == ShiftStatus.Closed)
            {
                throw new InvalidOperationException($"Shift {shift.Id} đã Closed — InventoryCount immutable (NFR-7), sửa qua adjustment");
            }
        }

        private async Task<ShiftAlertContext> BuildAlertContextAsync(
            Shift shift,
            IReadOnlyList<InventoryCount> counts,
            IReadOnlyList<TheoreticalConsumption> consumptions,
            CancellationToken ct)
        {
            IReadOnlyList<Guid> ingredientIds = counts.Select(c => c.IngredientId)
                .Concat(consumptions.Select(c => c.IngredientId))
                .Distinct()
                .ToList();
            Dictionary<Guid, Ingredient> ingredients = await _context.Ingredients
                .Where(i => ingredientIds.Contains(i.Id))
                .ToDictionaryAsync(i => i.Id, ct);

            DateTime end = shift.EndTime ?? DateTime.UtcNow;
            List<Order> orders = await _context.Orders
                .Include(o => o.Items)
                .Where(o => o.CreatedAt >= shift.StartTime && o.CreatedAt <= end && o.TenantId == shift.TenantId)
                .ToListAsync(ct);

            Dictionary<Guid, decimal> revenueByProduct = [];
            Dictionary<Guid, int> unitsByProduct = [];
            decimal totalSales = 0m;
            int totalUnits = 0;
            foreach (Order order in orders)
            {
                if (order.Status != OrderStatusId.Completed)
                {
                    continue;
                }
                totalSales += order.TotalPrice;
                foreach (OrderItem item in order.Items)
                {
                    revenueByProduct[item.ProductId] = revenueByProduct.GetValueOrDefault(item.ProductId) + (item.Quantity * item.UnitPrice);
                    unitsByProduct[item.ProductId] = unitsByProduct.GetValueOrDefault(item.ProductId) + item.Quantity;
                    totalUnits += item.Quantity;
                }
            }

            List<Guid> soldProductIds = revenueByProduct.Keys.ToList();
            Dictionary<Guid, Recipe> recipesByProduct = await _context.Recipes
                .Include(r => r.Lines)
                .Where(r => r.IsActive && soldProductIds.Contains(r.ProductId) && !r.IsDeleted)
                .ToDictionaryAsync(r => r.ProductId, ct);

            Dictionary<Guid, decimal> opening = [];
            Dictionary<Guid, decimal> closing = [];
            Dictionary<Guid, decimal> restock = [];
            foreach (InventoryCount count in counts)
            {
                if (count.CountType == CountType.Opening)
                {
                    opening[count.IngredientId] = count.Quantity;
                    restock[count.IngredientId] = restock.GetValueOrDefault(count.IngredientId) + (count.MidShiftStockIn ?? 0m);
                }
                else if (count.CountType == CountType.Closing)
                {
                    closing[count.IngredientId] = count.Quantity;
                }
            }

            Dictionary<Guid, decimal> stockChange = [];
            foreach (Guid id in opening.Keys.Concat(closing.Keys).Distinct())
            {
                // Net change = Closing − Opening − Restock (âm = nguyên liệu giảm — SRS §4.1 rule 4/5).
                stockChange[id] = closing.GetValueOrDefault(id) - opening.GetValueOrDefault(id) - restock.GetValueOrDefault(id);
            }

            Dictionary<Guid, decimal> revenueByIngredient = [];
            foreach ((Guid productId, decimal revenue) in revenueByProduct)
            {
                if (recipesByProduct.TryGetValue(productId, out Recipe? recipe))
                {
                    foreach (RecipeLine line in recipe.Lines)
                    {
                        revenueByIngredient[line.IngredientId] = revenueByIngredient.GetValueOrDefault(line.IngredientId) + revenue;
                    }
                }
            }

            return new ShiftAlertContext
            {
                Shift = shift,
                InventoryCounts = counts,
                Consumptions = consumptions,
                Ingredients = ingredients,
                RecipesByProduct = recipesByProduct,
                RevenueByProduct = revenueByProduct,
                TotalSales = totalSales,
                TotalUnitsSold = totalUnits,
                Now = DateTime.UtcNow,
                OpeningCountByIngredient = opening,
                ClosingCountByIngredient = closing,
                RestockByIngredient = restock,
                StockChangeByIngredient = stockChange,
                RevenueByIngredient = revenueByIngredient
            };
        }
    }
}
