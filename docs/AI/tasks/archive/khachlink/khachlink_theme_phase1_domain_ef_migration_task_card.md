# TASK CARD: THEME - PHASE 1 - Domain + EF + Migration

## 1. GOAL & CONTEXT
- **Mục tiêu cốt lõi:** Thêm `ThemeType Theme` vào `TenantSettings` (Domain), map EF Core column `Settings_Theme`, tạo migration thêm cột vào PostgreSQL.
- **Nghiệp vụ áp dụng:** Foundation cho feature "SysAdmin chọn 1 trong 5 phong cách giao diện cho tenant". Phase 1 là backend foundation — không có UI thay đổi, chỉ thêm data model + persistence.

## 2. ACTIVE WORKFLOW ROUTING
- **Target Workflow:** `.devin/workflows/newfeaturebuild.md`
- **Execution Mode:** IMPLEMENT (plan đã approved trong master plan)

## 3. RELEVANT FILES (CONTEXT BOUNDARY)
- **Files được phép đọc/sửa:**
  - `docs/AI/project_state.md` (Bắt buộc đọc đầu phiên)
  - `docs/AI/tasks/khachlink_theme_customization_master_plan.md` (master plan reference)
  - `1_Shared/Domain/Aggregates/TenantAggregate/TenantSettings.cs` (SỬA — thêm Theme property + WithTheme)
  - `3_CoreHub/Infrastructure/Configurations/TenantConfiguration.cs` (SỬA — map Settings_Theme column)
  - `3_CoreHub/Infrastructure/Migrations/` (TẠO — AddTenantTheme migration, auto-generated)
- **Boundary Rules (Nghiêm cấm):**
  - KHÔNG sửa `Tenant.cs` aggregate root (chỉ sửa `TenantSettings` value object)
  - KHÔNG sửa `ShopConfig` record (đã có `ActiveTheme` — không cần thêm)
  - KHÔNG sửa service/controller/UI (Phase 2-4)
  - KHÔNG thêm enum value vào `ThemeType` (giữ 5 giá trị hiện có)
  - KHÔNG xoá `With*` methods hiện có trong `TenantSettings`

## 4. TECHNICAL & REGULATORY CONSTRAINTS (HARDENING GATES)
- [ ] **Domain purity:** `TenantSettings` là value object trong Domain — KHÔNG có EF Core attributes, KHÔNG có `[Column]`, `[Table]`. Mapping nằm ở `TenantConfiguration.cs` (Infrastructure).
- [ ] **Immutable pattern:** `TenantSettings` dùng `With*` methods (return new instance) — `WithTheme()` phải tuân thủ pattern, KHÔNG mutate trực tiếp.
- [ ] **Constructor sync:** Thêm `theme` parameter vào constructor `TenantSettings(...)` — phải cập nhật TẤT CẢ `With*` methods hiện có (chúng gọi `new TenantSettings(...)` với positional args).
- [ ] **EF enum conversion:** `ThemeType` enum → int via `.HasConversion<int>()` — cùng pattern với `BusinessType`, `HKDGroup`, `Status`.
- [ ] **Migration default:** `Settings_Theme` column phải có `DEFAULT 0` (Classic) — tenant cũ không break. PG 11+ fast default, không lock table.
- [ ] **EF parameterless constructor:** `TenantSettings` có `private TenantSettings() { }` cho EF — KHÔNG xoá. EF dùng parameterless constructor khi load từ DB, rồi set properties via reflection.
- [ ] **Theme property setter:** `public ThemeType Theme { get; private set; }` — `private set` để EF reflection set được, nhưng code không mutate trực tiếp.

