# TASK CARD: KhachLink Full Flow — Wave 0 — Module Toggle Infrastructure ✅ COMPLETE

## 1. GOAL & CONTEXT
- **Mục tiêu cốt lõi:** Tạo Shop Settings page + toggle storage + logic read/write cho 6 module toggles. Đây là nền tảng BLOCKING cho mọi wave sau (W1-W4).
- **Nghiệp vụ áp dụng:** Section 3 (Module Toggles) của `Tai_lieu_yeu_cau_nghiep_vu_Khachlink.md` v1.2
- **Status:** ✅ COMPLETE — commit `999d5d8` on `feature/khachlink-flow-wave0-toggle-infrastructure`
- **Branch:** `feature/khachlink-flow-wave0-toggle-infrastructure`
- **Tech Debt:** TD-KL-12 (High) — RESOLVED

---

## 2. ACTIVE WORKFLOW ROUTING
- **Target Workflow:** `newfeaturebuild.md` (ANALYZE → IMPLEMENT)
- **Execution Mode:** ANALYZE → IMPLEMENT
- **Current Phase:** Wave 0 of 5
- **Dependency:** None (first wave — BLOCKING cho W1-W4)

---

## 3. RELEVANT FILES (CONTEXT BOUNDARY)

### Files được phép đọc/sửa
- `docs/AI/project_state.md` (Bắt buộc đọc đầu phiên)
- `docs/AI/tasks/khachlink_full_flow_master_plan.md` (READ — master plan)
- `docs/MVP_Product/Tai_lieu_yeu_cau_nghiep_vu_Khachlink.md` (READ — requirements v1.2)

### Files cần CREATE
- `3_CoreHub/Services/IShopFeatureSettingsService.cs` — interface
- `3_CoreHub/Services/ShopFeatureSettingsService.cs` — implementation (read/write toggles per tenant)
- `5_WebApps/ShopERP/Controllers/ShopSettingsController.cs` — API endpoints
- `5_WebApps/ShopERP/Components/Pages/Settings/ShopFeatures.razor` — ShopERP Settings UI
- `5_WebApps/KhachLink/Services/Http/ShopFeatureSettingsHttpService.cs` — KhachLink HTTP service

### Files cần MODIFY
- `5_WebApps/ShopERP/Program.cs` — DI registration + default seed
- `5_WebApps/KhachLink/Program.cs` — DI registration (HTTP service)
- `6_Tests/VanAn.Integration.Tests/KhachLinkStartupTests.cs` — assertion

### Files READ ONLY (investigate patterns)
- `1_Shared/Domain.cs` — check existing Shop/Tenant entity structure
- `3_CoreHub/Infrastructure/IVanAnDbContext.cs` — check DbSet availability
- `5_WebApps/ShopERP/Components/Pages/Settings/` — check existing settings pages (if any) for UI patterns
- `5_WebApps/KhachLink/Services/Http/` — check existing HTTP service patterns
- `UI.Platform/` — check VanAnButton, VanAnForm, VanAToggle components

### Boundary Rules
- KHÔNG sửa `1_Shared/Domain.cs` trừ khi cần thêm entity (phải approval)
- KHÔNG tạo UI custom HTML/CSS — dùng UI Platform components
- KHÔNG inject CoreHub services có repository dependencies vào KhachLink — dùng HTTP service
- KHÔNG implement bypass logic trong services (đó là W1-W3) — Wave 0 chỉ tạo infrastructure

---

## 4. TECHNICAL CONSTRAINTS
- [ ] **Domain Protection:** Nếu cần entity mới, report as potential Domain Modeling — ưu tiên dùng Infrastructure entity (xem precedent: `AccountChartEntity`, `PlatformUser`)
- [ ] **UI Platform:** Shop Settings page MUST dùng VanAnButton, VanAnForm, VanAToggle (hoặc tương đương) — KHÔNG custom HTML
- [ ] **KhachLink HTTP-only:** `ShopFeatureSettingsHttpService` gọi Gateway/ShopERP qua HttpClient — KHÔNG inject DbContext
- [ ] **Multi-tenancy:** Toggles lưu per tenant (mỗi shop có settings riêng)
- [ ] **DI Checklist:** Mỗi service mới vào KhachLink → (1) DI trong Program.cs, (2) assertion trong KhachLinkStartupTests
- [ ] **Default seed:** kitchen=ON, loyalty=ON, accounting=ON, QR_table=OFF, voice=OFF, einvoice=OFF

