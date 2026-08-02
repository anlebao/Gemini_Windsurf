# TASK CARD: ShopERP UI Fix - Wave 6 - Governance Cleanup (Pattern G)

## 1. GOAL & CONTEXT
- **Mục tiêu cốt lõi:** Dọn dẹp governance violations — inline `<style>`, `eval` logout, demo leftover, naked redirect, broken emoji
- **Nghiệp vụ áp dụng:** Code hygiene — tuân thủ governance rules (no inline CSS, no eval, no demo code)
- **Status:** PENDING — Planning & Approval
- **Branch:** `feature/shoperp-ui-fix-wave6-governance-cleanup`
- **Estimated Sessions:** 1

---

## 2. ACTIVE WORKFLOW ROUTING
- **Target Workflow:** `newfeaturebuild.md`
- **Execution Mode:** IMPLEMENT
- **Current Phase:** Wave 6 of 6 (final)
- **Dependency:** Wave 5 merged (all 5 patterns fixed)

---

## 3. RELEVANT FILES (CONTEXT BOUNDARY)

### Files được phép đọc/sửa
- `docs/AI/project_state.md` (Bắt buộc đọc đầu phiên)
- `docs/AI/tasks/shoperp_ui_fix_master_plan.md` (READ)
- `5_WebApps/ShopERP/Components/Pages/AccessDenied.razor` (UPDATE — move inline style)
- `5_WebApps/ShopERP/Components/Pages/AccessDenied.razor.css` (NEW — CSS isolation)
- `5_WebApps/ShopERP/Components/Pages/Sitemap.razor` (UPDATE — move inline style + fix eval + fix emoji)
- `5_WebApps/ShopERP/Components/Pages/Sitemap.razor.css` (NEW — CSS isolation)
- `5_WebApps/ShopERP/Components/Pages/Counter.razor` (DELETE — demo leftover)
- `5_WebApps/ShopERP/Components/Pages/Home.razor` (UPDATE — add PageTitle + loading)
- `5_WebApps/ShopERP/Components/Pages/_Imports.razor` (READ — verify if Counter referenced)

### Boundary Rules (Nghiêm cấm)
- KHÔNG tạo component mới
- KHÔNG thêm dependency
- KHÔNG sửa business logic
- KHÔNG xóa file ngoài `Counter.razor`
- KHÔNG thay đổi route paths

---

## 4. TECHNICAL & REGULATORY CONSTRAINTS (HARDENING GATES)
- [ ] **No Inline Style:** Move tất cả `<style>` blocks sang `.razor.css`
- [ ] **No Eval:** Thay `JSRuntime.InvokeVoidAsync("eval", ...)` bằng server-side approach
- [ ] **CSS Isolation:** `.razor.css` tự động scoped by Blazor
- [ ] **PageTitle:** Mọi page có `<PageTitle>` cho browser tab
- [ ] **Build Check:** `dotnet build VanAn.sln` 0 errors

---

## 5. SUCCESS CRITERIA (ĐO LƯỜNG ĐƯỢC)
- [ ] **SC1:** `AccessDenied.razor` — 0 inline `<style>` block, CSS moved to `AccessDenied.razor.css`
- [ ] **SC2:** `Sitemap.razor` — 0 inline `<style>` block, CSS moved to `Sitemap.razor.css`
- [ ] **SC3:** `Sitemap.razor` — 0 `eval` call, logout dùng `NavigationManager.NavigateTo("/Logout")` hoặc server endpoint
- [ ] **SC4:** `Sitemap.razor` — 0 broken emoji (`` replaced với emoji đúng)
- [ ] **SC5:** `Counter.razor` — đã xóa, 0 reference còn lại
- [ ] **SC6:** `Home.razor` — có `<PageTitle>Redirecting...</PageTitle>` + loading state
- [ ] **SC7:** `dotnet build VanAn.sln` 0 errors
- [ ] **SC8:** Visual smoke test pass — navigate các trang chính, verify layout render

---

## 6. ACTIVE SKILLS (MAX 3)
- `ui-platform-compliance-review` — Ensure governance compliance
- `build-error-analysis` — Fix any breakage

---

## 7. AI HEALTH CHECK MATRIX (INITIAL)
- **Evidence Count:** 5
- **Verified Facts:**
  - Fact 1: `AccessDenied.razor` L22-59 có 37 dòng inline `<style>`
  - Fact 2: `Sitemap.razor` L178-250 có 72 dòng inline `<style>`
  - Fact 3: `Sitemap.razor` L275-278 dùng `JSRuntime.InvokeVoidAsync("eval", ...)` cho logout
  - Fact 4: `Counter.razor` là Blazor template demo (363 bytes, `/counter` route)
  - Fact 5: `Home.razor` chỉ có 10 dòng, redirect naked không có PageTitle
