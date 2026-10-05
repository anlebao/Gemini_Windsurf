# TASK CARD: VA-IIE Phase 3-4 — Forecast + Profitability + Export + Bot Config (Sprint C-D)

> Created: 2026-10-05 (user directive 2026-10-05: **VA-IIE Phase 3-4 TRƯỚC → Sprint B2 SAU**)
> Source SRS: `docs/requirements/Van_An_SRS_Inventory_Intelligence_Engine.md` (§7.5 Forecast, §8.6 Forecast UI, §5.2 Analytics, Phase 3-4 roadmap §10)
> Master plan: `docs/AI/tasks/master_plan_financial_intelligence_mvp2_va_iie.md` (SPRINT C-D, status DEFER → ACTIVE)
> Branch: `main`
> Status: **CODE DONE P3 + P4a + P4b (2026-10-05: `880b1249` + `4a94dd9e` + `9ef418e8` — Core.Tests 1977 · ShopERP.Tests 136 · guard ALL PASSED) — ⏳ P4c Deploy + RV pending**

---

## 1. GOAL & CONTEXT

- **Mục tiêu Phase 3 (Forecasting):** Restock Forecast + Stockout Forecast dựa trên AvgDailyConsumption (rolling window, mặc định 14 ngày từ `TheoreticalConsumption`) + Forecast UI + trend analysis (SRS §7.5, §8.6).
- **Mục tiêu Phase 4 (Polish & Integration):** Profitability per Item/Shift + Export PDF/Excel (báo cáo ca + analytics + forecast) + Telegram/Zalo bot alert Critical (**config-only** — quyết định user 2026-10-05) + E2E tests (Gate 4).
- **Nền tảng đã có (Sprint B Phase 1-2, deployed + RV pass 2026-10-03/04):** 4 entity + 6 services (`3_CoreHub/Services/InventoryIntelligence/`) + 4 UI pages (`/inventory/shifts|recipes|dashboard|alerts`) + 10 alert rules + `va-iie-shift.spec.ts` 7/7 PASS. Core.Tests 1958 · ShopERP.Tests 126 · guard ALL PASSED.
- **Data flow:** per-tenant SQLite (ShopERP) — giữ nguyên như Phase 1-2. Không cần Gateway API cho MVP (in-process services, precedent Phase 1-2).
- **Điều kiện trigger — ĐÃ THỎA:** Sprint B Phase 1-2 complete + deployed + RV pass (2026-10-03/04) + user directive 2026-10-05 chốt thứ tự.

## 2. ACTIVE WORKFLOW ROUTING

- **Workflow:** `newfeaturebuild.md` (ANALYZE → IMPLEMENT — 7 step)
- **Execution Mode:** ANALYZE (hiện tại — task card chờ duyệt) → IMPLEMENT (sau user approve)
- **Skills (max 3):** `ui-platform-compliance-review` (Forecast UI + settings modal) + `domain-integrity-validation` (Single-Identity, Domain purity) + `test-system-upgrade` (unit/integration/bUnit)
- **Session strategy (precedent Sprint B — session-per-phase):**
  - Session 1 (P3): Domain + EF + migration → ForecastService → Forecast UI + tests → guard/build/tests PASS → commit
  - Session 2 (P4a): Profitability + Export Excel/PDF + tests → guard/build/tests PASS → commit
  - Session 3 (P4b): Bot config (IAlertNotifier + settings UI) + E2E spec → guard/build/tests PASS → commit
  - Session 4 (P4c): Deploy + RV production (5 lớp) + cập nhật project_state + master plan status

## 3. SCOPE (Phase 3-4 per SRS §10)

### Phase 3 — Forecasting (SRS §7.5, §8.6)

