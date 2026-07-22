# TASK CARD: THEME - PHASE 3 - ShopERP Admin UI

## 1. GOAL & CONTEXT
- **Mục tiêu cốt lõi:** Thêm theme selector dropdown vào Edit Tenant modal — SysAdmin chọn 1 trong 5 phong cách, save qua API.
- **Nghiệp vụ áp dụng:** SysAdmin vào /admin/tenants → Sửa tenant → chọn theme → Lưu. Theme persist vào DB, KhachLink render đúng theme.

## 2. ACTIVE WORKFLOW ROUTING
- **Target Workflow:** `.devin/workflows/newfeaturebuild.md`
- **Execution Mode:** IMPLEMENT

## 3. RELEVANT FILES (CONTEXT BOUNDARY)
- **Files được phép đọc/sửa:**
  - `docs/AI/project_state.md` (Bắt buộc đọc đầu phiên)
  - `5_WebApps/ShopERP/Components/Pages/Admin/TenantManagement.razor` (SỬA — thêm theme selector)
- **Boundary Rules (Nghiêm cấm):**
  - KHÔNG sửa service/controller (Phase 2 đã xong)
  - KHÔNG sửa KhachLink (Phase 4)
  - KHÔNG thêm page mới — chỉ sửa Edit modal trong page hiện có
  - KHÔNG dùng custom HTML/CSS — dùng UI Platform components (`VanAButton`, `VanAForm`, `vanan-select`)

## 4. TECHNICAL & REGULATORY CONSTRAINTS (HARDENING GATES)
- [ ] **UI Platform compliance:** Dropdown dùng `<select class="vanan-select">` — cùng pattern với dropdowns hiện có trong edit modal (BusinessType, ShopInstance)
- [ ] **Bind 2 chiều:** `@bind="_editForm.Theme"` — load theme hiện tại, save theme mới
- [ ] **EditForm class:** Thêm `public ThemeType Theme { get; set; } = ThemeType.Classic;` vào `EditForm` record
- [ ] **OpenEditModal:** Set `Theme = t.Theme` khi load tenant vào form
- [ ] **HandleEditSubmit:** Truyền `Theme = _editForm.Theme` vào `UpdateTenantProfileApiRequest`
- [ ] **5 options:** Classic, Modern, Teen, Lady, Premium — với mô tả tiếng Việt kèm màu sắc
- [ ] **Gate 4 (UI Layout → E2E):** UI thay đổi → cần E2E test (optional cho Phase 3, verify manual acceptable)

## 5. SUCCESS CRITERIA (ĐO LƯỜNG ĐƯỢC)
- [ ] **SC1:** `EditForm` class có `public ThemeType Theme { get; set; } = ThemeType.Classic;`
- [ ] **SC2:** `OpenEditModal` set `Theme = t.Theme` (load từ TenantApiDto)
- [ ] **SC3:** Edit modal có `<select class="vanan-select" @bind="_editForm.Theme">` với 5 options:
  - Classic — Cà phê truyền thống (kem nâu)
  - Modern — Tối giản hiện đại (trắng)
  - Teen — Trẻ trung (gradient hồng-tím)
  - Lady — Nữ tính (gradient hồng pastel)
  - Premium — Cao cấp (đen + vàng gold)
- [ ] **SC4:** `HandleEditSubmit` truyền `Theme = _editForm.Theme` vào `UpdateTenantProfileApiRequest`
- [ ] **SC5:** Dropdown hiển thị theme hiện tại khi mở edit modal (bind 2 chiều)
- [ ] **SC6:** Small text hint dưới dropdown: "Áp dụng cho trang khách hàng KhachLink của tenant này"
- [ ] **SC7:** `dotnet build VanAn.sln` — 0 errors
- [ ] **SC8:** (Optional) Tenant list table hiển thị cột "Phong cách" với theme hiện tại

**Implementation Date:** 2026-07-22
**Branch:** `main`

