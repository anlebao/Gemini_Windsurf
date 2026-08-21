# MASTER PLAN: Financial Intelligence MVP-2 + VA-IIE (Sequence Decision)

> Created: 2026-08-21
> Source: `Van_An_SRS_Financial_Intelligence_MVP2.md` + `Van_An_SRS_Inventory_Intelligence_Engine.md`
> Status: PENDING USER APPROVAL

## SEQUENCE DECISION

**Khuyến nghị: MVP-2 TRƯỚC, VA-IIE SAU.** Lý do:

| Tiêu chí | MVP-2 (Break-even + Unit Economics + Dashboard) | VA-IIE (Shift Report + Variance + Alert) |
|---|---|---|
| Foundation reuse | 80% (P&L + TrialBalance + Product.CostPrice đã có) | 60% (Ingredient/Recipe/Inventory có nhưng Recipe flat→structured là breaking) |
| Target audience | Universal — mọi tenant HKD | Chỉ F&B |
| Breaking change | Không (pure additive — 1 entity mới) | Có (Recipe refactor + migration + data backfill) |
| Rủi ro break | Thấp | Trung bình |
| TT 152/2025 match | Trực tiếp (HKD cần hiểu số kế toán) | Gián tiếp (chỉ F&B) |
| Trigger | Universal — không cần tenant demand cụ thể | Cần tenant F&B active demand |
| Effort | 2-3 tuần | 3-4 tuần (Recipe refactor thêm 1 tuần) |
| ROI đo được | Biết lời/lỗ + hòa vốn + món nào lời → quyết pricing/mix | Giảm hao hụt 5-15% (cần tenant thực际 input kiểm kê mới đo được) |

**Điều kiện build VA-IIE (Sprint 2):**
- (a) MVP-2 đã merge + deploy + RV pass, AND
- (b) Có ≥ 1 tenant F&B active demand tính năng kiểm kê cuối ca, OR user approve regardless

## TRIAGE MATRIX

| # | Item | Sprint | Effort | Risk |
|---|---|---|---|---|
| 1 | Financial Intelligence MVP-2 | A | 2-3 tuần | Thấp |
| 2 | VA-IIE Phase 1-2 (Shift + Variance + Alert) | B (defer) | 3-4 tuần | Trung bình |
| 3 | VA-IIE Phase 3 (Forecast) | C (defer) | 1 tuần | Thấp |
| 4 | VA-IIE Phase 4 (Export + Bot) | D (defer) | 1 tuần | Thấp |
| 5 | VA-IIE Phase 5 (ML) | OUT (R&D) | — | — |
| 6 | Business OS MVP-3 (Plan + Forecast) | E (future) | 2-3 tuần | Trung bình |
| 7 | Business OS MVP-4 (Scenario) | F (future) | 2-3 tuần | Trung bình |
| 8 | Business OS MVP-5 (AI Advisor) | OUT (R&D 1-2 năm) | — | — |

## SPRINT A: Financial Intelligence MVP-2 (ACTIVE — pending approval)

**Goal:** Break-even + Unit Economics + Financial Dashboard cho mọi tenant HKD/SME nhỏ.

**Source SRS:** `docs/requirements/Van_An_SRS_Financial_Intelligence_MVP2.md`

**Scope:**
- 1 entity mới: `BusinessProfile` (fixed costs + capacity + pricing)
- 5 result records (immutable, không persist): `ProfitSummary`, `BreakEvenAnalysis`, `MultiProductBreakEven`, `UnitEconomicsReport`, `TargetProfitAnalysis`
- 5 services: `IBusinessProfileService`, `IProfitSummaryService`, `IBreakEvenAnalysisService`, `IUnitEconomicsService`, `ITargetProfitService`
- 7 API endpoints (Gateway) + 1 HTTP proxy service (ShopERP)
- 4 UI pages + NavMenu + Sitemap
- 1 migration `AddBusinessProfile`

**Phases (per SRS §8):**
- Phase 1 — Foundation (3-4 ngày): Domain + EF config + migration + repository
- Phase 2 — Calculation Services (4-5 ngày): 5 services + unit tests + integration tests
- Phase 3 — API + HTTP Proxy (2-3 ngày): Controller + DTOs + W12-G7 arch test
- Phase 4 — UI (3-4 ngày): 4 pages + NavMenu + Sitemap + Playwright E2E
- Phase 5 — Polish (1-2 ngày): Export PDF/Excel + i18n + admin guide

**Task card:** `task_financial_intelligence_mvp2.md`