- [x] **P3.1 Domain (1_Shared/Domain.cs, Single-Identity):** `VaIIeTenantConfig` entity mới — per-tenant 1 row (precedent `LoyaltyTenantConfig`), lưu ShopERP SQLite:
  - `AvgDailyConsumptionWindowDays` (int = 14) — rolling window ADC (SRS §7.5)
  - `LeadTimeDays` (int = 2) — thời gian nhập hàng (SRS: "Lead Time")
  - `SafetyDays` (int = 1) — ngày an toàn (SRS: "Safety Days")
  - Notification (Phase 4, config-only): `TelegramEnabled` (bool=false), `TelegramBotToken` (string? masked), `TelegramChatId` (string?), `ZaloEnabled` (bool=false), `ZaloAccessToken` (string? masked), `ZaloRecipientId` (string?)
  - Constructor `Id = VaIIeTenantConfigId.Value` sync + EF `Ignore` VO + update methods (`UpdateForecastConfig`, `UpdateNotificationConfig`) + `UpdateAudit()`
  - **KHÔNG thêm field per-ingredient** (LeadTime/SafetyDays per-tenant chung — quyết định user 2026-10-05)
- [x] **P3.2 EF config + migration ShopERP** (`AddVaIIeTenantConfig`) + `DbSet` trên ShopERPDbContext + IVanAnDbContext + seed mặc định cho tenant F&B hiện hữu (get-or-create khi đọc — fallback defaults, không cần backfill data)
- [x] **P3.3 `IForecastService` + `ForecastService`** (`3_CoreHub/Services/InventoryIntelligence/`, namespace `VanAn.CoreHub.Services.InventoryIntelligence`):
  - `GetForecastAsync(TenantId, ct)` → `ForecastReport` { GeneratedAt, WindowDays, LeadTimeDays, SafetyDays, Items[] }
  - **ADC (AvgDailyConsumption)** per ingredient = Σ `TheoreticalConsumption.TheoreticalQuantity` trong window [now − WindowDays, now] ÷ **số ngày có dữ liệu ca trong window** (min 1; 0 ca → `HasData=false` — "Chưa đủ dữ liệu"); **filter theo TenantId** (bài học multi-tenancy a21f97f2 — KHÔNG query cross-tenant, join Shift để lấy TenantId + ShiftDate)
  - **StockoutDays** = `CurrentStock` ÷ ADC (ADC > 0); nếu ADC = 0 hoặc CurrentStock = 0 → xử lý edge (0 ngày → Critical "hết hàng")
  - **Restock suggestion** = max(0, ADC × (LeadTimeDays + SafetyDays) − CurrentStock) (làm tròn lên 0.5 đơn vị — round half up theo Unit)
  - **Trạng thái:** Critical (StockoutDays ≤ LeadTime+Safety) / Warning (StockoutDays ≤ 2×(LeadTime+Safety)) / OK / NoData
  - **Trend:** 14 ngày gần nhất — tiêu hao theo ngày per ingredient (cho UI trend table)
- [x] **P3.4 UI `/inventory/forecast` (Forecast.razor)** — UI Platform 100% (Gate 5):
  - Card cấu hình forecast (Owner): WindowDays / LeadTimeDays / SafetyDays → save `VaIIeTenantConfig` (modal VanAnModal + VanAForm, precedent AlertCenter/ShiftReport)
  - Bảng **Restock Forecast**: nguyên liệu, tồn kho, ADC, ngày còn lại, đề xuất nhập (số + đơn vị) + badge trạng thái
  - Bảng **Stockout Forecast**: nguyên liệu, ngày còn lại (StockoutDays), ngưỡng (LeadTime+Safety), badge Critical/Warning/OK
  - **Trend table**: tiêu hao 7/14 ngày per ingredient (progress bar precedent InventoryDashboard — KHÔNG thêm chart lib mới)
  - NavMenu "Dự báo" (Owner/StoreKeeper) + Sitemap card
- [x] **P3.5 Tests:** `ForecastServiceTests` (ADC rolling window, stockout days, restock qty, edge: no-data/ADC=0/CurrentStock=0, làm tròn) + integration (seed shifts → forecast đúng) + bUnit `ForecastPageTests` → Core.Tests + ShopERP.Tests PASS

### Phase 4 — Polish & Integration (SRS §5.2, §8.5, §10 Phase 4)

- [x] **P4.1 Profitability per Item / per Shift:** mở rộng `IFoodCostService` (hoặc service con `IProfitabilityService`):
  - Per Item: `ProfitabilityPerItem` = Revenue − FoodCost per món (kế thừa `FoodCostPerItem` đã có — thêm field `Profit`/`ProfitPercent`)
  - Per Shift: `ShiftProfitability` = Sales − Cogs (đã có trong `FoodCostReport` — expose qua DTO mới `ProfitabilityReport`)
  - UI: section "Lợi nhuận ca" trên ShiftReport (tóm tắt 6 phần) + widget top món lãi/lỗ trên InventoryDashboard
