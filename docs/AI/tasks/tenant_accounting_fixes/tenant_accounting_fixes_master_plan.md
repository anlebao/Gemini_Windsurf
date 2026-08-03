# MASTER PLAN — Tenant Management + Accounting UI Fixes (4 Bugs)

> **Status:** 🟡 PLANNED — awaiting Phase 0 (Bug 3 runtime debug) + Phase 3 Domain approval confirmation
> **Created:** 2026-08-03 · **Last Updated:** 2026-08-03
> **Workflow:** `newfeaturebuild.md` (ANALYZE → IMPLEMENT) · **Branch:** per-phase feature branch, always-green main
> **Source:** User bug report 2026-08-03 (3 reports → 4 bugs after ANALYZE)

---

## 0. JIT PLANNING STRATEGY (NON-NEGOTIABLE)

**Nguyên tắc cốt lõi:** KHÔNG code mò mẫm — **Investigate trước, Implement sau**. Áp dụng cho mỗi phase.

### 3-Phase per wave
```
Phase 1 (INVESTIGATE): Đọc task card phase + verify codebase hiện tại
  → Confirm file paths, signatures, dependencies vẫn đúng
  → Grep usage của methods/symbols sẽ touch
  → Identify blast radius (ai gọi method này?)
  → Output: confirm task card vẫn accurate, hoặc flag drift

Phase 2 (PLAN): Detail coding plan
  → Liệt kê exact changes (file:line, old→new)
  → Identify test files cần update
  → Identify DI registrations cần thêm
  → Output: checklist implement

Phase 3 (IMPLEMENT): Code + verify
  → Apply changes theo checklist
  → Build + guard + tests pass
  → Commit
```

### Task Card Protocol
- **Mỗi phase có 1 task card** tại `docs/AI/tasks/tenant_accounting_fixes/phase{N}_task_card.md`
- Task card chứa: objective, prerequisites, exact file changes, code snippets, verification, rollback
- **Task card phải được đọc TRƯỚC khi code** (Phase 1)
- **Task card có thể update** nếu INVESTIGATE phát hiện drift
- **Task card KHÔNG thay thế master plan** — master plan là chiến lược, task card là chiến thuật

### Anti-Guessing Gate (Gate 1 từ .windsurfrules)
- Assumptions ≥ Verified Facts → CẤM code, chuyển Investigate
- Mỗi phase phải có ≥ 3 verified facts trước khi implement:
  1. File path tồn tại (verify bằng read/glob)
  2. Method signature đúng (verify bằng grep)
  3. Dependency chain đúng (verify bằng trace)

---

## 1. EXECUTION RULES

### Dependency chain
```
Phase 0 (Bug 3 Runtime Debug) ──→ Phase 3-fix (depends on root cause)
Phase 1 (Bug 2A — Hide "Sổ HKD" for Company)  ──┐
Phase 2 (Bug 2B — VAS Reports Export)           │  Independent, can run parallel
Phase 3 (Bug 1 — Edit BusinessType)             │  Requires Domain approval (APPROVED 2026-08-03)
                                                 ┘
```
- Phase 1 + Phase 2 + Phase 3 độc lập, có thể làm song song
- Phase 0 phải hoàn thành trước khi fix Bug 3 (cần runtime evidence)
- Mỗi phase xong: `dotnet build VanAn.sln` Release pass + `guard-check.ps1` pass + commit

### Session protocol
1. Mỗi phase làm 1 session (Phase 2 có thể 2 session do 4 reports)
2. Bắt đầu session: đọc `project_state.md` + task card phase
3. Trước session end: build pass + commit
4. Commit format: `[TENANT-FIX P{N}] <short description>`

### Branch protocol
```
main ← feature/tenant-fix-phase0-bug3-debug
main ← feature/tenant-fix-phase1-hide-hkd-menu
main ← feature/tenant-fix-phase2-vas-export
main ← feature/tenant-fix-phase3-edit-businesstype
```

---

## 2. BUG INVENTORY (ANALYZE RESULTS 2026-08-03)

### Bug 1 — SystemAdmin không sửa được loại hình (Công ty/HKD) cho tenant
- **Status:** ✅ CONFIRMED via code inspection
- **Root cause:**
  - `EditForm` class (`TenantManagement.razor:1034-1049`) không có field `BusinessType`/`HKDGroup`
  - `HandleEditSubmit` (`TenantManagement.razor:895-951`) chỉ gọi 3 API: UpdateProfile, UpdateSlug, AssignShopInstance — không có API đổi BusinessType
  - Domain method `Tenant.SetTenantType()` (`Tenant.cs:221-232`) có guard "Cannot change Type of already-classified tenant" — không phù hợp
  - **Domain defect:** Không có method nào cho phép đổi `BusinessType` sau khi tạo
- **Fix scope:** Domain + Service + Gateway API + TenantApiClient + UI Edit Modal + Tests
- **Domain approval:** ✅ APPROVED 2026-08-03 (user) — guard: block nếu tenant đã có AccountingEntry

