# MASTER IMPLEMENTATION PLAN — ShopERP UI Fix (Pattern-Based Batch)

> **Status:** IN PROGRESS — Wave 0+1+2 COMPLETE
> **Created:** 2026-07-02
> **Last Updated:** 2026-07-03 (Wave 2 complete)
> **Target Workflow:** `newfeaturebuild.md` (ANALYZE → IMPLEMENT)
> **Branch strategy:** `main` → feature branches per wave
> **Execution principle:** Pattern-Based Batch Fix — phân loại theo pattern, KHÔNG fix case-by-case
> **Prerequisite:** UI review report (verify 23 .razor files in Components/Pages, 2026-07-02)

---

## 0. EXECUTION RULES

### Pattern-Based Batch Fix Strategy
**Nguyên tắc cốt lõi:** Phân loại 13 issues thành **6 patterns**. Mỗi wave = 1 pattern, áp dụng cùng 1 thao tác cơ học cho nhiều files. KHÔNG fix từng file riêng lẻ.

**Bước 1: INVESTIGATE & ANALYZE (Đã xong — Review v1)**
- Đã đọc 23 .razor files trong `Components/Pages/`
- Đã verify CSS missing bằng grep (zero match cho tất cả custom classes)
- Đã verify `@rendermode` missing bằng grep (14/23 files)
- Đã verify component version drift (VanAnX vs VanX)
- Đã phân loại thành 6 patterns

**Bước 2: IMPLEMENT (Execution Phase)**
- Mỗi wave fix 1 pattern cơ học trên nhiều files
- KHÔNG thay đổi approach khi đang implement
- Mỗi wave xong, chạy `dotnet build VanAn.sln` (verify không break)
- Sau wave cuối: chạy app + verify visual

### Session protocol
1. **Mỗi session chỉ làm 1 wave**
2. **Bắt đầu mỗi session:** Đọc `project_state.md` + task card wave đang làm
3. **Sau khi plan chốt:** Execution Phase
4. **Trước khi session end:** Build pass + commit
5. **Sau mỗi wave:** Commit với message format `[UI-FIX WAVE X] Pattern <name>`

### Branch protocol
```
main
  └── feature/shoperp-ui-fix-wave1-platform-infra
      └── feature/shoperp-ui-fix-wave2-rendermode
          └── feature/shoperp-ui-fix-wave3-page-css
              └── feature/shoperp-ui-fix-wave4-component-consolidation
                  └── feature/shoperp-ui-fix-wave5-admin-layout
                      └── feature/shoperp-ui-fix-wave6-governance-cleanup
```
- Mỗi wave có branch riêng
- Merge wave vào branch trước đó
- Final merge vào `main` khi tất cả waves complete

### Hard rules
- **KHÔNG sửa code C# business logic** — toàn bộ thay đổi chỉ trong Razor markup + CSS
- **KHÔNG sửa Domain layer** — không liên quan
- **KHÔNG tạo component mới** — chỉ fix/sửa existing components
- **KHÔNG thêm dependency mới** — dùng Bootstrap + CSS isolation có sẵn
- **Pattern-based:** Mỗi wave áp dụng 1 thao tác đồng nhất cho tất cả files match pattern
- **Build pass sau mỗi wave:** `dotnet build VanAn.sln` 0 errors
- **Playwright DISABLED** cho đến khi build pass + implementation complete (Wave 6 xong)

### Critical context
- **23 .razor files** trong `Components/Pages/` (5 root + 7 Accounting + 4 Admin + 7 EInvoice)
- **3 layouts:** AccountingLayout, EInvoiceLayout, VanADashboard — tất cả dùng VanALayout sai
- **14/23 files** thiếu `@rendermode InteractiveServer` → interactive handlers dead
- **18/23 files** không có CSS cho custom classes → plain HTML render
- **UI.Platform project** không có `.razor.css` nào → VanALayout/VanANavigation unstyled
- **2 component versions:** VanAnX (cũ) vs VanX (mới) — cần consolidate
- **Admin folder** 4 files không có `@layout` — dùng MainLayout default, inconsistent

