# MASTER PLAN — Platform SystemAdmin (Cross-Tenant Production Admin)

> **Status:** 🟡 PLANNED — awaiting IMPLEMENT approval
> **Created:** 2026-07-08 · **Last Updated:** 2026-07-08
> **Workflow:** `newfeaturebuild.md` (ANALYZE → IMPLEMENT) · **Branch:** `feature/platform-systemadmin`
> **Prerequisite audits:** DevLoginController analysis + UserRole/PlatformRole split + AccountChartEntity precedent

---

## 0. JIT PLANNING STRATEGY (NON-NEGOTIABLE)

**Nguyên tắc cốt lõi:** KHÔNG code mò mẫm — **Investigate trước, Implement sau**.

### 3-Phase
```
Phase 1 (INVESTIGATE): Verify codebase hiện tại
  → Confirm file paths, signatures, dependencies vẫn đúng
  → Grep usage của methods/symbols sẽ touch
  → Identify blast radius (ai gọi method này?)
  → Output: confirm task card vẫn accurate, hoặc flag drift

Phase 2 (PLAN): Detail coding plan
  → Liệt kê exact changes (file:line, old→new)
  → Identify test files cần update
  → Identify DI registrations cần thêm
  → Output: checklist implement

Phase 3 (IMPLEMENT): Code + verify
  → Apply changes theo checklist
  → Build + guard + tests pass
  → Commit
```

### Task Card Protocol
- **Task card** tại `docs/AI/tasks/platform_systemadmin_task_card.md`
- Task card chứa: objective, prerequisites, exact file changes, code snippets, verification, rollback
- **Task card phải được đọc TRƯỚC khi code** (Phase 1)
- **Task card có thể update** nếu INVESTIGATE phát hiện drift

### Anti-Guessing Gate (Gate 1 từ .windsurfrules)
- Assumptions ≥ Verified Facts → CẤM code, chuyển Investigate
- Phải có ≥ 3 verified facts trước khi implement:
  1. File path tồn tại (verify bằng read/glob)
  2. Method signature đúng (verify bằng grep)
  3. Dependency chain đúng (verify bằng trace)

---

## 1. EXECUTION RULES

### Dependency chain
```
T1 (Entity) → T2 (Config) → T3 (DbContext) → T4 (Migration) → T5 (Service) → T6 (Controller) → T7 (DI + Policies + Seed) → T8 (Tests) → T9 (Build + Guard)
```
- Tất cả task tuần tự nghiêm ngặt
- Mỗi task xong: verify compile không lỗi trước khi qua task tiếp theo
- Hoàn tất T9: `dotnet build VanAn.sln` Release pass + `guard-check.ps1` pass + commit

### Session protocol
1. Bắt đầu session: đọc `project_state.md` + task card
2. Trước session end: build pass + commit
3. Commit format: `[PLATFORM-ADMIN] <short description>`

### Branch protocol
```
main ← feature/platform-systemadmin
```

---

## 2. AUDIT FINDINGS SUMMARY

### 2.1. Current Auth Architecture (2 role systems)

| Enum | Location | Vai trò | Phạm vi |
|------|----------|---------|---------|
| `UserRole` | `1_Shared/Domain/Aggregates/UserAggregate/UserRole.cs` L8-16 | Owner, StoreKeeper, Guard, Staff, Masterchef | Trong 1 tenant |
| `PlatformRole` | `1_Shared/Domain/Common.cs` L46-50 | SystemAdmin | Cross-tenant (platform) |

### 2.2. DevLoginController hiện tại

| Endpoint | Mục đích | Guard |
|----------|----------|-------|
| `POST /dev/login` | Owner mặc định | `#if DEBUG` |
| `POST /dev/login/{role}` | Login theo role | `#if DEBUG` |
| `POST /dev/login/systemadmin` | SystemAdmin (cross-tenant) | `#if DEBUG` |
| `POST /dev/login/vas` | VAS Enterprise tenant | `#if DEBUG` |

**Đặc điểm:**
- Hardcoded claims — không query DB, không verify password
- `#if DEBUG` compile-time guard — stripped khỏi Release binary
- `DevLoginControllerReleaseBuildGuardTests` (Arch test) enforce
- SystemAdmin endpoint (L172-216) đã mint đúng claim shape: `role=SystemAdmin`, không `tenant_id`

### 2.3. Blockers cho SystemAdmin production

| # | Blocker | Severity | Fix |
|---|---------|----------|-----|
| B1 | Không có PlatformUser entity/table | 🔴 | T1-T4: tạo entity + config + migration |
| B2 | Không có login endpoint production | 🔴 | T6: PlatformUserLoginController (không `#if DEBUG`) |
| B3 | Policy `OwnerOnly` không accept SystemAdmin | 🔴 | T7: thêm `RequireRole("SystemAdmin")` |
| B4 | Không có service verify password | 🔴 | T5: PlatformUserLoginService (BCrypt verify) |
| B5 | Không seed user thật | 🟡 | T7: seed 1 PlatformUser idempotent |

### 2.4. Precedent: AccountChartEntity (non-tenant Infrastructure entity)

`3_CoreHub/Infrastructure/Entities/AccountChartEntity.cs` L10-13:
> "Standalone entity (does NOT inherit BaseEntity, does NOT implement IMustHaveTenant):
> AccountCharts is global reference data shared across all tenants per standard.
> This avoids the multi-tenancy query filter."

