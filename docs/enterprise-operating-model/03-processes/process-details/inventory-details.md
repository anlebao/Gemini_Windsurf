# Inventory Details — D2 deep-dive

> Metadata in `registries/activities.yaml` ACTV-641..650 · coverage anchors ACTV-601..603 · captured at `f447a1a8`.

## Verified mechanics

- **Store split:** stock entities (`StockMovementEntity`, `Ingredient`, `InventoryCount`, `ShiftAlert`) live on **ShopERPDbContext (SQLite per-tenant)** — business data, not accounting. `PurchaseReceipt` voucher lives on PG. Link = Id references only, **no physical FK** (cross-DB).
- **Voucher → stock:** `PurchaseReceiptService` takes `IStockMovementService?` optional — **only ShopERP host registers it**; CoreHub/Gateway hosts skip silently (by design). `ApplyReceiptStockAsync` writes `PurchaseIn` per line with `IngredientId`, bumps `CurrentStock`, sets `PricePerUnit` = latest purchase cost (user decision), stamps `StockAppliedAt`. `ApplyReturnStockAsync` writes `PurchaseReturn`, allows negative stock (goods consumed before return). Idempotency key: `(PurchaseReceiptId, PurchaseReceiptLineId, MovementType)` — retry never double-applies.
- **Stock truth:** `StockMovement` = audit SoT (`Quantity` always positive; direction from `MovementType` 6 values: PurchaseIn/Return, ManualIn/Out, AdjustmentIn/Out). `CurrentStock` = snapshot/cache. `GetStockReportAsync` rebuilds Nhập–Xuất–Tồn: baseline = `CurrentStock − Σmovements` → opening/closing correct even for stock predating the movement table.
- **Manual ops:** `RecordManualAsync` (ManualIn/ManualOut) on `/inventory/ingredients`; out > stock → `InvalidOperationException`. `ListMovementsAsync` for drill-down/card view.
- **Count:** `InventoryCount` per shift×ingredient (`CountType`, base unit, `MidShiftStockIn` restock accumulation); `Update`/`AddRestock` allowed only while shift Draft/Submitted — blocked after Closed (NFR-7).
- **Alerts:** shift close → `AlertEngine` (10 rules, `IAlertRule` implementations) → `ShiftAlert` (`ING_VARIANCE_HIGH`, `STOCK_LOW`, …, severity + variance value/%, resolve tracking) — in-app default; `TelegramAlertNotifier`/`ZaloAlertNotifier` optional via dispatcher.
- **Consumption:** `InventoryService` (CoreHub, `IInventoryService` in 1_Shared) — `CalculateIngredientDeductionAsync` (order items → Recipe → RecipeLines → per-ingredient qty), `CanFulfillOrderAsync` (short → false, warn-log), `UpdateInventoryAsync` applies deductions.
- **Valuation:** `InventoryValuationController` (`GET /api/shoperp/inventory/valuation?tenantId`, internal) → `Σ(CurrentStock × PricePerUnit)` point-in-time snapshot — feeds reconcile R13 via `InventoryValuationHttpProvider` (fail-open).
- **Intelligence suite** (`3_CoreHub/Services/InventoryIntelligence/`): ForecastService, VarianceAnalysisService, TheoreticalConsumptionService, FoodCostService, ProfitabilityService, RecipeService, ShiftReportService + alert rules/thresholds — consumed by Forecast/AlertCenter/ShiftReport/StockReport pages. Excel export: `Reports/InventoryExcelReport`.

## Execution split

MANUAL: ACTV-643/644/650 (3) · AUTOMATED: ACTV-641/642/645/646/647/648/649 (7) · HYBRID: none.

## INFERRED items

- Alert rule set content (10 rules) — `IAlertRule`/`AlertThresholds` exist; individual thresholds not enumerated this pass.
- Forecast algorithm (moving average vs other) — service verified, internals not traced.
- Deduction write-path on order completion (who calls `UpdateInventoryAsync` in the live order flow) — interface verified; call site not traced.

## Draft gaps → gap register candidates

| Candidate | Class | Status |
|---|---|---|
| Voucher posted on PG but stock skipped on non-ShopERP host (optional dep) | OPERATIONAL | → RISK-017 registered |
| Negative stock on return skews point-in-time valuation | OPERATIONAL | → RISK-018 registered |
| Variance alerts in-app only — may go unseen | OPERATIONAL | → RISK-019 registered |
| Deduction check vs actual stock write — `InventoryService` (PG context) vs `StockMovement` (SQLite): two stock representations, unverified sync | OPERATIONAL | draft — needs call-site trace |
