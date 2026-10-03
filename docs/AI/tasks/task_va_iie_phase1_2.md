# TASK CARD: VA-IIE Phase 1-2 — Shift Report + Variance Analysis + Alert Engine (Sprint B)

> Created: 2026-10-03 (Sprint B ACTIVE — user approved)
> Source SRS: `docs/requirements/Van_An_SRS_Inventory_Intelligence_Engine.md`
> Master plan: `docs/AI/tasks/master_plan_financial_intelligence_mvp2_va_iie.md` (SPRINT B + review notes)
> Branch: `main`
> Status: PLANNED — chờ user review task card trước khi implement

---

## 1. GOAL & CONTEXT

- **Mục tiêu:** Số hóa báo cáo cuối ca (mẫu giấy "ĐẦM COFFEE") → kiểm kê đầu/cuối ca + tiêu hao lý thuyết từ POS×Recipe + variance analysis + alert engine (10 rules) cho tenant F&B.
- **Trigger thỏa (2026-10-03):** MVP-2 (FI) merged + deployed + RV pass ✓ + user approve regardless ✓.
- **Data flow:** per-tenant SQLite (ShopERP) — dữ liệu vận hành, KHÔNG lên PG ở MVP (SRS §6.4). Không cần Gateway API cho MVP — services in-process (pattern #185-1, như LoyaltyDashboard).
- **HR-Payroll (Sprint B2, user chốt 2026-10-03):** làm SAU Phase 1-2 này → Sprint B phải chuẩn bị: `Shift.StaffUserId` (FK) thay `StaffName` string + xuất KPI thưởng (doanh số/StaffShift, Food Cost, Waste Ratio).

## 2. ACTIVE WORKFLOW ROUTING

- **Workflow:** `newfeaturebuild.md` (ANALYZE → IMPLEMENT — 7 step)
- **Execution Mode:** ANALYZE (khi bắt đầu) → IMPLEMENT (sau user approve task card)
- **Skills (max 3):** `dynamic-hkd-book-architecture` (không — F&B) → dùng `order-workflow-unified` (Order/POS integration) + `ui-platform-compliance-review` (4 pages) + `domain-integrity-validation` (Single-Identity, Domain purity)

## 3. SCOPE (Phase 1-2 per SRS §10)

### Phase 1 — Foundation (Domain + EF + migration)
- [ ] P1.1: **4 entity mới** trong Domain.cs (Single-Identity: constructor set `Id = BusinessKey.Value`, EF `Ignore` VO):
  - `Shift` (ShiftId VO, ShiftType, StartTime, EndTime, **StaffUserId Guid FK → User.Id** [thay StaffName string — chuẩn bị HR B2], AcknowledgedBy/At, Status Draft/Submitted/Acknowledged/Closed, HandoverNotes, CashCount, PosCashTotal)
  - `InventoryCount` (ShiftId FK, IngredientId FK, CountType Opening/Closing, Quantity, Unit, MidShiftStockIn?)
  - `ShiftAlert` (ShiftId FK, AlertCode, Severity Warning/Critical, Message, IngredientId?, VarianceValue?, VariancePercent?, IsResolved, ResolvedAt/By, ResolutionNote)
  - `TheoreticalConsumption` (ShiftId FK, IngredientId FK, TheoreticalQuantity, ActualQuantity, Variance, VariancePercent)
- [ ] P1.2: **Ingredient extend additive** (entity đã tồn tại Domain.cs:1430): thêm `IngredientCategory Category` (RawMaterial/Consumable/Supply) + `VarianceThresholdPercent?` (override per-item). **ReorderPoint = dùng `MinStockThreshold` có sẵn — KHÔNG thêm field trùng.**
- [ ] P1.3: **Recipe REFACTOR (breaking):** flat `Recipe` (1 row = ProductId+IngredientId+QuantityNeeded) → `Recipe` header (ProductId, Version, Yield=1, WasteFactor, IsActive, EffectiveFrom) + `RecipeLine` (RecipeId FK, IngredientId, Quantity, Unit). Migration + **backfill**: mỗi row flat cũ → Recipe v1 + RecipeLine. Audit mọi nơi dùng Recipe cũ (OrderService COGS, onboarding seed, ProductDetail, kế toán).
- [ ] P1.4: EF configs + migration `AddShiftReportRecipeRefactor` (ShopERP SQLite). Enum mới: ShiftType, ShiftStatus, CountType, IngredientCategory, AlertSeverity (SRS §6.2).

### Phase 2 — Services (CoreHub, namespace `VanAn.CoreHub.Services.InventoryIntelligence`)
- [ ] P2.1: `IShiftReportService` — mở/đóng ca, kiểm kê đầu/cuối, restock, submit/acknowledge, cash count; `InventoryCount` immutable sau Closed (NFR-7, sửa qua adjustment)
- [ ] P2.2: `IRecipeService` — CRUD + versioning + cache active theo ProductId (SRS §7.3)
- [ ] P2.3: `ITheoreticalConsumptionService` — POS OrderItems × Recipe (theo version active tại thời điểm bán) × (1+WasteFactor); cache vào table; batch nếu ca > 500 đơn (SRS §7.3)
- [ ] P2.4: `IVarianceAnalysisService` — Actual = Opening + MidShiftStockIn − Closing; Variance = Actual − Theoretical; phân loại hao hụt/bất thường/bình thường (SRS §3.4)
- [ ] P2.5: `IAlertEngine` + 10 rule (`IAlertRule` interface, SRS §7.4): ING_VARIANCE_HIGH, STOCK_LOW, CONSUMPTION_OVER_LIMIT, SALES_HIGH_STOCK_STABLE, STOCK_DROP_NO_SALES, MIDSHIFT_RESTOCK_UNUSUAL, CONSUMABLE_OVER_STANDARD, CASH_MISMATCH, RECIPE_MISSING, SHIFT_NOT_ACKNOWLEDGED; thresholds per-tenant + defaults (SRS §4.2)
- [ ] P2.6: `IFoodCostService` — Food Cost / COGS / Waste Ratio theo món/ca/ngày
- [ ] P2.7: Unit tests (variance calc, 10 alert rules, food cost, recipe versioning, shift lifecycle) + integration tests (shift workflow end-to-end)

### Phase 3 — API + UI (ShopERP)
- [ ] P3.1: UI (UI Platform 100%, Gate 5): `ShiftReport.razor` (kiểm kê đầu/cuối, tiền mặt, ghi chú, tính & đóng ca, hiển thị 6 phần — SRS §5.1), `RecipeManagement.razor` (CRUD + version history), `InventoryDashboard.razor` (tồn kho + widget alert + chart), `AlertCenter.razor` (filter/resolve/history)
- [ ] P3.2: NavMenu (Owner/StoreKeeper) + Sitemap
- [ ] P3.3: REST API (SRS §7.6) — **DEFER đến Phase 4** (MVP in-process, tránh duplication; PWA/mobile chưa có demand). Ghi chú trong SRS nếu cần.
- [ ] P3.4: E2E spec Gate 4: `va-iie-shift.spec.ts` (mở ca → kiểm kê → POS bán → đóng ca → variance + alert hiển thị)

### Phase 4 — Validation & RV
- [ ] P4.1: `guard-check.ps1` + `dotnet build VanAn.sln` + Core.Tests + ShopERP.Tests ALL PASS
- [ ] P4.2: Deploy + RV production (5 lớp) — cần tenant F&B thật (user tạo/duyệt)

## 4. SRS REVIEW NOTES — RESOLUTION (2026-10-03)

| # | SRS vấn đề | Resolution |
|---|---|---|
| 1 | `Shift.StaffName` string | → `StaffUserId` (Guid FK → User.Id) — **bắt buộc** (HR B2 chấm công theo nhân viên) |
| 2 | Ingredient mới? | Đã tồn tại — extend additive (Category, VarianceThresholdPercent?); ReorderPoint = MinStockThreshold có sẵn |
| 3 | NFR-5 role "Manager" không tồn tại | MVP: Manager = Owner (xem report + resolve alert); StoreKeeper = nhập kiểm kê; Staff = nhập kiểm kê ca mình. Không thêm role mới (tránh scope) |
| 4 | Offline-first (NFR-3/PWA) | MVP **online-first** — offline để Phase 4 (Blazor Server limitation) |
| 5 | Alert channels (Email/Telegram/Zalo) | MVP **in-app only** (SRS §4.3: in-app = tất cả) — bots Phase 4 |
| 6 | Recipe refactor breaking | Migration + backfill + audit callers (P1.3) |
| 7 | REST API (SRS §7.6) | Defer Phase 4 — MVP in-process (P3.3) |
| 8 | `Order.StaffId` chưa tồn tại | Không đụng ở Sprint B (thưởng doanh số là B2) — chỉ note trong master plan |

## 5. SUCCESS CRITERIA (SRS §12)

- [ ] Mở ca → kiểm kê đầu ca → lưu OK
- [ ] POS bán trong ca → book inventory cập nhật
- [ ] Nhập thêm trong ca → MidShiftStockIn ghi nhận
- [ ] Đóng ca → kiểm kê cuối + tiền mặt + ghi chú → tự tính tiêu hao lý thuyết + variance
- [ ] Đóng ca → sinh cảnh báo đúng 10 rules
- [ ] Shift Report hiển thị đủ 6 phần
- [ ] Bàn giao ca: SUBMITTED → ACKNOWLEDGED → CLOSED
- [ ] Recipe CRUD + versioning
- [ ] Alert Center: filter + resolve
- [ ] Analytics: Food Cost / COGS / Variance chính xác
- [ ] Multi-tenancy: không leak cross-tenant
- [ ] NFR-1: đóng ca < 3s (ca ≤ 500 đơn)
- [ ] guard-check + build PASS · UI Platform 100% · Domain pure · Single-Identity 100%
- [ ] `Shift.StaffUserId` (FK) — chuẩn bị HR B2

## 6. CONSTRAINTS (governance)

- Domain PURE (no EF Core/DbContext/DataAnnotations) — mọi entity trong `1_Shared/Domain.cs`
- Single-Identity Pattern 100% (Id = PK, business key VO Ignore, constructor sync)
- Multi-tenancy mọi layer (TenantId filter)
- AccountingEntry immutable — không đụng
- UI Platform components 100% (Gate 5) — không bypass
- E2E spec cho UI (Gate 4)
- Domain modification CHỈ khi được duyệt (task card này = đề xuất; user review → approve trước khi implement Phase 1)
- `guard-check.ps1` + `dotnet build VanAn.sln` MUST PASS trước mọi commit

## 7. MỞ (OPEN QUESTIONS — trước khi implement)

1. Recipe cũ có dữ liệu production không? (backfill: chuyển toàn bộ hay chỉ active products)
2. Tenant F&B nào sẽ RV production? (cần user chọn/duyệt — đề xuất tenant "Đầm Coffee" mẫu nếu chưa có)
3. Kiểm kê theo đơn vị cơ sở (g/ml/cái) — Ingredient hiện có Unit string tự do → có cần chuẩn hóa unit không (MVP: giữ nguyên, validation nhẹ)?
