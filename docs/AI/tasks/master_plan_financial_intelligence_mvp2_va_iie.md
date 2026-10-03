# MASTER PLAN: Financial Intelligence MVP-2 + VA-IIE (Sequence Decision)

> Created: 2026-08-21
> Source: `Van_An_SRS_Financial_Intelligence_MVP2.md` + `Van_An_SRS_Inventory_Intelligence_Engine.md`
> Updated: 2026-10-03
> Status: **SPRINT A COMPLETE (deployed + RV pass) — Sprint B ACTIVE (user approved 2026-10-03) — HR-Payroll PROPOSED (pending user decision)**

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

| # | Item | Sprint | Effort | Risk | Status |
|---|---|---|---|---|---|
| 1 | Financial Intelligence MVP-2 | A | 2-3 tuần | Thấp | ✅ **COMPLETE 2026-10-03** — deployed + RV pass |
| 2 | VA-IIE Phase 1-2 (Shift + Variance + Alert) | B | 3-4 tuần | Trung bình | ▶ **ACTIVE** (user approved 2026-10-03) |
| 3 | VA-IIE Phase 3 (Forecast) | C (defer) | 1 tuần | Thấp | Pending (sau Sprint B) |
| 4 | VA-IIE Phase 4 (Export + Bot) | D (defer) | 1 tuần | Thấp | Pending (sau Sprint B) |
| 5 | VA-IIE Phase 5 (ML) | OUT (R&D) | — | — | — |
| 6 | Business OS MVP-3 (Plan + Forecast) | E (future) | 2-3 tuần | Trung bình | Pending |
| 7 | Business OS MVP-4 (Scenario) | F (future) | 2-3 tuần | Trung bình | Pending |
| 8 | Business OS MVP-5 (AI Advisor) | OUT (R&D 1-2 năm) | — | — | — |
| 9 | **HR-Payroll (chấm công + lương + thưởng)** | B2/C (proposed) | 2-3 tuần | Trung bình | ⏳ **PROPOSED 2026-10-03 — chờ user quyết định scope** |

## SPRINT A: Financial Intelligence MVP-2 (✅ COMPLETE 2026-10-03 — deployed + RV production pass)

**Goal:** Break-even + Unit Economics + Financial Dashboard cho mọi tenant HKD/SME nhỏ.

**Source SRS:** `docs/requirements/Van_An_SRS_Financial_Intelligence_MVP2.md`

**Trạng thái (verified 2026-10-03):**
- Phase 1-3 (Foundation + Services + API/Proxy): ✅ COMPLETE (commit `bb7f72c0` + `e9598115` — mint JWT tenant_id fix)
- Phase 4 (UI): ✅ COMPLETE — 4 pages (`/admin/business-profile`, `/financial`, `/financial/break-even`, `/financial/unit-economics`) + E2E spec `financial-dashboard.spec.ts`
- Phase 5 (Polish): ✅ COMPLETE — export PDF/Excel (BreakEven + UnitEconomics) + `docs/admin-guide-financial-intelligence.md`
- Tests: Core `6_Tests/VanAn.Core.Tests/FinancialIntelligence/` (5 files) · Integration `FinancialIntelligenceEndpointsTests` + `FinancialIntelligenceServicesTests`
- **Bonus 2026-10-03 (Option B, user approved):** SystemAdmin tenant selector (`/admin/business-profile` + `/financial`) — `MintSystemAdminTokenAsync(Guid? tenantIdOverride)` gated `IsInRole("SystemAdmin")` (chống tenant-escalation) + fix VanAAlert ChildContent (UI.Platform). Commit `b6143992` + `3438cd87` — CD Multi-VPS SUCCESS ×2 + RV pass (dữ liệu kế toán THẬT qua override: tenant "Test Minimal" doanh thu 100đ).

**Task card:** `task_financial_intelligence_mvp2.md`