---

## 5. SUCCESS CRITERIA
- [x] **SC1:** `IShopFeatureSettingsService` + implementation tồn tại, read/write 6 toggles per tenant
- [x] **SC2:** API `GET /api/shop/settings/features` trả về 6 toggles
- [x] **SC3:** API `PUT /api/shop/settings/features` cập nhật toggles
- [x] **SC4:** ShopERP Settings page hiển thị 6 toggle switches (UI Platform — VanAForm + form-check form-switch)
- [x] **SC5:** KhachLink `ShopFeatureSettingsHttpService` fetch được toggles qua HTTP
- [x] **SC6:** Default seed: 6 toggles với giá trị mặc định
- [x] **SC7:** KhachLinkStartupTests assertion pass
- [x] **SC8:** Build: 0 errors
- [x] **SC9:** guard-check.ps1 ALL CHECKS PASSED
- [x] **SC10:** Architecture Tests 38/38 PASS (W12-S3 [Authorize] fix)

---

## 6. DETAILED IMPLEMENTATION

### 6.1. ANALYZE Phase (trước khi code)

**Cần investigate:**
1. **Toggle storage strategy:** 2 options:
   - **Option A:** Thêm JSON column `FeatureSettings` vào existing `Shop` hoặc `Tenant` entity → không cần table mới
   - **Option B:** Tạo table `ShopFeatureSettings` mới (Infrastructure entity, không phải Domain entity)
   - **Investigate:** Đọc `1_Shared/Domain.cs` để check `Shop` / `Tenant` entity structure → quyết định Option A hay B

2. **UI Platform components:** Đọc `UI.Platform/` để check có VanAToggle / VanAnSwitch component không. Nếu chưa có → dùng VanAnForm với checkbox hoặc tạo VanAToggle mới.

3. **Existing settings pages:** Check `5_WebApps/ShopERP/Components/Pages/Settings/` có page nào chưa — follow existing pattern nếu có.

4. **KhachLink HTTP service pattern:** Đọc 1 file trong `5_WebApps/KhachLink/Services/Http/` (ví dụ `ProductsHttpService.cs`) để follow pattern.

5. **API auth:** Check `ShopSettingsController` cần auth gì (Admin role? Tenant-scoped?)

### 6.2. Toggle Storage (W0-T1)

**6 toggles cần lưu:**
```csharp
public class ShopFeatureSettings
{
    public bool QR_TableNumber_Enabled { get; set; } = false;
    public bool Kitchen_Workflow_Enabled { get; set; } = true;
    public bool Voice_Note_Enabled { get; set; } = false;
    public bool Loyalty_Program_Enabled { get; set; } = true;
    public bool Accounting_Sync_Enabled { get; set; } = true;
    public bool EInvoice_Auto_Export_Enabled { get; set; } = false;
}
```

**Storage decision:** Sau ANALYZE, chọn Option A (JSON column) hoặc Option B (separate table).

**If Option A (JSON column on Shop/Tenant):**
- Thêm property `FeatureSettings` (string JSON) vào Shop entity qua EF Core fluent config (KHÔNG sửa Domain — thêm column qua Infrastructure config)
- Hoặc: nếu Domain entity đã có `Settings` hoặc `Metadata` field → dùng nó

**If Option B (separate table):**
- Tạo `ShopFeatureSettingsEntity` trong `3_CoreHub/Infrastructure/Entities/`
- Add DbSet vào `IVanAnDbContext` (business — không phải accounting)
- EF Core configuration trong `3_CoreHub/Infrastructure/Configurations/`

### 6.3. Service Layer (W0-T2)

**File:** `3_CoreHub/Services/IShopFeatureSettingsService.cs` (NEW)
```csharp
public interface IShopFeatureSettingsService
{
    Task<ShopFeatureSettingsDto> GetSettingsAsync(Guid tenantId, CancellationToken ct = default);
    Task UpdateSettingsAsync(Guid tenantId, ShopFeatureSettingsDto settings, CancellationToken ct = default);
    Task<bool> IsEnabledAsync(Guid tenantId, string toggleName, CancellationToken ct = default);
}
```