### Bug 2A — Tenant "Công ty" hiển thị link "Sổ HKD" (sai)
- **Status:** ✅ CONFIRMED via code inspection
- **Root cause:**
  - `AccountingLayout.razor:79` — menu "Sổ HKD" nằm trong base menu (always visible), không có điều kiện `_isHkd`
  - So sánh: menu "Báo Cáo Tài Chính" (line 84-96) CÓ điều kiện `_isEnterprise` qua `CanAccessVasReportsAsync`
- **Fix scope:** UI only (1 file + 1 E2E test)
- **Domain approval:** Không cần

### Bug 2B — Báo cáo tài chính không xuất file được như sổ HKD
- **Status:** ✅ CONFIRMED via code inspection
- **Root cause:**
  - `HKDBookDetail.razor:61-66` CÓ 2 nút export (DOCX + XLSX) + JS `vanAn.downloadFile` (App.razor:30-48)
  - `BalanceSheet.razor`, `IncomeStatement.razor`, `CashFlowStatement.razor`, `TrialBalance.razor` — KHÔNG có nút export, KHÔNG inject export service
  - `IHKDBookExportService` tồn tại (HKDBookExportService.cs) nhưng không có `IFinancialReportExportService` tương đương cho VAS reports
- **Fix scope:** 1 service mới + DI registration + 4 UI pages + 4 E2E tests
- **Domain approval:** Không cần (feature gap, không động Domain)

### Bug 3 — Tenant HKD bấm "Mở sổ" không hoạt động
- **Status:** ⚠️ NEEDS RUNTIME DEBUG
- **Code path verified correct (static):**
  - Button "📖 Mở sổ" (`HKDBooks.razor:74-76`) → `OpenBook(t.TemplateCode)`
  - `OpenBook` (`HKDBooks.razor:122-125`) → `NavigationManager.NavigateTo($"/accounting/hkd-books/{templateCode}")`
  - Route exists: `@page "/accounting/hkd-books/{TemplateCode}"` (`HKDBookDetail.razor:1`)
  - Both pages: `@rendermode InteractiveServer` + `@attribute [Authorize(Policy = "OwnerOnly")]`
- **Possible root causes (4 hypotheses):**
  - (a) Blazor interactivity Category C — button click không fire (SignalR issue)
  - (b) Navigation fail (URL không đổi)
  - (c) `GenerateBookAsync` runtime exception → trang load nhưng hiện errorMessage
  - (d) Layout/Authorize block (trang trắng)
- **Fix scope:** TBD sau runtime debug
- **Domain approval:** Không cần (likely UI/runtime fix)

---

## 3. SCOPE DECISIONS (APPROVED 2026-08-03)

| # | Quyết định | Lựa chọn | Approved by |
|---|-------------|----------|-------------|
| D1 | Bug 1 Domain modification | ✅ Approved — thêm `Tenant.ChangeBusinessType()` với guard block nếu tenant đã có AccountingEntry | User 2026-08-03 |
| D2 | Bug 1 guard policy | Block change nếu tenant có ANY AccountingEntry (data integrity — different accounting standards HKD vs DN) | User 2026-08-03 |
| D3 | Bug 2A fix approach | Compute `_isHkd = !_isEnterprise` (reuse existing `CanAccessVasReportsAsync` result) | Tech Lead |
| D4 | Bug 2B export scope | Cả DOCX + XLSX cho 4 reports (8 methods) — reuse Open XML SDK + EPPlus đã có | Tech Lead |
| D5 | Bug 2B export architecture | 1 generic `IFinancialReportExportService` với generic method nhận `FinancialStatementLine` list + tiêu đề (4 reports cấu trúc tương tự) | Tech Lead |
| D6 | Bug 3 debug approach | Runtime debug với browser DevTools (Console + Network) — KHÔNG fix blind | User 2026-08-03 |
| D7 | Phase ordering | Phase 0 (debug) → Phase 1+2+3 (độc lập, có thể song song) | Tech Lead |
| D8 | E2E tests | Gate 4 compliance — mỗi UI layout change BẮT BUỘC E2E test tại `6_Testing/e2e-tests/` | Governance |

---

## 4. PHASE OVERVIEW (4 phases)

| Phase | Bug | Mode | Domain? | Task Card | Status |
|-------|-----|------|---------|-----------|--------|
| P0 | Bug 3 — Runtime Debug | ANALYZE | ❌ | `phase0_task_card.md` | 🟡 PENDING (user runtime debug) |
| P1 | Bug 2A — Hide "Sổ HKD" for Company | IMPLEMENT | ❌ | `phase1_task_card.md` | 🟡 PLANNED |
| P2 | Bug 2B — VAS Reports Export | IMPLEMENT | ❌ | `phase2_task_card.md` | 🟡 PLANNED |
| P3 | Bug 1 — Edit BusinessType | IMPLEMENT | ✅ (D1) | `phase3_task_card.md` | 🟡 PLANNED (Domain approved) |

**Chi tiết từng phase:** xem task card tương ứng. Master plan chỉ giữ overview.

