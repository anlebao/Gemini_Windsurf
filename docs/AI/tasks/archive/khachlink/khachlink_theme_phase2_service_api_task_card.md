# TASK CARD: THEME - PHASE 2 - Service + Gateway API

## 1. GOAL & CONTEXT
- **Mục tiêu cốt lõi:** Thêm `Theme` vào service layer + Gateway API — cho phép SysAdmin update theme và KhachLink đọc theme qua HTTP.
- **Nghiệp vụ áp dụng:** Phase 2 nối Phase 1 (Domain + EF) với Phase 3 (Admin UI) và Phase 4 (KhachLink render). Service xử lý logic persist theme, API expose theme qua 2 endpoints (admin + public).

## 2. ACTIVE WORKFLOW ROUTING
- **Target Workflow:** `.devin/workflows/newfeaturebuild.md`
- **Execution Mode:** IMPLEMENT

## 3. RELEVANT FILES (CONTEXT BOUNDARY)
- **Files được phép đọc/sửa:**
  - `docs/AI/project_state.md` (Bắt buộc đọc đầu phiên)
  - `docs/AI/tasks/khachlink_theme_customization_master_plan.md` (master plan)
  - `3_CoreHub/Services/ITenantManagementService.cs` (SỬA — thêm Theme vào UpdateTenantProfileRequest)
  - `3_CoreHub/Services/TenantManagementService.cs` (SỬA — apply theme trong UpdateProfileAsync)
  - `2_Gateway/Controllers/TenantsController.cs` (SỬA — thêm Theme vào request/response DTO)
  - `2_Gateway/Controllers/TenantStoreController.cs` (SỬA — thêm Theme vào TenantStoreDto)
  - `5_WebApps/ShopERP/Services/TenantApiClient.cs` (SỬA — thêm Theme vào TenantApiDto + request)
- **Boundary Rules (Nghiêm cấm):**
  - KHÔNG sửa Domain layer (Phase 1 đã xong)
  - KHÔNG sửa UI components (Phase 3-4)
  - KHÔNG thêm endpoint mới — chỉ thêm field vào DTO hiện có
  - KHÔNG thay đổi auth policy (TenantsController = SystemAdmin, TenantStoreController = Anonymous)

## 4. TECHNICAL & REGULATORY CONSTRAINTS (HARDENING GATES)
- [ ] **Nullable Theme in request:** `UpdateTenantProfileRequest.Theme` phải là `ThemeType?` (nullable) — null = giữ existing theme, không reset về default
- [ ] **Service preserve existing:** `TenantManagementService.UpdateProfileAsync` đọc `existingSettings?.Theme` nếu request.Theme null — cùng pattern với Latitude/Longitude/SocialLinks
- [ ] **DTO mirror:** `UpdateTenantProfileApiRequest` tồn tại ở cả Gateway và ShopERP — cùng tên, cùng fields. Thêm Theme vào cả 2.
- [ ] **TenantStoreDto anonymous:** `TenantStoreController.GetStoreInfo` là `[AllowAnonymous]` — Theme trả về không cần auth, khách hàng KhachLink đọc được
- [ ] **Enum serialization:** `ThemeType` enum serialize as int (System.Text.Json default) — KhachLink deserialize as `ThemeType` enum (đã `@using VanAn.Shared.Domain`)

## 5. SUCCESS CRITERIA (ĐO LƯỜNG ĐƯỢC)
- [ ] **SC1:** `UpdateTenantProfileRequest` record có `ThemeType? Theme = null` parameter
- [ ] **SC2:** `TenantManagementService.UpdateProfileAsync` apply `request.Theme ?? existingSettings?.Theme ?? ThemeType.Classic` vào new `TenantSettings`
- [ ] **SC3:** Gateway `UpdateTenantProfileApiRequest` có `ThemeType? Theme` property
- [ ] **SC4:** Gateway `TenantsController.UpdateProfile` truyền `Theme = request.Theme` vào `UpdateTenantProfileRequest`
- [ ] **SC5:** Gateway `TenantDto` có `ThemeType Theme` property (response cho admin list)
- [ ] **SC6:** Gateway `MapToDto` set `Theme = t.Settings?.Theme ?? ThemeType.Classic`
- [ ] **SC7:** Gateway `TenantStoreDto` có `ThemeType Theme` property (response cho KhachLink)
- [ ] **SC8:** Gateway `TenantStoreController.MapToStoreDto` set `Theme = t.Settings?.Theme ?? ThemeType.Classic`
- [ ] **SC9:** ShopERP `TenantApiDto` có `ThemeType Theme` property
- [ ] **SC10:** ShopERP `UpdateTenantProfileApiRequest` có `ThemeType? Theme` property
- [ ] **SC11:** `dotnet build VanAn.sln` — 0 errors
- [ ] **SC12:** Unit test: `TenantManagementServiceTests` — update profile với theme Teen → verify DB có theme Teen