**Success Criteria (SRS §10):**
- [ ] Owner khai báo BusinessProfile → save + Version increment
- [ ] `/financial` hiển thị 5 widgets (KU-01/02/03/05)
- [ ] ProfitSummary cross-check với IncomeStatement
- [ ] BreakEven status đúng (Above/At/Below)
- [ ] UnitEconomics sorted by ProfitContribution DESC + warning CostPrice missing
- [ ] TargetProfit feasibility check vs capacity
- [ ] Guard conditions: 6 codes (PROFILE_MISSING, INSUFFICIENT_DATA, ...)
- [ ] Export PDF + Excel
- [ ] NFR: Perf < 500ms (BreakEven) / < 1s (UnitEconomics)
- [ ] W12-G7 + Single-Identity + Domain purity + UI Platform 100%
- [ ] `guard-check.ps1` + `dotnet build` PASS

## SPRINT B: VA-IIE Phase 1-2 (DEFER — trigger: tenant F&B demand)

**Goal:** Inventory Intelligence — số hóa báo cáo cuối ca + variance analysis + alert engine.

**Source SRS:** `docs/requirements/Van_An_SRS_Inventory_Intelligence_Engine.md`

**Trigger:**
- MVP-2 Sprint A merged + deployed + RV pass, AND
- (a) Có tenant F&B active demand, OR (b) user approve regardless

**Scope (per VA-IIE SRS §8 Phase 1-2):**
- 4 entity mới: `Shift`, `InventoryCount`, `ShiftAlert`, `TheoreticalConsumption`
- Recipe refactor: flat → `Recipe` + `RecipeLine` (1 recipe nhiều lines + versioning) — **breaking change + migration + backfill**
- 6 services: `IShiftReportService`, `IRecipeService`, `ITheoreticalConsumptionService`, `IVarianceAnalysisService`, `IAlertEngine`, `IFoodCostService`
- API + UI (ShiftReport, RecipeManagement, InventoryDashboard, AlertCenter)

**Task card:** `task_va_iie_phase1_2.md` (tạo khi Sprint B trigger — không tạo trước để tránh stale)

## SPRINT C-D: VA-IIE Phase 3-4 (DEFER — sau Sprint B)

- Phase 3: Restock Forecast + Stockout Forecast + Forecast UI (1 tuần)
- Phase 4: Profitability per Item/Shift + Export PDF/Excel + Telegram/Zalo bot + PWA offline + E2E (1 tuần)

## OUT OF SCOPE

- **VA-IIE Phase 5 (ML forecasting, IoT, multi-branch)** — R&D, không MVP
- **Business OS MVP-5 (AI Advisor)** — R&D 1-2 năm, cần domain expert + 6-12 tháng data
- **AR/AP aging** — không match target audience HKD siêu nhỏ (cash-based)
- **Inventory Manager actor** — HKD siêu nhỏ chủ tự làm, không cần role riêng

## EXECUTION ORDER

1. **Sprint A (MVP-2)** — pending user approval → implement → merge → deploy → RV
2. **Sprint B (VA-IIE)** — defer cho đến khi (MVP-2 stable) AND (tenant F&B demand OR user approve)
3. **Sprint C-D (VA-IIE P3-4)** — sau Sprint B merge
4. **Sprint E-F (Business OS MVP-3/4)** — sau MVP-2 stable 3-6 tháng + tenant demand

## CONSTRAINTS

- Tuân thủ SRS MVP-2 §0.1 (in scope) + §0.2 (out scope)
- **Architecture Option B (approved 2026-08-21):** MVP-2 services trong `3_CoreHub/Services/FinancialIntelligence/` subfolder + namespace `VanAn.CoreHub.Services.FinancialIntelligence`. NO new csproj. Match precedent `Journal/`, `Template/`, `Reports/`, `Orchestration/`.
- **A1 resolution Option 2 (approved 2026-08-21):** Extend `IncomeStatement` record tại `1_Shared/Domain.cs:3531` với 4 additive fields `TotalCogsEnding`, `TotalCogsOpening`, `TotalOpExEnding`, `TotalOpExOpening` (default = 0m). Update 2 return statements trong `IncomeStatementService.cs` (line 165 TT99 + line 244 flat). Precedent: VALCN v2.0 Phase 1.
- **W8 feature flag bypass (verified 2026-08-21):** `IncomeStatementsController.cs:44` block HKD tenant. `FinancialIntelligenceController` inject `IIncomeStatementService` trực tiếp (services layer), KHÔNG qua controller. Match precedent `NetworkDashboardService`.
- Domain purity + Single-Identity Pattern + AccountingEntry immutable (governance hard stops)
- UI Platform components 100% (Gate 5)
- E2E test cho UI pages (Gate 4)
- ShopERP HTTP proxy only — không inject `IVanAnDbContext` cho MVP-2 (match `NetworkDashboardHttpService`)
- Pattern #10 charset strip trên Gateway controller (if any forward variant)
- W12-G7 architecture test: add `FinancialIntelligenceController` to authorized list
