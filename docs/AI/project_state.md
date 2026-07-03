# Project State

> **Mục đích:** Single Source of Truth cho AI về trạng thái dự án. BẮT BUỘC đọc đầu mỗi phiên.

---

## 0. Maintenance Rules

1. One-and-only-one: Mỗi section chỉ tồn tại 1 lần.
2. No contradiction: Một hạng mục chỉ có 1 trạng thái.
3. Ground Truth first: Verify path/branch với codebase trước khi ghi.
4. Now over History: Section 2-4 chỉ mô tả việc ĐANG làm và KẾ TIẾP. Việc xong → gom vào Section 6.
5. Actionable Next Actions: Xóa action đã quá hạn/sai bối cảnh.
6. Stamp every edit: Cập nhật Section 11 mỗi lần sửa.

---

## 1. Project Overview

**Dự án:** Vạn An Accounting System MVP — giải pháp kế toán HKD theo TT 152/2025/TT-BTC.
**Stack:** .NET 8 · EF Core · SQLite · Blazor Server (ShopERP) · Blazor WebAssembly (KhachLink PWA) · SignalR · YARP Gateway · xUnit · Playwright.
**Kiến trúc:** Clean Architecture + DDD + Multi-tenancy. Data flow: `KhachLink (5002) → Gateway (5001) → ShopERP (5003) → SQLite`.

**Modules:** `1_Shared` (Domain) · `2_Gateway` (YARP) · `3_CoreHub` (Services, in-process) · `5_WebApps/ShopERP` (Blazor Server) · `5_WebApps/KhachLink` (Blazor WASM) · `UI.Platform` (Shared components) · `6_Tests/6_Testing`.

**Hard stops:** Domain PURE · `AccountingEntry` immutable · Gateway STATELESS · KhachLink HTTP-only · ShopERP SQLite-only · ALWAYS dùng UI Platform components.

---

## 2. Current Objective

**[STREAM D: HKD BOOK ACCOUNTING REPORT FIX (TT 152/2025/TT-BTC + 2026 REGULATORY COMPLIANCE) — ACTIVE, WAVE 0 + WAVE 0.5 EXECUTION ~70% COMPLETE, 3 NEW GAPS DISCOVERED, STREAM E SPAWNED]**

Fix 8 root-cause issues + 2 architecture/legal findings preventing correct TT 152 HKD book report generation. Dependency-ordered 12-wave fix (data → DI → routing → formulas → 2026 regulatory → tests → API → UI → export). Wave 0 + Wave 0.5 execution in progress on `feature/hkd-fix-wave0-wave0p5-preflight` branch: 15/21 tasks done, 3 new gaps found (stale DB schema → Stream E spawned, S1a .doc binary format, docx extraction incomplete), 6 tasks remaining (propagation + extraction + commit).

- **Master plan:** `docs/AI/tasks/hkd_book_accounting_fix_master_plan.md` (v3 — 12 waves, 8 root-cause issues + 2 architecture/legal findings, 5 amendments + 5 concerns resolved)
- **Planning commits:** `c4acb15` (v1) → `c8d4a6c` (v2) → `22d3976` (W0 expand) → `88e635a` (v3) → `4b2f077` (W0.5+W5c) → `7fb6dec` (W5a+W5b+slim)
- **Execution branch:** `feature/hkd-fix-wave0-wave0p5-preflight` (Wave 0 + Wave 0.5 in progress)
- **Wave 0 + 0.5 execution results (2026-07-03):**
  - **W0-T1 Build:** ✅ PASS — 0 errors, 962 warnings
  - **W0-T2 DI:** ✅ 0 matches — 5 calc engine services chưa wire DI
  - **W0-T3 Endpoint:** ✅ 0 matches — no `hkd-books`/`GenerateS*BookAsync` in Controllers
  - **W0-T4 UI:** ✅ 0 matches — no S1a/S2a-S2e/S3a in `5_WebApps`
  - **W0-T5 DB Query:** ⚠️ `JournalEntries` 0 rows + **CRITICAL: AccountingEntries schema STALE** (missing Amount, EntryType, AccountCode, PeriodYear, PeriodMonth — only BaseEntity columns). Root cause: `EnsureCreatedAsync()` không update schema, no Migrations. **→ Stream E spawned (EF Core Migrations strategy).**
  - **W0-T6 Git:** ✅ Clean
  - **W0-T7 docx:** ⚠️ PARTIAL — 7 files found (1 binary .doc + 6 .docx). S2a_HKD.docx extracted (122 `<w:t>` elements), text output incomplete. S1a_HKD.doc là binary format cần pandoc/LibreOffice.
  - **W0-T8 ITemplateFactory:** ✅ RESOLVED — old `Services/TemplateFactory.cs` implements `ITemplateFactory` (for OrderService), new `Services/Template/TemplateFactory.cs` KHÔNG implement interface → no conflict. Keep both, register new as concrete.
  - **W0-T9 Export lib:** ✅ RESOLVED — EPPlus 7.6.1 (XLSX OK). DOCX: user approved thêm `DocumentFormat.OpenXml` dependency (Wave 8).
  - **W0-T10 Tenant.IndustrySector:** ✅ RESOLVED — MISSING. Wave 5b CONDITIONAL (Tech Lead approval for W5b-T0 OR descope).
  - **W0-T11 Double-write audit:** ✅ RESOLVED — Path B. OrderService L114-141 đã persist JournalEntry via `AddToBookAsync`. Nhưng W0.5 chọn Option A (query AccountingEntries, không viết JournalEntry) → no double-write risk.
  - **W0.5-T1 AccountCode:** ✅ Populated for OrderService path (511/621), NULL for `RecordRevenueAsync` path. Option A needs EntryType-based heuristic for null AccountCode.
  - **W0.5-T2 Formula Engine:** ✅ CAN map EntryType → Credit/Debit (Revenue=Credit, Expense=Debit). `Account_{pattern}_Credit/Debit` aggregates mappable.
  - **W0.5-T3 Product roadmap:** ✅ HKD-ONLY (no Doanh nghiệp engine sharing).
  - **W0.5-T4 DECISION:** ✅ **Option A — refactor `SmartPreAggregationService.GetAccountSumAsync` to query `AccountingEntries` directly** (no JournalEntry writing, no Domain modification). Rationale: HKD-only product + AccountingEntry is immutable SSoT + EntryType mappable + avoids existing OrderService double-write.