---

## 0.5. WAVE 0 — PARALLEL: Pre-flight Verification (Non-code)

> **Verify nhanh trước khi bắt đầu — đảm bảo môi trường sạch**

### Tasks
| # | Task | Owner | Status |
|---|---|---|---|
| 1 | Confirm `dotnet build VanAn.sln` pass trước khi bắt đầu | AI | ✅ DONE |
| 2 | Snapshot `git status` sạch | AI | ✅ DONE |
| 3 | Confirm UI.Platform project structure (Components folder, no .razor.css) | AI | ✅ DONE (0 .razor.css confirmed) |
| 4 | Confirm `Routes.razor` DefaultLayout = MainLayout | AI | ✅ DONE |

### Tracking
- Update `project_state.md` Maintenance Log khi verify xong

---

## 1. CURRENT ISSUES SUMMARY (CLASSIFIED BY PATTERN)

### Pattern P: UI.Platform Infrastructure (Root Cause)
**Status:** ❌ BROKEN — VanALayout + VanANavigation chưa hoàn thiện
**Priority:** 1 (Critical — root cause cho 13/23 files)
**Count:** 3 layout files + 2 component files

**Issues:**
- VanALayout: ZERO CSS (no `.razor.css`) — `vanan-layout`, `vanan-layout__sidebar`, `vanan-layout__main`, `vanan-layout__content` unstyled
- VanALayout: Sai slot structure — navigation vào ChildContent thay vì Sidebar
- VanANavigation: ZERO CSS — `vanan-navigation`, `vanan-navigation__list`, `vanan-navigation__item`, `vanan-navigation__link` unstyled
- VanANavigation: Icons render as text — `"dashboard"` hiển thị thành chữ thay vì Bootstrap Icon
- Nested `<main>` — 3 cấp lồng nhau (MainLayout → VanALayout → page)

**Files liên quan:**
- `UI.Platform/Components/VanALayout.razor` (FIX slot + add `.razor.css`)
- `UI.Platform/Components/VanALayout.razor.css` (NEW)
- `UI.Platform/Components/VanANavigation.razor` (FIX icon rendering)
- `UI.Platform/Components/VanANavigation.razor.css` (NEW)
- `5_WebApps/ShopERP/Components/Pages/Accounting/AccountingLayout.razor` (FIX slot usage)
- `5_WebApps/ShopERP/Components/Pages/EInvoice/EInvoiceLayout.razor` (FIX slot usage)
- `5_WebApps/ShopERP/Components/VanADashboard.razor` (FIX slot usage)

### Pattern R: Missing `@rendermode InteractiveServer`
**Status:** ❌ BROKEN — 14 files có interactive handlers nhưng SSR mode
**Priority:** 2 (Critical — buttons/forms dead)
**Count:** 14 files

**Files liên quan (14):**
- Root: `AccessDenied.razor`, `Sitemap.razor`
- Accounting: `AccountingIndex.razor`, `TransactionHistory.razor`
- EInvoice: `EInvoiceDashboard.razor`, `ProviderManagement.razor`, `ProviderConfiguration.razor`, `HealthMonitoring.razor`, `AlertManagement.razor`, `InvoiceManagement.razor`
- Admin: `AuditTrail.razor`, `UserManagement.razor`, `PermissionGroupManagement.razor`, `TenantManagement.razor`

### Pattern C: Page CSS Isolation Missing
**Status:** ❌ UNSTYLED — 18 files dùng custom classes không có CSS
**Priority:** 3 (High — plain HTML render)
**Count:** 18 files (tất cả trừ Home, Counter, Error)

**Shared classes cần CSS:** `page-header`, `header-actions`, `metrics-grid`, `metrics-section`, `filter-grid`, `filter-group`, `filter-section`, `filter-actions`, `loading-state`, `empty-state`, `status-badge`, `action-badge`, `pagination`, `page-info`, `form-group`, `form-actions`, `vanan-input`, `vanan-select`, `vanan-table`

