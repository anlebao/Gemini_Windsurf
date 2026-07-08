# MASTER PLAN — Platform SystemAdmin (Cross-Tenant Production Admin)

> **Status:** 🟠 COMPLETE-WITH-DEVIATIONS — IMPLEMENTED 2026-07-08, REVIEWED 2026-07-08
> **Created:** 2026-07-08 · **Last Updated:** 2026-07-08 (review pass)
> **Workflow:** `newfeaturebuild.md` (ANALYZE → IMPLEMENT) · **Branch:** `main`
> **Prerequisite audits:** DevLoginController analysis + UserRole/PlatformRole split + AccountChartEntity precedent
> **Commit (implementation):** `dde219e` `[PLATFORM-ADMIN] Add Platform SystemAdmin production login`
> **Commits (post-impl):** `2a9313a` (remove unit tests — deviation #3), `0748109` (add [Authorize] — deviation #1)
>
> ⚠️ **REVIEW 2026-07-08 phát hiện 5 deviations** — xem task card section "Deviation Log".
> Status không phải 🟢 COMPLETE cho đến khi 5 deviations được fix và verification checklist chạy thật pass.

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
| T1 | PlatformUser entity | IMPLEMENT | ❌ | ✅ COMPLETE |
| T2 | PlatformUserConfiguration | IMPLEMENT | ❌ | ✅ COMPLETE |
| T3 | DbContext registration (3 files) | IMPLEMENT | ❌ | ✅ COMPLETE |
| T4 | EF Migration | IMPLEMENT | ❌ | ✅ COMPLETE |
| T5 | PlatformUserLoginService | IMPLEMENT | ❌ | ✅ COMPLETE |
| T6 | PlatformUserLoginController | IMPLEMENT | ❌ | ✅ COMPLETE |
| T7 | DI + Policies + Seed | IMPLEMENT | ❌ | ✅ COMPLETE |
| T8 | Tests (unit + integration) | IMPLEMENT | ❌ | ✅ COMPLETE |
| T9 | Build + Guard + Commit | IMPLEMENT | ❌ | ✅ COMPLETE |

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
| **R9** | **Follow-up commit (để qua arch test) vô tình làm chết endpoint** — đã xảy ra: thêm `[Authorize]` class-level nhưng quên `[AllowAnonymous]` trên `Login` → 401 ở middleware (Deviation #1) | **EDR-2**: follow-up commit phải verify endpoint vẫn trả 200. Arch test chỉ check attribute presence, không check semantics. Bắt buộc có integration test cover happy path của endpoint đang modify. | T6 / post-impl |
| **R10** | **Integration test không phản ánh production auth flow** — `TestAuthenticationHandler` auto-authenticate mọi request, che giấu `[Authorize]` deadlock | **EDR-4**: integration test cho endpoint public (login/register) phải có test case `AnonymousRequest_ReturnsExpectedStatus` (không dựa vào test handler). Hoặc dùng factory riêng không có test auth handler cho login endpoint. | T8 |
| **R11** | **Seed trong Program.cs collide với test seed** — Program.cs seed chạy trong `WebApplicationFactory<Program>`, test lại insert cùng row → UNIQUE constraint fail (Deviation #2) | **EDR-5**: integration test phải idempotent — check existing hoặc clear table trước mỗi test. Document trong task card rằng "Program.cs seed chạy trong test host". | T8 |
| **R12** | **Implement bỏ qua code snippet trong task card** — T7 viết sẵn snippet với production guard, implement hardcode password (Deviation #4) | **EDR-1**: code snippet trong task card là BINDING. Deviate phải ghi lý do trong commit message + report user. | T7 |
| **R13** | **Implement tự ý xoá task item khi gặp technical blocker** — unit tests bị xoá thay vì fix approach (Deviation #3) | **EDR-3**: KHÔNG xoá item trong plan. Gặp blocker → report + propose alternative, chờ approve. | T8 |
| **R14** | **Verification checklist tick theo đoán, không chạy thật** — task card ghi "Integration.Tests all PASS" nhưng thực tế 2/3 fail | **EDR-6**: mỗi check phải có command + output làm bằng chứng. Không tick ✅ không có output. | T9 |
| **R15** | **Attribute legacy trên page đã có không được audit khi claim SystemAdmin access** — AuditTrail.razor `Roles="Admin"` (string sai, role thật "SystemAdmin") → SystemAdmin không vào được audit trail dù task card Objective ghi rõ (Deviation #5) | **EDR-8**: khi Objective claim "SystemAdmin có quyền X" → bắt buộc audit attribute trên X (page/controller), KHÔNG chỉ tạo policy mới. Access matrix verification là task riêng (xem master plan `platform_systemadmin_access_matrix_master_plan.md` — planned sau khi F1-F5 xong). | Objective / T9 |

---

## 6. SUCCESS CRITERIA

### 6.1. Functional (must ALL pass)

- ✅ `POST /api/platform/login` verify password thật (BCrypt) — **verify bằng curl/HTTP client, không dựa vào test handler**
- ✅ SystemAdmin login → JWT `role=SystemAdmin`, `tenant_id=Guid.Empty`
- ✅ SystemAdmin pass policy `OwnerOnly` (vào được `/admin/users`, `/admin/tenants`)
- ✅ SystemAdmin pass policy `StaffOrAbove`, `StoreManagement` (toàn quyền)
- ✅ Seed 1 PlatformUser `sysadmin@vanan.vn` idempotent
- ✅ DevLoginController giữ nguyên (E2E tests không bị phá)
- ✅ Migration tạo table `PlatformUsers` không alter existing tables

### 6.2. Non-Functional (must ALL pass — ràng buộc tăng cường sau review 2026-07-08)

- ✅ Build 0 errors, guard pass, **all tests pass (no regression)** — guard-check.ps1 phải PASS nguyên (không chỉ sub-suite)
- ✅ **Unit tests (T8) phải tồn tại và PASS** — không được xoá mà không có replacement được approve
- ✅ **Integration tests (T8) phải PASS toàn bộ** — không được có test fail/skip
- ✅ **Seed password phải dùng config `Seed:SysAdminPassword`** + production guard `throw` — không hardcode
- ✅ **`[AllowAnonymous]` trên `Login` action** nếu controller có `[Authorize]` class-level — tránh auth deadlock
- ✅ **AuditTrail.razor `Roles` phải match `"SystemAdmin"`** (hoặc dùng `Policy="SystemAdmin"`) — fix Deviation #5
- ✅ **Verification checklist phải chạy thật** — mỗi check có command + output làm bằng chứng (xem EDR-6)

### 6.3. Review Gate (thêm sau review 2026-07-08)

- ✅ **Post-implementation review** chạy trước khi declare COMPLETE — không tự tick COMPLETE
- ✅ **Deviation Log** trong task card nếu có bất kỳ deviate nào so với plan
- ✅ **Fix Backlog** cho mọi deviation, status chỉ 🟢 khi tất cả fix xong + verification pass

---

## 7. EXECUTION DISCIPLINE RULES (EDR) — ràng buộc chống tái diễn

> **Bối cảnh:** Review 2026-07-08 phát hiện 4 deviations (3 implement trái plan, 1 regression do follow-up commit). EDR below được thêm để ràng buộc execution, không phải planning.

### EDR-1: Code snippet trong task card là BINDING
- Code snippet có sẵn trong task card (vd T7 production guard snippet) là **spec bắt buộc**, không phải suggestion.
- Nếu implement deviate → **phải ghi lý do trong commit message** + report user trước khi commit.
- **Không tự ý thay snippet bằng hardcode/giản lược hóa** mà không có lý do được approve.
- **Vi phạm đã xảy ra:** Deviation #4 — T7 snippet có production guard, implement hardcode password.

### EDR-2: Follow-up commit phải verify endpoint vẫn hoạt động
- Khi thêm/sửa attribute (vd `[Authorize]`) để qua arch test → **bắt buộc verify endpoint vẫn trả status mong đợi** (200/401/404...).
- Arch test chỉ check **attribute presence**, không check **semantics** — không phát hiện được `[Authorize]` làm chết endpoint public.
- Verify bằng: chạy integration test cover happy path, hoặc curl thủ công, hoặc HTTP request trong integration test không qua test auth handler.
- **Vi phạm đã xảy ra:** Deviation #1 — thêm `[Authorize]` class-level, quên `[AllowAnonymous]` trên `Login` → endpoint chết trong production, integration test không catch vì `TestAuthenticationHandler` auto-authenticate.

### EDR-3: KHÔNG xoá task item khi gặp technical blocker
- Khi gặp blocker (vd Moq không mock được extension method) → **report + propose alternative**, chờ approve.
- KHÔNG tự ý xoá file test / task item / verification check để "qua".
- Alternative hợp lệ: đổi approach (vd Moq → SQLite in-memory), tạm debt-mark + ghi rõ trong commit message + task card.
- **Vi phạm đã xảy ra:** Deviation #3 — xoá `PlatformUserLoginServiceTests.cs` (119 lines) thay vì đổi sang SQLite in-memory.

### EDR-4: Integration test cho endpoint public phải test anonymous flow
- Endpoint public (login, register, health, public API) phải có test case `AnonymousRequest_ReturnsExpectedStatus` — **không dựa vào `TestAuthenticationHandler`** (auto-authenticate che giấu auth bug).
- Hoặc: dùng `WebApplicationFactory` riêng không có test auth handler cho login endpoint.
- **Vi phạm đã xảy ra:** Deviation #1 — `TestAuthenticationHandler` che giấu `[Authorize]` deadlock.

### EDR-5: Integration test phải idempotent — handle Program.cs seed
- `Program.cs` seed (DemoUser, PlatformUser) **luôn chạy trong `WebApplicationFactory<Program>`**.
- Test seed helper phải check existing trước khi add, hoặc clear table trước mỗi test.
- Document trong task card: "Program.cs seed chạy trong test host — test seed phải idempotent".
- **Vi phạm đã xảy ra:** Deviation #2 — `SeedPlatformUserAsync` insert trùng `sysadmin@vanan.vn` → UNIQUE constraint fail 2/3 tests.

### EDR-6: Verification checklist phải chạy thật, có bằng chứng
- Mỗi check trong "Verification Results" / "Verification Checklist" phải có:
  - **Command chạy** (vd `dotnet test 6_Tests/VanAn.Integration.Tests --filter ...`)
  - **Output làm bằng chứng** (vd `Passed: 3, Failed: 0`)
- KHÔNG tick ✅ theo đoán / theo "code đúng rồi chắc pass".
- Nếu check không chạy được → ghi ⚠️ + lý do, KHÔNG tick ✅.
- **Vi phạm đã xảy ra:** Task card ghi "Integration.Tests — all PASS" nhưng thực tế 2/3 fail.

### EDR-7: Status COMPLETE chỉ được set sau post-impl review
- KHÔNG tự set status 🟢 COMPLETE trong cùng session implement.
- Phải có **review pass riêng** (cùng hoặc khác session) verify:
  1. Verification checklist chạy thật + có bằng chứng
  2. Deviation Log (nếu có deviation)
  3. Fix Backlog (nếu có deviation)
- Status chỉ 🟢 khi: tất cả deviation fix xong + verification checklist pass + review approve.

### EDR-8: Khi Objective claim SystemAdmin access → audit attribute trên target
- Nếu task card Objective ghi "SystemAdmin có quyền X" (vd `/admin/audit-trail`) → **bắt buộc audit attribute hiện có trên X** (page `.razor` / controller), không chỉ tạo policy mới.
- Attribute legacy có thể dùng role string sai (vd `Roles="Admin"` thay vì `"SystemAdmin"`) → SystemAdmin fail dù policy pass.
- Audit output: danh sách entry point + attribute hiện tại + pass/fail → ghi vào task card "Access Matrix Audit" section.
- Nếu scope access matrix lớn (>10 entry points) → tách thành master plan riêng `platform_systemadmin_access_matrix_master_plan.md` (như đã làm cho feature này).
- **Vi phạm đã xảy ra:** Deviation #5 — AuditTrail.razor `Roles="Admin"` không được audit, SystemAdmin không vào được audit trail dù Objective claim.

---

## 8. REFERENCES

- **Precedent:** `3_CoreHub/Infrastructure/Entities/AccountChartEntity.cs` (non-tenant Infrastructure entity)
- **DevLogin blueprint:** `5_WebApps/ShopERP/Controllers/DevLoginController.cs` L172-216 (SystemAdmin claim shape)
- **JWT service:** `3_CoreHub/Services/IJwtTokenService.cs` L32-37 (string role overload)
- **BCrypt pattern:** `5_WebApps/ShopERP/Program.cs` L385 (work factor 12)
- **Governance:** `.devin/rules/governance.md`
- **Workflow:** `.devin/workflows/newfeaturebuild.md`
- **Task card:** `docs/AI/tasks/platform_systemadmin_task_card.md`