- **3 NEW GAPS discovered (not in original plan):**
  - **Gap-1 (BLOCKER):** Dev DB schema stale — `EnsureCreatedAsync()` không update schema khi model thay đổi. Production deployment trap. **→ Stream E: EF Core Migrations Strategy spawned.** User decision: Enable EF Migrations, loại bỏ EnsureCreated khỏi production, sửa VA-ARCH-001 (cho phép Migrations ở đúng layer Infrastructure).
  - **Gap-2 (Medium):** `S1a_HKD.doc` binary format — cần pandoc/LibreOffice (free, sẽ install).
  - **Gap-3 (Medium):** 6 .docx extraction script incomplete (PowerShell regex issue).
- **Stream E: DB Migration Strategy (NEW — spawned from Wave 0 Gap-1):**
  - **Decision (user-approved 2026-07-03):** (1) Use EF Core Migrations as official schema management, (2) Remove `EnsureCreated()` from production, (3) Modify VA-ARCH-001 to allow Migrations at correct layer (Infrastructure) while still preventing other architecture violations.
  - **Status:** ✅ COMPLETE & MERGED TO MAIN (`b2e0431`). InitialCreate migration created (6037 lines), DesignTimeDbContextFactory added, VA-ARCH-001 modified (allows Infrastructure Migrations, forbids Application Migrations), `EnsureCreatedAsync` → `MigrateAsync` in CoreHub/Program.cs, dev DB migrated (AccountingEntries has ALL domain columns). 28/28 architecture tests pass.
  - **Blocks:** ~~Wave 2 verification~~ ✅ UNBLOCKED. ~~Wave 7 manual testing~~ ✅ UNBLOCKED. ~~Wave 8 manual testing~~ ✅ UNBLOCKED.
- **v3 amendments (approved 2026-07-03, from phản biện kiến trúc + pháp lý):**
  4. **Add Wave 0.5 — Architecture Decision: HKD Data Source (A vs B, loại C dual-write)** — Option C (hardcode 2 lệnh ghi trong RecordRevenue) bị LOẠI per phản biện. Option A (refactor SmartPreAggregationService query AccountingEntries) hoặc Option B (event-driven Outbox). Codebase verify: AccountingEntry có đủ field, Outbox infrastructure đã có.
  5. **Add HKD Accounting Regime Disclaimer + Fix suất thuế + Add Wave 5c** —
     - 5a: HKD = single-entry per TT 88/2021 + TT 152/2025, account mapping là Internal Synthetic Mapping (KHÔNG phải TT 200/133)
     - 5b: Sửa suất thuế — xóa "2,5%" fabricated, dùng đúng 4 nhóm per Luật 2025 + ND 117/2025 (1%/0.5%, 3%/1.5%, 5%/2%, 2%/1%)
     - 5c: Add Wave 5c — 2026 Regulatory Compliance Fix (threshold 500M→1B, 4 revenue groups mới, TNCN formulas Nhóm 2/3/4, thuế khoán abolished, lệ phí môn bài abolished) — CRITICAL pháp lý
- **v2 amendments (approved 2026-07-03):**
  1. **Promote 4 architecture decisions to Wave 0** — W0-T8 (`ITemplateFactory` conflict), W0-T9 (docx/xlsx lib), W0-T10 (`Tenant.IndustrySector`), W0-T11 (double-write audit). Resolve BEFORE IMPLEMENT mode.
  2. **Split Wave 5 into 5a + 5b** — 5a (account mapping + PIT-on-revenue), 5b (industry-sector tax rates, CONDITIONAL).
  3. **Add Wave 4 smoke test (W4-T11)** — `NumericValues` not-empty tripwire.