**Page-specific classes:** `accounting-dashboard`, `einvoice-dashboard`, `provider-grid`, `activity-list`, `audit-trail-page`, `user-management-page`, `tenant-management-page`, v.v.

**Files liên quan:** 18 files cần `.razor.css` (hoặc shared CSS)

### Pattern V: Component Version Drift (VanAnX → VanX)
**Status:** ❌ INCONSISTENT — 2 version cùng tồn tại
**Priority:** 4 (Medium — functional nhưng inconsistent)
**Count:** 11 occurrences trong 6 EInvoice files

**Drift:**
- `VanAnAlert` (cũ, 2640 bytes) vs `VanAAlert` (mới, 2179 bytes) — 10 occurrences
- `VanAnModal` (cũ, 5013 bytes) vs `VanAModal` (mới, 2850 bytes) — 1 occurrence

**Files liên quan:** 6 EInvoice files + InvoiceManagement.razor

### Pattern L: Admin Layout Inconsistency
**Status:** ❌ INCONSISTENT — Admin dùng MainLayout, Accounting/EInvoice dùng VanALayout
**Priority:** 5 (Medium — functional nhưng 2 pattern layout khác nhau)
**Count:** 4 Admin files

**Files liên quan:**
- `Admin/AuditTrail.razor` — không có `@layout`
- `Admin/UserManagement.razor` — không có `@layout`
- `Admin/PermissionGroupManagement.razor` — không có `@layout`
- `Admin/TenantManagement.razor` — không có `@layout`

### Pattern G: Governance Cleanup
**Status:** ❌ VIOLATIONS — inline style, demo leftover, eval logout
**Priority:** 6 (Low — không break functionality)
**Count:** 4 files

**Issues:**
- `AccessDenied.razor` — inline `<style>` 37 dòng (governance bypass)
- `Sitemap.razor` — inline `<style>` 72 dòng + `eval` logout (security concern) + emoji broken
- `Counter.razor` — Blazor template demo chưa xóa
- `Home.razor` — naked redirect, không có PageTitle/loading

---

## 2. WAVE 1 — UI.Platform Infrastructure (Pattern P)

**Branch:** `feature/shoperp-ui-fix-wave1-platform-infra`
**Estimated sessions:** 1
**Conflict risk:** MEDIUM (sửa UI.Platform — ảnh hưởng tất cả apps dùng nó)
**Priority:** 1
**Task Card:** `docs/AI/tasks/wave1_shoperp_ui_platform_infra_task_card.md`

### Tasks
| # | Task ID | Task | Files | Status |
|---|---|---|---|---|
| 1 | W1-T1 | Fix VanALayout slot structure — thêm `Sidebar`/`ChildContent` RenderFragment usage rõ ràng | `VanALayout.razor` | ✅ N/A (already correct) |
| 2 | W1-T2 | Create `VanALayout.razor.css` — CSS cho `vanan-layout`, sidebar (250px), main, content, responsive | `VanALayout.razor.css` (NEW) | ✅ DONE |
| 3 | W1-T3 | Fix VanANavigation icon rendering — đổi từ text sang `<i class="bi bi-@item.Icon">` | `VanANavigation.razor` | ✅ DONE |
| 4 | W1-T4 | Create `VanANavigation.razor.css` — CSS cho nav list, items, links, active state, submenu | `VanANavigation.razor.css` (NEW) | ✅ DONE |
| 5 | W1-T5 | Fix AccountingLayout — wrap VanANavigation trong `<Sidebar>` + bỏ nested `<main>` | `AccountingLayout.razor` | ✅ DONE |
| 6 | W1-T6 | Fix EInvoiceLayout — same pattern | `EInvoiceLayout.razor` | ✅ DONE |
| 7 | W1-T7 | Fix VanADashboard — same pattern | `VanADashboard.razor` | ✅ DONE (+ emoji→BI icons) |
| 8 | W1-T8 | Verify `dotnet build VanAn.sln` pass | Solution-wide | ✅ DONE (0 errors) |

