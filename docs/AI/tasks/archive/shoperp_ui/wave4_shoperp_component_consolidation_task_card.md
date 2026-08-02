# TASK CARD: ShopERP UI Fix - Wave 4 - Component Version Consolidation (Pattern V)

## 1. GOAL & CONTEXT
- **Mục tiêu cốt lõi:** Migrate `VanAnAlert` → `VanAAlert` (10 occurrences) + `VanAnModal` → `VanAModal` (1 occurrence) — consolidate 2 component versions thành 1
- **Nghiệp vụ áp dụng:** UI Platform consistency — 1 component version cho tất cả
- **Status:** PENDING — Planning & Approval
- **Branch:** `feature/shoperp-ui-fix-wave4-component-consolidation`
- **Estimated Sessions:** 0.5

---

## 2. ACTIVE WORKFLOW ROUTING
- **Target Workflow:** `newfeaturebuild.md`
- **Execution Mode:** IMPLEMENT
- **Current Phase:** Wave 4 of 6
- **Dependency:** Wave 3 merged

---

## 3. RELEVANT FILES (CONTEXT BOUNDARY)

### Files được phép đọc/sửa
- `docs/AI/project_state.md` (Bắt buộc đọc đầu phiên)
- `docs/AI/tasks/shoperp_ui_fix_master_plan.md` (READ)
- `UI.Platform/Components/VanAAlert.razor` (READ — verify API)
- `UI.Platform/Components/VanAnAlert.razor` (READ — verify API compat)
- `UI.Platform/Components/VanAModal.razor` (READ — verify API)
- `UI.Platform/Components/VanAnModal.razor` (READ — verify API compat)
- `5_WebApps/ShopERP/Components/Pages/EInvoice/EInvoiceDashboard.razor` (UPDATE — VanAnAlert → VanAAlert)
- `5_WebApps/ShopERP/Components/Pages/EInvoice/ProviderManagement.razor` (UPDATE)
- `5_WebApps/ShopERP/Components/Pages/EInvoice/ProviderConfiguration.razor` (UPDATE — 3 occurrences)
- `5_WebApps/ShopERP/Components/Pages/EInvoice/HealthMonitoring.razor` (UPDATE)
- `5_WebApps/ShopERP/Components/Pages/EInvoice/AlertManagement.razor` (UPDATE)
- `5_WebApps/ShopERP/Components/Pages/EInvoice/InvoiceManagement.razor` (UPDATE — VanAnAlert + VanAnModal)

### Boundary Rules (Nghiêm cấm)
- KHÔNG xóa `VanAnAlert.razor` / `VanAnModal.razor` — chỉ migrate usage, xóa sau trong debt cleanup
- KHÔNG sửa component code — chỉ sửa usage trong pages
- KHÔNG thay đổi API parameters — verify compat trước khi replace
- KHÔNG sửa files ngoài EInvoice folder (chỉ EInvoice dùng VanAnX)

---

## 4. TECHNICAL & REGULATORY CONSTRAINTS (HARDENING GATES)
- [ ] **API Compat:** Verify `VanAAlert` có cùng parameters: `Type`, `Message`, `Dismissible`, `OnDismiss`, `data-testid`
- [ ] **API Compat:** Verify `VanAModal` có cùng parameters: `Title`, `OnClose`, `IsVisible`, `Body`, `Footer`
- [ ] **Type Values:** Verify `VanAAlert.Type` accept same values: `success`, `error`, `warning`, `danger`, `info`
- [ ] **Build Check:** `dotnet build VanAn.sln` 0 errors

---

## 5. SUCCESS CRITERIA (ĐO LƯỜNG ĐƯỢC)
- [ ] **SC1:** 0 `VanAnAlert` reference trong 6 EInvoice files (10 → 0)
- [ ] **SC2:** 0 `VanAnModal` reference trong InvoiceManagement (1 → 0)
- [ ] **SC3:** All replaced với `VanAAlert` / `VanAModal` — API compatible
- [ ] **SC4:** `dotnet build VanAn.sln` 0 errors
- [ ] **SC5:** 0 markup change ngoài component name

---

## 6. ACTIVE SKILLS (MAX 3)
- `pattern-based-fixing` — replace_all operation
- `build-error-analysis` — Fix API compat issues

---

## 7. AI HEALTH CHECK MATRIX (INITIAL)
- **Evidence Count:** 3
- **Verified Facts:**
  - Fact 1: 10 `VanAnAlert` occurrences trong 6 EInvoice files (grep confirmed)
  - Fact 2: 1 `VanAnModal` occurrence trong InvoiceManagement (grep confirmed)
  - Fact 3: `VanAAlert.razor` (2179 bytes) vs `VanAnAlert.razor` (2640 bytes) — 2 versions tồn tại
- **Assumptions:**
  - `VanAAlert` API compatible với `VanAnAlert` (cần verify)
  - `VanAModal` API compatible với `VanAnModal` (cần verify — `VanAModal` có `IsVisible`/`Body`/`Footer`, `VanAnModal` có thể khác)
- **Open Questions:**
  - Q1: `VanAnModal` có `Body`/`Footer` RenderFragments không? (Cần đọc verify — nếu khác, cần adapt markup)
  - Q2: `VanAAlert.Type` có accept `"danger"` không? (`VanAnAlert` dùng `"danger"`, `VanAAlert` có thể dùng `"error"`)
- **Recommended Action:** INVESTIGATE API compat trước, rồi PROCEED

---

## 8. REVERSE IMPACT ANALYSIS
| File thay đổi | Reverse impact | Mitigation |
|---|---|---|
| 6 EInvoice files | Component name change — render same if API compat | Verify API trước |
| InvoiceManagement.razor | Modal API có thể khác — cần adapt Body/Footer | Read VanAModal.razor verify |

---

## 9. TDD & TESTING STRATEGY
- **Build check:** `dotnet build VanAn.sln` sau batch
- **Verification:** Grep verify 0 `VanAnAlert`/`VanAnModal` trong EInvoice

---

## 10. JIT PLANNING + PURE EXECUTION (MICRO-PHASES)

### Chiến lược: Verify-Then-Replace
1. Đọc `VanAAlert.razor` + `VanAnAlert.razor` — compare API
2. Đọc `VanAModal.razor` + `VanAnModal.razor` — compare API
3. Nếu compat → `replace_all` batch
4. Nếu không compat → adapt markup per occurrence

### Micro-phase breakdown cho Wave 4

| Session | JIT Planning (chốt gì) | Pure Execution (viết gì) |
|---|---|---|
| **S1** | - Read + compare VanAAlert vs VanAnAlert API<br>- Read + compare VanAModal vs VanAnModal API<br>- Chốt: replace_all hay adapt markup | - Replace `VanAnAlert` → `VanAAlert` (10 occurrences)<br>- Replace `VanAnModal` → `VanAModal` (1 occurrence, adapt if needed)<br>- Run `dotnet build VanAn.sln`<br>- Grep verify<br>- Commit |

### Rules
- Verify API compat TRƯỚC khi replace
- Nếu `Type="danger"` không compat → map sang `"error"` hoặc thêm `"danger"` vào VanAAlert
- Nếu `VanAnModal` markup khác `VanAModal` → adapt Body/Footer wrapping

---

## 11. ESTIMATED EFFORT
- 0.5 session (11 replacements + API verify)
- **BLOCKER:** API compat verification
