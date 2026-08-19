using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using VanAn.CoreHub.Infrastructure;
using VanAn.Shared.Domain.Aggregates.SystemSettingAggregate;

namespace VanAn.CoreHub.Services;

/// <summary>
/// OCR Hub S2: OCR engine config service — reads/writes SystemSetting in PG.
/// Pattern: copied from FeatureFlagService (IServiceScopeFactory singleton-safe + IMemoryCache 60s).
/// Keys: "Ocr:PlateEngine", "Ocr:MenuEngine". Default: "Tesseract" (backward compat).
/// </summary>
public class OcrConfigService : IOcrConfigService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IMemoryCache _cache;
    private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(60);

    private const string PlateEngineKey = "Ocr:PlateEngine";
    private const string MenuEngineKey = "Ocr:MenuEngine";
    private const string CacheKey = "ocr_config";
    private const string DefaultPlateEngine = "Tesseract";
    private const string DefaultMenuEngine = "Tesseract";

    public OcrConfigService(IServiceScopeFactory scopeFactory, IMemoryCache cache)
    {
        _scopeFactory = scopeFactory;
        _cache = cache;
    }

    public async Task<OcrEngineConfig> GetConfigAsync(CancellationToken ct = default)
    {
        if (_cache.TryGetValue<OcrEngineConfig>(CacheKey, out var cached))
            return cached!;

        string plateEngine = DefaultPlateEngine;
        string menuEngine = DefaultMenuEngine;

        using (var scope = _scopeFactory.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<IVanAnDbContext>();
            var settings = await dbContext.SystemSettings
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(s => s.Key == PlateEngineKey || s.Key == MenuEngineKey)
                .ToListAsync(ct);

            var plateSetting = settings.FirstOrDefault(s => s.Key == PlateEngineKey);
            if (plateSetting != null && !string.IsNullOrWhiteSpace(plateSetting.Value))
                plateEngine = plateSetting.Value;

            var menuSetting = settings.FirstOrDefault(s => s.Key == MenuEngineKey);
            if (menuSetting != null && !string.IsNullOrWhiteSpace(menuSetting.Value))
                menuEngine = menuSetting.Value;
        }

        var config = new OcrEngineConfig { PlateEngine = plateEngine, MenuEngine = menuEngine };
        _cache.Set(CacheKey, config, CacheTtl);
        return config;
    }

    public async Task UpdateConfigAsync(OcrEngineConfig config, Guid updatedBy, CancellationToken ct = default)
    {
        // Validate engine names
        var plateEngine = string.IsNullOrWhiteSpace(config.PlateEngine) ? DefaultPlateEngine : config.PlateEngine;
        var menuEngine = string.IsNullOrWhiteSpace(config.MenuEngine) ? DefaultMenuEngine : config.MenuEngine;

        if (plateEngine is not ("Tesseract" or "PaddleOCR"))
            throw new ArgumentException($"Invalid PlateEngine: {plateEngine}. Must be 'Tesseract' or 'PaddleOCR'.");
        if (menuEngine is not ("Tesseract" or "EasyOCR"))
            throw new ArgumentException($"Invalid MenuEngine: {menuEngine}. Must be 'Tesseract' or 'EasyOCR'.");

        using (var scope = _scopeFactory.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<IVanAnDbContext>();

            await UpsertSettingAsync(dbContext, PlateEngineKey, plateEngine, updatedBy, ct);
            await UpsertSettingAsync(dbContext, MenuEngineKey, menuEngine, updatedBy, ct);

            await dbContext.SaveChangesAsync(ct);
        }

        // Invalidate cache
        _cache.Remove(CacheKey);
    }

    private static async Task UpsertSettingAsync(IVanAnDbContext dbContext, string key, string value, Guid updatedBy, CancellationToken ct)
    {
        var setting = await dbContext.SystemSettings
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(s => s.Key == key, ct);

        if (setting == null)
        {
            setting = new SystemSetting(new(Guid.Empty), key, value, updatedBy);
            dbContext.SystemSettings.Add(setting);
        }
        else
        {
            setting.Update(value, updatedBy);
        }
    }
}
