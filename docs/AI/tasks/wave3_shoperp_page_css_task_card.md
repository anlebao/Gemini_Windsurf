# TASK CARD: ShopERP UI Fix - Wave 3 - Page CSS Isolation (Pattern C)

## 1. GOAL & CONTEXT
- **Mục tiêu cốt lõi:** Tạo CSS cho 18 unstyled pages — 1 shared CSS file cho common classes + 18 `.razor.css` files cho page-specific classes
- **Nghiệp vụ áp dụng:** UI styling — pages render plain HTML hiện tại cần grid/flex/styling
- **Status:** PENDING — Planning & Approval
- **Branch:** `feature/shoperp-ui-fix-wave3-page-css`
- **Estimated Sessions:** 1-2

---

## 2. ACTIVE WORKFLOW ROUTING
- **Target Workflow:** `newfeaturebuild.md`
- **Execution Mode:** IMPLEMENT
- **Current Phase:** Wave 3 of 6
- **Dependency:** Wave 2 merged

---

## 3. RELEVANT FILES (CONTEXT BOUNDARY)

### Files được phép đọc/sửa
- `docs/AI/project_state.md` (Bắt buộc đọc đầu phiên)
- `docs/AI/tasks/shoperp_ui_fix_master_plan.md` (READ)
- `5_WebApps/ShopERP/wwwroot/css/pages.css` (NEW — shared CSS)
- `5_WebApps/ShopERP/Components/App.razor` (UPDATE — link shared CSS in `<head>`)
- 18 `.razor.css` files (NEW — per-page CSS isolation)

### 18 files cần `.razor.css`
**Root (2):** `AccessDenied.razor.css`, `Sitemap.razor.css`
**Accounting (7):** `AccountingIndex.razor.css`, `AccountingLayout.razor.css`, `AccountBalance.razor.css`, `ExpenseEntry.razor.css`, `PeriodClosing.razor.css`, `RevenueEntry.razor.css`, `TransactionHistory.razor.css`
**EInvoice (6):** `EInvoiceDashboard.razor.css`, `EInvoiceLayout.razor.css`, `AlertManagement.razor.css`, `HealthMonitoring.razor.css`, `InvoiceManagement.razor.css`, `ProviderConfiguration.razor.css`, `ProviderManagement.razor.css`
**Admin (4):** `AuditTrail.razor.css`, `UserManagement.razor.css`, `PermissionGroupManagement.razor.css`, `TenantManagement.razor.css`

### Boundary Rules (Nghiêm cấm)
- KHÔNG sửa `.razor` markup — chỉ tạo `.razor.css`
- KHÔNG inline `<style>` — dùng CSS isolation
- KHÔNG thêm dependency — dùng CSS thuần + Bootstrap tokens
- KHÔNG tạo CSS cho classes đã có trong Bootstrap (`.table`, `.badge`, `.form-control`)

---

## 4. TECHNICAL & REGULATORY CONSTRAINTS (HARDENING GATES)
- [ ] **CSS Isolation:** `.razor.css` tự động scoped by Blazor — KHÔNG cần namespace
- [ ] **Shared CSS:** `wwwroot/css/pages.css` — global, link trong `App.razor`
- [ ] **Design Tokens:** Dùng CSS variables (`--vanan-primary`, `--vanan-text`, etc.) — KHÔNG hardcoded
- [ ] **Responsive:** Mobile-first, breakpoints ≤640px (mobile), 641-1024px (tablet), ≥1025px (desktop)
- [ ] **Build Check:** `dotnet build VanAn.sln` 0 errors

---

## 5. SUCCESS CRITERIA (ĐO LƯỜNG ĐƯỢC)
- [ ] **SC1:** `wwwroot/css/pages.css` tạo mới với common classes:
  - `.page-header` (flex, space-between, align-center, border-bottom)
  - `.header-actions` (flex, gap)
  - `.metrics-grid` (CSS Grid, auto-fill, minmax 280px, gap 1.5rem)
  - `.metrics-section` (margin-bottom)
  - `.filter-grid` (CSS Grid, repeat auto-fill, minmax 200px)
  - `.filter-group` (flex column, gap)
  - `.filter-section` / `.filter-actions` (margin, flex gap)
  - `.loading-state` (flex center, padding)
  - `.empty-state` (text-center, padding, color muted)
  - `.form-group` (margin-bottom, flex column, gap)
  - `.form-actions` (flex, gap, margin-top)
  - `.vanan-input` / `.vanan-select` (padding, border, border-radius, focus state)
  - `.vanan-table` (width 100%, border-collapse, th/td padding, hover)
  - `.status-badge` (padding, border-radius, font-size, color variants)
  - `.action-badge` (same pattern)
  - `.pagination` (flex, gap, align-center)
  - `.page-info` (padding)
