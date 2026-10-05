using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using VanAn.CoreHub.Infrastructure;
using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Services.InventoryIntelligence
{
    /// <summary>
    /// VA-IIE Phase 3 (2026-10-05): dự báo nhập hàng / hết hàng (SRS §7.5, §8.6).
    /// ADC rolling window từ TheoreticalConsumption (chỉ ca đã submit trở lên — rows tồn tại sau khi đóng ca).
    /// Nguồn dữ liệu: ShopERP SQLite per-tenant — mọi query filter TenantId (bài học RV a21f97f2).
    /// </summary>
    public sealed class ForecastService(IVanAnDbContext context, ILogger<ForecastService> logger) : IForecastService
    {
        private readonly IVanAnDbContext _context = context;
        private readonly ILogger<ForecastService> _logger = logger;

        public async Task<VaIIeTenantConfig> GetConfigAsync(TenantId tenantId, CancellationToken ct = default)
        {
            VaIIeTenantConfig? config = await _context.VaIIeTenantConfigs
                .FirstOrDefaultAsync(c => c.TenantId == tenantId, ct);

            if (config is null)
            {
                // Get-or-create với defaults (SRS §4.2 sane defaults; không cần backfill data).
                config = new VaIIeTenantConfig(tenantId);
                _context.VaIIeTenantConfigs.Add(config);
                _ = await _context.SaveChangesAsync(ct);
                _logger.LogInformation("VaIIeTenantConfig created (defaults) for tenant {TenantId}", tenantId.Value);
            }

            return config;
        }

        public async Task<VaIIeTenantConfig> UpdateConfigAsync(TenantId tenantId, int windowDays, int leadTimeDays, int safetyDays, CancellationToken ct = default)
        {
            VaIIeTenantConfig config = await GetConfigAsync(tenantId, ct);
            config.UpdateForecastConfig(windowDays, leadTimeDays, safetyDays);
            _ = await _context.SaveChangesAsync(ct);
            _logger.LogInformation("VaIIeTenantConfig forecast updated for tenant {TenantId}: window={Window} lead={Lead} safety={Safety}",
                tenantId.Value, windowDays, leadTimeDays, safetyDays);
            return config;
        }

        public async Task<VaIIeTenantConfig> UpdateNotificationConfigAsync(
            TenantId tenantId,
            bool telegramEnabled, string? telegramBotToken, string? telegramChatId,
            bool zaloEnabled, string? zaloAccessToken, string? zaloRecipientId,
            CancellationToken ct = default)
        {
            VaIIeTenantConfig config = await GetConfigAsync(tenantId, ct);
            config.UpdateNotificationConfig(telegramEnabled, telegramBotToken, telegramChatId, zaloEnabled, zaloAccessToken, zaloRecipientId);
            _ = await _context.SaveChangesAsync(ct);
            // KHÔNG log token — secrets governance.
            _logger.LogInformation("VaIIeTenantConfig notification updated for tenant {TenantId}: Telegram={TelegramEnabled} Zalo={ZaloEnabled}",
                tenantId.Value, telegramEnabled, zaloEnabled);
            return config;
        }

        public async Task<ForecastReport> GetForecastAsync(TenantId tenantId, CancellationToken ct = default)
        {
            VaIIeTenantConfig config = await GetConfigAsync(tenantId, ct);
            int thresholdDays = config.LeadTimeDays + config.SafetyDays;

            DateTime now = DateTime.UtcNow;
            DateTime windowStart = now.AddDays(-config.AvgDailyConsumptionWindowDays);

            // Multi-tenancy: filter theo TenantId — catalog chung 1 SQLite nhiều tenant (RV 2026-10-04).
            List<Ingredient> ingredients = await _context.Ingredients
                .Where(i => i.TenantId == tenantId && !i.IsDeleted)
                .OrderBy(i => i.Name)
                .ToListAsync(ct);

            // Tiêu hao lý thuyết trong window (rows chỉ tồn tại cho ca đã submit/closed — SRS §7.3 cache).
            // Include Shift để lấy ngày + loại ca Draft (nếu có — defense-in-depth).
            List<TheoreticalConsumption> consumptions = await _context.TheoreticalConsumptions
                .Include(t => t.Shift)
                .Where(t => t.TenantId == tenantId
                            && t.Shift.StartTime >= windowStart
                            && t.Shift.StartTime <= now
                            && t.Shift.Status != ShiftStatus.Draft)
                .ToListAsync(ct);

            // Group theo ingredient: tổng tiêu hao + số ngày có ca trong window.
            Dictionary<Guid, (decimal Total, int DaysWithData)> agg = consumptions
                .GroupBy(t => t.IngredientId)
                .ToDictionary(
                    g => g.Key,
                    g => (Total: g.Sum(t => t.TheoreticalQuantity),
                          DaysWithData: g.Select(t => t.Shift.StartTime.Date).Distinct().Count()));

            List<ForecastItem> items = [];
            foreach (Ingredient ingredient in ingredients)
            {
                bool hasData = agg.TryGetValue(ingredient.Id, out (decimal Total, int DaysWithData) data) && data.DaysWithData > 0;
                decimal adc = hasData ? data.Total / Math.Max(1, data.DaysWithData) : 0m;

                int daysRemaining = hasData && adc > 0m
                    ? (int)Math.Floor(ingredient.CurrentStock / adc)
                    : (ingredient.CurrentStock <= 0m ? 0 : int.MaxValue);

                if (!hasData && ingredient.CurrentStock <= 0m)
                {
                    daysRemaining = 0; // hết hàng — Critical kể cả không có dữ liệu tiêu hao
                }

                ForecastStatus status = !hasData && ingredient.CurrentStock > 0m
                    ? ForecastStatus.NoData
                    : daysRemaining <= thresholdDays
                        ? ForecastStatus.Critical
                        : daysRemaining <= thresholdDays * 2
                            ? ForecastStatus.Warning
                            : ForecastStatus.Ok;

                decimal suggested = hasData && adc > 0m
                    ? RoundUp(adc * thresholdDays - ingredient.CurrentStock, 2)
                    : 0m;

                items.Add(new ForecastItem(
                    ingredient.Id, ingredient.Name, ingredient.Unit, ingredient.CurrentStock,
                    adc, daysRemaining, thresholdDays, suggested, status));
            }

            // Restock: ưu tiên Critical/Warning + có đề xuất > 0; Stockout: sắp cạn trước.
            List<ForecastItem> restock = items
                .Where(i => i.Status != ForecastStatus.NoData && i.SuggestedOrderQuantity > 0m)
                .OrderBy(i => i.Status == ForecastStatus.Critical ? 0 : 1)
                .ThenBy(i => i.DaysRemaining)
                .ToList();
            if (restock.Count == 0)
            {
                restock = items
                    .Where(i => i.Status == ForecastStatus.Critical && i.SuggestedOrderQuantity <= 0m)
                    .ToList(); // hết hàng không cần nhập (0 stock) vẫn hiện
            }

            List<ForecastItem> stockout = items
                .OrderBy(i => i.DaysRemaining)
                .ToList();

            // Trend: tiêu hao theo ngày (window) per ingredient có dữ liệu — giới hạn top 20 theo tổng tiêu hao.
            Dictionary<Guid, IReadOnlyList<DailyConsumption>> trend = [];
            foreach ((Guid ingredientId, (decimal Total, int _) data) in agg
                         .OrderByDescending(kv => kv.Value.Total)
                         .Take(20))
            {
                List<DailyConsumption> daily = consumptions
                    .Where(t => t.IngredientId == ingredientId)
                    .GroupBy(t => t.Shift.StartTime.Date)
                    .OrderBy(g => g.Key)
                    .Select(g => new DailyConsumption(g.Key, g.Sum(t => t.TheoreticalQuantity)))
                    .ToList();
                trend[ingredientId] = daily;
            }

            _logger.LogDebug("Forecast computed for tenant {TenantId}: {IngredientCount} ingredients, {ConsumptionCount} consumption rows",
                tenantId.Value, ingredients.Count, consumptions.Count);

            return new ForecastReport(now, config.AvgDailyConsumptionWindowDays, config.LeadTimeDays, config.SafetyDays,
                restock, stockout, trend);
        }

        private static decimal RoundUp(decimal value, int decimals)
        {
            decimal factor = (decimal)Math.Pow(10, decimals);
            return Math.Ceiling(value * factor) / factor;
        }
    }
}