### Entry criteria
- [x] Wave 0 complete
- [x] Git status clean
- [x] `dotnet build VanAn.sln` pass trước khi sửa

### Exit criteria
- [x] VanALayout có `.razor.css` với sidebar 250px, main flex, responsive
- [x] VanANavigation có `.razor.css` với list styling, active state
- [x] VanANavigation render `<i class="bi bi-@icon">` thay vì text
- [x] 3 layout files dùng `<Sidebar>` slot đúng
- [x] 0 nested `<main>` (bỏ `<main>` trong layout files, chỉ giữ trong VanALayout)
- [x] `dotnet build VanAn.sln` 0 errors

### Additional changes
- Bootstrap Icons CDN added to `App.razor` (was missing entirely)
- VanADashboard: emoji icons → Bootstrap Icon names (speedometer2, clipboard-data, calculator, graph-up, gear, people)

### Commit: `3b893e8` on `feature/shoperp-ui-fix-wave1-platform-infra`

### Why first
- Root cause cho 13/23 files — fix infrastructure trước thì các wave sau có base đúng
- Risk medium — sửa UI.Platform ảnh hưởng KhachLink nếu cũng dùng, cần verify

---

## 3. WAVE 2 — Add `@rendermode InteractiveServer` (Pattern R)

**Branch:** `feature/shoperp-ui-fix-wave2-rendermode`
**Estimated sessions:** 1
**Conflict risk:** LOW
**Priority:** 2
**Task Card:** `docs/AI/tasks/wave2_shoperp_rendermode_task_card.md`

### Tasks
| # | Task ID | Task | Files | Status |
|---|---|---|---|---|
| 1 | W2-T1 | Add `@rendermode InteractiveServer` to 14 files (batch operation) | 14 files (list trong task card) | ✅ DONE (`79ec512`) |
| 2 | W2-T2 | Verify `dotnet build VanAn.sln` pass | Solution-wide | ✅ DONE (0 errors) |

### Entry criteria
- [x] Wave 1 merged
- [x] Build pass

### Exit criteria
- [x] 14 files có `@rendermode InteractiveServer` ở line 2 (sau `@page`)
- [x] 0 interactive handler nào ở SSR mode
- [x] `dotnet build VanAn.sln` 0 errors

### Why second
- Thao tác cơ học nhất — chỉ thêm 1 line per file
- Risk thấp nhất — không thay đổi logic, chỉ enable interactivity
- Fix ngay 14 dead pages

---

## 4. WAVE 3 — Page CSS Isolation (Pattern C)

**Branch:** `feature/shoperp-ui-fix-wave3-page-css`
**Estimated sessions:** 1-2
**Conflict risk:** LOW
**Priority:** 3
**Task Card:** `docs/AI/tasks/wave3_shoperp_page_css_task_card.md`

### Tasks
| # | Task ID | Task | Files | Status |
|---|---|---|---|---|
| 1 | W3-T1 | Create shared CSS file `Components/Pages/_PagesShared.css` (hoặc `wwwroot/css/pages.css`) — define shared classes: `page-header`, `header-actions`, `metrics-grid`, `metrics-section`, `filter-grid`, `filter-group`, `filter-actions`, `loading-state`, `empty-state`, `form-group`, `form-actions`, `vanan-input`, `vanan-select`, `vanan-table`, `status-badge`, `action-badge`, `pagination`, `page-info` | Shared CSS (NEW) | PENDING |
| 2 | W3-T2 | Link shared CSS in `App.razor` `<head>` | `App.razor` | PENDING |
| 3 | W3-T3 | Create `.razor.css` per page cho page-specific classes (18 files) — chỉ classes riêng, shared classes ở file chung | 18 `.razor.css` files (NEW) | PENDING |
| 4 | W3-T4 | Verify `dotnet build VanAn.sln` pass | Solution-wide | PENDING |