**Success Criteria (SRS §10) — verified:**
- [x] Owner khai báo BusinessProfile → save + Version increment (unit tests + form RV)
- [x] `/financial` hiển thị 5 widgets (RV production PASS)
- [x] ProfitSummary cross-check với IncomeStatement (integration tests)
- [x] BreakEven status đúng (Above/At/Below) (unit + integration tests)
- [x] UnitEconomics sorted by ProfitContribution DESC + warning CostPrice missing (unit tests + UI)
- [x] TargetProfit feasibility check vs capacity (unit tests)
- [x] Guard conditions: 6 codes (unit tests)
- [x] Export PDF + Excel (code present)
- [ ] NFR: Perf < 500ms (BreakEven) / < 1s (UnitEconomics) — **chưa đo production, để RV tiếp theo**
- [x] W12-G7 + Single-Identity + Domain purity + UI Platform 100% (arch/guard PASS)
- [x] `guard-check.ps1` + `dotnet build` PASS (mọi batch)

## SPRINT B: VA-IIE Phase 1-2 (ACTIVE — user approved 2026-10-03)

**Goal:** Inventory Intelligence — số hóa báo cáo cuối ca + variance analysis + alert engine.

**Source SRS:** `docs/requirements/Van_An_SRS_Inventory_Intelligence_Engine.md`

**Trigger — ĐÃ THỎA:**
- [x] MVP-2 Sprint A merged + deployed + RV pass (2026-10-03)
- [x] (a) Có tenant F&B active demand, OR (b) **user approve regardless** (user directive 2026-10-03: "chuẩn bị làm tiếp VA-IIE Sprint 2")

**Scope (per VA-IIE SRS §8 Phase 1-2):**
- 4 entity mới: `Shift`, `InventoryCount`, `ShiftAlert`, `TheoreticalConsumption`
- Recipe refactor: flat → `Recipe` + `RecipeLine` (1 recipe nhiều lines + versioning) — **breaking change + migration + backfill**
- 6 services: `IShiftReportService`, `IRecipeService`, `ITheoreticalConsumptionService`, `IVarianceAnalysisService`, `IAlertEngine`, `IFoodCostService`
- API + UI (ShiftReport, RecipeManagement, InventoryDashboard, AlertCenter)

**SRS review notes (2026-10-03) — cần xử lý khi tạo task card:**
1. **`Shift.StaffName` là string** — nên đổi thành `StaffUserId` (FK → User.Id) để phục vụ chấm công/lương (HR-Payroll) về sau. **Nếu user duyệt HR-Payroll → bắt buộc từ Sprint B.**
2. `Ingredient` đã tồn tại trong ShopERP (onboarding tạo Ingredients/Recipes) — SRS thêm `ReorderPoint`/`CurrentStock`/`VarianceThresholdPercent` → extend entity hiện có (đã flat) + migration additive.
3. **NFR-5 "Manager" role không tồn tại** trong ShopERP (chỉ Staff/StoreKeeper/Owner/Masterchef/Guard) — map Manager → Owner/StoreKeeper hoặc thêm role mới.
4. **Offline-first (NFR-3/PWA)** khó với Blazor Server — đề xuất MVP online-first, offline để Phase 4.
5. Alert notification Email/Telegram/Zalo — Telegram/Zalo bot là Phase 4; MVP chỉ in-app (như SRS §4.3: in-app = tất cả).
6. Recipe refactor breaking — migration + backfill từ Recipe flat hiện có; kiểm tra mọi nơi dùng Recipe cũ (OrderService, onboarding, kế toán COGS).

**Task card:** `task_va_iie_phase1_2.md` — tạo khi bắt đầu Sprint B

## SPRINT C-D: VA-IIE Phase 3-4 (DEFER — sau Sprint B)

- Phase 3: Restock Forecast + Stockout Forecast + Forecast UI (1 tuần)
- Phase 4: Profitability per Item/Shift + Export PDF/Excel + Telegram/Zalo bot + PWA offline + E2E (1 tuần)

## SPRINT B2: HR-PAYROLL — CHẤM CÔNG + TÍNH LƯƠNG + THƯỞNG (PROPOSED 2026-10-03 — chờ user quyết định)