**File:** `3_CoreHub/Services/ShopFeatureSettingsService.cs` (NEW)
- Inject `IVanAnDbContext` (business)
- Read/write toggles per tenant
- `IsEnabledAsync` — helper cho các service khác check toggle state (sẽ dùng trong W1-W3)

### 6.4. API Endpoints (W0-T5)

**File:** `5_WebApps/ShopERP/Controllers/ShopSettingsController.cs` (NEW)
```csharp
[ApiController]
[Route("api/shop/settings")]
[Authorize] // Admin role + tenant-scoped
public class ShopSettingsController : ControllerBase
{
    [HttpGet("features")]
    public async Task<ActionResult<ShopFeatureSettingsDto>> GetFeatures() { ... }

    [HttpPut("features")]
    public async Task<IActionResult> UpdateFeatures([FromBody] ShopFeatureSettingsDto dto) { ... }
}
```

**Gateway forwarding:** Check `2_Gateway/Program.cs` YARP config — có cần thêm route cho `/api/shop/settings/{*path}` không.

### 6.5. ShopERP Settings UI (W0-T4)

**File:** `5_WebApps/ShopERP/Components/Pages/Settings/ShopFeatures.razor` (NEW)

**UI Platform components:**
- `VanAnCard` — container
- `VanAnForm` — form layout
- Toggle switches (VanAToggle nếu có, hoặc VanAnButton toggle pattern)
- `VanAnButton` — Save button

**Layout:**
```
┌─────────────────────────────────────────┐
│  Cấu hình tính năng Shop                │
├─────────────────────────────────────────┤
│  ☐ Hiển thị số bàn trong QR Code        │
│  ☑ Bật luồng nhà bếp / pha chế          │
│  ☐ Bật ghi chú giọng nói (STT + TTS)    │
│  ☑ Bật chương trình điểm thưởng         │
│  ☑ Đồng bộ kế toán HKD tự động          │
│  ☐ Tự động xuất hóa đơn điện tử         │
├─────────────────────────────────────────┤
│  [Lưu cấu hình]                         │
└─────────────────────────────────────────┘
```

### 6.6. KhachLink HTTP Service (W0-T6)

**File:** `5_WebApps/KhachLink/Services/Http/ShopFeatureSettingsHttpService.cs` (NEW)

```csharp
public class ShopFeatureSettingsHttpService : IShopFeatureSettingsService
{
    private readonly HttpClient _http;
    // Fetch toggles via Gateway: GET /api/shop/settings/features
    // Cache trong localStorage + refresh on app start
}
```

**Interface:** Dùng cùng `IShopFeatureSettingsService` hoặc tạo `IShopFeatureSettingsHttpService` riêng (follow existing KhachLink pattern — check `Services/Http/` folder).

### 6.7. DI Registration (W0-T3)

**ShopERP `Program.cs`:**
```csharp
_ = builder.Services.AddScoped<IShopFeatureSettingsService, ShopFeatureSettingsService>();
```

**KhachLink `Program.cs`:**
```csharp
_ = builder.Services.AddScoped<IShopFeatureSettingsHttpService, ShopFeatureSettingsHttpService>();
// hoặc IShopFeatureSettingsService nếu dùng cùng interface
```

### 6.8. Default Seed (W0-T8)

**File:** `5_WebApps/ShopERP/Program.cs` (seed block — cùng khu vực với default tenant seed)

```csharp
// Seed default feature settings for default tenant
if (!await db.ShopFeatureSettings.AnyAsync(s => s.TenantId == seedTenantId))
{
    db.ShopFeatureSettings.Add(new ShopFeatureSettingsEntity
    {
        TenantId = seedTenantId,
        QR_TableNumber_Enabled = false,
        Kitchen_Workflow_Enabled = true,
        Voice_Note_Enabled = false,
        Loyalty_Program_Enabled = true,
        Accounting_Sync_Enabled = true,
        EInvoice_Auto_Export_Enabled = false
    });
    await db.SaveChangesAsync();
}
```

### 6.9. KhachLinkStartupTests (W0-T7)

**File:** `6_Tests/VanAn.Integration.Tests/KhachLinkStartupTests.cs`

```csharp
Assert.NotNull(sp.GetRequiredService<IShopFeatureSettingsHttpService>());
```

---

## 7. AI HEALTH CHECK MATRIX

