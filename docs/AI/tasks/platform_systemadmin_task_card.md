# TASK CARD — Platform SystemAdmin (Cross-Tenant Production Admin)

> **Status:** 🟢 COMPLETE — IMPLEMENTED 2026-07-08
> **Prerequisite:** User-approved pattern 2 lớp (2026-07-08)
> **Branch:** `main`
> **Estimated sessions:** 1 (actual: 1)
> **Master plan:** `docs/AI/tasks/platform_systemadmin_master_plan.md`
> **Commit:** `dde219e` `[PLATFORM-ADMIN] Add Platform SystemAdmin production login`

## Objective
Thêm SystemAdmin production thật (cross-tenant, toàn quyền) theo pattern 2 lớp:
- **Lớp 1 (dev):** Giữ nguyên `DevLoginController` (`#if DEBUG`, bypass auth cho E2E)
- **Lớp 2 (prod):** Tạo `PlatformUserLoginController` (verify BCrypt, mint JWT, không `#if DEBUG`)

SystemAdmin có quyền truy cập mọi admin page (`/admin/users`, `/admin/tenants`, `/admin/audit-trail`) + cross-tenant Tenant CRUD.

## Architecture Decision
**PlatformUser = Infrastructure entity** (KHÔNG Domain aggregate), theo precedent `AccountChartEntity` (`3_CoreHub/Infrastructure/Entities/AccountChartEntity.cs` L10-13):
- Không inherit `BaseEntity` (tránh `IMustHaveTenant`)
- Không có `TenantId` (cross-tenant)
- Standalone entity trong `3_CoreHub/Infrastructure/Entities/`

## Prerequisites (verify before code)
- [ ] Verify `AccountChartEntity.cs` path: `3_CoreHub/Infrastructure/Entities/AccountChartEntity.cs`
- [ ] Verify `AccountChartConfiguration.cs` path: `3_CoreHub/Infrastructure/Configurations/AccountChartConfiguration.cs`
- [ ] Verify `IJwtTokenService.cs` string role overload: `3_CoreHub/Services/IJwtTokenService.cs` L32-37
- [ ] Verify `DevLoginController.cs` SystemAdmin claim shape: L176-184
- [ ] Verify `IVanAnDbContext.cs` DbSet pattern: `3_CoreHub/Infrastructure/IVanAnDbContext.cs`
- [ ] Verify `VanAnDbContext.cs` DbSet pattern: `3_CoreHub/Infrastructure/VanAnDbContext.cs`
- [ ] Verify `ShopERPDbContext.cs` DbSet pattern: `5_WebApps/ShopERP/Infrastructure/ShopERPDbContext.cs`
- [ ] Verify `Program.cs` policies: `5_WebApps/ShopERP/Program.cs` L315-320
- [ ] Verify `Program.cs` seed block: `5_WebApps/ShopERP/Program.cs` L378-429
- [ ] Verify `PlatformRole` enum: `1_Shared/Domain/Common.cs` L46-50

## Files Created/Modified
| File | Action | Purpose |
|------|--------|---------|
| `3_CoreHub/Infrastructure/Entities/PlatformUser.cs` | CREATE | Infrastructure entity (non-tenant, non-BaseEntity) |
| `3_CoreHub/Infrastructure/Configurations/PlatformUserConfiguration.cs` | CREATE | EF config: table `PlatformUsers`, unique index, enum conversion |
| `3_CoreHub/Infrastructure/IVanAnDbContext.cs` | MODIFY | Add `DbSet<PlatformUser> PlatformUsers` |
| `3_CoreHub/Infrastructure/VanAnDbContext.cs` | MODIFY | Add `DbSet<PlatformUser> PlatformUsers` |
| `5_WebApps/ShopERP/Infrastructure/ShopERPDbContext.cs` | MODIFY | Add `DbSet<PlatformUser> PlatformUsers` |
| `3_CoreHub/Infrastructure/Migrations/<timestamp>_AddPlatformUsersTable.cs` | CREATE | Migration: add `PlatformUsers` table |
| `3_CoreHub/Services/IPlatformUserLoginService.cs` | CREATE | Interface: `LoginAsync(username, password)` |
| `3_CoreHub/Services/PlatformUserLoginService.cs` | CREATE | Impl: BCrypt verify + JWT mint |
| `5_WebApps/ShopERP/Controllers/PlatformUserLoginController.cs` | CREATE | `POST /api/platform/login` (production, no `#if DEBUG`) |
| `5_WebApps/ShopERP/Program.cs` | MODIFY | (a) DI register service; (b) update 3 policies; (c) seed PlatformUser |
| `6_Tests/VanAn.Core.Tests/Services/PlatformUserLoginServiceTests.cs` | CREATE | Unit tests: verify password, reject wrong, reject inactive |
| `6_Tests/VanAn.Integration.Tests/PlatformUserLoginEndpointTests.cs` | CREATE | Integration tests: login endpoint 200/401/403 |

