using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Services.InventoryIntelligence
{
    /// <summary>
    /// VA-IIE (Sprint B, P2.5): context đầy đủ cho 1 lần evaluate alert engine khi đóng ca (SRS §7.4).
    /// Được dựng bởi ShiftReportService từ dữ liệu đã load.
    /// </summary>
    public sealed record ShiftAlertContext
    {
        /// <summary>Ca đang evaluate (Draft — sắp submit khi đóng ca).</summary>
        public required Shift Shift { get; init; }

        /// <summary>Toàn bộ kiểm kê của ca (Opening + Closing + MidShiftStockIn).</summary>
        public required IReadOnlyList<InventoryCount> InventoryCounts { get; init; }

        /// <summary>Tiêu hao lý thuyết + thực tế + variance (kết quả variance analysis).</summary>
        public required IReadOnlyList<TheoreticalConsumption> Consumptions { get; init; }

        /// <summary>Ingredient theo Id (kèm ReorderPoint = MinStockThreshold, VarianceThresholdPercent override, Category).</summary>
        public required IReadOnlyDictionary<Guid, Ingredient> Ingredients { get; init; }

        /// <summary>Recipe theo ProductId (active — kèm lines) — cho RECIPE_MISSING + revenue theo ingredient.</summary>
        public required IReadOnlyDictionary<Guid, Recipe> RecipesByProduct { get; init; }

        /// <summary>Doanh thu theo product (VND) — Σ item subtotal của order Completed.</summary>
        public required IReadOnlyDictionary<Guid, decimal> RevenueByProduct { get; init; }

        /// <summary>Tổng doanh thu ca (VND).</summary>
        public decimal TotalSales { get; init; }

        /// <summary>Tổng số món bán trong ca.</summary>
        public int TotalUnitsSold { get; init; }

        /// <summary>Thời điểm evaluate (testability — mặc định UtcNow).</summary>
        public DateTime Now { get; init; } = DateTime.UtcNow;

        // ── Derived helpers (tính 1 lần khi dựng context) ──

        /// <summary>Opening count theo ingredient.</summary>
        public IReadOnlyDictionary<Guid, decimal> OpeningCountByIngredient { get; init; } = new Dictionary<Guid, decimal>();

        /// <summary>Net stock change theo ingredient = Closing − Opening − Restock (âm = nguyên liệu giảm — SRS §4.1 rule 4/5).</summary>
        public IReadOnlyDictionary<Guid, decimal> StockChangeByIngredient { get; init; } = new Dictionary<Guid, decimal>();

        /// <summary>Closing count theo ingredient (STOCK_LOW).</summary>
        public IReadOnlyDictionary<Guid, decimal> ClosingCountByIngredient { get; init; } = new Dictionary<Guid, decimal>();

        /// <summary>Nhập thêm trong ca theo ingredient (MIDSHIFT_RESTOCK_UNUSUAL).</summary>
        public IReadOnlyDictionary<Guid, decimal> RestockByIngredient { get; init; } = new Dictionary<Guid, decimal>();

        /// <summary>Doanh thu theo ingredient — Σ revenue của product dùng ingredient đó (SALES_HIGH_STOCK_STABLE / STOCK_DROP_NO_SALES).</summary>
        public IReadOnlyDictionary<Guid, decimal> RevenueByIngredient { get; init; } = new Dictionary<Guid, decimal>();
    }
}