## 5. SUCCESS CRITERIA (ĐO LƯỜNG ĐƯỢC)
- [ ] **SC1:** `TenantSettings` có property `public ThemeType Theme { get; private set; } = ThemeType.Classic;`
- [ ] **SC2:** `TenantSettings` constructor nhận `ThemeType theme = ThemeType.Classic` parameter (default = backward compatible)
- [ ] **SC3:** `WithTheme(ThemeType theme)` method trả về `TenantSettings` mới với theme updated, giữ nguyên các field khác
- [ ] **SC4:** TẤT CẢ `With*` methods hiện có (`WithContactEmail`, `WithContactPhone`, `WithAddress`, `WithTaxCode`, `WithCoordinates`, `WithSlug`, `WithSocialLinks`, `WithBrandStory`) truyền `Theme` vào `new TenantSettings(...)` — không mất theme khi update field khác
- [ ] **SC5:** `TenantConfiguration.cs` map `settings.Property(s => s.Theme).HasColumnName("Settings_Theme").HasConversion<int>().HasDefaultValue(ThemeType.Classic);`
- [ ] **SC6:** Migration `AddTenantTheme` tạo cột `Settings_Theme` (integer, NOT NULL, DEFAULT 0)
- [ ] **SC7:** `dotnet build VanAn.sln` — 0 errors, 0 warnings
- [ ] **SC8:** `guard-check.ps1` PASS (Windsurf Guard, Architecture Guard, Roslyn Analyzers)
- [ ] **SC9:** Unit test `TenantManagementServiceTests` — existing tests vẫn pass (không break)
- [ ] **SC10:** `TenantSettings.Empty()` static factory trả về `Theme = Classic` (default)

**Implementation Date:** 2026-07-22
**Branch:** `main`

## 6. ACTIVE SKILLS (MAX 3)
- `domain-integrity-validation` — Verify TenantSettings immutable pattern + constructor sync
- `build-error-analysis` — Nếu build fail sau khi thêm Theme property
- `pattern-based-fixing` — Apply known EF enum conversion pattern

## 7. AI HEALTH CHECK MATRIX (INITIAL)
- **Evidence Count:** 8
- **Verified Facts:**
  - Fact 1: `TenantSettings` có 8 `With*` methods, tất cả gọi `new TenantSettings(email, phone, address, logoUrl, taxCode, lat, lng, slug, socialFb, socialTiktok, brandStory)` — positional args (verified `TenantSettings.cs` line 62-84)
  - Fact 2: `TenantSettings` có `private TenantSettings() { }` cho EF (line 34) + public constructor với 11 params (line 36-60)
  - Fact 3: `TenantConfiguration.cs` line 52-70: `OwnsOne(e => e.Settings, settings => { ... })` — map 9 properties, chưa có Theme
  - Fact 4: `ThemeType` enum có 5 giá trị: Classic=0, Modern=1, Teen=2, Lady=3, Premium=4 (`Domain.cs` line 1327-1334)
  - Fact 5: `ShopConfig` record đã có `ActiveTheme` property (line 1308) — không cần thêm
  - Fact 6: EF enum conversion pattern: `.HasConversion<int>()` — dùng cho `BusinessType` (line 33), `HKDGroup` (line 37), `Status` (line 46)
  - Fact 7: `TenantSettings.Empty()` (line 86) trả về `new(null, null, null)` — cần thêm theme default
  - Fact 8: Migration command: `dotnet ef migrations add AddTenantTheme --project 3_CoreHub --startup-project 2_Gateway`
- **Assumptions:**
  - PostgreSQL hỗ trợ fast default cho ADD COLUMN NOT NULL DEFAULT (PG 11+ — VPS chạy PG 16)
  - EF Core 8 auto-generate migration đúng từ model diff
- **Open Questions:**
  - Q1: Có cần update `TenantSettings.Empty()` để nhận theme parameter không? → Có, thêm default `ThemeType.Classic`
- **Recommended Action:** PROCEED — evidence sufficient, no architectural risk