### Pre-ANALYZE
- **Evidence Count:** 3
- **Verified Facts:**
  - Fact 1: 6 toggles cần thiết (từ requirements v1.2 Section 3)
  - Fact 2: KhachLink dùng HTTP services (pattern: `Services/Http/` folder)
  - Fact 3: ShopERP hosts in-process CoreHub services (Option B)
- **Assumptions:**
  - Assumption 1: `Shop` hoặc `Tenant` entity đã có field có thể lưu JSON settings (Cần verify trong ANALYZE)
  - Assumption 2: UI Platform có toggle/switch component (Cần verify)
- **Open Questions:**
  - Q1: Option A (JSON column) hay Option B (separate table) cho toggle storage?
  - Q2: UI Platform có VanAToggle component chưa?
- **Gate check:** Assumptions (2) >= Verified Facts (3) → ❌ CHƯA được sửa code, cần INVESTIGATE thêm

### Post-ANALYZE (subagent 694af2f9 — 6 questions answered)
- **Evidence Count:** 6
- **Verified Facts:**
  - Fact 1: `Shop` entity KHÔNG có Settings/Metadata field. `Tenant` có `TenantSettings` (value object: ContactEmail, Phone, Address, LogoUrl, TaxCode) — không phù hợp cho toggles
  - Fact 2: UI Platform KHÔNG có VanAToggle/VanAnSwitch. `DynamicFormFields` hỗ trợ `FieldType.Checkbox`. `VanAStatusForm` dùng HTML checkbox trong VanAForm
  - Fact 3: ShopERP không có Settings folder. Pattern: direct service injection + VanAForm + UI Platform components
  - Fact 4: KhachLink HTTP service pattern: class only (no interface), `IHttpClientFactory` named "gateway", path `shoperp/api/*`, try-catch return empty
  - Fact 5: YARP routes: `shoperp/{**catch-all}` → shoperp-cluster. Gateway controllers handle `/api/*` directly. NO new route needed
  - Fact 6: ShopERP DI pattern: `AddScoped<Interface, Implementation>()`. Seed block at lines 479-598
- **Decisions:**
  - **D1:** Option B (separate Infrastructure entity `ShopFeatureSettingsEntity`) — giữ Domain pure, precedent `AccountChartEntity` + `PeriodClosingStatusEntity`
  - **D2:** UI: VanAForm + HTML `form-check form-switch` (follow `VanAStatusForm` pattern)
  - **D3:** API: ShopERP controller (YARP forwarding `shoperp/api/*`)
- **Gate check:** Assumptions (0) < Verified Facts (6) → ✅ OK để proceed IMPLEMENT

---

## 8. REVERSE IMPACT ANALYSIS
| File thay đổi | Reverse impact | Mitigation |
|---|---|---|
| `IShopFeatureSettingsService.cs` (new) | No impact — new interface | None |
| `ShopFeatureSettingsService.cs` (new) | No impact — new service | None |
| `ShopSettingsController.cs` (new) | No impact — new controller | None |
| `ShopFeatures.razor` (new) | No impact — new page | None |
| `ShopFeatureSettingsHttpService.cs` (new) | No impact — new service | None |
| `Program.cs` (ShopERP) — DI + seed | Low — additive only | None |
| `Program.cs` (KhachLink) — DI | Low — additive only | KhachLinkStartupTests sẽ catch nếu thiếu |
| `KhachLinkStartupTests.cs` — assertion | Low — additive | None |
| `IVanAnDbContext.cs` (if Option B) | Medium — new DbSet | Build sẽ catch miss |

---

## 9. EXECUTION CHECKLIST

### ANALYZE Phase
- [x] Read `1_Shared/Domain.cs` — check Shop/Tenant entity (Option A vs B decision)
- [x] Read `UI.Platform/` — check toggle/switch component availability
- [x] Read existing `5_WebApps/ShopERP/Components/Pages/Settings/` — check patterns
- [x] Read 1 file in `5_WebApps/KhachLink/Services/Http/` — check HTTP service pattern
- [x] Read `2_Gateway/Program.cs` — check YARP routing (need new route?)
- [x] Decision: Option B (separate Infrastructure entity)
- [x] Update Health Check Matrix (Assumptions 0 < Verified Facts 6)

