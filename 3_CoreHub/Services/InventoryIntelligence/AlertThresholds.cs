namespace VanAn.CoreHub.Services.InventoryIntelligence
{
    /// <summary>
    /// VA-IIE (Sprint B, P2.5): ngưỡng cảnh báo — per-tenant configurable + sane defaults (SRS §4.2).
    /// Per-ingredient override: <c>Ingredient.VarianceThresholdPercent</c> cho ING_VARIANCE_HIGH.
    /// Per-category: <c>ConsumableStandardRatio</c> áp cho IngredientCategory.Consumable.
    /// </summary>
    public sealed record AlertThresholds
    {
        /// <summary>ING_VARIANCE_HIGH — ngưỡng chênh lệch tiêu hao cho phép (%) — mặc định 5%.</summary>
        public decimal VarianceThresholdPercent { get; init; } = 5m;

        /// <summary>CONSUMPTION_OVER_LIMIT — ngưỡng vượt định mức → Critical (%) — mặc định 15%.</summary>
        public decimal MaxVariancePercent { get; init; } = 15m;

        /// <summary>CONSUMABLE_OVER_STANDARD — dung sai ly/nắp/ống hút (%) — mặc định 10%.</summary>
        public decimal ConsumableTolerancePercent { get; init; } = 10m;

        /// <summary>CONSUMABLE_OVER_STANDARD — số consumable dùng chuẩn mỗi món bán (mặc định 1).</summary>
        public decimal ConsumableStandardRatio { get; init; } = 1m;

        /// <summary>CASH_MISMATCH — dung sai tiền mặt (VND) — mặc định 10.000.</summary>
        public decimal CashTolerance { get; init; } = 10_000m;

        /// <summary>SHIFT_NOT_ACKNOWLEDGED — thời gian tối đa chờ xác nhận bàn giao (giờ) — mặc định 2h.</summary>
        public int ShiftAckTimeoutHours { get; init; } = 2;

        /// <summary>SALES_HIGH_STOCK_STABLE — doanh số (VND) coi là "cao" — mặc định 500.000.</summary>
        public decimal SalesHighThreshold { get; init; } = 500_000m;

        /// <summary>SALES_HIGH_STOCK_STABLE — dung sai stock change ≈ 0 (% của opening) — mặc định 5%.</summary>
        public decimal StockStableTolerancePercent { get; init; } = 5m;

        /// <summary>MIDSHIFT_RESTOCK_UNUSUAL — nhập thêm trong ca vượt % của opening coi là bất thường — mặc định 50%.</summary>
        public decimal RestockUnusualThresholdPercent { get; init; } = 50m;

        /// <summary>Rule bị disable per-tenant (theo AlertCode).</summary>
        public IReadOnlySet<string> DisabledRules { get; init; } = new HashSet<string>();
    }
}
