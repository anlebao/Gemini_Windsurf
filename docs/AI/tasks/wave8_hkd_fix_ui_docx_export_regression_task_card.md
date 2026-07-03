# TASK CARD: HKD Book Fix - Wave 8 - UI Page + DOCX Export + Regression Prevention

## 1. GOAL & CONTEXT
- **Mục tiêu cốt lõi:** (1) Tạo Razor page `/accounting/hkd-books` list templates + `/accounting/hkd-books/{templateCode}` render book theo TT 152 layout, (2) Add Export DOCX/XLSX button, (3) Add E2E test, (4) Add architecture test (no no-op CalculateAsync), (5) Add encoding lint, (6) Update docs
- **Nghiệp vụ áp dụng:** User-facing output — final wave, user request "xuất ra đúng mẫu"
- **Status:** PENDING — Planning & Approval
- **Branch:** `feature/hkd-fix-wave8-ui-docx-export-regression`
- **Estimated Sessions:** 2-3

---

## 2. ACTIVE WORKFLOW ROUTING
- **Target Workflow:** `newfeaturebuild.md` (IMPLEMENT phase — UI + export + regression prevention)
- **Execution Mode:** IMPLEMENT
- **Current Phase:** Wave 8 of 9 (final)
- **Dependency:** Wave 7 (endpoint có sẵn), UI Platform components available

---

## 3. RELEVANT FILES (CONTEXT BOUNDARY)

### Files được phép đọc/sửa
- `docs/AI/project_state.md` (Bắt buộc đọc đầu phiên)
- `docs/AI/tasks/hkd_book_accounting_fix_master_plan.md` (READ)
- `docs/plan_MVP/HKD_BookAcc/*.docx` (READ — TT 152 layout spec, extracted Wave 0)
- `5_WebApps/ShopERP/Components/Pages/Accounting/HKDBooks.razor` (NEW — list page)
- `5_WebApps/ShopERP/Components/Pages/Accounting/HKDBookDetail.razor` (NEW — detail + export)
- `5_WebApps/ShopERP/Components/Pages/Accounting/AccountingIndex.razor` (UPDATE — add link to HKD books)
- `5_WebApps/ShopERP/Services/` (NEW — `HKDBookExportService.cs` if needed)
- `6_Testing/e2e-tests/hkd-books.spec.ts` (NEW — E2E test)
- `6_Tests/VanAn.Core.Tests/HKDBookTemplateArchitectureTests.cs` (NEW — architecture test)
- `scripts/check-encoding.ps1` (NEW — encoding lint) hoặc update `guard-check.ps1`
- `docs/UI_Platform_Implementation_Guide.md` (UPDATE — document HKD book pattern)
- `docs/AI/project_state.md` (UPDATE — mark stream complete)

### Boundary Rules (Nghiêm cấm)
- KHÔNG tạo custom HTML/CSS — MUST dùng UI Platform components (VanAnCard, VanATable, VanAForm, VanAnButton) — governance Hard Stop
- KHÔNG sửa `1_Shared/Domain/*.cs`
- KHÔNG thêm dependency mới mà không verify package có sẵn (DocX/OpenXML/ClosedXML/EPPlus)
- KHÔNG sửa existing Razor pages (trừ AccountingIndex add link)
- KHÔNG chạy Playwright runtime trong Wave 1-7 — chỉ Wave 8 (UI xong)

---

## 4. TECHNICAL & REGULATORY CONSTRAINTS (HARDENING GATES)
- [ ] **UI Platform Compliance:** MUST dùng VanAnCard, VanATable, VanAForm, VanAnButton — KHÔNG custom HTML/CSS (governance Hard Stop)
- [ ] **TT 152 Layout:** Page phải match mẫu docx: header (HỘ/CÁ NHÂN KD + MST + địa chỉ + "Mẫu số X-HKD (Kèm theo TT 152/2025/TT-BTC)"), bảng (chứng từ + diễn giải + số tiền), footer (tổng thuế + chữ ký NGƯỜI ĐẠI DIỆN HKD)
- [ ] **Responsive:** Mobile-first design (Mobile ≤640px, Tablet 641-1024px, Desktop ≥1025px)
- [ ] **Multi-tenancy:** Page phải filter theo TenantId (Blazor Server auth context)
- [ ] **Export Library:** Verify package có sẵn trước khi dùng (grep packages.config / .csproj)
- [ ] **Architecture Test:** Verify không có `HKDBookTemplate` subclass với `CalculateAsync` body chỉ `await Task.CompletedTask` (regression cho Issue 1)
- [ ] **Encoding Lint:** Grep mojibake pattern (`Ã|Â|á»|áº|Ä`) trong `.cs` files — fail CI nếu có
- [ ] **Build Check:** `dotnet build VanAn.sln` Release pass
- [ ] **E2E Parse Check:** `npx playwright test --list` pass