### Entry criteria
- [ ] Wave 2 merged
- [ ] Build pass

### Exit criteria
- [ ] 1 shared CSS file với tất cả common classes
- [ ] 18 `.razor.css` files cho page-specific classes
- [ ] `metrics-grid` render thành CSS Grid (auto-fill, minmax 280px)
- [ ] `page-header` render flex (space-between)
- [ ] `filter-grid` render CSS Grid
- [ ] `vanan-table` có styling (border, padding, hover)
- [ ] `dotnet build VanAn.sln` 0 errors

### Why third
- Sau khi infrastructure (Wave 1) + interactivity (Wave 2) xong, CSS là layer cuối
- Risk thấp — chỉ thêm CSS, không sửa markup
- 18 files nhưng dùng shared CSS + per-page CSS → ít code trùng

---

## 5. WAVE 4 — Component Version Consolidation (Pattern V)

**Branch:** `feature/shoperp-ui-fix-wave4-component-consolidation`
**Estimated sessions:** 1
**Conflict risk:** LOW
**Priority:** 4
**Task Card:** `docs/AI/tasks/wave4_shoperp_component_consolidation_task_card.md`

### Tasks
| # | Task ID | Task | Files | Status |
|---|---|---|---|---|
| 1 | W4-T1 | Replace all `VanAnAlert` → `VanAAlert` trong 6 EInvoice files (10 occurrences) | 6 EInvoice files | PENDING |
| 2 | W4-T2 | Replace `VanAnModal` → `VanAModal` trong InvoiceManagement.razor (1 occurrence) — verify API compat (IsVisible, Body, Footer) | `InvoiceManagement.razor` | PENDING |
| 3 | W4-T3 | Verify `VanAnAlert`/`VanAnModal` (cũ) không còn reference nào — nếu 0 reference, note để xóa trong debt cleanup | UI.Platform | PENDING |
| 4 | W4-T4 | Verify `dotnet build VanAn.sln` pass | Solution-wide | PENDING |

### Entry criteria
- [ ] Wave 3 merged
- [ ] Build pass

### Exit criteria
- [ ] 0 `VanAnAlert` reference trong EInvoice files
- [ ] 0 `VanAnModal` reference trong InvoiceManagement
- [ ] `VanAAlert` API compatible (Type, Message, Dismissible, OnDismiss, data-testid)
- [ ] `VanAModal` API compatible (Title, IsVisible, OnClose, Body, Footer)
- [ ] `dotnet build VanAn.sln` 0 errors

### Why fourth
- Sau khi CSS + rendermode xong, consolidate component versions
- Risk thấp — `replace_all` cơ học, verify API compat

---

## 6. WAVE 5 — Admin Layout Consistency (Pattern L)

**Branch:** `feature/shoperp-ui-fix-wave5-admin-layout`
**Estimated sessions:** 1
**Conflict risk:** LOW
**Priority:** 5
**Task Card:** `docs/AI/tasks/wave5_shoperp_admin_layout_task_card.md`

### Tasks
| # | Task ID | Task | Files | Status |
|---|---|---|---|---|
| 1 | W5-T1 | Create `AdminLayout.razor` — same pattern as AccountingLayout/EInvoiceLayout (VanALayout + VanANavigation với Admin menu items) | `Admin/AdminLayout.razor` (NEW) | PENDING |
| 2 | W5-T2 | Add `@layout AdminLayout` to 4 Admin files | 4 Admin files | PENDING |
| 3 | W5-T3 | Define Admin menu items (Users, Permission Groups, Audit Trail, Tenants) | `AdminLayout.razor` | PENDING |
| 4 | W5-T4 | Verify `dotnet build VanAn.sln` pass | Solution-wide | PENDING |

