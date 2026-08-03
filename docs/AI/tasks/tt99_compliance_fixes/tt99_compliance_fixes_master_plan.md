# MASTER PLAN — TT 99/2025/TT-BTC Compliance Fixes (8 Gaps)

> **Status:** 🟡 PLANNED + ANALYZE COMPLETE — 8 gaps identified, 6 task cards verified against codebase via parallel subagent investigation (2026-08-03). See `ANALYZE_REPORT_reverse_impact.md` for full findings.
> **Created:** 2026-08-03 · **Last Updated:** 2026-08-03 (ANALYZE pass)
> **Workflow:** `newfeaturebuild.md` (ANALYZE → IMPLEMENT) · **Branch:** per-phase feature branch, always-green main
> **Source:** User request 2026-08-03 — verify codebase against TT 99/2025/TT-BTC (BCTC năm, DN hoạt động liên tục)
> **Official sources verified:** MISA (amis.misa.vn), thuvienphapluat.vn, Grant Thornton, Bộ Tài chính (portal.mof.gov.vn), tanngoctax.vn
> **Codebase verification:** 6 subagents verified all task card claims against actual files — see `ANALYZE_REPORT_reverse_impact.md`

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
- **Mỗi phase có 1 task card** tại `docs/AI/tasks/tt99_compliance_fixes/phase{N}_task_card.md`
- Task card chứa: objective, prerequisites, exact file changes, code snippets, verification, rollback
- **Task card phải được đọc TRƯỚC khi code** (Phase 1)
- **Task card có thể update** nếu INVESTIGATE phát hiện drift

### Anti-Guessing Gate (Gate 1 từ .windsurfrules)
- Assumptions ≥ Verified Facts → CẤM code, chuyển Investigate
- Mỗi phase phải có ≥ 3 verified facts trước khi implement:
  1. File path tồn tại (verify bằng read/glob)
  2. Method signature đúng (verify bằng grep)
  3. Dependency chain đúng (verify bằng trace)

---

## 1. EXECUTION RULES

### Dependency chain (REVISED after ANALYZE)
```
Phase 5a (NEW — TenantSettings extension) ──┐
Phase 1 (P0 — Rename B 01-DN)               │  Independent, parallel
Phase 2 (P4 — Auto-standard + split TrialBalance) │
Phase 6 (P2 — BĐSĐT, needs seeder update)   │
                                             ┘
Phase 3 (P2 — Indirect method) ← needs CashFlowStatementService DI change
Phase 4 (P3 — Template structure) ← depends on Phase 1+2 (naming + standard)
Phase 5 (P1 — B 09-DN Thuyết minh) ← depends on Phase 5a (Tenant fields) + Phase 4 (template for Phần IV)
```

- **Phase 5a + 1 + 2 + 6** độc lập, có thể làm song song (4-way parallel)
- **Phase 3** phụ thuộc service DI change (inject IBalanceSheetService + IIncomeStatementService)
- **Phase 4** phụ thuộc Phase 1 (naming) + Phase 2 (standard selection)
- **Phase 5** phụ thuộc Phase 5a (Tenant fields) + Phase 4 (template for Phần IV giải thích)
- **Phase 6** cần seed TK 5117/6327 trước (prerequisite trong AccountChartSeeder)
- Mỗi phase xong: `dotnet build VanAn.sln` Release pass + `guard-check.ps1` pass + commit

### Session protocol
1. Mỗi phase làm 1 session (Phase 4 + 5 có thể 2-3 session do complexity)
2. Bắt đầu session: đọc `project_state.md` + task card phase
3. Trước session end: build pass + commit
4. Commit format: `[TT99-FIX P{N}] <short description>`

### Branch protocol
```
main ← feature/tt99-fix-phase1-rename-b01dn
main ← feature/tt99-fix-phase2-standard-autoselect
main ← feature/tt99-fix-phase3-cashflow-indirect
main ← feature/tt99-fix-phase4-template-structure
main ← feature/tt99-fix-phase5-b09dn-thuyet-minh
main ← feature/tt99-fix-phase6-bdsdt-indicator
```

---

## 2. GAP INVENTORY (VERIFIED 2026-08-03)

### Nguồn chính thức xác nhận
**Thông tư 99/2025/TT-BTC** (ban hành 27/10/2025, hiệu lực 01/01/2026, thay thế TT 200/2014/TT-BTC):
- Điều 17: quy định hệ thống mẫu biểu BCTC năm
- Phụ lục IV: biểu mẫu BCTC năm (B 01-DN, B 02-DN, B 03-DN, B 09-DN)
- Điều 31: hiệu lực từ 01/01/2026, áp dụng cho năm tài chính bắt đầu từ hoặc sau 01/01/2026