**PlatformUser theo cùng pattern** — không inherit BaseEntity, không IMustHaveTenant, standalone entity trong Infrastructure.

---

## 3. SCOPE DECISIONS (APPROVED 2026-07-08)

| # | Quyết định | Lựa chọn |
|---|-------------|----------|
| D1 | Kiến trúc | Pattern 2 lớp: giữ DevLoginController (dev) + thêm PlatformUserLoginController (prod) |
| D2 | Entity type | Infrastructure entity (KHÔNG Domain aggregate) — theo AccountChartEntity precedent |
| D3 | Domain modification | KHÔNG sửa Domain (UserRole.cs, PlatformRole.cs, DemoUser.cs giữ nguyên) |
| D4 | Auth flow | Verify BCrypt password + mint JWT (claim shape copy từ DevLoginController L176-184) |
| D5 | Policy scope | `OwnerOnly`, `StoreManagement`, `StaffOrAbove` thêm `RequireRole("SystemAdmin")` |
| D6 | Seed user | `sysadmin@vanan.vn` / `VanAn@2026` (BCrypt work factor 12, idempotent) |
| D7 | TenantId | `Guid.Empty` (SystemAdmin không thuộc tenant nào) |
| D8 | DevLoginController | Giữ nguyên — E2E tests vẫn dùng `/dev/login/systemadmin` |

---

## 4. TASK OVERVIEW (9 tasks, 1 wave)

| Task | Tên | Mode | Domain? | Status |
|------|-----|------|---------|--------|
| T1 | PlatformUser entity | IMPLEMENT | ❌ | ⏳ PENDING |
| T2 | PlatformUserConfiguration | IMPLEMENT | ❌ | ⏳ PENDING |
| T3 | DbContext registration (3 files) | IMPLEMENT | ❌ | ⏳ PENDING |
| T4 | EF Migration | IMPLEMENT | ❌ | ⏳ PENDING |
| T5 | PlatformUserLoginService | IMPLEMENT | ❌ | ⏳ PENDING |
| T6 | PlatformUserLoginController | IMPLEMENT | ❌ | ⏳ PENDING |
| T7 | DI + Policies + Seed | IMPLEMENT | ❌ | ⏳ PENDING |
| T8 | Tests (unit + integration) | IMPLEMENT | ❌ | ⏳ PENDING |
| T9 | Build + Guard + Commit | IMPLEMENT | ❌ | ⏳ PENDING |

**Chi tiết từng task:** xem `docs/AI/tasks/platform_systemadmin_task_card.md`

---

## 5. RISK REGISTER

| # | Risk | Mitigation | Task |
|---|------|------------|------|
| R1 | Migration break existing DB | T4: migration chỉ ADD table, không alter existing | T4 |
| R2 | Multi-tenant query filter leak PlatformUser | PlatformUser không IMustHaveTenant → không bị query filter | T1 |
| R3 | Policy change break existing tests | T8: run full test suite, verify no regression | T8 |
| R4 | BCrypt verify sai | T5: dùng cùng BCrypt.Net.BCrypt.Verify pattern như UserManagementService | T5 |
| R5 | Seed user tạo duplicate | T7: idempotent — check `AnyAsync(u => u.Username == "sysadmin@vanan.vn")` trước | T7 |
| R6 | DevLoginController bị phá | D8: KHÔNG động vào DevLoginController | — |
| R7 | Arch test fail (new entity không có config) | T2: PlatformUserConfiguration implements IEntityConfiguration | T2 |
| R8 | JWT claim shape sai | T5/T6: copy exact claim set từ DevLoginController L176-184 | T5/T6 |

---

## 6. SUCCESS CRITERIA

- ✅ `POST /api/platform/login` verify password thật (BCrypt)
- ✅ SystemAdmin login → JWT `role=SystemAdmin`, `tenant_id=Guid.Empty`
- ✅ SystemAdmin pass policy `OwnerOnly` (vào được `/admin/users`, `/admin/tenants`)
- ✅ SystemAdmin pass policy `StaffOrAbove`, `StoreManagement` (toàn quyền)
- ✅ Seed 1 PlatformUser `sysadmin@vanan.vn` idempotent
- ✅ DevLoginController giữ nguyên (E2E tests không bị phá)
- ✅ Build 0 errors, guard pass, all tests pass (no regression)
- ✅ Migration tạo table `PlatformUsers` không alter existing tables

---

## 7. REFERENCES

- **Precedent:** `3_CoreHub/Infrastructure/Entities/AccountChartEntity.cs` (non-tenant Infrastructure entity)
- **DevLogin blueprint:** `5_WebApps/ShopERP/Controllers/DevLoginController.cs` L172-216 (SystemAdmin claim shape)
- **JWT service:** `3_CoreHub/Services/IJwtTokenService.cs` L32-37 (string role overload)
- **BCrypt pattern:** `5_WebApps/ShopERP/Program.cs` L385 (work factor 12)
- **Governance:** `.devin/rules/governance.md`
- **Workflow:** `.devin/workflows/newfeaturebuild.md`
- **Task card:** `docs/AI/tasks/platform_systemadmin_task_card.md`
