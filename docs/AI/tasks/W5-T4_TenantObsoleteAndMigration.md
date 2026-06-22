# TASK CARD: INFRASTRUCTURE - WAVE 5 - Tenant Obsolete & EF Migration

## 1. GOAL & CONTEXT
- **Mục tiêu cốt lõi:** Mark `record Tenant` trong `Domain.cs` là `[Obsolete]`, cập nhật `TenantConfiguration.cs` để map sang `Tenant` class mới (từ W5-T2), thêm `TenantStatus` column và `TenantSettings` owned entity vào EF Core mapping, và chuẩn bị EF migration.
- **Nghiệp vụ áp dụng:** Database schema evolution — thêm `Status` (NVARCHAR/TEXT) và TenantSettings columns (ContactEmail, BusinessAddress, LogoUrl, PhoneNumber) vào bảng `Tenants`. Existing records được migrate với `Status = 'Active'` (backward compatible).

## 2. ACTIVE WORKFLOW ROUTING
- **Target Workflow:** `.devin/workflows/newfeaturebuild.md`
- **Execution Mode:** IMPLEMENT

## 3. RELEVANT FILES (CONTEXT BOUNDARY)
- **Files được phép đọc/sửa:**
  - `docs/AI/project_state.md`
  - `1_Shared/Domain.cs` — SỬA: thêm `[Obsolete(...)]` attribute trước `public record Tenant` (line ~156). Không xóa gì.
  - `3_CoreHub/Infrastructure/Configurations/TenantConfiguration.cs` — SỬA: update mapping sang `Tenant` class mới
  - `3_CoreHub/Infrastructure/VanAnDbContext.cs` — ĐỌC/SỬA nhẹ nếu cần để verify `DbSet<Tenant>`
  - `1_Shared/Domain/Aggregates/TenantAggregate/Tenant.cs` — ĐỌC để lấy class definition (từ W5-T2)
  - `1_Shared/Domain/Aggregates/TenantAggregate/TenantSettings.cs` — ĐỌC để biết owned entity properties
- **Boundary Rules (Nghiêm cấm):**
  - KHÔNG xóa `record Tenant` trong `Domain.cs` — chỉ thêm `[Obsolete]` attribute
  - KHÔNG chạy `dotnet ef migrations add` — chỉ ghi lệnh trong task card (DBA/DevOps chạy)
  - KHÔNG thêm EF Core annotations vào Domain layer (`Tenant.cs`, `TenantSettings.cs`)
  - KHÔNG sửa bất kỳ file nào ngoài danh sách trên
  - KHÔNG xóa `TenantConfiguration.cs` cũ — update in-place

## 4. TECHNICAL & REGULATORY CONSTRAINTS (HARDENING GATES)
- [ ] **[Obsolete] Chỉ Warning:** `[Obsolete("Use VanAn.Shared.Domain.Tenant class in Domain/Aggregates/TenantAggregate/Tenant.cs")]` — KHÔNG dùng `[Obsolete(..., true)]` (second param = true sẽ gây compile ERROR, không phải warning).
- [ ] **Backward Compatible Migration:** Migration file phải có `DEFAULT 'Active'` cho cột `Status` — existing rows không bị NULL.
- [ ] **TenantSettings Owned Entity:** Dùng EF Core `OwnsOne<TenantSettings>` pattern — các columns flatten vào bảng `Tenants` (không tạo bảng riêng).
- [ ] **TenantConfiguration Namespace:** Import `VanAn.Shared.Domain` để dùng `Tenant` class mới (không phải `record Tenant` cũ).
- [ ] **DbSet Compatibility:** `DbSet<Tenant>` trong `VanAnDbContext` phải resolve sang `class Tenant` (từ `Aggregates/TenantAggregate/`) — không phải `record Tenant` — có thể cần explicit using alias.

