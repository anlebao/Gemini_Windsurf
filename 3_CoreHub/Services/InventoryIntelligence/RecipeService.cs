using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using VanAn.CoreHub.Infrastructure;
using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Services.InventoryIntelligence
{
    /// <summary>
    /// VA-IIE (Sprint B, P2.2): Recipe CRUD + versioning + cache active per ProductId (SRS §7.3).
    /// Cache in-memory ConcurrentDictionary — invalidate trên mọi thao tác ghi.
    /// </summary>
    public sealed class RecipeService(IVanAnDbContext context, ILogger<RecipeService> logger) : IRecipeService
    {
        private readonly IVanAnDbContext _context = context;
        private readonly ILogger<RecipeService> _logger = logger;

        // Cache active recipe per ProductId — invalidation explicit (SRS §7.3: không tính lại).
        private static readonly ConcurrentDictionary<Guid, Lazy<Recipe?>> ActiveRecipeCache = new();

        public async Task<Recipe?> GetActiveByProductIdAsync(Guid productId, CancellationToken ct = default)
        {
            if (ActiveRecipeCache.TryGetValue(productId, out Lazy<Recipe?>? cached) && cached.IsValueCreated)
            {
                return cached.Value;
            }

            Recipe? recipe = await QueryActive(productId, ct);
            ActiveRecipeCache[productId] = new Lazy<Recipe?>(() => recipe, LazyThreadSafetyMode.ExecutionAndPublication);
            return recipe;
        }

        public async Task<Recipe?> GetByIdAsync(Guid recipeId, bool includeLines = true, CancellationToken ct = default)
        {
            IQueryable<Recipe> query = _context.Recipes.Where(r => r.Id == recipeId && !r.IsDeleted);
            if (includeLines)
            {
                query = query.Include(r => r.Lines);
            }
            return await query.FirstOrDefaultAsync(ct);
        }

        public async Task<IReadOnlyList<Recipe>> GetVersionsAsync(Guid productId, CancellationToken ct = default)
        {
            return await _context.Recipes
                .Include(r => r.Lines)
                .Where(r => r.ProductId == productId && !r.IsDeleted)
                .OrderByDescending(r => r.Version)
                .ToListAsync(ct);
        }

        public async Task<Recipe> CreateAsync(TenantId tenantId, Guid productId, IReadOnlyList<RecipeLineInput> lines, decimal yield = 1m, decimal wasteFactor = 0m, CancellationToken ct = default)
        {
            ValidateLines(lines);

            if (await QueryActive(productId, ct) is not null)
            {
                throw new InvalidOperationException($"Product {productId} đã có recipe active — dùng PublishNewVersionAsync để tạo version mới");
            }
            if (!await _context.Products.AnyAsync(p => p.Id == productId && !p.IsDeleted, ct))
            {
                throw new InvalidOperationException($"Product {productId} không tồn tại");
            }

            Recipe recipe = new(tenantId, productId, yield, wasteFactor);
            foreach (RecipeLineInput line in lines)
            {
                recipe.AddLine(line.IngredientId, line.Quantity, line.Unit);
            }

            _context.Recipes.Add(recipe);
            _ = await _context.SaveChangesAsync(ct);
            InvalidateCache(productId);
            _logger.LogInformation("Recipe v1 created for product {ProductId} — {LineCount} lines", productId, lines.Count);
            return recipe;
        }

        public async Task<Recipe> PublishNewVersionAsync(TenantId tenantId, Guid productId, IReadOnlyList<RecipeLineInput> lines, decimal yield = 1m, decimal wasteFactor = 0m, CancellationToken ct = default)
        {
            ValidateLines(lines);

            Recipe? current = await QueryActive(productId, ct);
            if (current is null)
            {
                return await CreateAsync(tenantId, productId, lines, yield, wasteFactor, ct);
            }

            // Domain: deactivate cũ + tạo v+1 với lines mới (SRS §3.2.3).
            Recipe next = current.PublishNewVersion(yield, wasteFactor, isActive: true, replacementLines: lines.Select(l => (l.IngredientId, l.Quantity, l.Unit)).ToList());
            _context.Recipes.Add(next);
            _ = await _context.SaveChangesAsync(ct);
            InvalidateCache(productId);
            _logger.LogInformation("Recipe v{Version} published for product {ProductId}", next.Version, productId);
            return next;
        }

        public async Task<Recipe> SetActiveAsync(Guid recipeId, bool isActive, CancellationToken ct = default)
        {
            Recipe? recipe = await _context.Recipes.FirstOrDefaultAsync(r => r.Id == recipeId && !r.IsDeleted, ct)
                ?? throw new InvalidOperationException($"Recipe {recipeId} không tồn tại");

            if (recipe.IsActive == isActive)
            {
                return recipe;
            }

            // Đảm bảo chỉ 1 active per product khi bật active.
            if (isActive)
            {
                Recipe? other = await QueryActive(recipe.ProductId, ct);
                if (other is not null && other.Id != recipeId)
                {
                    throw new InvalidOperationException($"Product {recipe.ProductId} đã có recipe active khác (v{other.Version}) — deactivate nó trước");
                }
            }

            recipe.SetActive(isActive);
            _ = await _context.SaveChangesAsync(ct);
            InvalidateCache(recipe.ProductId);
            return recipe;
        }

        public void InvalidateCache(Guid productId) => ActiveRecipeCache.TryRemove(productId, out _);

        public async Task<IReadOnlyList<Product>> GetProductsAsync(CancellationToken ct = default)
        {
            return await _context.Products
                .Where(p => !p.IsDeleted)
                .OrderBy(p => p.Name)
                .ToListAsync(ct);
        }

        public async Task<IReadOnlyList<Ingredient>> GetIngredientsAsync(CancellationToken ct = default)
        {
            return await _context.Ingredients
                .OrderBy(i => i.Name)
                .ToListAsync(ct);
        }

        private Task<Recipe?> QueryActive(Guid productId, CancellationToken ct)
        {
            return _context.Recipes
                .Include(r => r.Lines)
                .Where(r => r.ProductId == productId && r.IsActive && !r.IsDeleted)
                .OrderByDescending(r => r.Version)
                .FirstOrDefaultAsync(ct);
        }

        private static void ValidateLines(IReadOnlyList<RecipeLineInput> lines)
        {
            if (lines is null || lines.Count == 0)
            {
                throw new ArgumentException("Recipe cần ít nhất 1 RecipeLine", nameof(lines));
            }
            if (lines.GroupBy(l => l.IngredientId).Any(g => g.Count() > 1))
            {
                throw new ArgumentException("RecipeLine trùng ingredient trong cùng recipe", nameof(lines));
            }
            foreach (RecipeLineInput line in lines)
            {
                if (line.Quantity <= 0)
                {
                    throw new ArgumentException($"Quantity phải > 0 cho ingredient {line.IngredientId}", nameof(lines));
                }
                if (string.IsNullOrWhiteSpace(line.Unit))
                {
                    throw new ArgumentException($"Unit bắt buộc cho ingredient {line.IngredientId}", nameof(lines));
                }
            }
        }
    }
}