- [x] **P4.2 Export PDF/Excel:**
  - **Excel:** `VaIIeExportService` (ShopERP `Services/`, EPPlus — precedent `FinancialExportService`, đã có trong Directory.Packages.props 7.6.1):
    - `ExportShiftReportExcelAsync` (6 phần báo cáo ca — SRS §5.1)
    - `ExportForecastExcelAsync` (Restock + Stockout + config)
    - `ExportAnalyticsExcelAsync` (Variance + Food Cost per shift — nguồn ShiftReport/InventoryDashboard data)
  - **PDF:** print view qua `vananPrintBill` (precedent Tt71ReceiptVoucher/PrintBill — repo KHÔNG có PDF lib server-side; user duyệt approach này trong task card) — thêm CSS print + nút "In báo cáo" trên ShiftReport
  - Nút "Xuất Excel" + "In" trên ShiftReport / Forecast / InventoryDashboard (download qua `vanAn.downloadFile` — precedent BreakEven.razor)
- [x] **P4.3 Bot alert — CONFIG-ONLY (quyết định user 2026-10-05):**
  - `IAlertNotifier` interface (SendCriticalAsync/SendWarningAsync — `3_CoreHub/Services/InventoryIntelligence/`)
  - `NullAlertNotifier` (log-only, fail-safe khi chưa cấu hình) + `TelegramAlertNotifier`/`ZaloAlertNotifier` **stub** (định nghĩa contract + payload, KHÔNG gọi API thật — chưa có token; ghi chú "khi có token → implement HTTP call")
  - Config UI: modal trên AlertCenter (Owner) — Telegram (enabled, bot token masked, chat id) + Zalo (enabled, access token masked, recipient) → `VaIIeTenantConfig.UpdateNotificationConfig`
  - Hook: sau `SubmitShiftAsync` sinh alert → gọi notifier cho Critical (log-only ở MVP)
  - **Secret governance:** token hiển thị masked (••••), KHÔNG echo vào log/conversation, không commit token
- [x] **P4.4 E2E Gate 4:** spec `va-iie-forecast.spec.ts` (Forecast page render: config card + 2 bảng hoặc empty state; nút Xuất Excel/In render) + extend `va-iie-shift.spec.ts` nếu cần (nút export trên ShiftReport)
- [ ] **P4.5 Validation & RV:** `guard-check.ps1` + `dotnet build VanAn.sln` + Core.Tests + ShopERP.Tests ALL PASS → deploy (CD Multi-VPS) → RV 5 lớp (L1 API/markers → L2 login → L3 E2E → L4 flow → L5 manual) → cập nhật project_state + master plan + đóng task card

## 4. USER DECISIONS — ĐÃ CHỐT (2026-10-05)

