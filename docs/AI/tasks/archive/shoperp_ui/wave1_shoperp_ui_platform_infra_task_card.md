# TASK CARD: ShopERP UI Fix - Wave 1 - UI.Platform Infrastructure (Pattern P)

## 1. GOAL & CONTEXT
- **Mục tiêu cốt lõi:** Fix VanALayout (slot structure + CSS) + VanANavigation (icon rendering + CSS) — root cause cho 13/23 files unstyled/broken layout
- **Nghiệp vụ áp dụng:** UI Platform infrastructure — foundation cho tất cả feature pages
- **Status:** PENDING — Planning & Approval
- **Branch:** `feature/shoperp-ui-fix-wave1-platform-infra`
- **Estimated Sessions:** 1

---

## 2. ACTIVE WORKFLOW ROUTING
- **Target Workflow:** `newfeaturebuild.md`
- **Execution Mode:** IMPLEMENT
- **Current Phase:** Wave 1 of 6
- **Dependency:** Wave 0 (pre-flight verification)

---

## 3. RELEVANT FILES (CONTEXT BOUNDARY)

### Files được phép đọc/sửa
- `docs/AI/project_state.md` (Bắt buộc đọc đầu phiên)
- `docs/AI/tasks/shoperp_ui_fix_master_plan.md` (READ)
- `UI.Platform/Components/VanALayout.razor` (UPDATE — verify slot structure)
- `UI.Platform/Components/VanALayout.razor.css` (NEW — CSS isolation)
- `UI.Platform/Components/VanANavigation.razor` (UPDATE — fix icon rendering)
- `UI.Platform/Components/VanANavigation.razor.css` (NEW — CSS isolation)
- `5_WebApps/ShopERP/Components/Pages/Accounting/AccountingLayout.razor` (UPDATE — fix slot usage)
- `5_WebApps/ShopERP/Components/Pages/EInvoice/EInvoiceLayout.razor` (UPDATE — fix slot usage)
- `5_WebApps/ShopERP/Components/VanADashboard.razor` (UPDATE — fix slot usage)

### Boundary Rules (Nghiêm cấm)
- KHÔNG sửa code C# business logic
- KHÔNG sửa Domain layer
- KHÔNG tạo component mới — chỉ fix existing
- KHÔNG thêm dependency — dùng Bootstrap Icons (đã có trong `App.razor` bootstrap)
- KHÔNG sửa `MainLayout.razor` — root layout giữ nguyên

---

## 4. TECHNICAL & REGULATORY CONSTRAINTS (HARDENING GATES)
- [ ] **Slot Structure:** VanALayout có 3 RenderFragments: `Sidebar`, `Header`, `ChildContent` — MUST use đúng
- [ ] **CSS Isolation:** Dùng `.razor.css` (Blazor CSS isolation) — KHÔNG inline `<style>`
- [ ] **Bootstrap Icons:** Dùng `<i class="bi bi-@item.Icon"></i>` — KHÔNG render text thuần
- [ ] **Responsive:** Sidebar 250px desktop, collapse mobile (≤640px)
- [ ] **No Nested `<main>`:** Bỏ `<main>` trong layout files, chỉ giữ trong VanALayout
- [ ] **Build Check:** `dotnet build VanAn.sln` 0 errors

---

## 5. SUCCESS CRITERIA (ĐO LƯỜNG ĐƯỢC)
- [ ] **SC1:** `VanALayout.razor.css` tạo mới với: `.vanan-layout` (flex row), `.vanan-layout__sidebar` (250px, sticky), `.vanan-layout__main` (flex 1), `.vanan-layout__content` (padding), responsive ≤640px
- [ ] **SC2:** `VanANavigation.razor.css` tạo mới với: `.vanan-navigation__list` (flex column), `.vanan-navigation__item`, `.vanan-navigation__link` (flex, hover), `.vanan-navigation__icon`, `.vanan-navigation__item--active`
- [ ] **SC3:** VanANavigation render `<i class="bi bi-@item.Icon" aria-hidden="true"></i>` thay vì `<span>@item.Icon</span>`
- [ ] **SC4:** AccountingLayout dùng `<Sidebar><VanANavigation ... /></Sidebar>` + `<ChildContent>@Body</ChildContent>`
- [ ] **SC5:** EInvoiceLayout — same pattern as SC4
- [ ] **SC6:** VanADashboard — same pattern as SC4
- [ ] **SC7:** 0 nested `<main>` trong 3 layout files (bỏ `<main class="...">` wrapper)
- [ ] **SC8:** `dotnet build VanAn.sln` 0 errors