---

## 5. SUCCESS CRITERIA (ĐO LƯỜNG ĐƯỢC)
- [ ] **SC1:** Page `/accounting/hkd-books` list available templates theo HKDGroup (dùng VanAnCard + VanATable)
- [ ] **SC2:** Page `/accounting/hkd-books/{templateCode}` render book với TT 152 layout (header + bảng + footer + chữ ký)
- [ ] **SC3:** "Export DOCX" button generate .docx đúng layout TT 152
- [ ] **SC4:** "Export XLSX" button generate .xlsx đúng layout
- [ ] **SC5:** E2E test `hkd-books.spec.ts` pass (parse + runtime nếu services chạy)
- [ ] **SC6:** Architecture test pass — 0 no-op `CalculateAsync` subclass
- [ ] **SC7:** Encoding lint pass — 0 mojibake trong `.cs` files
- [ ] **SC8:** `AccountingIndex.razor` có link tới `/accounting/hkd-books`
- [ ] **SC9:** `docs/UI_Platform_Implementation_Guide.md` updated với HKD book pattern
- [ ] **SC10:** `project_state.md` updated — mark HKD Book Fix stream complete
- [ ] **SC11:** `dotnet build VanAn.sln` Release — 0 errors
- [ ] **SC12:** `dotnet test` — all pass
- [ ] **SC13:** `npx playwright test --list` pass
- [ ] **SC14:** guard-check.ps1 PASSED

---

## 6. ACTIVE SKILLS (MAX 3)
- `accounting-ui-implementation` — HKD book UI pattern
- `ui-platform-compliance-review` — Verify UI Platform components
- `playwright_cost_optimizer` — E2E test cost control

---

## 7. AI HEALTH CHECK MATRIX (INITIAL)
- **Evidence Count:** 6
- **Verified Facts:**
  - Fact 1: `Components/Pages/Accounting/` có 6 pages (AccountBalance, AccountingIndex, ExpenseEntry, PeriodClosing, RevenueEntry, TransactionHistory) — không có HKD book page
  - Fact 2: UI Platform components có VanAnCard, VanATable, VanAForm, VanAnButton (governance)
  - Fact 3: 7 mẫu docx TT 152 layout extracted Wave 0 (header + bảng + footer + chữ ký)
  - Fact 4: Endpoint `GET /api/hkd-books/{templateCode}` có sẵn sau Wave 7
  - Fact 5: `AccountingIndex.razor` là entry point cho Accounting section — add link here
  - Fact 6: E2E test pattern — `6_Testing/e2e-tests/` có 21 spec files (reference)
- **Assumptions:**
  - DocX hoặc OpenXML SDK package có sẵn (verify .csproj)
  - ClosedXML hoặc EPPlus package có sẵn (verify)
  - Blazor Server auth context có TenantId (verify)
- **Open Questions:**
  - Q1: Export library nào có sẵn? (Grep .csproj — DocX/OpenXML/ClosedXML/EPPlus)
  - Q2: TenantId extraction trong Blazor Server — pattern gì? (Verify existing pages)
  - Q3: Mobile layout cho bảng chứng từ — scroll horizontal hay collapse? (Decide per TT 152 layout)
- **Recommended Action:** PROCEED — risk medium, UI + export + regression prevention

---

## 8. REVERSE IMPACT ANALYSIS
| File thay đổi | Reverse impact | Mitigation |
|---|---|---|
| `HKDBooks.razor` (new) | None — new page | N/A |
| `HKDBookDetail.razor` (new) | None — new page | N/A |
| `AccountingIndex.razor` (update — add link) | Add 1 link — không break | Verify navigation |
| `HKDBookExportService.cs` (new, if needed) | None — new service | N/A |
| `hkd-books.spec.ts` (new E2E) | None — new test | N/A |
| Architecture test (new) | None — new test | N/A |
| `check-encoding.ps1` (new lint) | CI fail nếu mojibake | Fix mojibake trước merge |
| `project_state.md` (update) | Mark stream complete | N/A |

---

## 9. TDD & TESTING STRATEGY
- **Unit tests:** N/A (Wave 6 đã cover)
- **Integration tests:** N/A (Wave 7 đã cover)
- **Architecture tests:** 1 new (no no-op CalculateAsync)
- **E2E tests:** 1 new (`hkd-books.spec.ts`)
- **Verification:** `dotnet build` + `dotnet test` + `npx playwright test --list` pass

