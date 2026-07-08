# MASTER PLAN — Platform SystemAdmin Access Matrix (Cross-Tenant Verification)

> **Status:** 🟡 PLANNED — awaiting user approval
> **Created:** 2026-07-08 · **Last Updated:** 2026-07-08
> **Workflow:** `newfeaturebuild.md` (ANALYZE → DESIGN → IMPLEMENT → VERIFY) · **Branch:** `main`
> **Prerequisite:** F1-F5 từ `platform_systemadmin_task_card.md` phải COMPLETE trước khi bắt đầu VERIFY phase
> **Parent feature:** `docs/AI/tasks/platform_systemadmin_master_plan.md` (login — COMPLETE-WITH-DEVIATIONS)
> **Sibling task card:** `docs/AI/tasks/platform_systemadmin_access_matrix_task_card.md`
>
> ⚠️ **Plan này KHÔNG thực hiện cho đến khi user approve.**
> ⚠️ **F1-F5 (login + admin pages fix) phải COMPLETE trước khi chạy VERIFY phase.**

---

## 0. CONTEXT & RATIONALE

### Tại sao tách plan riêng?

Review 2026-07-08 sau khi implement `platform_systemadmin_master_plan.md` phát hiện:
- Login plan chỉ lo "tạo user + login endpoint + policy", không audit access matrix toàn app
- Deviation #5 (AuditTrail `Roles="Admin"` mismatch) phát hiện khi listing entry points — không phải khi implement login
- Verification checklist login plan claim "SystemAdmin can access /admin/audit-trail" — sai (role string mismatch)
- ~41 entry points trong app, chia 5 category, mỗi category cần decision riêng

→ Access matrix verification là **concern khác**, **scope lớn hơn**, **cần design decisions** → tách plan riêng (theo governance Context Control + EDR-8).

### Phụ thuộc

| Phụ thuộc | Lý do |
|---|---|
| F1-F5 COMPLETE | Không verify được access khi login endpoint chết + AuditTrail role sai |
| `TestAuthenticationHandler` refactor (TBD trong VERIFY phase) | Integration test hiện tại auto-authenticate che giấu auth bug (EDR-4) |

---

## 1. EXECUTION DISCIPLINE RULES (EDR-AM) — ràng buộc cho Access Matrix plan

### EDR-AM-1: Verify access phải dùng HTTP test với auth thật
- **KHÔNG dùng `TestAuthenticationHandler`** (auto-authenticate mọi request) cho access matrix verification.
- Test phải:
  1. Gọi `POST /api/platform/login` thật → lấy JWT/cookie
  2. Dùng token/cookie đó gọi entry point thật
  3. Assert status code mong đợi (200/401/403)