## Detailed Task List

### T1: PlatformUser entity
- [ ] Create `3_CoreHub/Infrastructure/Entities/PlatformUser.cs`
- [ ] Properties: `Id` (Guid), `Username` (string, required, max 100), `PasswordHash` (string, required, max 500), `DisplayName` (string, required, max 200), `Email` (string?, max 500), `Role` (PlatformRole, default SystemAdmin), `IsActive` (bool, default true), `CreatedAt` (DateTime, default UtcNow)
- [ ] Private parameterless constructor (EF Core)
- [ ] Public constructor with validation (Username, PasswordHash, DisplayName required)
- [ ] Follow `AccountChartEntity.cs` pattern (private set, private ctor, public ctor with validation)

### T2: PlatformUserConfiguration
- [ ] Create `3_CoreHub/Infrastructure/Configurations/PlatformUserConfiguration.cs`
- [ ] `ToTable("PlatformUsers")`
- [ ] `HasKey(e => e.Id)` + `ValueGeneratedOnAdd`
- [ ] `Username` required max 100, unique index
- [ ] `PasswordHash` required max 500
- [ ] `DisplayName` required max 200
- [ ] `Email` max 500 (no encryption for platform users — they're platform-level, not tenant PII)
- [ ] `Role` HasConversion<int> required
- [ ] `IsActive` required
- [ ] `CreatedAt` required, HasDefaultValueSql("CURRENT_TIMESTAMP")
- [ ] Implement `IEntityTypeConfiguration<PlatformUser>` + `IEntityConfiguration` (auto-discovered via `ApplyConfigurationsFromAssembly`)

### T3: DbContext registration (3 files)
- [ ] `IVanAnDbContext.cs`: add `DbSet<PlatformUser> PlatformUsers { get; }`
- [ ] `VanAnDbContext.cs`: add `public DbSet<PlatformUser> PlatformUsers { get; set; }`
- [ ] `ShopERPDbContext.cs`: add `public DbSet<PlatformUser> PlatformUsers { get; set; }`
- [ ] Add `using VanAn.CoreHub.Infrastructure.Entities;` if not present

### T4: EF Migration
- [ ] Run `dotnet ef migrations add AddPlatformUsersTable` (from project with VanAnDbContext)
- [ ] Verify migration only creates `PlatformUsers` table (no alter existing)
- [ ] Run `dotnet ef database update` on dev DB
- [ ] Verify table created with correct schema

### T5: PlatformUserLoginService
- [ ] Create `3_CoreHub/Services/IPlatformUserLoginService.cs`
  - `Task<PlatformLoginResult?> LoginAsync(string username, string password, CancellationToken ct = default)`
- [ ] Create `3_CoreHub/Services/PlatformUserLoginService.cs`
  - Inject `IVanAnDbContext` + `IJwtTokenService`
  - Query: `await db.PlatformUsers.IgnoreQueryFilters().FirstOrDefaultAsync(u => u.Username == username)` (no tenant filter)
  - Verify: `BCrypt.Net.BCrypt.Verify(password, user.PasswordHash)`
  - Guard: `if (!user.IsActive) return null` (or throw)
  - Mint JWT: `_jwtTokenService.GenerateToken(user.Id, user.Email ?? user.Username, PlatformRole.SystemAdmin.ToString(), Guid.Empty)`
  - Return `PlatformLoginResult(userId, email, role, token)`
- [ ] **Claim shape** (copy từ DevLoginController L176-184):
  - `ClaimTypes.Name` = user.DisplayName
  - `ClaimTypes.Email` = user.Email
  - `ClaimTypes.Role` = "SystemAdmin"
  - `sub` = user.Email
  - `role` = "SystemAdmin"
  - **KHÔNG** có `tenant_id` (cross-tenant)

### T6: PlatformUserLoginController
- [ ] Create `5_WebApps/ShopERP/Controllers/PlatformUserLoginController.cs`
- [ ] `[ApiController] [Route("api/platform")]`
- [ ] **KHÔNG** `#if DEBUG` (production controller)
- [ ] `POST /api/platform/login` — accept `{ username, password }` JSON body
- [ ] Call `IPlatformUserLoginService.LoginAsync(username, password)`
- [ ] If null → `401 Unauthorized` `{ success = false, message = "Invalid credentials" }`
- [ ] If success → `200 OK` `{ success, email, role, token, message }`
- [ ] Also issue Cookie auth (like DevLoginController L66-69) for Blazor Server
- [ ] Rate limit: reuse existing rate limiter (or add per-IP limit for login)

### T7: DI + Policies + Seed (Program.cs)
- [ ] **DI:** `_ = builder.Services.AddScoped<IPlatformUserLoginService, PlatformUserLoginService>();`
- [ ] **Policies** (L315-320):
  ```csharp
  // BEFORE
  .AddPolicy("OwnerOnly", policy => policy.RequireRole(UserRole.Owner.ToString()))
  .AddPolicy("StoreManagement", policy => policy.RequireRole(UserRole.Owner.ToString(), UserRole.StoreKeeper.ToString()))
  .AddPolicy("StaffOrAbove", policy => policy.RequireRole(UserRole.Staff.ToString(), UserRole.StoreKeeper.ToString(), UserRole.Owner.ToString()))
  
  // AFTER
  .AddPolicy("OwnerOnly", policy => policy.RequireRole(UserRole.Owner.ToString(), "SystemAdmin"))
  .AddPolicy("StoreManagement", policy => policy.RequireRole(UserRole.Owner.ToString(), UserRole.StoreKeeper.ToString(), "SystemAdmin"))
  .AddPolicy("StaffOrAbove", policy => policy.RequireRole(UserRole.Staff.ToString(), UserRole.StoreKeeper.ToString(), UserRole.Owner.ToString(), "SystemAdmin"))
  ```
- [ ] **Seed** (after existing DemoUser seed block, ~L430):
  ```csharp
  // Seed PlatformUser (SystemAdmin — cross-tenant, idempotent)
  var platformUserRepo = context.PlatformUsers; // DbSet<PlatformUser>
  var existingPlatformAdmin = await platformUserRepo
      .IgnoreQueryFilters()
      .FirstOrDefaultAsync(u => u.Username == "sysadmin@vanan.vn");
  
  if (existingPlatformAdmin == null)
  {
      var sysadminHash = BCrypt.Net.BCrypt.HashPassword("VanAn@2026", 12);
      platformUserRepo.Add(new PlatformUser(
          "sysadmin@vanan.vn",
          sysadminHash,
          "System Administrator",
          "sysadmin@vanan.vn",
          PlatformRole.SystemAdmin));
      _ = await context.SaveChangesAsync();
      Console.WriteLine("PlatformUser seeded — sysadmin@vanan.vn (SystemAdmin, cross-tenant)");
  }
  ```
- [ ] **Production guard:** Use config for password like DemoUser seed:
  ```csharp
  string sysadminPassword = builder.Configuration["Seed:SysAdminPassword"]
      ?? (builder.Environment.IsProduction()
          ? throw new InvalidOperationException("Seed:SysAdminPassword configuration is required in Production.")
          : "VanAn@2026");
  ```

### T8: Tests
- [ ] **Unit tests** (`6_Tests/VanAn.Core.Tests/Services/PlatformUserLoginServiceTests.cs`):
  - `LoginAsync_ValidCredentials_ReturnsToken`
  - `LoginAsync_WrongPassword_ReturnsNull`
  - `LoginAsync_NonExistentUser_ReturnsNull`
  - `LoginAsync_InactiveUser_ReturnsNull`
  - `LoginAsync_EmptyUsername_ReturnsNull`
- [ ] **Integration tests** (`6_Tests/VanAn.Integration.Tests/PlatformUserLoginEndpointTests.cs`):
  - `POST /api/platform/login` valid → 200 + JWT in response
  - `POST /api/platform/login` wrong password → 401
  - `POST /api/platform/login` non-existent user → 401
  - SystemAdmin JWT passes `OwnerOnly` policy (hit `/admin/users` → 200)

### T9: Build + Guard + Commit
- [ ] `dotnet build VanAn.sln` — 0 errors
- [ ] `guard-check.ps1` — ALL CHECKS PASSED
- [ ] Core.Tests — all PASS (no regression)
- [ ] Arch.Tests — all PASS (DevLoginControllerReleaseBuildGuardTests still pass)
- [ ] Integration.Tests — all PASS (new PlatformUserLoginEndpointTests PASS)
- [ ] Commit: `[PLATFORM-ADMIN] Add PlatformUser + login controller + policy updates + seed`

## Verification Results
- [ ] `POST /api/platform/login` with `sysadmin@vanan.vn` / `VanAn@2026` → 200 + JWT
- [ ] JWT contains `role=SystemAdmin`, no `tenant_id`
- [ ] SystemAdmin can access `/admin/users` (OwnerOnly policy passes)
- [ ] SystemAdmin can access `/admin/tenants` (SystemAdmin policy passes)
- [ ] DevLoginController unchanged (E2E tests still pass)
- [ ] Build 0 errors, guard pass, all tests pass

## Rollback
- `dotnet ef migrations remove` (remove AddPlatformUsersTable migration)
- Delete 6 new files (entity, config, service interface, service impl, controller, tests)
- Revert 4 modified files (IVanAnDbContext, VanAnDbContext, ShopERPDbContext, Program.cs)
- Recreate dev DB from migrations if needed

## Open Questions
- Q1: Email encryption for PlatformUser.Email? → **NO** — platform users are not tenant PII, no encryption needed (unlike DemoUser.Email which uses EncryptedStringConverter). Platform users are system-level, managed by infra team.
- Q2: Rate limiting for `/api/platform/login`? → Reuse existing rate limiter config (Program.cs L322+). If not covered, add per-IP policy in T7.
- Q3: Multiple PlatformUsers? → Seed only 1 (`sysadmin@vanan.vn`). CRUD endpoint for managing PlatformUsers is out of scope (deferred — can use DB tooling for now).

## Decisions Made
- D-T1: PlatformUser = Infrastructure entity (non-tenant, non-BaseEntity) — follows AccountChartEntity precedent
- D-T5: BCrypt verify (not hash comparison) — same pattern as UserManagementService
- D-T6: Cookie + JWT dual issue (like DevLoginController) — Cookie for Blazor Server, JWT for API
- D-T7: Policies add "SystemAdmin" string (not PlatformRole enum) — matches existing `SystemAdmin` policy registration (Program.cs L320)
- D-T7: Seed password from config in Production, default `VanAn@2026` in Dev — matches DemoUser seed pattern
- D-T8: 5 unit tests + 4 integration tests — covers happy path + all failure modes + policy pass-through

## Auth Flow Diagram
```
POST /api/platform/login
  { "username": "sysadmin@vanan.vn", "password": "VanAn@2026" }
    ↓
PlatformUserLoginService.LoginAsync()
  → query PlatformUsers by username (IgnoreQueryFilters — no tenant)
  → BCrypt.Verify(password, user.PasswordHash)
  → if !user.IsActive → return null (401)
  → if !verify → return null (401)
  → IJwtTokenService.GenerateToken(userId, email, "SystemAdmin", Guid.Empty)
  → SignInAsync cookie (role=SystemAdmin, no tenant_id)
    ↓
200 OK
  { success: true, email: "sysadmin@vanan.vn", role: "SystemAdmin", token: "..." }
    ↓
SystemAdmin accesses /admin/users
  → Policy "OwnerOnly" → RequireRole("Owner", "SystemAdmin") → PASS ✅
  → Policy "SystemAdmin" → RequireRole("SystemAdmin") → PASS ✅
```

## Files NOT Modified (Hard Stops)
- `5_WebApps/ShopERP/Controllers/DevLoginController.cs` — giữ nguyên (E2E tests)
- `1_Shared/Domain/Aggregates/UserAggregate/UserRole.cs` — không sửa enum
- `1_Shared/Domain/Common.cs` — không sửa PlatformRole enum
- `1_Shared/Domain/Aggregates/UserAggregate/DemoUser.cs` — không sửa aggregate