### Entry criteria
- [ ] Wave 4 merged
- [ ] Build pass
- [ ] VanALayout slot structure đã fix (Wave 1)

### Exit criteria
- [ ] `AdminLayout.razor` tạo mới, dùng VanALayout + VanANavigation đúng slot
- [ ] 4 Admin files có `@layout AdminLayout`
- [ ] Admin menu items: Users, Permission Groups, Audit Trail, Tenants
- [ ] 0 Admin file dùng MainLayout default
- [ ] `dotnet build VanAn.sln` 0 errors

### Why fifth
- Sau khi VanALayout fix (Wave 1), Admin có thể dùng cùng pattern
- Consistency: tất cả feature folders (Accounting, EInvoice, Admin) dùng cùng layout pattern

---

## 7. WAVE 6 — Governance Cleanup (Pattern G)

**Branch:** `feature/shoperp-ui-fix-wave6-governance-cleanup`
**Estimated sessions:** 1
**Conflict risk:** LOW
**Priority:** 6
**Task Card:** `docs/AI/tasks/wave6_shoperp_governance_cleanup_task_card.md`

### Tasks
| # | Task ID | Task | Files | Status |
|---|---|---|---|---|
| 1 | W6-T1 | Move inline `<style>` từ `AccessDenied.razor` sang `AccessDenied.razor.css` | `AccessDenied.razor` + `.razor.css` (NEW) | PENDING |
| 2 | W6-T2 | Move inline `<style>` từ `Sitemap.razor` sang `Sitemap.razor.css` | `Sitemap.razor` + `.razor.css` (NEW) | PENDING |
| 3 | W6-T3 | Fix Sitemap logout — thay `eval` bằng server-side logout endpoint hoặc `NavigationManager.NavigateTo("/Logout")` | `Sitemap.razor` | PENDING |
| 4 | W6-T4 | Fix Sitemap emoji broken — verify encoding, replace `` với emoji đúng | `Sitemap.razor` | PENDING |
| 5 | W6-T5 | Delete `Counter.razor` — Blazor template demo, không thuộc nghiệp vụ | `Counter.razor` | PENDING |
| 6 | W6-T6 | Fix Home.razor — thêm `<PageTitle>` + loading state trước redirect | `Home.razor` | PENDING |
| 7 | W6-T7 | Verify `dotnet build VanAn.sln` pass | Solution-wide | PENDING |
| 8 | W6-T8 | Visual smoke test — chạy app, navigate các trang chính, verify layout render | Solution-wide | PENDING |

### Entry criteria
- [ ] Wave 5 merged
- [ ] Build pass
- [ ] All 5 patterns fixed

### Exit criteria
- [ ] 0 inline `<style>` block trong tất cả .razor files
- [ ] 0 `eval` call trong Sitemap
- [ ] `Counter.razor` đã xóa
- [ ] `Home.razor` có `<PageTitle>` + loading
- [ ] Sitemap emoji hiển thị đúng
- [ ] `dotnet build VanAn.sln` 0 errors
- [ ] Visual smoke test pass — layout render có sidebar, navigation, metrics grid

### Why last
- Cleanup nhỏ, không ảnh hưởng architecture
- Sau khi tất cả patterns fix xong, dọn dẹp governance violations
- Visual smoke test ở cuối verify toàn bộ

---

## 8. CROSS-WAVE CONCERNS

### CSS Strategy
**Approach:** Shared CSS + Per-page CSS isolation
- **Shared CSS** (`_PagesShared.css` hoặc `wwwroot/css/pages.css`): define common classes dùng across pages (`page-header`, `metrics-grid`, `filter-grid`, `form-group`, `vanan-input`, `vanan-select`, `vanan-table`, `loading-state`, `empty-state`, `status-badge`, `pagination`)
- **Per-page `.razor.css`**: chỉ page-specific classes (`accounting-dashboard`, `einvoice-dashboard`, `audit-trail-page`, `provider-grid`, `activity-list`)
- **UI.Platform `.razor.css`**: VanALayout + VanANavigation (component-level CSS isolation)