### E2E test spec: `hkd-books.spec.ts`
- Test 1: Navigate `/accounting/hkd-books` — verify list templates (S1a hoặc S2a-S2e tùy HKDGroup)
- Test 2: Click template — verify navigation to `/accounting/hkd-books/{templateCode}`
- Test 3: Verify page render có header (HỘ/CÁ NHÂN KD + MST), bảng (chứng từ + diễn giải + số tiền), footer (tổng thuế + chữ ký)
- Test 4: Verify "Export DOCX" button visible + click triggers download
- Test 5: Verify "Export XLSX" button visible + click triggers download

### Architecture test spec: `HKDBookTemplateArchitectureTests`
- Scan all `HKDBookTemplate` subclasses (reflection)
- For each, verify `CalculateAsync(GenericHKDBook)` method body is NOT just `await Task.CompletedTask`
- Fail if any subclass has no-op CalculateAsync (regression cho Issue 1)

---

## 10. JIT PLANNING + PURE EXECUTION (MICRO-PHASES)

### Chiến lược thực thi: UI → Export → E2E → Architecture → Lint → Docs
1. Verify export library available (grep .csproj)
2. Create `HKDBooks.razor` (list page)
3. Create `HKDBookDetail.razor` (detail + export buttons)
4. Create `HKDBookExportService` (DOCX + XLSX generation)
5. Update `AccountingIndex.razor` (add link)
6. Add E2E test `hkd-books.spec.ts`
7. Add architecture test
8. Add encoding lint script
9. Update docs
10. Build + test + parse check

### Micro-phase breakdown

| Session | JIT Planning (chốt gì) | Pure Execution (viết gì) |
|---|---|---|
| **S1** | - Đọc 7 mẫu docx layout (Wave 0 extracted)<br>- Grep export library (.csproj)<br>- Đọc UI Platform components API<br>- Chốt: layout per template (header + bảng + footer)<br>- Chốt: export library (DocX/ClosedXML) | - Create `HKDBooks.razor` (list)<br>- Create `HKDBookDetail.razor` (detail + export buttons)<br>- Create `HKDBookExportService`<br>- Update `AccountingIndex.razor`<br>- Run `dotnet build`<br>- Commit |
| **S2** | - Đọc E2E test pattern (reference spec)<br>- Chốt: E2E test scenarios (5 test)<br>- Chốt: architecture test approach (reflection) | - Add `hkd-books.spec.ts` (E2E)<br>- Add `HKDBookTemplateArchitectureTests.cs`<br>- Add `check-encoding.ps1`<br>- Run `dotnet build` + `dotnet test` + `npx playwright test --list`<br>- Update `docs/UI_Platform_Implementation_Guide.md`<br>- Update `project_state.md`<br>- Commit |

### Rules
- 1 page tại 1 thời điểm — build verify trước khi sang page tiếp
- MUST dùng UI Platform components — KHÔNG custom HTML/CSS
- Mobile-first — test responsive layout
- Export file phải match TT 152 layout (header + bảng + footer + chữ ký)
- Architecture test dùng reflection — scan all HKDBookTemplate subclasses

---

## 11. ESTIMATED EFFORT
- 2-3 sessions (2 Razor pages + export service + E2E + architecture test + lint + docs)
- **BLOCKER:** Wave 7 phải merged (endpoint có sẵn)
- **VALUE:** Final wave — user request "xuất ra đúng mẫu" + regression prevention đảm bảo bug không tái xuất

---

## 12. Export Library Status (from Wave 0 T9 — propagated 2026-07-03, UPDATED with user approval)

- DocX: **NOT FOUND**
- DocumentFormat.OpenXml: **NOT FOUND** → **USER APPROVED adding this dependency (2026-07-03)**
- ClosedXML: **NOT FOUND**
- EPPlus: **FOUND** — Version 7.6.1 (`Directory.Packages.props` L42: `<PackageVersion Include="EPPlus" Version="7.6.1" />`)
- **DECISION: Use EPPlus for XLSX + DocumentFormat.OpenXml for DOCX (BOTH approved)**
  - EPPlus 7.6.1 handles `.xlsx` (Excel) — already in deps, no approval needed
  - `DocumentFormat.OpenXml` for `.docx` (Word) — **user approved adding new dependency** (2026-07-03)
  - Wave 8 implements BOTH XLSX + DOCX export
- **Wave 8 action items:**
  - Add `DocumentFormat.OpenXml` to `Directory.Packages.props` (PackageVersion) + to `5_WebApps/ShopERP/*.csproj` (PackageReference)
  - SC3 ("Export DOCX") → **PROCEED** with `DocumentFormat.OpenXml`
  - SC4 ("Export XLSX") → **PROCEED** with EPPlus 7.6.1
  - E2E test 4: test BOTH DOCX + XLSX export buttons
  - `HKDBookExportService` → implement XLSX via EPPlus + DOCX via DocumentFormat.OpenXml