### IMPLEMENT Phase
- [x] W0-T1: Toggle storage (`ShopFeatureSettingsEntity` + `ShopFeatureSettingsConfiguration`)
- [x] W0-T2: Service layer (`IShopFeatureSettingsService` + `ShopFeatureSettingsService`)
- [x] W0-T3: DI registration (ShopERP + KhachLink + 3 DbContexts)
- [x] W0-T4: ShopERP Settings UI (VanAForm + 6 form-switch toggles)
- [x] W0-T5: API endpoints (`ShopSettingsController` — GET/PUT `/api/shop/settings/features`)
- [x] W0-T6: KhachLink HTTP service (`ShopFeatureSettingsHttpService`)
- [x] W0-T7: KhachLinkStartupTests assertion
- [x] W0-T8: Default seed (Program.cs lines 618-627)
- [x] W0-T9: Build 0 errors + guard-check.ps1 ALL CHECKS PASSED + Architecture Tests 38/38

### Post-IMPLEMENT
- [x] Commit: `[KL WAVE 0] Module toggle infrastructure — 6 toggles + Shop Settings UI` (`999d5d8`)
- [x] Update `project_state.md` (Section 2, 3, 4, 9)

---

## 10. COMPLETION SUMMARY

**Wave 0 COMPLETE** — commit `999d5d8` on `feature/khachlink-flow-wave0-toggle-infrastructure`.

### Files created (7)
| File | Purpose |
|------|---------|
| `3_CoreHub/Infrastructure/Entities/ShopFeatureSettingsEntity.cs` | Tenant-scoped entity (BaseEntity), 6 toggles, factory + UpdateToggles |
| `3_CoreHub/Infrastructure/Configurations/ShopFeatureSettingsConfiguration.cs` | EF config — unique index TenantId, default values |
| `3_CoreHub/Services/IShopFeatureSettingsService.cs` | Interface + ShopFeatureSettingsDto (6 toggle properties) |
| `3_CoreHub/Services/ShopFeatureSettingsService.cs` | Implementation — Get/Update/IsEnabled via IVanAnDbContext |
| `5_WebApps/ShopERP/Controllers/ShopSettingsController.cs` | API GET/PUT `/api/shop/settings/features` ([Authorize]) |
| `5_WebApps/ShopERP/Components/Pages/Settings/ShopFeatures.razor` | UI — VanAForm + 6 form-switch toggles + Save button |
| `5_WebApps/KhachLink/Services/Http/ShopFeatureSettingsHttpService.cs` | KhachLink HTTP service — Get/Update/IsEnabled via Gateway |

### Files modified (6)
| File | Change |
|------|--------|
| `3_CoreHub/Infrastructure/IVanAnDbContext.cs` | +DbSet<ShopFeatureSettingsEntity> ShopFeatureSettings |
| `3_CoreHub/Infrastructure/VanAnDbContext.cs` | +DbSet<ShopFeatureSettingsEntity> ShopFeatureSettings |
| `5_WebApps/ShopERP/Infrastructure/ShopERPDbContext.cs` | +DbSet<ShopFeatureSettingsEntity> ShopFeatureSettings |
| `5_WebApps/ShopERP/Program.cs` | +DI registration (line 155) + default seed (lines 618-627) |
| `5_WebApps/KhachLink/Program.cs` | +DI registration (line 105) |
| `6_Tests/VanAn.Integration.Tests/KhachLinkStartupTests.cs` | +assertion ShopFeatureSettingsHttpService |

### Verification

#### Static Verification (compile-time)
- **Build:** 0 errors ✅
- **Architecture Tests:** 38/38 PASS ✅ (W12-S3 [Authorize] fix applied)
- **guard-check.ps1:** ALL CHECKS PASSED ✅
- **13 files changed, 526 insertions**

#### Live Runtime Verification (boot + HTTP + UI) — commit `3d0e72f`
> **Lesson learned:** Build + Architecture Tests + guard-check PASS ≠ runtime works.
> Wave 0 initially passed all static checks but failed at runtime due to (1) missing EF migration
> and (2) LINQ translation error. Live runtime verification is MANDATORY for all waves.