## 5. SUCCESS CRITERIA (ĐO LƯỜNG ĐƯỢC)
- [ ] **SC-1:** `dotnet build VanAn.sln` → 0 errors. Chỉ có CS0618 warnings (Obsolete usage) từ code đang dùng `record Tenant` — KHÔNG có errors.
- [ ] **SC-2:** `Domain.cs` line ~156 có `[Obsolete("Use VanAn.Shared.Domain.Tenant class in Domain/Aggregates/TenantAggregate/Tenant.cs")]` trước `public record Tenant`.
- [ ] **SC-3:** `TenantConfiguration.cs` map `Tenant` class với `TenantStatus Status` column (string conversion).
- [ ] **SC-4:** `TenantConfiguration.cs` dùng `OwnsOne<TenantSettings>` với mapping cho `ContactEmail`, `BusinessAddress`, `LogoUrl`, `PhoneNumber`.
- [ ] **SC-5:** EF migration command ghi trong task: `dotnet ef migrations add AddTenantStatusAndSettings --project 3_CoreHub --startup-project 5_WebApps/ShopERP` — với instruction về `DEFAULT 'Active'` cho Status column.
- [ ] **SC-6:** `VanAnDbContext.DbSet<Tenant>` vẫn query được — không có runtime exception khi query `context.Tenants.ToListAsync()`.
- [ ] **SC-7:** `guard-check.ps1` PASS.
- [ ] **SC-8:** Architecture tests 7/7 PASS.

**Implementation Date:** 2026-06-23
**Branch:** feature/wave5-tenant-mgmt

## 6. ACTIVE SKILLS (MAX 3)
- `domain-integrity-validation` — Verify [Obsolete] không vi phạm domain purity, EF mapping ở infra layer
- `build-error-analysis` — Handle CS0618 warnings vs errors, namespace ambiguity resolution
- `system-refactor-safety` — Backward compatible migration, không xóa existing data contract

## 7. AI HEALTH CHECK MATRIX (INITIAL)
- **Verified Facts:**
  - Fact 1: `record Tenant` tại `1_Shared/Domain.cs` line 156: `Id (TenantId), Name, BusinessType, HKDGroup, IsActive` — immutable record, NO domain methods
  - Fact 2: `TenantConfiguration.cs` tồn tại tại `3_CoreHub/Infrastructure/Configurations/TenantConfiguration.cs` — EF mapping, TenantId PK
  - Fact 3: `DbSet<Tenant> Tenants` trong `VanAnDbContext.cs` — đã tồn tại
  - Fact 4: `Tenant` class mới (từ W5-T2) có `TenantStatus Status` và `TenantSettings Settings` properties
  - Fact 5: `TenantSettings` record có: `ContactEmail, BusinessAddress, LogoUrl?, PhoneNumber?` (từ W5-T2 spec)
  - Fact 6: EF Core Owned Entity pattern (`OwnsOne`) flatten properties vào parent table — không tạo bảng riêng
  - Fact 7: `[Obsolete]` attribute với 1 param (message) → compiler warning CS0618, KHÔNG phải error
- **Assumptions:**
  - `TenantConfiguration.cs` hiện tại dùng `IEntityTypeConfiguration<Tenant>` với `record Tenant` — cần update generic type sang `class Tenant`
  - Migration command dùng `--project 3_CoreHub --startup-project 5_WebApps/ShopERP` (cần verify project paths khi implement)
- **Open Questions:**
  - Q1: `TenantConfiguration.cs` hiện tại map TenantId PK như thế nào? (`HasKey(x => x.Id)` hay `HasKey(x => x.TenantId)`?) — cần đọc file trước khi sửa
  - Q2: Có bất kỳ code nào trong `3_CoreHub` đang dùng `record Tenant` trực tiếp mà sẽ nhận CS0618 warning không? (Impact assessment)
- **Recommended Action:** IMPLEMENT — đọc TenantConfiguration.cs + VanAnDbContext.cs → update mapping → add [Obsolete] → build verify → document migration command

