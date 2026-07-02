# TASK CARD: ShopERP UI Fix - Wave 2 - Add `@rendermode InteractiveServer` (Pattern R)

## 1. GOAL & CONTEXT
- **Mục tiêu cốt lõi:** Add `@rendermode InteractiveServer` to 14 files có interactive handlers nhưng đang SSR mode — fix dead buttons/forms
- **Nghiệp vụ áp dụng:** Blazor interactivity — tất cả pages có `@bind`/`OnClick`/`@onchange` MUST có rendermode
- **Status:** PENDING — Planning & Approval
- **Branch:** `feature/shoperp-ui-fix-wave2-rendermode`
- **Estimated Sessions:** 0.5

---

## 2. ACTIVE WORKFLOW ROUTING
- **Target Workflow:** `newfeaturebuild.md`
- **Execution Mode:** IMPLEMENT
- **Current Phase:** Wave 2 of 6
- **Dependency:** Wave 1 merged

---

## 3. RELEVANT FILES (CONTEXT BOUNDARY)

### Files được phép đọc/sửa (14 files)
- `docs/AI/project_state.md` (Bắt buộc đọc đầu phiên)
- `docs/AI/tasks/shoperp_ui_fix_master_plan.md` (READ)
- `5_WebApps/ShopERP/Components/Pages/AccessDenied.razor`
- `5_WebApps/ShopERP/Components/Pages/Sitemap.razor`
- `5_WebApps/ShopERP/Components/Pages/Accounting/AccountingIndex.razor`
- `5_WebApps/ShopERP/Components/Pages/Accounting/TransactionHistory.razor`
- `5_WebApps/ShopERP/Components/Pages/EInvoice/EInvoiceDashboard.razor`
- `5_WebApps/ShopERP/Components/Pages/EInvoice/ProviderManagement.razor`
- `5_WebApps/ShopERP/Components/Pages/EInvoice/ProviderConfiguration.razor`
- `5_WebApps/ShopERP/Components/Pages/EInvoice/HealthMonitoring.razor`
- `5_WebApps/ShopERP/Components/Pages/EInvoice/AlertManagement.razor`
- `5_WebApps/ShopERP/Components/Pages/EInvoice/InvoiceManagement.razor`
- `5_WebApps/ShopERP/Components/Pages/Admin/AuditTrail.razor`
- `5_WebApps/ShopERP/Components/Pages/Admin/UserManagement.razor`
- `5_WebApps/ShopERP/Components/Pages/Admin/PermissionGroupManagement.razor`
- `5_WebApps/ShopERP/Components/Pages/Admin/TenantManagement.razor`

### Boundary Rules (Nghiêm cấm)
- KHÔNG sửa bất kỳ gì khác ngoài thêm 1 line `@rendermode InteractiveServer`
- KHÔNG thay đổi `@page`, `@layout`, `@attribute`, `@using` directives
- KHÔNG sửa markup hoặc `@code` block
- KHÔNG thêm file mới

---

## 4. TECHNICAL & REGULATORY CONSTRAINTS (HARDENING GATES)
- [ ] **Line Position:** `@rendermode InteractiveServer` ở line 2 (sau `@page`, trước `@layout`/`@attribute`)
- [ ] **No Double:** Verify file chưa có `@rendermode` trước khi thêm
- [ ] **Build Check:** `dotnet build VanAn.sln` 0 errors

---

## 5. SUCCESS CRITERIA (ĐO LƯỜNG ĐƯỢC)
- [ ] **SC1:** 14 files có `@rendermode InteractiveServer` ở line 2
- [ ] **SC2:** 0 file có duplicate `@rendermode`
- [ ] **SC3:** `dotnet build VanAn.sln` 0 errors
- [ ] **SC4:** 0 markup/code change ngoài `@rendermode` line

---

## 6. ACTIVE SKILLS (MAX 3)
- `pattern-based-fixing` — Same operation 14 times

---

## 7. AI HEALTH CHECK MATRIX (INITIAL)
- **Evidence Count:** 3
- **Verified Facts:**
  - Fact 1: 14 files có interactive handlers (`OnClick`, `@bind`, `@onchange`, `OnValidSubmit`) nhưng thiếu `@rendermode`
  - Fact 2: 5 files đã có `@rendermode` đúng (Counter, RevenueEntry, ExpenseEntry, AccountBalance, PeriodClosing)
  - Fact 3: `@rendermode InteractiveServer` là directive chuẩn Blazor .NET 8
- **Assumptions:**
  - Thêm `@rendermode` không break build (directive only)
  - Pages sẽ chuyển từ SSR sang interactive server mode
- **Open Questions:**
  - Q1: `AccessDenied.razor` có thực sự cần interactivity không? (Có — `OnClick="GoHome"`)
- **Recommended Action:** PROCEED — batch add

---

## 8. REVERSE IMPACT ANALYSIS
| File thay đổi | Reverse impact | Mitigation |
|---|---|---|
| 14 files | Pages chuyển sang interactive mode — cần SignalR circuit | OK — ShopERP đã cấu hình interactive server |

---

## 9. TDD & TESTING STRATEGY
- **Build check:** `dotnet build VanAn.sln` sau batch
- **Verification:** Grep verify 14 files có `@rendermode`

---

## 10. JIT PLANNING + PURE EXECUTION (MICRO-PHASES)

### Pattern fix template
```razor
@page "/some-path"
@rendermode InteractiveServer    ← ADD THIS LINE
@layout SomeLayout
@attribute [Authorize(...)]
```

### Micro-phase breakdown cho Wave 2

| Session | JIT Planning (chốt gì) | Pure Execution (viết gì) |
|---|---|---|
| **S1** | - Confirm 14 files list<br>- Confirm line position (line 2, sau @page) | - Add `@rendermode InteractiveServer` to 14 files<br>- Run `dotnet build VanAn.sln`<br>- Grep verify<br>- Commit |

### Rules
- Batch operation — add same line to 14 files
- Verify build 1 lần sau khi add all
- KHÔNG sửa gì khác

---

## 11. ESTIMATED EFFORT
- 0.5 session (14 files, 1 line each)
- **BLOCKER:** None