---

## 5. FILE IMPACT MATRIX

| File | P0 | P1 | P2 | P3 | Total |
|------|----|----|----|----|-------|
| `1_Shared/Domain/Aggregates/TenantAggregate/Tenant.cs` | | | | ✏️ | 1 |
| `1_Shared/Domain/Aggregates/TenantAggregate/Events/*` (new) | | | | ✏️ | 1 |
| `3_CoreHub/Services/TenantManagementService.cs` | | | | ✏️ | 1 |
| `3_CoreHub/Services/ITenantManagementService.cs` | | | | ✏️ | 1 |
| `2_Gateway/Controllers/TenantsController.cs` | | | | ✏️ | 1 |
| `5_WebApps/ShopERP/Services/TenantApiClient.cs` | | | | ✏️ | 1 |
| `5_WebApps/ShopERP/Services/FinancialReportExportService.cs` (new) | | | ✏️ | | 1 |
| `5_WebApps/ShopERP/Program.cs` | | | ✏️ | | 1 |
| `5_WebApps/ShopERP/Components/Pages/Admin/TenantManagement.razor` | | | | ✏️ | 1 |
| `5_WebApps/ShopERP/Components/Pages/Accounting/AccountingLayout.razor` | | ✏️ | | | 1 |
| `5_WebApps/ShopERP/Components/Pages/Accounting/BalanceSheet.razor` | | | ✏️ | | 1 |
| `5_WebApps/ShopERP/Components/Pages/Accounting/IncomeStatement.razor` | | | ✏️ | | 1 |
| `5_WebApps/ShopERP/Components/Pages/Accounting/CashFlowStatement.razor` | | | ✏️ | | 1 |
| `5_WebApps/ShopERP/Components/Pages/Accounting/TrialBalance.razor` | | | ✏️ | | 1 |
| `5_WebApps/ShopERP/Components/Pages/Accounting/HKDBooks.razor` | TBD | | | | 0-1 |
| `5_WebApps/ShopERP/Components/Pages/Accounting/HKDBookDetail.razor` | TBD | | | | 0-1 |
| `6_Testing/e2e-tests/*.spec.ts` (new) | TBD | ✏️ | ✏️ | ✏️ | 3-4 |
| `6_Tests/VanAn.Core.Tests/...` (new) | | | | ✏️ | 1 |
| `6_Tests/VanAn.Integration.Tests/...` (new) | | | | ✏️ | 1 |

**Total new/modified files:** ~15-18 (excluding P0 TBD)

---

## 6. VERIFICATION GATES

### Per-phase gates
- [ ] `dotnet build VanAn.sln` Release — 0 errors
- [ ] `guard-check.ps1` — ALL CHECKS PASSED
- [ ] Unit tests pass (if applicable)
- [ ] E2E tests pass (if UI change — Gate 4)
- [ ] Commit on feature branch

### Final gate (after all 4 phases)
- [ ] Full solution build 0 errors
- [ ] All tests pass (unit + integration + E2E)
- [ ] VPS RV (manual verification on staging):
  - Bug 1: SystemAdmin đổi BusinessType thành công cho tenant chưa có accounting data
  - Bug 1: SystemAdmin đổi BusinessType FAIL cho tenant đã có accounting data (error message)
  - Bug 2A: Tenant Company → menu không có "Sổ HKD", có "Báo Cáo Tài Chính"
  - Bug 2A: Tenant HKD → menu có "Sổ HKD", không có "Báo Cáo Tài Chính"
  - Bug 2B: 4 VAS reports xuất được DOCX + XLSX
  - Bug 3: Tenant HKD bấm "Mở sổ" → trang detail load thành công

---

## 7. ROLLBACK STRATEGY

- Mỗi phase là 1 commit trên feature branch → rollback = git revert
- Phase 3 (Domain modification): nếu gây regression, revert commit + remove `ChangeBusinessType` method
- Không có migration mới (BusinessType + HKDGroup đã là column hiện có) → không cần migration rollback

---

## 8. OPEN QUESTIONS (RESOLVED)

| # | Question | Resolution | Date |
|---|----------|------------|------|
| Q1 | Bug 1 — Domain approval cho `Tenant.ChangeBusinessType()`? | ✅ APPROVED with guard: block nếu tenant đã có AccountingEntry | 2026-08-03 |
| Q2 | Bug 2B — Export scope (DOCX+XLSX hay chỉ XLSX)? | ✅ Cả DOCX + XLSX (8 methods hoặc generic) | 2026-08-03 |
| Q3 | Bug 3 — Debug environment (local hay VPS)? | ⏳ PENDING — user sẽ confirm khi bắt đầu Phase 0 | — |
| Q4 | Phase ordering? | ✅ Phase 0 → Phase 1+2+3 song song | 2026-08-03 |

---

## 9. MAINTENANCE LOG

| Date | Change | Author |
|------|--------|--------|
| 2026-08-03 | Initial creation — 4 bugs analyzed, 4 phases planned, D1-D8 approved | Devin (ANALYZE mode) |