## 6. ACTIVE SKILLS (MAX 3)
- `ui-platform-compliance-review` — Verify dùng vanan-select, không custom HTML
- `accounting-ui-implementation` — Pattern reference cho Blazor form binding
- `build-error-analysis` — Nếu @bind enum fails

## 7. AI HEALTH CHECK MATRIX (INITIAL)
- **Evidence Count:** 6
- **Verified Facts:**
  - Fact 1: `EditForm` class (line 1018-1032) — 12 properties, chưa có Theme
  - Fact 2: `OpenEditModal` (line 772-793) — set `_editForm` từ `TenantApiDto t`, chưa có Theme
  - Fact 3: `HandleEditSubmit` (line 880-935) — gọi `TenantApi.UpdateProfileAsync` với `UpdateTenantProfileApiRequest`, chưa có Theme
  - Fact 4: Edit modal (line 376-492) — có dropdowns `vanan-select` cho BusinessType, ShopInstance — pattern reference
  - Fact 5: `TenantApiDto` (Phase 2 sẽ thêm `Theme` property) — `OpenEditModal` đọc `t.Theme`
  - Fact 6: `@bind` với enum works in Blazor — `<select @bind="someEnum">` với `<option value="Teen">` auto-convert string→enum
- **Assumptions:**
  - Blazor `<select @bind="enumType">` hoạt động với enum values (verified pattern trong Blazor docs)
- **Open Questions:**
  - Q1: Có cần theme preview (mini color swatch) bên cạnh dropdown? → KHÔNG, mô tả text đủ
- **Recommended Action:** PROCEED

## 8. REVERSE IMPACT ANALYSIS
| File thay đổi | Reverse impact | Mitigation |
|---|---|---|
| `TenantManagement.razor` — EditForm class | Thêm 1 property — không ảnh hưởng existing | Default Classic — backward compatible |
| `TenantManagement.razor` — OpenEditModal | Thêm 1 line `Theme = t.Theme` | `t.Theme` từ Phase 2 TenantApiDto |
| `TenantManagement.razor` — HandleEditSubmit | Thêm 1 field `Theme = _editForm.Theme` vào request | Non-breaking — API accept nullable Theme |
| `TenantManagement.razor` — Edit modal HTML | Thêm 1 form-group block | Chèn trước modal-actions, không phá layout |

## 9. TDD & E2E TESTING STRATEGY
- **Manual verification:**
  - Mở /admin/tenants → Sửa tenant → thấy dropdown 5 theme
  - Chọn Teen → Lưu → mở lại → dropdown hiển thị Teen
- **E2E (optional, Gate 4):**
  - Playwright: login as SystemAdmin → navigate to /admin/tenants → open edit modal → select theme → save → verify API call contains theme
- **Test boundary:**
  - Unit tests: KHÔNG (UI component, không có logic testable)
  - Integration tests: KHÔNG
  - E2E tests: Optional (manual verify acceptable cho Phase 3)

## 10. JIT PLANNING + PURE EXECUTION (MICRO-PHASES)

| Session | JIT Planning | Pure Execution |
|---|---|---|
| **S1** | Confirm dropdown placement (trước ShopInstance, sau BrandStory) | Sửa `EditForm` class + `OpenEditModal` + edit modal HTML + `HandleEditSubmit` |
| **S2** | Verify build + manual check | `dotnet build` + inspect rendered HTML |

### Rules
- 1 session đủ — chỉ thêm 1 dropdown + 1 property + 1 line trong submit
- Build sau S1

## 11. COMPLETION SUMMARY

**Phase 3 COMPLETE** — commit `<HASH>` on `main`.

### Files modified
| File | Change |
|------|--------|
| _TBD_ | _TBD_ |

### Verification
| # | Test | Status | Evidence |
|---|------|--------|----------|
| RV1 | _TBD_ | _TBD_ | _TBD_ |

## 12. ESTIMATED EFFORT
- 2 sessions (S1-S2)
- **BLOCKER:** Phase 2 phải complete trước (TenantApiDto cần có Theme property)