### Rendermode Strategy
**Approach:** Add `@rendermode InteractiveServer` to ALL pages có interactive handlers
- Line 2 (sau `@page`), trước `@layout`
- Pages CHỈ có static content (Home, Error) — không cần
- Pages có `@bind`, `OnClick`, `@onchange`, `OnValidSubmit` — MUST have

### Component Consolidation Strategy
**Approach:** Migrate VanAnX (cũ) → VanX (mới)
- `VanAnAlert` → `VanAAlert` (10 occurrences)
- `VanAnModal` → `VanAModal` (1 occurrence)
- Verify API compat trước khi replace
- Note: `VanAnButton`, `VanAnCard`, `VanAnSpinner` — cần audit riêng (có thể đã migrate)

### Layout Strategy
**Approach:** Tất cả feature folders dùng cùng pattern
- AccountingLayout → VanALayout + VanANavigation (Accounting menu)
- EInvoiceLayout → VanALayout + VanANavigation (EInvoice menu)
- AdminLayout (NEW) → VanALayout + VanANavigation (Admin menu)
- Root pages (Home, Sitemap, AccessDenied, Error) → MainLayout (default, không cần feature nav)

### File Boundary
- **KHÔNG sửa code C#** — toàn bộ thay đổi chỉ trong `.razor` + `.razor.css` + `.css`
- **KHÔNG sửa Domain layer**
- **KHÔNG tạo component mới** (trừ AdminLayout — layout file, không phải component)
- **KHÔNG thêm dependency**

### Testing Strategy
- **Build check:** `dotnet build VanAn.sln` sau mỗi wave
- **Visual check:** Chỉ sau Wave 6 — chạy app, navigate, verify layout
- **Playwright:** DISABLED cho đến khi build pass + implementation complete

---

## 9. APPROVAL CHECKLIST

- [ ] Master plan reviewed (v1 — 6 waves, 6 patterns)
- [ ] 6 task cards reviewed (Wave 1-6)
- [ ] UI review report reviewed (23 files, 13 issues, 6 patterns)
- [ ] CSS strategy confirmed (shared + per-page isolation)
- [ ] Rendermode strategy confirmed (add to all interactive pages)
- [ ] Component consolidation confirmed (VanAnX → VanX)
- [ ] Admin layout strategy confirmed (create AdminLayout)
- [ ] Branch strategy confirmed (6 feature branches)
- [ ] Sẵn sàng implement Wave 1

---

## 10. EFFORT SUMMARY

| Wave | Pattern | Description | Sessions | Risk | Files |
|---|---|---|---|---|---|
| Wave 0 | — | Pre-flight verification | 0.5 | None | 0 |
| Wave 1 | P | UI.Platform infra (VanALayout CSS + slot + VanANavigation CSS + icons) | 1 | Medium | 7 |
| Wave 2 | R | Add `@rendermode InteractiveServer` (14 files) | 0.5 | Low | 14 |
| Wave 3 | C | Page CSS isolation (shared + per-page) | 1-2 | Low | 19 |
| Wave 4 | V | Component consolidation (VanAnX → VanX) | 0.5 | Low | 7 |
| Wave 5 | L | Admin layout consistency | 0.5 | Low | 5 |
| Wave 6 | G | Governance cleanup | 1 | Low | 6 |
| **Total** | | | **5-6 sessions** | | |

**Critical path:** Wave 0 → Wave 1 → Wave 2 → Wave 3 → Wave 4 → Wave 5 → Wave 6
**Parallel path:** None (sequential — mỗi wave build trên wave trước)

**Impact target:**
- Before: 23 files, 14 dead pages, 18 unstyled, 3 broken layouts
- After: 23 files, all interactive, all styled, all consistent
- Dead pages: 14 → 0
- Unstyled pages: 18 → 0
- Broken layouts: 3 → 0