| # | Câu hỏi | Quyết định | Chi tiết |
|---|---|---|---|
| 1 | Telegram/Zalo bot scope? | **Cả 2 nhưng config-only** | Xây `IAlertNotifier` + config UI cho cả Telegram & Zalo, KHÔNG gọi API thật (chưa có token). Khi có token chỉ cần nhập → hook sẵn. |
| 2 | PWA offline scope? | **Defer PWA** | Không làm PWA trong Phase 4 (Blazor Server không offline-first được — SRS review #4 Phase 1-2). Ghi nợ kỹ thuật rõ ràng. |
| 3 | `Order.StaffId` + POS ghi NV bán? | **Để Sprint B2** | Không đụng Order bây giờ — giữ scope Phase 3-4 nguyên chất. |
| 4 | LeadTimeDays + SafetyDays lưu ở đâu? | **Config per-tenant chung** | 1 bộ cho cả quán trong `VaIIeTenantConfig` (window/leadtime/safety + notification). KHÔNG thêm field per-ingredient. |

## 5. SRS REVIEW NOTES — RESOLUTION

| # | SRS vấn đề | Resolution |
|---|---|---|
| 1 | ADC chia cho ngày nào? (§7.5 "rolling window 14 ngày") | Σ tiêu hao trong window ÷ **số ngày có ca đóng trong window** (min 1) — tránh underestimate khi quán đóng ca không đều; 0 ca → NoData |
| 2 | `ReorderPoint` cho Stockout? | Stockout dùng `CurrentStock` (Ingredient) + ADC — KHÔNG phụ thuộc MinStockThreshold (ReorderPoint chỉ cho STOCK_LOW alert) |
| 3 | Restock Forecast "N ngày tới"? (§5.2) | Ngưỡng = LeadTimeDays + SafetyDays; đề xuất = ADC × ngưỡng − CurrentStock (SRS §7.5) |
| 4 | PDF export không có lib server-side | Dùng print view `vananPrintBill` (precedent TT71 + PrintBill) — Excel dùng EPPlus (đã có) |
| 5 | Analytics §8.5 (Variance/FoodCost/COGS/Waste) | Đã phủ qua ShiftReport (6 phần) + InventoryDashboard + FoodCostService — Phase 4 chỉ thêm Export + Profitability, không làm page Analytics riêng (tránh scope) |
| 6 | Bot token bảo mật | Masked trong UI + NullAlertNotifier fail-safe + không commit token (governance secrets) |
| 7 | Chart lib cho trend? | KHÔNG thêm lib — trend dùng table + progress bar (precedent InventoryDashboard) |

## 6. SUCCESS CRITERIA (SRS §12 + Phase 3-4)

- [ ] Forecast: Restock + Stockout hiển thị đúng (ADC rolling window, StockoutDays = CurrentStock ÷ ADC, cảnh báo < LeadTime + SafetyDays)
- [ ] Forecast UI: config (window/leadtime/safety) save được + 2 bảng + trend (Owner/StoreKeeper)
- [ ] Profitability per Item/Shift tính đúng (Revenue − FoodCost)
- [ ] Export Excel (Shift Report + Forecast + Analytics) download được, dữ liệu khớp màn hình
- [ ] Export PDF: in được báo cáo ca qua print view
- [ ] Bot config: lưu/load Telegram+Zalo settings (masked), notifier log-only khi chưa cấu hình, hook Critical sau đóng ca
- [ ] E2E: `va-iie-forecast.spec.ts` PASS (Gate 4)
- [ ] Multi-tenancy: không leak cross-tenant (filter TenantId mọi query forecast — bài học a21f97f2)
- [ ] NFR-1: đóng ca < 3s không regress (forecast tính lazy — không chèn vào đường đóng ca)
- [ ] `guard-check.ps1` + `dotnet build VanAn.sln` PASS mọi batch · UI Platform 100% · Domain pure · Single-Identity 100%
- [ ] Deploy + RV production PASS (CD Multi-VPS) → đóng task card + master plan status

## 7. CONSTRAINTS (governance)

- Domain PURE (no EF Core/DbContext/DataAnnotations) — mọi entity trong `1_Shared/Domain.cs`
- Single-Identity Pattern 100% (Id = PK, business key VO Ignore, constructor sync `Id = VaIIeTenantConfigId.Value`)
- Multi-tenancy mọi layer (TenantId filter — KHÔNG leak cross-tenant, bài học #22-23 governance)
- AccountingEntry immutable — không đụng
- UI Platform components 100% (Gate 5) — không bypass
- E2E spec cho UI mới (Gate 4)
- Playwright DISABLED trong IMPLEMENT — chỉ chạy spec ở session P4c/RV (playwright.rules)
- Domain modification CHỈ khi được duyệt (task card này = đề xuất; user review → approve trước khi implement)
- `guard-check.ps1` + `dotnet build VanAn.sln` MUST PASS trước mọi commit
- Bot token KHÔNG commit/echo/log (secrets governance)

## 8. DEFERRED (ghi nợ rõ ràng)

- **PWA offline (NFR-3)** — defer (quyết định user 2026-10-05): Blazor Server không offline-first; khi có demand → cân nhắc WASM client hoặc API + local storage
- **Telegram/Zalo API thật** — khi có token: implement HTTP call trong `TelegramAlertNotifier`/`ZaloAlertNotifier` (contract đã có)
- **`Order.StaffId` + POS ghi NV bán** — Sprint B2 (HR-Payroll, thưởng doanh số)
- **REST API VA-IIE (SRS §7.6)** — tiếp tục defer (chưa có mobile/PWA demand — Phase 1-2 note)