- **Assumptions:**
  - Có `/Logout` endpoint hoặc có thể tạo (cần verify)
  - Broken emoji là encoding issue — có thể fix bằng cách type lại
- **Open Questions:**
  - Q1: Có `/Logout` endpoint không? (Cần grep — nếu không, dùng `NavigationManager.NavigateTo("/Login")` sau khi clear cookie qua server)
  - Q2: `Counter.razor` có referenced ở đâu không? (Cần grep — NavMenu không có, Sitemap không có)
- **Recommended Action:** INVESTIGATE logout endpoint + Counter references, rồi PROCEED

---

## 8. REVERSE IMPACT ANALYSIS
| File thay đổi | Reverse impact | Mitigation |
|---|---|---|
| `AccessDenied.razor` + `.razor.css` | CSS isolation — same visual | Safe |
| `Sitemap.razor` + `.razor.css` | CSS isolation + logout change | Verify logout works |
| `Counter.razor` (DELETE) | 404 on `/counter` — không ai link | Safe (verify no references) |
| `Home.razor` | Add PageTitle + loading | Positive UX |

---

## 9. TDD & TESTING STRATEGY
- **Build check:** `dotnet build VanAn.sln` sau batch
- **Visual smoke test:** Chạy app, navigate: `/`, `/sitemap`, `/access-denied`, `/accounting`, `/einvoice`, `/admin/users`
- **Verification:** Build pass + visual render OK

---

## 10. JIT PLANNING + PURE EXECUTION (MICRO-PHASES)

### Chiến lược: Cleanup Batch
1. Move inline styles → `.razor.css` (2 files)
2. Fix Sitemap logout (eval → server-side)
3. Fix Sitemap emoji
4. Delete Counter.razor
5. Fix Home.razor
6. Build + visual smoke test

### Template cho Home.razor fix
```razor
@page "/"
@attribute [Microsoft.AspNetCore.Authorization.Authorize]
@inject NavigationManager NavigationManager

<PageTitle>Redirecting...</PageTitle>

<div class="redirect-loading">
    <p>Đang chuyển hướng...</p>
</div>

@code {
    protected override void OnInitialized()
    {
        NavigationManager.NavigateTo("/sitemap", replace: true);
    }
}
```

### Template cho Sitemap logout fix
```razor
// BEFORE
private async Task Logout()
{
    await JSRuntime.InvokeVoidAsync("eval", @"
        document.cookie = '.VanAn.Auth=; Path=/; Expires=Thu, 01 Jan 1970 00:00:00 GMT; SameSite=Strict; Secure';
        window.location.href = '/Login';
    ");
}

// AFTER — option 1: server endpoint
private void Logout()
{
    NavigationManager.NavigateTo("/Logout", forceLoad: true);
}

// AFTER — option 2: if no /Logout endpoint, use JSInterop without eval
private async Task Logout()
{
    await JSRuntime.InvokeVoidAsync("vananLogout");
    NavigationManager.NavigateTo("/Login", forceLoad: true);
}
// + add vananLogout function in App.razor <script>
```

### Micro-phase breakdown cho Wave 6

| Session | JIT Planning (chốt gì) | Pure Execution (viết gì) |
|---|---|---|
| **S1** | - Verify `/Logout` endpoint exists (grep)<br>- Verify Counter.razor references (grep)<br>- Chốt logout approach (server endpoint vs JSInterop)<br>- Identify broken emojis | - Move AccessDenied inline style → `.razor.css`<br>- Move Sitemap inline style → `.razor.css`<br>- Fix Sitemap logout<br>- Fix Sitemap emojis<br>- Delete Counter.razor<br>- Fix Home.razor<br>- Run `dotnet build VanAn.sln`<br>- Visual smoke test<br>- Commit |

### Rules
- Move CSS, KHÔNG rewrite CSS (giữ nguyên styling)
- Verify logout works sau fix
- Verify Counter không có reference trước khi xóa
- Visual smoke test ở cuối — navigate 6 trang chính

---

## 11. ESTIMATED EFFORT
- 1 session (2 CSS moves + 1 logout fix + 1 delete + 1 Home fix + smoke test)
- **BLOCKER:** Verify `/Logout` endpoint + Counter references