---

## 6. ACTIVE SKILLS (MAX 3)
- `ui-platform-compliance-review` — Ensure VanALayout/VanANavigation compliant
- `build-error-analysis` — Fix any breakage

---

## 7. AI HEALTH CHECK MATRIX (INITIAL)
- **Evidence Count:** 5
- **Verified Facts:**
  - Fact 1: VanALayout.razor L46-56 có 3 RenderFragments: `ChildContent`, `Sidebar`, `Header`, `Footer`
  - Fact 2: VanALayout.razor L5-16 render `vanan-layout__sidebar` div, L18-42 render `vanan-layout__main` div
  - Fact 3: VanANavigation.razor L13-16 render `<span class="vanan-navigation__icon">@item.Icon</span>` — text thuần
  - Fact 4: AccountingLayout.razor L6-11 đặt VanANavigation + `<main>@Body</main>` đều vào ChildContent (không wrap `<Sidebar>`)
  - Fact 5: UI.Platform project không có `.razor.css` file nào (grep confirmed)
- **Assumptions:**
  - Bootstrap Icons đã có trong project (bootstrap.min.css trong wwwroot)
  - `bi bi-dashboard`, `bi bi-plus-circle` etc. là valid Bootstrap Icon classes
- **Open Questions:**
  - Q1: VanALayout có `Header` RenderFragment — có nên dùng cho page header không? (Recommend: KHÔNG — để page tự render header, Header slot cho global header)
  - Q2: VanADashboard có cần fix không nếu nó là component (không phải layout)? (Recommend: CÓ — cùng pattern sai)
- **Recommended Action:** PROCEED — fix infrastructure

---

## 8. REVERSE IMPACT ANALYSIS
| File thay đổi | Reverse impact | Mitigation |
|---|---|---|
| `VanALayout.razor` | Có thể break KhachLink nếu cũng dùng | Verify grep — KhachLink có dùng VanALayout không |
| `VanALayout.razor.css` (NEW) | CSS isolation — chỉ affect VanALayout | Safe |
| `VanANavigation.razor` | Icon rendering change — affect tất cả apps dùng | Verify API compat |
| `VanANavigation.razor.css` (NEW) | CSS isolation — chỉ affect VanANavigation | Safe |
| `AccountingLayout.razor` | Layout structure change — affect 7 Accounting pages | Build verify |
| `EInvoiceLayout.razor` | Same — affect 6 EInvoice pages | Build verify |
| `VanADashboard.razor` | Component structure change | Build verify |

---

## 9. TDD & TESTING STRATEGY
- **Build check:** `dotnet build VanAn.sln` sau mỗi file fix
- **Visual check:** Skip (Wave 6)
- **Verification:** Build pass + grep verify slot structure

---

## 10. JIT PLANNING + PURE EXECUTION (MICRO-PHASES)

### Chiến lược: Infrastructure-First
1. Fix VanALayout + VanANavigation (UI.Platform) trước
2. Tạo CSS isolation files
3. Fix 3 layout files dùng slot đúng
4. Build verify

### Micro-phase breakdown cho Wave 1

| Session | JIT Planning (chốt gì) | Pure Execution (viết gì) |
|---|---|---|
| **S1** | - Verify KhachLink có dùng VanALayout không (grep)<br>- Chốt CSS specs (sidebar 250px, responsive breakpoints)<br>- Chốt icon class format (`bi bi-@icon`)<br>- Chốt slot usage pattern | - Create `VanALayout.razor.css`<br>- Create `VanANavigation.razor.css`<br>- Fix VanANavigation icon rendering<br>- Fix AccountingLayout slot<br>- Fix EInvoiceLayout slot<br>- Fix VanADashboard slot<br>- Run `dotnet build VanAn.sln`<br>- Commit |

### Rules
- Fix UI.Platform components trước, layout files sau
- Verify build sau mỗi file
- KHÔNG thay đổi RenderFragment parameter names (backward compat)

---

## 11. ESTIMATED EFFORT
- 1 session (2 CSS files + 5 Razor fixes)
- **BLOCKER:** Verify KhachLink dependency trước khi sửa UI.Platform