- [ ] **SC2:** `App.razor` link `<link rel="stylesheet" href="css/pages.css" />` trong `<head>`
- [ ] **SC3:** 18 `.razor.css` files tạo cho page-specific classes
- [ ] **SC4:** `dotnet build VanAn.sln` 0 errors

---

## 6. ACTIVE SKILLS (MAX 3)
- `ui-platform-compliance-review` — Ensure CSS follows design tokens
- `pattern-based-fixing` — Shared CSS pattern across pages

---

## 7. AI HEALTH CHECK MATRIX (INITIAL)
- **Evidence Count:** 4
- **Verified Facts:**
  - Fact 1: 18/23 files dùng custom classes không có CSS (grep confirmed zero match)
  - Fact 2: `app.css` chỉ có 76 dòng — không define page classes
  - Fact 3: Bootstrap classes (`.table`, `.badge`, `.form-control`) đã có trong `bootstrap.min.css`
  - Fact 4: Blazor CSS isolation — `.razor.css` tự động scoped, không cần namespace
- **Assumptions:**
  - CSS variables `--vanan-primary` etc. đã define ở đâu đó (cần verify — có thể chưa có)
  - Shared CSS approach giảm trùng lặp vs per-page isolation
- **Open Questions:**
  - Q1: CSS variables `--vanan-*` đã define chưa? (Cần grep verify — nếu chưa, define trong `app.css` hoặc `pages.css` `:root`)
  - Q2: Nên dùng `wwwroot/css/pages.css` (global) hay `_LayoutShared.css`? (Recommend: `pages.css` — đơn giản, link 1 lần)
- **Recommended Action:** PROCEED — verify CSS variables trước, tạo shared + per-page CSS

---

## 8. REVERSE IMPACT ANALYSIS
| File thay đổi | Reverse impact | Mitigation |
|---|---|---|
| `pages.css` (NEW) | Global CSS — affect tất cả pages | Safe — chỉ add styling, không override |
| `App.razor` | Add 1 `<link>` | Safe |
| 18 `.razor.css` (NEW) | CSS isolation — chỉ affect page tương ứng | Safe |

---

## 9. TDD & TESTING STRATEGY
- **Build check:** `dotnet build VanAn.sln` sau batch
- **Visual check:** Skip (Wave 6)
- **Verification:** Build pass + grep verify 18 `.razor.css` created

---

## 10. JIT PLANNING + PURE EXECUTION (MICRO-PHASES)

### Chiến lược: Shared-First
1. Verify CSS variables (`--vanan-*`) — nếu chưa có, define trong `:root` của `pages.css`
2. Tạo `pages.css` với tất cả common classes
3. Link trong `App.razor`
4. Tạo 18 `.razor.css` cho page-specific classes (batch, same pattern)

### Template cho `pages.css` (common classes)
```css
:root {
  --vanan-primary: #0d6efd;
  --vanan-text: #212529;
  --vanan-text-muted: #6c757d;
  --vanan-border: #dee2e6;
  --vanan-hover-bg: #f0f4ff;
  --vanan-danger: #dc3545;
  --vanan-warning: #ffc107;
  --vanan-success: #198754;
}

.page-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  flex-wrap: wrap;
  gap: 1rem;
  margin-bottom: 1.5rem;
  padding-bottom: 1rem;
  border-bottom: 1px solid var(--vanan-border);
}

.metrics-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(280px, 1fr));
  gap: 1.5rem;
}

.filter-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(200px, 1fr));
  gap: 1rem;
}

.vanan-input, .vanan-select {
  padding: 0.5rem 0.75rem;
  border: 1px solid var(--vanan-border);
  border-radius: 6px;
  font-size: 1rem;
}

.vanan-table {
  width: 100%;
  border-collapse: collapse;
}
.vanan-table th, .vanan-table td {
  padding: 0.75rem;
  border-bottom: 1px solid var(--vanan-border);
  text-align: left;
}
.vanan-table tbody tr:hover {
  background-color: var(--vanan-hover-bg);
}
```

### Micro-phase breakdown cho Wave 3

| Session | JIT Planning (chốt gì) | Pure Execution (viết gì) |
|---|---|---|
| **S1** | - Verify CSS variables existing<br>- Chốt shared CSS class list<br>- Chốt per-page CSS class list | - Create `pages.css` (shared)<br>- Link in `App.razor`<br>- Create 9 `.razor.css` (Accounting + EInvoice)<br>- Run `dotnet build VanAn.sln` |
| **S2** | - (Nếu S1 chưa xong) continue | - Create 9 `.razor.css` (Admin + Root)<br>- Run `dotnet build VanAn.sln`<br>- Commit |

### Rules
- Shared CSS trước, per-page sau
- Dùng CSS variables, KHÔNG hardcoded colors
- Mobile-first responsive
- KHÔNG style classes đã có trong Bootstrap

---

## 11. ESTIMATED EFFORT
- 1-2 sessions (1 shared CSS + 18 per-page CSS)
- **BLOCKER:** Verify CSS variables existing