**Implementation Date:** 2026-07-22
**Branch:** `main`

## 6. ACTIVE SKILLS (MAX 3)
- `domain-integrity-validation` — Verify service preserve-existing pattern
- `build-error-analysis` — Nếu DTO mismatch giữa Gateway và ShopERP
- `pattern-based-fixing` — Apply existing DTO mirror pattern

## 7. AI HEALTH CHECK MATRIX (INITIAL)
- **Evidence Count:** 7
- **Verified Facts:**
  - Fact 1: `UpdateTenantProfileRequest` (ITenantManagementService.cs line 50-61) — record với 11 params, tất cả nullable trừ Name
  - Fact 2: `TenantManagementService.UpdateProfileAsync` (line 66-96) — pattern `request.Latitude ?? existingSettings?.Latitude` cho preserve-existing
  - Fact 3: `TenantsController.UpdateProfile` (line 40-62) — map `UpdateTenantProfileApiRequest` → `UpdateTenantProfileRequest`, truyền từng field
  - Fact 4: `TenantsController.MapToDto` (line 111-129) — map Tenant → TenantDto, đọc `t.Settings?.FieldName`
  - Fact 5: `TenantStoreController.MapToStoreDto` (line 182-196) — map Tenant → TenantStoreDto, đọc `t.Settings?.FieldName`
  - Fact 6: `TenantApiClient.cs` có `TenantApiDto` (line 57-78) + `UpdateTenantProfileApiRequest` (line 80-94) — mirror Gateway DTOs
  - Fact 7: `TenantManagementServiceTests.cs` line 156 — existing test cho UpdateProfileAsync
- **Assumptions:**
  - System.Text.Json serialize `ThemeType` enum as int (default) — KhachLink deserialize đúng
- **Open Questions:**
  - Q1: Có cần thêm `Theme` vào `CreateTenantRequest` không? → KHÔNG, tenant mới luôn default Classic, đổi sau qua edit
- **Recommended Action:** PROCEED

## 8. REVERSE IMPACT ANALYSIS
| File thay đổi | Reverse impact | Mitigation |
|---|---|---|
| `ITenantManagementService.cs` | `UpdateTenantProfileRequest` record thêm param — caller phải update | Default `Theme = null` — caller cũ không cần thay đổi |
| `TenantManagementService.cs` | `UpdateProfileAsync` logic thêm theme handling | Pattern ?? existing — cùng style với fields hiện có |
| `TenantsController.cs` | DTO thêm field — JSON serialization thêm key | Non-breaking — client cũ ignore extra field |
| `TenantStoreController.cs` | `TenantStoreDto` thêm field — KhachLink cần update ShopDto | Phase 4 sẽ update KhachLink ShopDto |
| `TenantApiClient.cs` | DTO mirror — phải khớp Gateway | Thêm cùng field `ThemeType? Theme` |

## 9. TDD & E2E TESTING STRATEGY
- **Unit test — UpdateProfileAsync với theme:**
  - Update profile với `Theme = Teen` → verify `tenant.Settings.Theme == Teen`
  - Update profile với `Theme = null` → verify theme không đổi (preserve existing)
- **Unit test — existing tests:**
  - `TenantManagementServiceTests` — tất cả existing tests vẫn pass
- **Test boundary:**
  - Unit tests: `TenantManagementService` (Core.Tests)
  - Integration tests: KHÔNG (Phase 4 sẽ test end-to-end)
  - E2E tests: KHÔNG

## 10. JIT PLANNING + PURE EXECUTION (MICRO-PHASES)

| Session | JIT Planning | Pure Execution |
|---|---|---|
| **S1** | Confirm nullable pattern (?? existing) | Sửa `ITenantManagementService.cs` + `TenantManagementService.cs` |
| **S2** | Confirm DTO mirror giữa Gateway + ShopERP | Sửa `TenantsController.cs` + `TenantStoreController.cs` + `TenantApiClient.cs` |
| **S3** | Verify build + unit tests | `dotnet build` + unit tests |

### Rules
- DTO mirror phải khớp EXACT — cùng property name, cùng type
- Nullable `ThemeType?` ở request, non-nullable `ThemeType` ở response (default Classic)

## 11. COMPLETION SUMMARY

**Phase 2 COMPLETE** — commit `<HASH>` on `main`.

### Files modified
| File | Change |
|------|--------|
| _TBD_ | _TBD_ |

### Verification
| # | Test | Status | Evidence |
|---|------|--------|----------|
| RV1 | _TBD_ | _TBD_ | _TBD_ |

## 12. ESTIMATED EFFORT
- 3 sessions (S1-S3)
- **BLOCKER:** Phase 1 phải complete trước