- **v2 concern resolutions:** C1 (double-write tree), C2 (ITemplateFactory→W0-T8), C3 (Wave 5 split), C4 (Tenant.IndustrySector→W0-T10), C5 (smoke test), C6 (export lib→W0-T9), C7 (multi-tenancy test), C8 (per-wave merge).
- **Root cause (8 issues):**
  1. Production path `CalculateAsync` no-op → `NumericValues` always empty (Critical)
  2. Calc engine exists (`Services/Template/`) but not wired in DI (Critical)
  3. Data flow gap: `JournalEntries` table empty — no bridge from `AccountingEntry` (Critical)
  4. Tests pass white (no numeric assertions) — bug hidden (Critical)
  5. Tax formulas hardcoded 5%/10% — non-compliant vs TT 152 industry rates (High)
  6. Output is plain text, not docx/xlsx TT 152 layout (High)
  7. UTF-8 mojibake in `Services/Template/TemplateFactory.cs` (Medium)
  8. Account mapping hallucinated (`_vietnameseAccounts` 211/811/821/841/521 wrong) (Medium)
- **v3 findings (from phản biện):**
  9. **Dual Write anti-pattern risk** — Option C (hardcode 2 ghi trong RecordRevenue) bị LOẠI, Wave 0.5 chốt A or B
  10. **2026 regulatory non-compliance** — `HKDRevenueClassificationService` threshold 500M (sai, phải 1B), 4 groups sai, TNCN formula sai, missing thuế khoán abolished — Wave 5c fix
- **Target:** 7 HKD book templates (S1a, S2a-S2e, S3a) generate `NumericValues` with real data, output docx/xlsx per TT 152 layout, endpoint + UI page, tests assert numeric values, multi-tenancy enforced, **2026 regulatory compliant** (threshold 1B, 4 revenue groups, TNCN formulas đúng), **no dual write** (Option A or B), regression prevention.

### 12 Waves (v3 — dependency-ordered, per-wave merge to main)
| Wave | Description | Sessions | Risk | Status |
|---|---|---|---|---|
| 0 | Pre-flight verification (11 tasks — baseline + 4 promoted architecture decisions + double-write audit) | 0.5-1 | None | 🔄 IN PROGRESS (15/21 tasks done, 3 new gaps, 6 remaining) |
| 0.5 | **[v3] Architecture Decision: HKD Data Source (A vs B, loại C dual-write)** | 0.5-1 | None (decision) | ✅ DONE — Option A chosen (query AccountingEntries directly) |
| 1 | Fix UTF-8 mojibake in `Services/Template/TemplateFactory.cs` (S1a + S2a TemplateImpl) | 0.5 | Low | ⏳ PENDING |
| 2 | Data source bridge (Option A: refactor query OR Option B: event-driven — per W0.5) | 0.5-2 | Medium/Low | ⏳ PENDING |
| 3 | Wire 5 calc engine services into DI (conflict pre-resolved in W0-T8) | 1 | Low | ⏳ PENDING |
| 4 | Route `HKDBookService.GenerateS*BookAsync` through `IHKDBookGenerationService` + smoke test tripwire (W4-T11) | 1 | Medium | ⏳ PENDING |
| 5a | Fix account mapping + PIT-on-revenue (no industry modeling, 1 Domain account-number fix W5a-T4) — **W5a-T4 needs Tech Lead approval** | 1 | Medium | ⏳ PENDING |
| 5b | Industry-sector tax rates per Luật 2025 + ND 117/2025 (CONDITIONAL — may descope if W0-T10 finds Tenant.IndustrySector missing) — **W5b-T0 needs Tech Lead approval IF executed** | 1-2 | High | ⏳ PENDING |
| 5c | **[v3] 2026 Regulatory Compliance Fix (threshold 500M→1B, 4 revenue groups, TNCN formulas, thuế khoán abolished) — CRITICAL pháp lý — Legal review recommended** | 1-2 | High (pháp lý) | ⏳ PENDING |
| 6 | Retrofit tests with numeric assertions (3 update + 5 new + 1 regression) | 1-2 | Low | ⏳ PENDING |
| 7 | API endpoint `GET /api/hkd-books/{templateCode}` + DI smoke + multi-tenancy isolation test (W7-T6) | 1 | Low | ⏳ PENDING |
| 8 | UI page `/accounting/hkd-books` + DOCX/XLSX export + architecture test + encoding lint | 2-3 | Medium | ⏳ PENDING |

**Critical path:** Wave 0→0.5→1→2→3→4→5a→**5c**→6→7→8 (sequential). **Optional:** Wave 5b (between 5a and 5c, conditional). **Parallel:** Wave 0+0.5+1 có thể cùng session.
**Branch strategy:** Per-wave merge to main (always-green, no long-lived branch).
**Estimated total:** 11-18 sessions (5b optional, 5c mandatory).

### Parked / Completed Streams
- **Stream A: EInvoice Provider Rewrite** — Planning complete (`59b60fe`). Blocker: Wave 0 sandbox credentials (1-2 tuần).
- **Stream B: E2E Test Cleanup** — ✅ COMPLETE (`ffe8607`). 8 waves, 7 anti-patterns, all merged-ready on `feature/e2e-cleanup-wave8-regression-prevention`. Awaiting merge to main.
- **Stream C: ShopERP UI Fix** — ✅ COMPLETE & MERGED TO MAIN (`f3ed2d2`). 6 waves, 23 .razor files fixed.
- **Stream E: DB Migration Strategy** — ✅ COMPLETE & MERGED TO MAIN (`b2e0431`). EF Core Migrations enabled, EnsureCreated replaced with MigrateAsync, VA-ARCH-001 modified. Unblocks Wave 2/7/8.

---

## 3. Current Status