- Exception: nếu tạo factory riêng không có test auth handler quá phức tạp → dùng sub-cutaneous test (`TestServer` + `HttpClient` với `Authorization: Bearer <jwt>`), vẫn phải mint JWT thật qua `IJwtTokenService`.
- Lý do: `TestAuthenticationHandler` che giát `[Authorize]` deadlock (đã xảy ra Deviation #1) và che giấu role string mismatch (đã xảy ra Deviation #5).

### EDR-AM-2: Audit trail phải có test case cho mỗi policy
- Mỗi policy trong Program.cs (`OwnerOnly`, `StoreManagement`, `StaffOrAbove`, `SystemAdmin`, `RequireTenantAccess`, `RequireAuthenticatedUser`, `GuardOnly`) phải có ít nhất 1 test case verify:
  - User có role X → pass policy Y (expected 200/expected redirect)
  - User có role X → fail policy Z (expected 401/403/redirect to login)
- Test case cho SystemAdmin phải cover **tất cả 7 policies** (pass hoặc fail với lý do rõ ràng).
- Lý do: chỉ test happy path = không phát hiện regression (Deviation #1 + #5 là regression không catch).

### EDR-AM-3: DESIGN phase phải có user decision trước khi IMPLEMENT
- Category B (tenant-scoped business), Category C (RequireTenantAccess), Category E (role string mismatch) có **design ambiguity** — không tự quyết.
- Mỗi design decision phải ghi vào task card "Design Decisions" section với:
  - Question
  - Options (min 2)
  - User's choice
  - Rationale
- KHÔNG implement category nào chưa có user decision.

### EDR-AM-4: Access Matrix Audit phải output artifact reviewable
- ANALYZE phase output: file `docs/AI/artifacts/platform_systemadmin_access_matrix_audit.md` với:
  - Bảng entry point × attribute × SystemAdmin pass/fail
  - Category A-E classification
  - Flagged entry points (potential bugs/gaps)
- Artifact này là input cho DESIGN phase — user review artifact trước khi decide.
- Lý do: decyzje design phải dựa trên data cụ thể, không đoán.

### EDR-AM-5: Không sửa Domain layer cho access matrix
- Access matrix fix chỉ chạm: `.razor` (attribute), Controller (attribute), Program.cs (policy registration).
- **KHÔNG sửa Domain entities** (UserRole, PlatformRole, Tenant, DemoUser) để fix access.
- Nếu cần Domain change → report Domain Modeling Defect, chờ Tech Lead approval (governance Domain Protection).

---

## 2. FOUR-PHASE WORKFLOW

```
ANALYZE (enum + categorize)
  ↓ output: access_matrix_audit.md artifact
DESIGN (user quyết định Category B/C/E)
  ↓ output: design decisions logged
IMPLEMENT (fix role mismatch + policy)
  ↓ output: code changes + build pass
VERIFY (HTTP test với auth thật)
  ↓ output: test suite pass + verification checklist
```

### Phase 1: ANALYZE (REVIEW_ONLY + investigate)
- **Allowed:** read, grep, glob, exec (read-only commands)
- **Forbidden:** modify code files
- **Output:** `docs/AI/artifacts/platform_systemadmin_access_matrix_audit.md`
- **Tasks:** AM-T1 (enum entry points), AM-T2 (categorize A-E), AM-T3 (flag gaps)

### Phase 2: DESIGN (await user decision)
- **Allowed:** propose options, ask user questions
- **Forbidden:** modify code files
- **Output:** Design Decisions section trong task card
- **Tasks:** AM-T4 (propose options cho B/C/E), AM-T5 (user decide), AM-T6 (log decisions)

### Phase 3: IMPLEMENT (code fix)
- **Allowed:** modify .razor / Controller / Program.cs / test files
- **Forbidden:** modify Domain layer (EDR-AM-5)
- **Output:** code changes + `dotnet build` pass
- **Tasks:** AM-T7 (fix role mismatch), AM-T8 (fix policy), AM-T9 (add tests infrastructure nếu cần)

### Phase 4: VERIFY (HTTP test với auth thật)
- **Allowed:** write + run integration tests với auth thật, run verification commands
- **Forbidden:** fix code (chỉ nếu test fail → quay lại IMPLEMENT)
- **Output:** test suite pass + verification checklist (có command + output làm bằng chứng, EDR-6)
- **Tasks:** AM-T10 (HTTP tests auth thật), AM-T11 (run verification), AM-T12 (final report)

---

## 3. SCOPE DECISIONS (PENDING — Phase 2 sẽ fill)

| # | Question | Category | Options | Status |
|---|----------|----------|---------|--------|
| D1 | SystemAdmin có nên truy cập tenant-scoped business data (Accounting/Orders/EInvoice) không? | B | (a) Có + impersonation tenant, (b) Có + aggregated view, (c) Không — chỉ admin pages | ⏳ Pending |
| D2 | `RequireTenantAccess` exclude SystemAdmin là cố ý hay bug? | C | (a) Cố ý — SystemAdmin không cần truy cập tenant-specific, (b) Bug — phải pass + auto-pick tenant | ⏳ Pending |
| D3 | AuditTrail đổi sang `Roles="SystemAdmin"` hay `Policy="SystemAdmin"`? | E (F5) | (a) Roles, (b) Policy (nhất quán TenantManagement.razor) | ⏳ Pending |
| D4 | ApiKeyController `Roles="Admin,Owner"` thêm SystemAdmin không? | E | (a) Có — SystemAdmin quản platform API keys, (b) Không — ApiKey tenant-scoped | ⏳ Pending |
| D5 | Kitchen `Roles="Masterchef,Staff,Manager"`, GuardRedirect `Roles="Guard"` — chạm không? | D | (a) Không chạm — đúng exclude SystemAdmin, (b) Review lại | ⏳ Pending |

---

## 4. TASK OVERVIEW (12 tasks, 4 phases)

| Task | Phase | Tên | Mode | Status |
|---|---|---|---|---|
| AM-T1 | ANALYZE | Enum tất cả entry points (`[Authorize]` trong Controllers + Pages) | REVIEW_ONLY | ⏳ Pending |
| AM-T2 | ANALYZE | Categorize A-E theo policy SystemAdmin pass/fail | REVIEW_ONLY | ⏳ Pending |
| AM-T3 | ANALYZE | Flag potential gaps (role string mismatch, missing SystemAdmin) | REVIEW_ONLY | ⏳ Pending |
| AM-T4 | DESIGN | Propose options cho D1-D5 | DESIGN | ⏳ Pending |
| AM-T5 | DESIGN | User decide | DESIGN | ⏳ Pending |
| AM-T6 | DESIGN | Log decisions vào task card | DESIGN | ⏳ Pending |
| AM-T7 | IMPLEMENT | Fix role mismatch (AuditTrail, ApiKeyController nếu approved) | IMPLEMENT | ⏳ Pending |
| AM-T8 | IMPLEMENT | Fix policy (RequireTenantAccess nếu approved) | IMPLEMENT | ⏳ Pending |
| AM-T9 | IMPLEMENT | Setup HTTP test infrastructure (factory không test auth handler hoặc JWT mint helper) | IMPLEMENT | ⏳ Pending |
| AM-T10 | VERIFY | Write HTTP tests với auth thật (cover 7 policies × SystemAdmin) | VERIFY | ⏳ Pending |
| AM-T11 | VERIFY | Run verification checklist (command + output làm bằng chứng) | VERIFY | ⏳ Pending |
| AM-T12 | VERIFY | Final report + update task card status | VERIFY | ⏳ Pending |

**Chi tiết từng task:** xem `docs/AI/tasks/platform_systemadmin_access_matrix_task_card.md`

---

## 5. RISK REGISTER

| # | Risk | Mitigation | Phase |
|---|------|------------|-------|
| RAM-R1 | DESIGN decision bị block → IMPLEMENT không chạy | User approve trước khi bắt đầu Phase 2 — set timeout, escalate nếu >1 session | DESIGN |
| RAM-R2 | Test infrastructure (factory không auth handler) phức tạp | AM-T9 scoped riêng — nếu >1 session → tách thành sub-task | IMPLEMENT |
| RAM-R3 | Tenant-scoped business (Category B) cần impersonation → scope phình | D1 default = (c) Không — chỉ admin pages, defer impersonation | DESIGN |
| RAM-R4 | Sửa policy break existing tests (Owner/StoreKeeper) | Run full test suite sau IMPLEMENT + VERIFY | VERIFY |
| RAM-R5 | Domain layer cần sửa cho impersonation → Hard Stop | EDR-AM-5: KHÔNG sửa Domain, report Domain Modeling Defect | IMPLEMENT |
| RAM-R6 | HTTP test auth thật chậm (BCrypt verify + JWT mint mỗi test) | Cache token trong test class, refresh per-class không per-test | VERIFY |
| RAM-R7 | `TestAuthenticationHandler` vẫn dùng cho tests khác → conflict | AM-T9: factory riêng cho access matrix tests, không ghi đè default factory | IMPLEMENT |
| RAM-R8 | Audit matrix artifact outdated sau IMPLEMENT | Re-run AM-T1 enum sau IMPLEMENT, diff với artifact gốc | VERIFY |

---

## 6. SUCCESS CRITERIA

### 6.1. ANALYZE Phase
- ✅ `docs/AI/artifacts/platform_systemadmin_access_matrix_audit.md` tồn tại
- ✅ Bảng enum ~41 entry points × attribute × SystemAdmin pass/fail
- ✅ Category A-E classification có definitions rõ ràng
- ✅ Flagged entry points có reasoning cụ thể

### 6.2. DESIGN Phase
- ✅ D1-D5 có user decision logged trong task card
- ✅ Mỗi decision có: question, options, user's choice, rationale

### 6.3. IMPLEMENT Phase
- ✅ Code fix match D1-D5 decisions
- ✅ `dotnet build VanAn.sln` 0 errors
- ✅ `guard-check.ps1` PASS
- ✅ Domain layer KHÔNG bị sửa (EDR-AM-5)
- ✅ Existing tests vẫn pass (no regression — Owner/StoreKeeper/Guard flow)

### 6.4. VERIFY Phase
- ✅ HTTP test suite với auth thật (EDR-AM-1) PASS
- ✅ Audit trail có test case cho mỗi policy (EDR-AM-2)
- ✅ SystemAdmin test case cover 7 policies
- ✅ Verification checklist có command + output làm bằng chứng (EDR-6)
- ✅ Full test suite PASS (no regression)

### 6.5. Review Gate
- ✅ Post-implementation review trước declare COMPLETE (EDR-7)
- ✅ Deviation Log nếu có deviation
- ✅ Fix Backlog nếu có deviation
- ✅ User approve final COMPLETE status

---

## 7. REFERENCES

- **Parent feature:** `docs/AI/tasks/platform_systemadmin_master_plan.md` (login)
- **Sibling task card:** `docs/AI/tasks/platform_systemadmin_access_matrix_task_card.md`
- **Predecessor deviations:** `platform_systemadmin_task_card.md` section "Deviation Log" (5 deviations)
- **Governance:** `.devin/rules/governance.md`
- **Workflow:** `.devin/workflows/newfeaturebuild.md`
- **EDR context:** `platform_systemadmin_master_plan.md` Section 7 (EDR-1 to EDR-8) — EDR-AM-1 to EDR-AM-5 bổ sung cho access matrix scope
- **Test infrastructure hiện tại:** `6_Tests/VanAn.Integration.Tests/Infrastructure/CustomWebApplicationFactory.cs` (cần refactor cho EDR-AM-1)