### Bộ BCTC năm theo TT 99 (DN hoạt động liên tục)
| STT | Báo cáo | Mẫu | File codebase |
|-----|---------|-----|---------------|
| 1 | Báo cáo tình hình tài chính | B 01-DN | `BalanceSheet` + `BalanceSheetService` + `BalanceSheet.razor` |
| 2 | Báo cáo kết quả HĐKD | B 02-DN | `IncomeStatement` + `IncomeStatementService` + `IncomeStatement.razor` |
| 3 | Báo cáo lưu chuyển tiền tệ | B 03-DN | `CashFlowStatement` + `CashFlowStatementService` + `CashFlowStatement.razor` |
| 4 | Bản thuyết minh BCTC | B 09-DN | **THIẾU** — chưa có |

### 8 Gaps đã verify

| # | Gap | Mức độ | Phase | Effort | Domain Mod |
|---|-----|--------|-------|--------|------------|
| 1 | B 09-DN Thuyết minh BCTC — THIẾU hoàn toàn | 🔴 Nghiêm trọng | Phase 5 | 2-3 sessions | YES (new record) |
| 2 | B 01-DN sai tên "Bảng CĐKT" → "Báo cáo tình hình TC" | 🟡 Trung bình | Phase 1 | 30 min | NO (UI + comment only) |
| 3 | B 03-DN thiếu phương pháp gián tiếp | 🟡 Trung bình | Phase 3 | 1 session | YES (enum + record) |
| 4 | Cấu trúc báo cáo flat account list, không phải TT 99 template (Mã 100/110...) | 🟡 Trung bình | Phase 4 | 2-3 sessions | YES (template records) |
| 5 | Default standard = TT133, không auto-select theo tenant type | 🟢 Nhẹ | Phase 2 | 30 min | NO (UI logic) |
| 6 | Thiếu TT58_2026 trong dropdown | 🟢 Nhẹ | Phase 2 | 15 min | NO (UI option) |
| 7 | B 03-DN thiếu chỉ tiêu "Lãi/lỗ bán BĐSĐT" | 🟢 Nhẹ | Phase 6 | 1 session | YES (service logic) |
| 8 | TrialBalance nằm trong bộ BCTC (không thuộc TT 99) | 🟢 Nhẹ | Phase 2 | 15 min | NO (UI layout) |

---

## 3. PHASE DETAILS

### Phase 1 — P0: Rename B 01-DN "Bảng Cân Đối Kế Toán" → "Báo Cáo Tình Hình Tài Chính"
**Task card:** `phase1_task_card.md`
**Objective:** Đổi tên hiển thị B 01-DN theo TT 99 tại 4 vị trí (UI heading, hub card, domain comment, export title).
**Files:** `BalanceSheet.razor`, `FinancialReports.razor`, `Domain.cs` (comment only), `FinancialReportExportService.cs`
**Domain mod:** NO (comment only)
**Effort:** 30 phút
**Verification:** Build pass + UI hiển thị "Báo Cáo Tình Hình Tài Chính" + export title đúng

### Phase 2 — P4: Auto-select standard + TT58 dropdown + tách TrialBalance
**Task card:** `phase2_task_card.md`
**Objective:** (a) Auto-select `AccountingStandard` theo `Tenant.Type` thay vì hardcode TT133. (b) Thêm TT58_2026 option trong dropdown. (c) Tách TrialBalance khỏi bộ BCTC trong `FinancialReports.razor`.
**Files:** `BalanceSheet.razor`, `IncomeStatement.razor`, `CashFlowStatement.razor`, `TrialBalance.razor`, `FinancialReports.razor`
**Domain mod:** NO (UI logic only)
**Effort:** 1 session
**Verification:** Build pass + DN lớn auto-select TT99 + DN siêu nhỏ có option TT58 + TrialBalance ở section riêng

### Phase 3 — P2: B 03-DN phương pháp gián tiếp
**Task card:** `phase3_task_card.md`
**Objective:** Thêm phương pháp gián tiếp cho Báo cáo lưu chuyển tiền tệ (TT 99 yêu cầu cả trực tiếp + gián tiếp).
**Files:** `Domain.cs` (CashFlowMethod enum + CashFlowStatement record update), `CashFlowStatementService.cs`, `CashFlowStatement.razor`
**Domain mod:** YES — add `CashFlowMethod` enum, update `CashFlowStatement` record với `Method` field
**Effort:** 1 session
**Verification:** Build pass + UI có toggle trực tiếp/gián tiếp + service generate đúng theo method

### Phase 4 — P3: TT 99 template structure (Mã số 100/110/120...)
**Task card:** `phase4_task_card.md`
**Objective:** Refactor BalanceSheet/IncomeStatement/CashFlowStatement services để group accounts thành chỉ tiêu TT 99 (Mã 100 "Tài sản ngắn hạn", Mã 110 "Tiền và tương đương tiền"...) thay vì flat account list.
**Files:** `Domain.cs` (template records), `BalanceSheetService.cs`, `IncomeStatementService.cs`, `CashFlowStatementService.cs`, new `Tt99ReportTemplate.cs` (mapping TK → chỉ tiêu)
**Domain mod:** YES — add template mapping records
**Effort:** 2-3 sessions (large refactor)
**Verification:** Build pass + báo cáo hiển thị đúng cấu trúc Mã số TT 99 + totals khớp