- **Branch:** `feature/hkd-fix-wave2-data-source-bridge-option-a`
- **Last commit:** `042c5ef` [STATE] Update project_state.md - Stream E complete, Wave 2 unblocked
- **Build:** `dotnet build VanAn.sln` Release → 0 errors ✅ (990 warnings)
- **Guard-check:** ✅ PASS (after commit — untracked test file will be committed)
- **Tests:** VanAn.Core.Tests 776 passed / 0 failed · VanAn.Architecture.Tests 28 passed / 0 failed · **Wave 2 targeted tests: 3/3 passed**
- **Uncommitted changes:** `SmartPreAggregationService.cs` (query refactor + SQLite decimal fix L155-205) + `SmartPreAggregationServiceWave2Tests.cs` (3 new tests, PASSING) — on `feature/hkd-fix-wave2-data-source-bridge-option-a` branch
- **Completed features (merged to main):** Tenant Onboarding (6 waves) · ShopConfig Refactor (3 phases) · Architecture Test Fixes · CI/CD Hotfix · **Stream C: ShopERP UI Fix (6 waves)** · **Stream B: E2E Test Cleanup (8 waves, planning merged; wave branches await merge)** · **Stream D Wave 0+0.5+1** · **Stream E: DB Migration Strategy**.
- **Stream D execution artifacts (merged to main):**
  - `docs/AI/tasks/wave0_hkd_fix_preflight_task_card.md` — updated with Section 13 (Execution Findings) + Section 14 (3 New Gaps) + Section 15 (Updated SC)
  - `docs/AI/tasks/wave0p5_hkd_fix_arch_decision_data_source_task_card.md` — updated with Section 13 (Decision Output: Option A) + Section 14 (DB Schema Caveat)
  - `docs/AI/project_state.md` — being updated with Wave 0+0.5 results + Stream E
- **Pre-existing defects found (NOT Stream C regressions):**
  1. **Blazor circuit crash on `/`, `/sitemap`, `/admin/users`** — `System.InvalidOperationException: Authorization requires a cascading parameter of type Task<AuthenticationState>` from `AuthorizeViewCore.OnParametersSetAsync()`. Pages prerender correctly (visual content visible) but interactivity breaks after circuit connect. Routes.razor has `<CascadingAuthenticationState>` + `<AuthorizeRouteView>` — cascade timing issue. Candidate for a dedicated Blazor auth fix stream.
  2. **DevLoginController role mismatch** — `/admin/users` uses `[Authorize(Policy = "OwnerOnly")]` (requires "Owner" role), but `POST /dev/login/systemadmin` issues "SystemAdmin" role → access denied. SystemAdmin dev login cannot reach admin pages. E2E tests must use Owner login for admin routes.
- **Dead code note:** `CustomerPage.ts` loyalty methods (`loyaltyPointsDisplay` L44, `getLoyaltyPoints` L191, `applyLoyaltyPoints` L201) are now unreferenced after Stream B Wave 4 SCENARIO 2 deletion. Candidate for future page-object cleanup.
- **In-progress:** Stream D Wave 2 (Option A — refactor `SmartPreAggregationService.GetAccountSumAsync` to query `AccountingEntries` directly). ✅ COMPLETE — Query refactor DONE (L155-205), 3 unit tests PASSING. **Root cause of prior test failure:** NOT a `TenantId` translation issue — SQLite `SumAsync` on `decimal` throws `NotSupportedException` (`SqliteQueryableAggregateMethodTranslator`). Fix: materialize `Amount` values via `ToListAsync()` then `Sum()` client-side to preserve decimal precision. Branch: `feature/hkd-fix-wave2-data-source-bridge-option-a`.

---

## 4. Next Actions

**Stream D — HKD Book Accounting Report Fix (active, Wave 0+0.5 in progress):**
1. ~~Audit + master plan v1 + 9 task cards~~ ✅ — Committed `c4acb15`.
2. ~~v2 plan optimization (3 amendments + 5 concerns resolved)~~ ✅ — Committed `c8d4a6c` + `22d3976`.
3. ~~v3 plan optimization (2 phản biện + Amendment 4+5)~~ ✅ — Committed `88e635a` + `4b2f077` + `7fb6dec`.
4. ~~Commit v3 master plan~~ ✅ — Committed `7fb6dec` + `dad3d76`.
5. ~~Wave 0: Pre-flight verification (11 tasks)~~ 🔄 IN PROGRESS — 15/21 tasks done (T1-T11 + W0.5-T1-T4). 3 new gaps found. See execution findings in `wave0_hkd_fix_preflight_task_card.md` Section 13-15.
6. ~~Wave 0.5: Architecture Decision~~ ✅ DONE — **Option A chosen** (refactor `SmartPreAggregationService` query `AccountingEntries` directly, no JournalEntry writing). See `wave0p5_hkd_fix_arch_decision_data_source_task_card.md` Section 13.
7. **Wave 0 remaining (6 tasks):** ⏳ PENDING
   - a. Propagate T8 decision → Wave 3 task card (ITemplateFactory: keep both, no conflict)
   - b. Propagate T9 decision → Wave 8 task card (EPPlus XLSX + DocumentFormat.OpenXml DOCX approved)
   - c. Propagate T10 decision → Wave 5b task card (IndustrySector MISSING, conditional)
   - d. Propagate T11 decision → Wave 2 task card (Path B, but Option A avoids JournalEntry writing)
   - e. W0-T7b: Install pandoc + extract S1a_HKD.doc layout
   - f. W0-T7c: Complete 6 .docx layout extraction