## 8. REVERSE IMPACT ANALYSIS
| File thay đổi | Reverse impact | Mitigation |
|---|---|---|
| `1_Shared/Domain.cs` (Obsolete tag) | CS0618 warnings trên tất cả usages của `record Tenant` | [Obsolete] là warning-only — code vẫn compile. Sẽ fix fully ở tương lai khi xóa record |
| `TenantConfiguration.cs` | EF Core sẽ map sang class Tenant thay vì record Tenant | Verify DbSet query vẫn work sau change |
| `VanAnDbContext.cs` | `DbSet<Tenant>` type resolution phải match class Tenant | Thêm using alias nếu cần: `using TenantClass = VanAn.Shared.Domain.Tenant;` |
| Migration file (mới) | Thêm 5 cột mới vào `Tenants` table — schema change | Cột Status có DEFAULT 'Active' → backward compatible. Cột TenantSettings nullable → no data loss |

## 9. TDD & E2E TESTING STRATEGY
- **Integration Test — EF Mapping:**
  - Test: `context.Tenants.AddAsync(new Tenant(...))` → `SaveChangesAsync()` → query lại → `Status == TenantStatus.Pending`
  - Test: Tenant với `TenantSettings` → roundtrip EF → `Settings.ContactEmail` được preserve
  - Test: Existing tenant (không có Status trước migration) → sau migration read → `Status == Active` (DEFAULT value)
- **Build Verification:**
  - Build với [Obsolete] → 0 errors, chỉ warnings
  - `DbSet<Tenant>.ToListAsync()` → không throw ambiguous type exception
- **Test boundary:**
  - Unit tests: N/A cho EF mapping (integration concern)
  - Integration tests: EF Core in-memory hoặc SQLite in-memory — verify owned entity round-trip
  - E2E tests: N/A trong task này

## 10. JIT PLANNING + PURE EXECUTION (MICRO-PHASES)

### Chiến lược thực thi: JIT Planning + Pure Execution

Task này là SINGLE-SESSION. 3 thay đổi độc lập: (1) [Obsolete] tag vào Domain.cs, (2) update TenantConfiguration.cs, (3) document migration command.

### Micro-phase breakdown cho W5-T4

| Session | JIT Planning (chốt gì) | Pure Execution (viết gì) |
|---|---|---|
| **S1 (phase A)** | Đọc `TenantConfiguration.cs` → xác nhận IEntityTypeConfiguration<> generic type, PK mapping pattern, hiện có columns gì | Update `TenantConfiguration.cs`: đổi generic type sang `class Tenant`, thêm `HasConversion<string>` cho TenantStatus, thêm `OwnsOne<TenantSettings>(...)` block |
| **S1 (phase B)** | Đọc `VanAnDbContext.cs` → verify DbSet type, check nếu cần using alias | Update DbSet type nếu ambiguous. Thêm `[Obsolete]` attribute vào `Domain.cs` line 156. Ghi migration command vào checklist. Run `dotnet build` → verify |

### Rules
- Đọc file trước, sửa sau — không assume existing code
- Nếu namespace conflict không giải được bằng using alias → dừng, báo cáo để quyết định đẩy W5-T4 lên trước W5-T2
- Migration KHÔNG chạy trong task này — chỉ document lệnh và nội dung expected

## 11. ESTIMATED EFFORT
- 1 session (45-60 phút)
- **Phụ thuộc:** W5-T2 (TenantAggregate class files phải tồn tại trước)
- **Migration Command (để record):**
  ```bash
  dotnet ef migrations add AddTenantStatusAndSettings \
    --project 3_CoreHub \
    --startup-project 5_WebApps/ShopERP \
    --output-dir Infrastructure/Migrations
  ```
  **Sau khi tạo migration:** Verify migration file UP method có `DEFAULT 'Active'` cho Status column. Nếu EF không tự thêm → sửa migration file thủ công.
- **BLOCKER:** Nếu compile error do ambiguous `Tenant` type (không phải warning) → cần `using TenantAggregate = VanAn.Shared.Domain; using TenantRecord = ...` alias pattern