## 8. REVERSE IMPACT ANALYSIS
| File thay đổi | Reverse impact | Mitigation |
|---|---|---|
| `TenantSettings.cs` | Tất cả code tạo `new TenantSettings(...)` phải truyền theme (hoặc dùng default) | Default param `theme = ThemeType.Classic` — caller cũ không cần thay đổi |
| `TenantSettings.cs` — `With*` methods | 8 methods phải truyền `Theme` vào `new TenantSettings(...)` | Thêm `Theme` vào cuối positional args — mechanical change |
| `TenantConfiguration.cs` | Thêm 1 property mapping — không ảnh hưởng mapping hiện có | Thêm vào cuối `OwnsOne` block, không sửa existing lines |
| Migration `AddTenantTheme` | Thêm cột vào PostgreSQL `Tenants` table | `DEFAULT 0` — tenant cũ giữ Classic, không downtime |

## 9. TDD & E2E TESTING STRATEGY
- **Unit test — TenantSettings.WithTheme:**
  - Tạo `TenantSettings` với theme Classic → `WithTheme(Teen)` → verify `Theme == Teen`, các field khác không đổi
  - Tạo `TenantSettings` với theme Premium → `WithBrandStory("...")` → verify `Theme == Premium` (không mất theme khi update field khác)
- **Unit test — TenantSettings.Empty:**
  - `TenantSettings.Empty().Theme == ThemeType.Classic`
- **Unit test — existing TenantManagementServiceTests:**
  - Tất cả tests hiện có vẫn pass (không break)
- **Test boundary:**
  - Unit tests: `TenantSettings` value object + `TenantManagementService` (existing)
  - Integration tests: KHÔNG (Phase 2 sẽ test API)
  - E2E tests: KHÔNG (Phase 4 sẽ test UI)

## 10. JIT PLANNING + PURE EXECUTION (MICRO-PHASES)

### Chiến lược thực thi: JIT Planning + Pure Execution

Phase 1 là mechanical change — thêm 1 property + 1 method + update 8 `With*` methods + 1 EF mapping + 1 migration. Không có architectural decision, không có ambiguity. Pure Execution.

### Micro-phase breakdown cho Phase 1

| Session | JIT Planning (chốt gì) | Pure Execution (viết gì) |
|---|---|---|
| **S1** | Confirm constructor signature (thêm `theme` ở cuối, default Classic) | Sửa `TenantSettings.cs`: thêm property + constructor param + `WithTheme()` + update 8 `With*` methods + `Empty()` |
| **S2** | Confirm EF mapping syntax (`.HasConversion<int>()` + `.HasDefaultValue()`) | Sửa `TenantConfiguration.cs`: thêm `Settings_Theme` mapping |
| **S3** | Confirm migration command + startup project | Run `dotnet ef migrations add AddTenantTheme` |
| **S4** | Verify build + tests | `dotnet build VanAn.sln` + `guard-check.ps1` + unit tests |

### Rules
- Mỗi session chỉ chốt 1 decision rồi execute — không phân tích quá mức
- Build sau mỗi session — bắt lỗi sớm
- Migration chỉ tạo sau khi EF config đúng — tránh migration sai

## 11. COMPLETION SUMMARY

**Phase 1 COMPLETE** — commit `<HASH>` on `main`.

### Files created
| File | Purpose |
|------|---------|
| _TBD_ | _TBD_ |

### Files modified
| File | Change |
|------|--------|
| _TBD_ | _TBD_ |

### Issues fixed during implementation
- _TBD_

### Verification

#### Static Verification (compile-time)
- **Build:** _TBD_
- **Unit tests:** _TBD_
- **guard-check.ps1:** _TBD_

#### Live Runtime Verification (boot + HTTP + UI)
> **Lesson learned (Wave 0):** Build + Architecture Tests + guard-check PASS ≠ runtime works.
> Live runtime verification is MANDATORY for all phases.

| # | Test | Status | Evidence |
|---|------|--------|----------|
| RV1 | _TBD_ | _TBD_ | _TBD_ |

## 12. ESTIMATED EFFORT
- 4 sessions theo JIT Planning (S1-S4)
- **BLOCKER:** None — mechanical change, no architectural risk