8. **W0.5-T5: Rewrite Wave 2 task card per Option A** ⏳ PENDING — Replace "Bridge JournalEntry persistence" with "Refactor SmartPreAggregationService query AccountingEntries". Rename file `wave2_hkd_fix_bridge_journal_persistence_task_card.md` → `wave2_hkd_fix_data_source_bridge_task_card.md`.
9. ~~Stream E: DB Migration Strategy~~ ✅ DONE — Merged `b2e0431`. InitialCreate migration + DesignTimeDbContextFactory + VA-ARCH-001 modified + EnsureCreated→MigrateAsync. Dev DB migrated.
10. ~~guard-check.ps1 + commit Wave 0+0.5~~ ✅ DONE — Committed `88a7fb4`, merged `06bfd44`.
11. ~~Merge Wave 0+0.5 to main~~ ✅ DONE — Merged `06bfd44`.
12. ~~Wave 1: Fix UTF-8 mojibake~~ ✅ DONE — Committed `83cca48`, merged `2f294c8`. S1a+S2a+S3a fixed (S3a also had mojibake, caught + fixed).
13. ~~Wave 2: Data source bridge (Option A per W0.5)~~ ✅ DONE — Query refactor + SQLite decimal `SumAsync` fix (materialize + client-side sum). 3/3 unit tests PASSING. Core.Tests 776/0, Arch.Tests 28/0. Branch: `feature/hkd-fix-wave2-data-source-bridge-option-a`. Awaiting user review before merge.
14. **Wave 3: Wire calc engine into DI** ⏳ PENDING — Conflict pre-resolved in W0-T8. Task card: `wave3_hkd_fix_wire_calc_engine_di_task_card.md`.
15. **Wave 4: Route through IHKDBookGenerationService + smoke test** ⏳ PENDING — Rewrite 7 methods + W4-T11 tripwire. Task card: `wave4_hkd_fix_route_through_generation_service_task_card.md`.
16. **Wave 5a: Fix account mapping + PIT-on-revenue** ⏳ PENDING — Fix `_vietnameseAccounts`, PIT base, S2b account (521→5118). **W5a-T4 needs Tech Lead approval (Domain account-number fix).** Task card: `wave5a_hkd_fix_account_mapping_pit_task_card.md`.
17. **Wave 5b: Industry-sector tax rates** ⏳ CONDITIONAL — W0-T10 confirmed `Tenant.IndustrySector` MISSING. **W5b-T0 needs Tech Lead approval (add IndustrySector to Tenant) OR descope.** Task card: `wave5b_hkd_fix_industry_sector_tax_rates_task_card.md`.
18. **Wave 5c: 2026 Regulatory Compliance Fix** ⏳ PENDING — **[v3] CRITICAL pháp lý.** Fix threshold 500M→1B, 4 revenue groups mới, TNCN formulas Nhóm 2/3/4, thuế khoán abolished. **Legal review recommended.** Task card: `wave5c_hkd_fix_2026_regulatory_compliance_task_card.md`.
19. **Wave 6: Retrofit tests with numeric assertions** ⏳ PENDING — Update 3 + add 5 + 1 regression test. Task card: `wave6_hkd_fix_retrofit_numeric_tests_task_card.md`.
20. **Wave 7: API endpoint + DI smoke + multi-tenancy test** ⏳ PENDING — Endpoint + W7-T6 isolation test. Task card: `wave7_hkd_fix_api_endpoint_di_smoke_task_card.md`.
21. **Wave 8: UI page + DOCX/XLSX export + regression prevention** ⏳ PENDING — UI + export (EPPlus XLSX + DocumentFormat.OpenXml DOCX, both approved) + architecture test + encoding lint. Task card: `wave8_hkd_fix_ui_docx_export_regression_task_card.md`.

**Critical path updated:** ~~Wave 0~~ ✅ → ~~Wave 0.5~~ ✅ → ~~commit~~ ✅ → ~~merge~~ ✅ → ~~Stream E~~ ✅ → ~~Wave 1~~ ✅ → **Wave 2 (Option A, UNBLOCKED)** → Wave 3 → Wave 4 → Wave 5a → **5c** → 6 → 7 → 8. **Optional:** Wave 5b (conditional, needs Tech Lead approval).

**Deferred (awaiting user decision):**
1. **Merge Stream B to main** — Stream B wave branches await merge to main. All 8 waves complete, guard PASSED.
2. **Push to origin** — `main` is ahead of `origin/main` (Stream B + Stream D planning commits).
3. **Stream A: EInvoice Provider Rewrite** — Planning complete (`59b60fe`). Blocker: Wave 0 sandbox credentials (1-2 tuần).
4. **Blazor `CascadingAuthenticationState` circuit crash** — pre-existing defect. Separate FIX_ONLY stream candidate.
5. **Tech Lead approval for Stream D Wave 5a W5a-T4** — Domain modification (`HKDTemplates.cs` S2b account "512"→"5118", no new field). Needed before Wave 5a.
6. **Tech Lead approval for Stream D Wave 5b W5b-T0** — W0-T10 confirmed `Tenant.IndustrySector` MISSING. Add `IndustrySector` to `Tenant` OR descope Wave 5b (use default tax rate, log technical debt). Needed before Wave 5b (if not descoped).
7. **Legal review for Stream D Wave 5c** — Confirm 2026 regulatory changes (threshold 1B, 4 revenue groups, TNCN formulas, thuế khoán abolished) với bộ phận pháp lý/thuế. Recommended before Wave 5c implementation.
8. ~~Stream E: DB Migration Strategy~~ ✅ DONE — Merged `b2e0431`.

