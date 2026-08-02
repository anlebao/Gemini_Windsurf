# TASK CARD: ShopERP UI Fix - Wave 5 - Admin Layout Consistency (Pattern L)

## 1. GOAL & CONTEXT
- **Mục tiêu cốt lõi:** Tạo `AdminLayout.razor` + add `@layout AdminLayout` to 4 Admin files — consistent với Accounting/EInvoice pattern
- **Nghiệp vụ áp dụng:** Layout architecture — tất cả feature folders dùng cùng VanALayout pattern
- **Status:** PENDING — Planning & Approval
- **Branch:** `feature/shoperp-ui-fix-wave5-admin-layout`
- **Estimated Sessions:** 0.5

---

## 2. ACTIVE WORKFLOW ROUTING
- **Target Workflow:** `newfeaturebuild.md`
- **Execution Mode:** IMPLEMENT
- **Current Phase:** Wave 5 of 6
- **Dependency:** Wave 4 merged; Wave 1 (VanALayout slot fix)

---

## 3. RELEVANT FILES (CONTEXT BOUNDARY)

### Files được phép đọc/sửa
- `docs/AI/project_state.md` (Bắt buộc đọc đầu phiên)
- `docs/AI/tasks/shoperp_ui_fix_master_plan.md` (READ)
- `5_WebApps/ShopERP/Components/Pages/Accounting/AccountingLayout.razor` (READ — template reference)
- `5_WebApps/ShopERP/Components/Pages/Admin/AdminLayout.razor` (NEW)
- `5_WebApps/ShopERP/Components/Pages/Admin/AuditTrail.razor` (UPDATE — add `@layout AdminLayout`)
- `5_WebApps/ShopERP/Components/Pages/Admin/UserManagement.razor` (UPDATE)
- `5_WebApps/ShopERP/Components/Pages/Admin/PermissionGroupManagement.razor` (UPDATE)
- `5_WebApps/ShopERP/Components/Pages/Admin/TenantManagement.razor` (UPDATE)

### Boundary Rules (Nghiêm cấm)
- KHÔNG sửa `MainLayout.razor` — vẫn là default layout cho root pages
- KHÔNG sửa `Routes.razor` — DefaultLayout giữ MainLayout
- KHÔNG tạo component mới — AdminLayout là layout file, không phải component
- KHÔNG thay đổi `@attribute [Authorize]` trong Admin files

---

## 4. TECHNICAL & REGULATORY CONSTRAINTS (HARDENING GATES)
- [ ] **Layout Pattern:** AdminLayout dùng VanALayout + VanANavigation — same as AccountingLayout
- [ ] **Slot Structure:** Dùng `<Sidebar>` + `<ChildContent>` đúng (fixed trong Wave 1)
- [ ] **Menu Items:** Admin menu: Users, Permission Groups, Audit Trail, Tenants
- [ ] **Authorize:** AdminLayout KHÔNG có `@attribute [Authorize]` — từng page tự authorize
- [ ] **Build Check:** `dotnet build VanAn.sln` 0 errors

---

## 5. SUCCESS CRITERIA (ĐO LƯỜNG ĐƯỢC)
- [ ] **SC1:** `AdminLayout.razor` tạo mới, dùng VanALayout + VanANavigation với Admin menu items
- [ ] **SC2:** Admin menu items: Dashboard (/admin không có — skip), Users (/admin/users), Permission Groups (/admin/permission-groups), Audit Trail (/admin/audit-trail), Tenants (/admin/tenants)
- [ ] **SC3:** 4 Admin files có `@layout AdminLayout` ở line 3 (sau `@page`, sau `@rendermode`)
- [ ] **SC4:** 0 Admin file dùng MainLayout default
- [ ] **SC5:** `dotnet build VanAn.sln` 0 errors

---

## 6. ACTIVE SKILLS (MAX 3)
- `pattern-based-fixing` — Same layout pattern as Accounting/EInvoice
- `build-error-analysis` — Fix any breakage

---

## 7. AI HEALTH CHECK MATRIX (INITIAL)
- **Evidence Count:** 3
- **Verified Facts:**
  - Fact 1: 4 Admin files không có `@layout` — dùng MainLayout default từ Routes.razor
  - Fact 2: AccountingLayout.razor là template reference (sau Wave 1 fix)
  - Fact 3: NavMenu.razor đã có Admin links (Users, Permission Groups, Audit Trail, Tenants)
- **Assumptions:**
  - AdminLayout không cần Dashboard item (không có `/admin` route)
  - Menu items match NavMenu.razor Admin section
- **Open Questions:**
  - Q1: AdminLayout có cần `@rendermode InteractiveServer` không? (Recommend: KHÔNG — layout file không có handlers)
- **Recommended Action:** PROCEED — create AdminLayout + add @layout

---

## 8. REVERSE IMPACT ANALYSIS
| File thay đổi | Reverse impact | Mitigation |
|---|---|---|
| `AdminLayout.razor` (NEW) | 4 Admin pages chuyển từ MainLayout sang AdminLayout | Positive — consistent |
| 4 Admin files | Add 1 line `@layout AdminLayout` | Safe |

---

## 9. TDD & TESTING STRATEGY
- **Build check:** `dotnet build VanAn.sln` sau batch
- **Verification:** Grep verify 4 files có `@layout AdminLayout`

---

## 10. JIT PLANNING + PURE EXECUTION (MICRO-PHASES)

### Template cho `AdminLayout.razor` (sau Wave 1 fix pattern)
```razor
@using VanAn.UI.Platform.Components
@using VanAn.UI.Platform.Components.Composite
@using VanAn.UI.Platform.Models
@inherits LayoutComponentBase

<VanALayout>
    <Sidebar>
        <VanANavigation MenuItems="@AdminMenuItems" />
    </Sidebar>
    <ChildContent>
        @Body
    </ChildContent>
</VanALayout>

@code {
    private List<NavigationItem> AdminMenuItems = new()
    {
        new() { Title = "Người Dùng", Icon = "people", Url = "/admin/users" },
        new() { Title = "Nhóm Quyền", Icon = "person-lock", Url = "/admin/permission-groups" },
        new() { Title = "Audit Trail", Icon = "shield-check", Url = "/admin/audit-trail" },
        new() { Title = "Tenant", Icon = "building", Url = "/admin/tenants" }
    };
}
```

### Micro-phase breakdown cho Wave 5

| Session | JIT Planning (chốt gì) | Pure Execution (viết gì) |
|---|---|---|
| **S1** | - Confirm AccountingLayout pattern (post-Wave 1)<br>- Chốt Admin menu items (match NavMenu)<br>- Chốt line position for `@layout` | - Create `AdminLayout.razor`<br>- Add `@layout AdminLayout` to 4 Admin files<br>- Run `dotnet build VanAn.sln`<br>- Commit |

### Rules
- Copy AccountingLayout pattern (post-Wave 1)
- Menu items match NavMenu.razor Admin section
- `@layout AdminLayout` ở line 3 (sau `@page` + `@rendermode`)

---

## 11. ESTIMATED EFFORT
- 0.5 session (1 new file + 4 line additions)
- **BLOCKER:** Wave 1 must be merged (VanALayout slot fix)