### Phase 5 — P1: B 09-DN Bản thuyết minh BCTC
**Task card:** `phase5_task_card.md`
**Objective:** Tạo báo cáo Thuyết minh BCTC (B 09-DN) — báo cáo dạng văn bản thuyết minh, không phải bảng số liệu.
**Files:** `Domain.cs` (FinancialStatementNotes record), `FinancialStatementNotesService.cs` (new), `FinancialStatementNotes.razor` (new), `FinancialReportExportService.cs` (add export), `FinancialReports.razor` (add card), `AccountingLayout.razor` (no change — đã có menu)
**Domain mod:** YES — new `FinancialStatementNotes` record + sections
**Effort:** 2-3 sessions
**Verification:** Build pass + UI hiển thị thuyết minh + export DOCX/XLSX

### Phase 6 — P2: B 03-DN chỉ tiêu "Lãi/lỗ bán BĐSĐT"
**Task card:** `phase6_task_card.md`
**Objective:** Bổ sung chỉ tiêu "Lãi/lỗ của hoạt động bán, thanh lý BĐSĐT" vào CashFlowStatement (TT 99 mới bổ sung).
**Files:** `CashFlowStatementService.cs`, `CashFlowStatement.razor` (display)
**Domain mod:** NO (service logic only — CashFlowStatement record đã có OperatingActivities section)
**Effort:** 1 session
**Verification:** Build pass + báo cáo hiển thị chỉ tiêu BĐSĐT khi có dữ liệu

---

## 4. ROLLBACK STRATEGY

- Mỗi phase = 1 commit độc lập → rollback bằng `git revert <commit>`
- Phase 4 (large refactor) có thể tách thành 2-3 commits (per report) để rollback granular
- Phase 5 (new report) không ảnh hưởng existing → safe rollback
- Domain modifications (Phase 3, 4, 5) cần migration check — verify không phá existing data

---

## 5. VERIFICATION GATES

| Gate | Check | When |
|------|-------|------|
| Gate 1 | Anti-Guessing: Assumptions < Verified Facts | Trước mỗi phase |
| Gate 2 | Build: `dotnet build VanAn.sln` Release 0 errors | Sau mỗi phase |
| Gate 3 | Tests: `dotnet test` pass (existing + new) | Sau mỗi phase |
| Gate 4 | Guard: `guard-check.ps1` pass | Trước commit |
| Gate 5 | Domain Integrity: AccountingEntry immutable, Domain pure | Phase 3, 4, 5 |
| Gate 6 | UI Platform: dùng VanA components, không bypass | Phase 1, 2, 5 |

---

## 6. PROGRESS TRACKING

| Phase | Status | Commit | CI | CD | VPS RV | Notes |
|-------|--------|--------|----|----|--------|-------|
| Phase 5a (TenantSettings extension) | 🟡 PLANNED (NEW) | — | — | — | — | Prerequisite for Phase 5 |
| Phase 1 (Rename B 01-DN) | 🟡 PLANNED + ANALYZED | — | — | — | — | 7 files (was 3) |
| Phase 2 (Auto-standard + split TrialBalance) | 🟡 PLANNED + ANALYZED | — | — | — | — | Use IVasFeatureFlagService (no DTO change); TT58 → info msg |
| Phase 3 (B 03-DN indirect method) | 🟡 PLANNED + ANALYZED | — | — | — | — | 10 files; inject 2 services |
| Phase 4 (TT 99 template structure) | 🟡 PLANNED + ANALYZED | — | — | — | — | Large refactor; 11+ tests per service |
| Phase 5 (B 09-DN Thuyết minh) | 🟡 PLANNED + ANALYZED | — | — | — | — | BLOCKER: needs Phase 5a first |
| Phase 6 (B 03-DN BĐSĐT indicator) | 🟡 PLANNED + ANALYZED | — | — | — | — | Seed TK 5117/6327 first; Mã số "75" UNVERIFIED |

---

## 7. REFERENCES

- **TT 99/2025/TT-BTC full text:** https://thuvienphapluat.vn/phap-luat-doanh-nghiep/bai-viet/cap-nhat-mau-bao-cao-tai-chinh-2026-theo-thong-tu-99-thong-tu-133-21519.html
- **MISA mẫu BCTC:** https://amis.misa.vn/251989/mau-bao-cao-tai-chinh-theo-thong-tu-99-2025-tt-btc/
- **Grant Thornton analysis:** https://www.grantthornton.com.vn/contentassets/af9027513fcd4f7bb7c9e9aa395c3390/slide-key-updates-in-circular-no.-992025tt-btc-on-the-vietnamese-corporate-accounting-framework.pdf
- **Bộ Tài chính Hỏi đáp:** https://portal.mof.gov.vn/hoidapcstc/home/cthoidap/163296
- **Existing codebase:** `1_Shared/Domain.cs` (VAS records line 3287+), `3_CoreHub/Services/` (BalanceSheet/IncomeStatement/CashFlowStatement services), `5_WebApps/ShopERP/Components/Pages/Accounting/` (4 report pages)