---

## 5. Active Architecture Decisions

| Decision | R lý do |
|---|---|
| CoreHub = in-process background service trong Gateway | Monolith Phase 1-2 |
| Gateway = DI composition root cho CoreHub | Program.cs đăng ký CoreHub DbContext/Services |
| ShopERP = SQLite-only edge node | Edge deployment offline-first |
| CustomerToken = `IDataProtector` | Tránh library mới |
| `AccountingEntry` immutable, Reversal Entry | Audit trail bất khả xâm phạm |
| Multi-tenancy `TenantId` filter mọi layer | Data isolation per HKD |
| **[NEW] EF Core Migrations = official schema management** | Stream E decision — replace `EnsureCreated` for production, allow Migrations at Infrastructure layer (VA-ARCH-001 modified) |
| **[NEW] HKD Data Source = Option A (query AccountingEntries directly)** | Wave 0.5 decision — HKD-only product, AccountingEntry is immutable SSoT, no JournalEntry writing needed |
| **[NEW] DOCX export = DocumentFormat.OpenXml + XLSX export = EPPlus 7.6.1** | Wave 0 T9 — user approved both libraries for Wave 8 |

---

## 6. History Log (compressed — see git log for details)

* [2026-07-03] **STREAM B WAVE 2 COMPLETE** — Pattern D (Fix wrong auth pattern). 6 spec files fixed. 5 simple files (`expense-entry-flow`, `export-excel-flow`, `einvoice-dashboard`, `invoice-management`, `provider-management`): removed form-fill `beforeEach` (`#username`/`#email` + `#password` + `waitForURL`), removed `test.use({ storageState: { cookies: [], origins: [] } })` override from 3 einvoice files, removed unused `loadEnvConfig` imports — all rely on global `storageState` (auth/admin.json via `playwright.config.ts` L34+L56). `rbac-enforcement`: removed `loginAs` helper (filled `#Username`/`#Password` on non-existent `/Login` form); Owner tests (2) use global storageState; 6 multi-role tests (Staff/StoreKeeper/Guard) skipped with `test.skip(true, 'Requires auth/<role>.json — not generated by global-setup')` + note; unauthenticated test wrapped in `test.describe` with `test.use({ storageState: { cookies: [], origins: [] } })`. `export-excel-flow` staff-role test also skipped. Verification: 0 `fill('#username'…)` / 0 `fill('#email'…)` / 0 `fill('#Username'…)` / 0 `VanAn@2026` / 0 broken `waitForURL` remaining (grep confirmed). `npx playwright test --list` → 759 tests in 20 files (unchanged). `dotnet build VanAn.sln` → 0 errors. guard-check.ps1 → ALL CHECKS PASSED. Commit `b40b640` on `feature/e2e-cleanup-wave2-fix-auth-pattern`. Next: Wave 3 (delete anti-schema tests — `voice-command.spec.ts` 4 tests, `i18n.spec.ts` 1 test).
* [2026-07-03] **STREAM B WAVE 0+1 COMPLETE** — E2E Test Cleanup started. Wave 0 pre-flight: verified `auth/admin.json` generation (`global-setup.ts` L111-112), `playwright.config.ts` L34+L56 global `storageState`, `isTierEnabled('e2e')` in `env-config.ts` L359-389, git tree clean, `npx playwright test --list` → 759 tests in 20 files (parse OK). Wave 1 (Pattern F — Remove decorative `reporter.pass()`): removed 59 calls across 9 spec files (`accounting-flow` 14, `order-flow` 7, `order-tracking` 7, `qr-payment-ui` 7, `audit-trail-flow` 6, `period-closing-flow` 5, `van-an-dashboard` 5, `balance-dashboard-flow` 4, `qr-payment` 4) via brace-balanced Node script `6_Testing/scripts/wave1-remove-reporter-pass.js` (handles single+multi-line forms, skips comments/strings, conditionally removes TestReporter import/decl — none unused). 4 `reporter.pass` comment refs preserved (T-17/T-19/T-21 FIX notes). All 9 files retain `reporter.log`/`setArchitectDecision` usage in `beforeAll`. Verification: 0 `reporter.pass(` calls remain (grep confirms only 4 comment refs), `npx playwright test --list` → 759 tests in 20 files (unchanged), `dotnet build VanAn.sln` → 0 errors, guard PASSED. Commit `0c7965e` on `feature/e2e-cleanup-wave1-remove-reporter-pass`. Pre-Stream-B hygiene commit `797ce36` (committed uncommitted smoke test state from prior session). Next: Wave 2 (fix auth pattern in 6 files — `expense-entry-flow`, `export-excel-flow`, `einvoice-dashboard`, `invoice-management`, `provider-management`, `rbac-enforcement`).
* [2026-07-03] **VISUAL SMOKE TEST PASSED** — Post-Stream-C merge validation. Built VanAn.sln (0 errors), started ShopERP on port 5003, authed via DevLoginController (`POST /dev/login` → Owner role). Playwright headless Chromium 1440×900 navigated 6 routes: `/` (→/sitemap redirect by design, Wave 6 Home.razor), `/sitemap` (Vạn An Ecosystem), `/access-denied` (403 — Không Có Quyền Truy Cập), `/accounting` (Kế Toán Dashboard), `/einvoice` (Dashboard Hóa Đơn Điện Tử), `/admin/users` (Quản lý người dùng). All 6 HTTP 200, all render with sidebar+nav+page title. Screenshots: `6_Testing/reports/smoke-shots/{Home,Sitemap,AccessDenied,Accounting,EInvoice,AdminUsers}.png`. Stream C waves verified in production render: Wave 1 (UI.Platform CSS + Bootstrap Icons CDN load), Wave 3 (`css/pages.css` 6585B loads), Wave 4 (`VanAButton` renders — confirmed in server logs), Wave 5 (AdminLayout on /admin/users), Wave 6 (Home redirect + AccessDenied 403 message, no inline `<style>`). **Pre-existing defects found (NOT Stream C regressions):** (1) Blazor circuit crash on `/`, `/sitemap`, `/admin/users` — `System.InvalidOperationException: Authorization requires a cascading parameter of type Task<AuthenticationState>` from `AuthorizeViewCore.OnParametersSetAsync()`; pages prerender correctly but interactivity breaks after circuit connect; Routes.razor has `<CascadingAuthenticationState>` + `<AuthorizeRouteView>` — cascade timing issue. (2) DevLoginController role mismatch — `/admin/users` requires "Owner" role (`OwnerOnly` policy) but `/dev/login/systemadmin` issues "SystemAdmin" → access denied for admin routes via SystemAdmin login.
* [2026-07-03] **STREAM C FULLY DONE & MERGED TO MAIN** — Wave 6 (Governance cleanup) + fast-forward merge of all 6 waves into `main` (`cc10e08..f3ed2d2`, 46 files, +1622/-428 lines). Wave 6 details: Remove inline `<style>` blocks from AccessDenied.razor (37 lines), Sitemap.razor (72 lines), AuditTrail.razor (165 lines — exit criteria required 0 inline `<style>` in ALL .razor files; CSS already covered by `.razor.css` from Wave 3 with better design tokens). Fix Sitemap logout: replace `JSRuntime.InvokeVoidAsync("eval", ...)` (security concern — eval + manual cookie clear) with `NavigationManager.NavigateTo("/Logout", forceLoad: true)` using existing `Pages/Logout.cshtml` server-side `SignOutAsync` endpoint. Remove now-unused `@inject IJSRuntime JSRuntime`. Fix 7 broken emojis (U+FFFD replacement chars from encoding corruption) in Sitemap with semantic emojis: 📷 (Guard scan), 💸 (Chi Phí), 📈 (Doanh Thu), 📜 (Lịch Sử), 📅 (Đóng Kỳ), 🌐 (KhachLink), 🔑 (Nhóm Quyền). Delete `Counter.razor` (Blazor template demo, 0 references in NavMenu/Sitemap). Fix `Home.razor`: add `<PageTitle>Đang chuyển hướng...</PageTitle>` + loading state div + new `Home.razor.css` (CSS isolation for `.redirect-loading`). Build 0 errors on main post-merge, guard PASSED. Commits `ea1ced5` + `f3ed2d2`. **Stream C complete — 14 dead → 0, 18 unstyled → 0, 3 broken layouts → 0.**
* [2026-07-03] **Wave 5 COMPLETE** — Admin layout consistency: Create `AdminLayout.razor` (VanALayout + VanANavigation with 4 Admin menu items matching NavMenu: Users `/admin/users`, Permission Groups `/admin/permission-groups`, Audit Trail `/admin/audit-trail`, Tenants `/admin/tenants`) following AccountingLayout pattern (post-Wave 1 slot fix). Add `@layout AdminLayout` to 4 Admin pages (AuditTrail, UserManagement, PermissionGroupManagement, TenantManagement) at line 3 (after `@page` + `@rendermode`). AdminLayout has no `@attribute [Authorize]` — each page self-authorizes. 5 files, +24 lines. Build 0 errors. Commit `7f05fa7` on `feature/shoperp-ui-fix-wave5-admin-layout`.
* [2026-07-03] **Wave 4 COMPLETE** — Component consolidation: `VanAnAlert` (old, Atomic namespace) → `VanAAlert` (new) in 6 EInvoice files (10 occurrences). API fix: `Type="..."` (unmatched attr, broken) → `Variant="..."` (real param). `Type="danger"` → `Variant="error"` (VanAAlert uses "error"). `VanAnModal`: 0 occurrences in EInvoice scope (only in KhachLink — out of scope, debt cleanup candidate). 6 files, 10 line changes. Build 0 errors. Commit `47268a0` on `feature/shoperp-ui-fix-wave4-component-consolidation`.
* [2026-07-03] **Wave 3 COMPLETE** — Page CSS isolation: `wwwroot/css/pages.css` (shared, 276 lines — `:root` design tokens + 17 common classes: page-header, metrics-grid, filter-grid, vanan-table, status-badge, pagination, etc.) + 18 `.razor.css` files (page-specific classes). Linked in `App.razor`. 20 files, +1228 lines. Build 0 errors. Commit `d2d058d` on `feature/shoperp-ui-fix-wave3-page-css`.
* [2026-07-03] **Wave 2 COMPLETE** — Add `@rendermode InteractiveServer` to 14 files (AccessDenied, Sitemap, AccountingIndex, TransactionHistory, 6 EInvoice, 4 Admin). 14 files, +14 lines. Build 0 errors. Commit `79ec512` on `feature/shoperp-ui-fix-wave2-rendermode`.
* [2026-07-03] **Wave 0 + Wave 1 COMPLETE** — Pre-flight verified. Wave 1: VanALayout.razor.css + VanANavigation.razor.css (NEW), icon fix (`<i class="bi bi-@icon">`), Bootstrap Icons CDN, 3 layout files slot fix, VanADashboard emoji→BI icons. Commit `3b893e8` on `feature/shoperp-ui-fix-wave1-platform-infra`.
* [2026-07-02] **ShopERP UI Fix + E2E Cleanup — PLANNING COMPLETE** — 2 master plans + 14 task cards (`51dd7ff`). UI: 23 files, 6 patterns (P/R/C/V/L/G). E2E: 20 spec files, 7 anti-patterns.
* [2026-07-02] **EInvoice Provider Rewrite — PLANNING COMPLETE** — Master plan + 4 task cards (`59b60fe`). 20 Viettel + 10 MISA API spec mismatches. Wave 0 credential request parallel.
* [2026-07-02] **ShopConfig Refactor — 3 PHASES COMPLETE** — Product→tenant refactor, KhachLink HTTP-only, merged to main.
* [2026-07-02] **Tenant Onboarding — 6 WAVES COMPLETE & MERGED** — Generic multi-industry onboarding (F&B enabled), orchestrator, Gateway API, ShopERP UI, integration tests. Commit `3123b6b`.
* [2026-07-02] **Architecture Test Fixes + CI/CD Hotfix** — 28/28 arch tests PASS, remote CI fixed.
* [2026-07-01] **Tenant Onboarding Waves 1-4** — Abstraction + F&B seed + orchestrator + Gateway API.
* [2026-07-01] **Documentation Added** — KhachLink + ShopERP module docs.