> User hỏi: "có thể bổ sung thêm phần chấm công tính lương và thưởng cho nhân viên được không?"
> Trả lời: **ĐƯỢC** — đề xuất là Sprint riêng (không trộn vào VA-IIE core — giữ module focus).

**Vì sao khả thi (tie-in sẵn có):**
- **Shift (VA-IIE) = nguồn chấm công tự động:** mở ca/đóng ca = check-in/check-out; ShiftType (Sáng/Trưa/Chiều/Tối/Full) = ca làm việc. Không cần thiết bị chấm công riêng.
- **Thưởng gắn dữ liệu VA-IIE:** Profitability per Shift, Food Cost, Waste Ratio, doanh số ca → công thức thưởng (thưởng doanh số, thưởng ca giảm hao hụt, thưởng KPI).
- **Lương = chi phí cố định:** đã có `BusinessProfile.MonthlyPayroll` (FI MVP-2) — bảng lương thực tế thay số ước lượng.
- Commission/SalesReferral đã tồn tại (doanh số → hoa hồng) — nền tảng cho thưởng theo doanh số.

**Scope đề xuất (HR-MVP):**
- `EmployeeProfile` (hồ sơ nhân viên: UserId FK, mức lương cơ bản, hình thức lương: cố định/giờ/doanh số, ngày vào, active)
- `AttendanceRecord` (chấm công: auto từ Shift + nhập tay bù ca, nghỉ phép) — hoặc tái dùng Shift trực tiếp
- `PayrollPeriod` + `PayrollEntry` (bảng lương tháng: lương cơ bản + phụ cấp + thưởng − khấu trừ, theo từng nhân viên; tổng = chi phí nhân công)
- Bonus rules (thưởng doanh số / KPI / giảm hao hụt) — rule-based như Alert Engine
- UI: Quản lý nhân viên (Owner), Chấm công + Bảng lương, phiếu lương cho Staff
- Data: ShopERP SQLite per-tenant (operational). KHÔNG đưa lên PG ở MVP.
- Accounting link (để sau): định khoản lương 642 → kế toán (chỉ khi user yêu cầu — HKD TT 152 đơn giản hơn DN)

**Quyết định cần user:**
1. Build HR-Payroll **sau VA-IIE Phase 1-2** (B2) hay **song song/trước**?
2. `Shift.StaffName` (SRS VA-IIE) đổi thành `StaffUserId` (FK) — **bắt buộc nếu làm HR** (chấm công theo nhân viên thật)
3. Lương theo hình thức nào trước: cố định tháng / theo giờ / theo doanh số (hay cả 3)?
4. Thưởng: chỉ thưởng doanh số (nhanh) hay cả KPI hao hụt (cần VA-IIE data)?
5. Có cần tích hợp kế toán (định khoản lương) ngay không, hay chỉ xuất bảng lương?

## OUT OF SCOPE

- **VA-IIE Phase 5 (ML forecasting, IoT, multi-branch)** — R&D, không MVP
- **Business OS MVP-5 (AI Advisor)** — R&D 1-2 năm, cần domain expert + 6-12 tháng data
- **AR/AP aging** — không match target audience HKD siêu nhỏ (cash-based)
- **Inventory Manager actor** — HKD siêu nhỏ chủ tự làm, không cần role riêng

## EXECUTION ORDER

1. **Sprint A (MVP-2)** — ✅ COMPLETE 2026-10-03 (deployed + RV pass)
2. **Sprint B (VA-IIE Phase 1-2)** — ACTIVE (user approved 2026-10-03) — tạo task card + implement
3. **Sprint B2 (HR-Payroll)** — PROPOSED — sau khi user chốt scope (xem SPRINT B2)
4. **Sprint C-D (VA-IIE P3-4)** — sau Sprint B merge
5. **Sprint E-F (Business OS MVP-3/4)** — sau MVP-2 stable 3-6 tháng + tenant demand

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
