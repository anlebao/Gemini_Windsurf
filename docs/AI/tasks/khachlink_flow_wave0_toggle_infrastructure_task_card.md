# TASK CARD: KhachLink Full Flow — Wave 0 — Module Toggle Infrastructure

## 1. GOAL & CONTEXT
- **Mục tiêu cốt lõi:** Tạo Shop Settings page + toggle storage + logic read/write cho 6 module toggles. Đây là nền tảng BLOCKING cho mọi wave sau (W1-W4).
- **Nghiệp vụ áp dụng:** Section 3 (Module Toggles) của `Tai_lieu_yeu_cau_nghiep_vu_Khachlink.md` v1.2
- **Status:** ⬜ NOT STARTED
- **Branch:** `feature/khachlink-flow-wave0-toggle-infrastructure`
- **Tech Debt:** TD-KL-12 (High)

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
- [ ] **SC1:** `IShopFeatureSettingsService` + implementation tồn tại, read/write 6 toggles per tenant
- [ ] **SC2:** API `GET /api/shop/settings/features` trả về 6 toggles
- [ ] **SC3:** API `PUT /api/shop/settings/features` cập nhật toggles
- [ ] **SC4:** ShopERP Settings page hiển thị 6 toggle switches (UI Platform components)
- [ ] **SC5:** KhachLink `ShopFeatureSettingsHttpService` fetch được toggles qua HTTP
- [ ] **SC6:** Default seed: 6 toggles với giá trị mặc định
- [ ] **SC7:** KhachLinkStartupTests assertion pass
- [ ] **SC8:** Build: 0 errors
- [ ] **SC9:** guard-check.ps1 pass

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

### Post-ANALYZE (update sau khi investigate)
- Cần verify: Shop/Tenant entity structure, UI Platform toggle component, existing settings page pattern, KhachLink HTTP service pattern

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
- [ ] Read `1_Shared/Domain.cs` — check Shop/Tenant entity (Option A vs B decision)
- [ ] Read `UI.Platform/` — check toggle/switch component availability
- [ ] Read existing `5_WebApps/ShopERP/Components/Pages/Settings/` — check patterns
- [ ] Read 1 file in `5_WebApps/KhachLink/Services/Http/` — check HTTP service pattern
- [ ] Read `2_Gateway/Program.cs` — check YARP routing (need new route?)
- [ ] Decision: Option A (JSON column) or Option B (separate table)
- [ ] Update Health Check Matrix (Assumptions < Verified Facts)

### IMPLEMENT Phase
- [ ] W0-T1: Toggle storage (entity/config)
- [ ] W0-T2: Service layer (interface + implementation)
- [ ] W0-T3: DI registration (ShopERP + KhachLink)
- [ ] W0-T4: ShopERP Settings UI (UI Platform)
- [ ] W0-T5: API endpoints
- [ ] W0-T6: KhachLink HTTP service
- [ ] W0-T7: KhachLinkStartupTests assertion
- [ ] W0-T8: Default seed
- [ ] W0-T9: Build + guard-check.ps1

### Post-IMPLEMENT
- [ ] Commit: `[KL WAVE 0] Module toggle infrastructure — 6 toggles + Shop Settings UI`
- [ ] Update `project_state.md` (if user requests)