---

## 7. Active Files Reference

### Stream C — ShopERP UI Fix
| File | Role |
|---|---|
| `docs/AI/tasks/shoperp_ui_fix_master_plan.md` | Master plan (6 waves) |
| `docs/AI/tasks/wave1_shoperp_ui_platform_infra_task_card.md` | Wave 1: Pattern P (UI.Platform) |
| `docs/AI/tasks/wave2_shoperp_rendermode_task_card.md` | Wave 2: Pattern R (rendermode) |
| `docs/AI/tasks/wave3_shoperp_page_css_task_card.md` | Wave 3: Pattern C (CSS) |
| `docs/AI/tasks/wave4_shoperp_component_consolidation_task_card.md` | Wave 4: Pattern V (versions) |
| `docs/AI/tasks/wave5_shoperp_admin_layout_task_card.md` | Wave 5: Pattern L (Admin layout) |
| `docs/AI/tasks/wave6_shoperp_governance_cleanup_task_card.md` | Wave 6: Pattern G (governance) |

### Parked Streams
| File | Role |
|---|---|
| `docs/AI/tasks/einvoice_provider_rewrite_master_plan.md` | Stream A (parked) |
| `docs/AI/tasks/e2e_test_cleanup_master_plan.md` | Stream B (parked) |

---