| # | Test | Status | Evidence |
|---|------|--------|----------|
| RV1 | Boot infra (Docker PostgreSQL 5432 + NATS 4222 + Seq) | ✅ | `vanan-pg-local Up 0.0.0.0:5432->5432/tcp` |
| RV2 | ShopERP start on http://localhost:5003 | ✅ | `Now listening on: http://localhost:5003` |
| RV3 | EF Migration applied | ✅ | `Applying migration '20260711143852_AddShopFeatureSettingsTable'` + `CREATE TABLE "ShopFeatureSettings"` |
| RV4 | Default seed inserted | ✅ | `KL W0: Shop feature settings seeded for tenant 00000000-0000-0000-0000-000000000001` |
| RV5 | DevLogin admin | ✅ | `Login status: 200` + cookie `.VanAn.Auth` issued |
| RV6 | GET `/api/shop/settings/features?tenantId=...` | ✅ 200 | `{"qR_TableNumber_Enabled":false,"kitchen_Workflow_Enabled":true,"voice_Note_Enabled":false,"loyalty_Program_Enabled":true,"accounting_Sync_Enabled":true,"eInvoice_Auto_Export_Enabled":false}` |
| RV7 | PUT `/api/shop/settings/features?tenantId=...` (QR false→true) | ✅ 200 | Response confirms `qR_TableNumber_Enabled:true` |
| RV8 | GET after PUT (persist check) | ✅ 200 | `qR_TableNumber_Enabled:true` — persisted to SQLite |
| RV9 | UI `/settings/shop-features` renders | ✅ 200 | HTTP 200 + HTML returned |
| RV10 | UI 6 toggle switches present | ✅ | 6× `form-check-input` + 6× `form-switch` + 4× `checked` (Kitchen, Loyalty, Accounting, QR after PUT) |
| RV11 | UI Platform components used | ✅ | VanACard + VanAAlert + VanAButton (no custom HTML) |
| RV12 | UI Save button present | ✅ | "Lưu cấu hình" button rendered |

**Live verification protocol executed:**
1. `Start-Process Docker Desktop` → wait `docker info` OK
2. `docker run -d --name vanan-pg-local -p 5432:5432 postgres:16-alpine` (PostgreSQL port mapping)
3. `dotnet build 5_WebApps/ShopERP/VanAn.ShopERP.csproj` (0 errors)
4. `dotnet run --project 5_WebApps/ShopERP --no-build` (background, watch logs for migration + seed)
5. `Invoke-WebRequest POST /dev/login` (admin@vanan.vn / VanAn@2026)
6. `Invoke-WebRequest GET /api/shop/settings/features` (assert 200 + 6 toggles)
7. `Invoke-WebRequest PUT /api/shop/settings/features` (assert 200 + updated values)
8. `Invoke-WebRequest GET /api/shop/settings/features` (assert persist)
9. `Invoke-WebRequest GET /settings/shop-features` (assert 200 + count form-switch + VanA components)

### Issues fixed during implementation
1. **Missing using directives:** `TenantId` (VanAn.Shared.Domain) + `ILogger<>` (Microsoft.Extensions.Logging) → added
2. **IVanAnDbContext.Entry() not available:** Entity constructor sets TenantId via `base(tenantId)` → removed `Entry().Property()` call
3. **DTO `init` properties incompatible with `@bind`:** Razor `@bind` needs `set` → changed `init` → `set`
4. **`TenantProvider.TenantId` is Guid not Guid?:** Removed `.Value` accessor
5. **W12-S3 Architecture Test fail:** `ShopSettingsController` missing `[Authorize]` → added attribute

### Runtime issues found during Live Verification (NOT caught by static checks)
6. **Missing EF Migration (RV3):** ShopERP dùng `MigrateAsync()` nhưng không có migration cho `ShopFeatureSettings` table → `SQLite Error 1: 'no such table: ShopFeatureSettings'`. Fix: tạo manual migration `20260711143852_AddShopFeatureSettingsTable` chỉ tạo/drop `ShopFeatureSettings` table (KHÔNG drop accounting tables đã move sang PostgreSQL per ADR-001).
7. **LINQ Translation Error (RV6):** `s.TenantId.Value == tenantId` không translate ra SQL → `System.InvalidOperationException: The LINQ expression could not be translated`. Fix: dùng direct comparison `s.TenantId == new TenantId(tenantId)` (Known Pattern #1 — EF Core tự apply TenantIdConverter).

> **WARNING for future waves:** Static verification (build + architecture tests + guard-check) chỉ đảm bảo compile-time. Runtime issues (EF migration, LINQ translation, DI resolution, HTTP routing, auth) chỉ phát hiện khi boot app + gọi API/UI thực tế. **Live Runtime Verification là BẮT BUỘC** trước khi mark wave COMPLETE.