## 8. Architecture Quick Reference

```
KhachLink (5002) → Gateway (5001) → ShopERP (5003) → SQLite
                        ↓
              [in-process CoreHub services]
                        ↓
                  PostgreSQL (prod) / SQLite (edge)
```

**Docker (prod):** postgres · nats · seq · gateway · shoperp · khachlink · nginx · certbot
**Docker (edge):** postgres · nats · gateway · shoperp · shoperp-nats-sync
**CoreHub:** NOT a Docker service — runs in-process inside Gateway.

---

## 9. Maintenance Log

* **Last Updated:** 2026-07-03 — Wave 2 (Option A) ✅ COMPLETE on `feature/hkd-fix-wave2-data-source-bridge-option-a`. `SmartPreAggregationService.GetAccountSumAsync` (L155-205) now queries `AccountingEntries` directly (Option A per W0.5). 3 unit tests written + PASSING (`SmartPreAggregationServiceWave2Tests.cs`). **Root cause of prior failure:** NOT `TenantId` translation — SQLite `SumAsync` on `decimal` throws `NotSupportedException` (`SqliteQueryableAggregateMethodTranslator`). Fix: materialize `Amount` via `ToListAsync()` + client-side `Sum()` to preserve decimal precision. Build: 0 errors / 990 warnings. Tests: Core 776/0, Arch 28/0. Awaiting user review before merge.
* **Current Branch:** `feature/hkd-fix-wave2-data-source-bridge-option-a`
* **Current Objective:** Stream D Wave 2 ✅ COMPLETE — awaiting user review, then merge + proceed to Wave 3 (wire calc engine into DI).
