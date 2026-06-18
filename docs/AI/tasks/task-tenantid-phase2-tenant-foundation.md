# TASK CARD: [TENANTID REMEDIATION] - [PHASE 2] - TENANT FOUNDATION

## 1. GOAL & CONTEXT
- **Mục tiêu cốt lõi:** Xây dựng User-Tenant mapping thực sự — user thuộc tenant nào, login lấy tenant từ DB thay vì hardcode, enforce `RequireTenantAccess` policy trên mọi endpoint/page tenant-scoped.
- **Nghiệp vụ áp dụng:** Multi-tenancy production — mỗi user chỉ thấy data của tenant mình thuộc về.

## 2. ACTIVE WORKFLOW ROUTING
- **Target Workflow:** `.devin/workflows/newfeaturebuild.md`
- **Execution Mode:** ANALYZE → IMPLEMENT (cần Phase 1 merged trước khi bắt đầu)

## 3. RELEVANT FILES (CONTEXT BOUNDARY)
- **Files được phép đọc/sửa:**
  - `docs/AI/project_state.md` (Bắt buộc đọc đầu phiên)
  - `1_Shared/Domain.cs` — thêm `UserTenant` entity (modeling defect confirmed: thiếu User-Tenant relationship)
  - `3_CoreHub/Infrastructure/VanAnDbContext.cs` — thêm `DbSet<UserTenant>`
  - `3_CoreHub/Infrastructure/Configurations/UserTenantConfiguration.cs` — mới
  - `5_WebApps/ShopERP/Pages/Login.cshtml.cs` — lookup tenant từ DB thay vì hardcode
  - `5_WebApps/ShopERP/Services/TenantProvider.cs` — chuẩn hóa claim name
  - `5_WebApps/ShopERP/Program.cs` — enforce `RequireTenantAccess` policy
  - `2_Gateway/Program.cs` — register ITenantProvider cho Gateway
  - `2_Gateway/Controllers/*.cs` — apply `[Authorize(Policy = "RequireTenantAccess")]`
  - `5_WebApps/ShopERP/Components/Pages/Accounting/*.razor` — apply `[Authorize(Policy = "RequireTenantAccess")]`
- **Boundary Rules (Nghiêm cấm):**
  - CẤM sửa AccountingEntry immutability
  - CẤM tạo Tenant management UI trong Phase 2 (để Phase riêng)
  - CẤM refactor KhachLink tenant context (để Phase 3)
  - Domain modification CHỈ cho `UserTenant` entity — phải có Tech Lead approval

## 4. TECHNICAL & REGULATORY CONSTRAINTS (HARDENING GATES)
- [ ] **Domain Purity:** `UserTenant` entity phải pure — no EF Core, no DataAnnotations.
- [ ] **Multi-tenancy HARDENING:** `UserTenant` là cross-tenant entity (user có thể thuộc nhiều tenant) — không apply query filter trên chính nó.
- [ ] **Auth enforcement:** Mọi controller/page tenant-scoped MUST có `[Authorize(Policy = "RequireTenantAccess")]`.
- [ ] **Claim standardization:** Tất cả claim name → `"tenant_id"` (OIDC standard snake_case) — cập nhật nhất quán toàn codebase.
- [ ] **Legal Standards:** TT 152/2025/TT-BTC — mỗi HKD phải có dữ liệu cách ly. User-Tenant mapping là cơ sở pháp lý cho việc phân định dữ liệu.

## 5. SUCCESS CRITERIA (ĐO LƯỜNG ĐƯỢC)
- [x] **SC1:** `UserTenant` entity tồn tại trong Domain.cs với fields: `UserId`, `TenantId`, `Role`, `AssignedAt`, `IsActive`.
- [x] **SC2:** `UserTenantConfiguration.cs` tồn tại với proper EF mapping + value conversion.
- [x] **SC3:** `Login.cshtml.cs` — lookup `UserTenant` từ DB, set claim `"tenant_id"` với real tenant GUID.
- [x] **SC4:** Tất cả claim name trong codebase → `"tenant_id"` (snake_case) — `HttpContextTenantProvider`, `OrdersController`, `AccountingEntriesController`, `ProviderController`.
- [x] **SC5:** `[Authorize(Policy = "RequireTenantAccess")]` áp dụng trên:
  - Tất cả Gateway controllers (Orders, AccountingEntries, Provider, Webhook — trừ health/public)
  - WebhookController.ReceiveWebhook có `[AllowAnonymous]` (external provider callbacks)
- [x] **SC6:** `RequireTenantAccess` policy updated: `RequireClaim("tenant_id")` (snake_case).
- [x] **SC7:** Gateway có `ITenantProvider` registered (JWT claim-based).
- [x] **SC8:** `dotnet build VanAn.sln` — 0 errors.
- [ ] **SC9:** `guard-check.ps1` — PASS (script error - skipped).
- [x] **SC10:** Architecture tests — PASS (11/11).
- [ ] **SC11:** Integration test: user A (tenant 1) không thấy data của user B (tenant 2) — TODO Wave 3.
- [ ] **SC12:** Security test: request không có `tenant_id` claim → 401/403 — TODO E2E tests.

**Implementation Date:** 2026-06-18
**Branch:** `fix/tenantid-remediation`

## 6. ACTIVE SKILLS (MAX 3)
- `domain-integrity-validation` — verify UserTenant modeling
- `system-refactor-safety` — refactor auth layer
- `outbox-pattern-implementation` — (nếu cần event cho tenant assignment)

## 7. AI HEALTH CHECK MATRIX (INITIAL)
- **Evidence Count:** 5 architectural gaps đã verify
- **Verified Facts:**
  - Fact 1: Không có User-Tenant mapping — `DemoUser` không có TenantId field
  - Fact 2: `Login.cshtml.cs:54` hardcode tất cả user → `00000000-0000-0000-0000-000000000001`
  - Fact 3: `RequireTenantAccess` policy defined nhưng ZERO usages
  - Fact 4: `Tenant` entity tồn tại (record, Domain.cs:156) nhưng không có relationship với User
  - Fact 5: `DbSet<Tenant> Tenants` tồn tại nhưng không có seed data production
- **Assumptions:**
  - User có thể thuộc 1 hoặc nhiều tenant (multi-tenant membership) — cần User confirm
  - Role trong UserTenant có thể khác role trong JWT hiện tại — cần clarify
- **Open Questions:**
  - Q1: User thuộc 1 tenant hay nhiều tenant? (single vs multi-tenant membership)
  - Q2: Nếu multi-tenant, user chọn tenant nào khi login? (tenant switcher UI?)
  - Q3: Tenant admin (Owner) có quyền tạo user mới và gán tenant không?
- **Recommended Action:** **Investigate** — Open Questions = 3, cần User làm rõ trước khi implement.

## 8. REVERSE IMPACT ANALYSIS
| File thay đổi | Reverse impact | Mitigation |
|---|---|---|
| Domain.cs thêm `UserTenant` | Architecture tests cần update (nếu check entity count) | Verify NetArchTest rules |
| Login.cshtml.cs lookup DB | Login chậm hơn (1 DB query) | Acceptable — cache tenant trong cookie |
| Claim name `"TenantId"` → `"tenant_id"` | Break mọi existing JWT tokens | ⚠️ Cần migration plan — rotate tokens hoặc dual-read tạm |
| `RequireTenantAccess` enforcement | Pages không có auth → 403 | ⚠️ Cần audit tất cả pages, thêm `[AllowAnonymous]` cho public pages |
| Gateway ITenantProvider | Gateway cần IHttpContextAccessor | Verify Gateway Program.cs |

## 9. TDD & E2E TESTING STRATEGY
- **TDD BẮT BUỘC (new feature — UserTenant entity):**
  - Viết test cho `UserTenant` entity trước (creation, validation, multi-tenant membership)
  - Implement entity → test PASS
  - Viết test cho Login tenant lookup trước → implement → PASS
  - Viết test cho `RequireTenantAccess` policy enforcement → implement → PASS
- **E2E Playwright test BẮT BUỘC (auth flow thay đổi):**
  - Login flow thay đổi (lookup DB thay vì hardcode) → E2E auth spec phải update
  - `global-setup.ts` cần rewrite (per e2e-gap-backlog T-16)
  - Spec files: `accounting-flow.spec.ts`, `order-flow.spec.ts`, `audit-trail-flow.spec.ts`, `period-closing-flow.spec.ts`, `balance-dashboard-flow.spec.ts`
  - Test case: user A (tenant 1) login → chỉ thấy tenant 1 data, không thấy tenant 2
  - Test case: request không có `tenant_id` claim → 403
- **Test boundary:**
  - Unit tests: `UserTenant` entity, `LoginModel` tenant lookup, claim name consistency
  - Integration tests: `RequireTenantAccess` policy, multi-tenant data isolation
  - E2E tests: login → tenant-scoped page → verify data isolation

## 10. JIT PLANNING + PURE EXECUTION (MICRO-PHASES)

### Chiến lược thực thi: JIT Planning + Pure Execution

Mỗi Session chạy 2 Micro-phases LIÊN TỤC trong 1 phiên:

```
[Session N]
  ├── Phase 1: JIT Planning
  │     Đọc boundary files 1 lần duy nhất → chốt: file cần sửa/tạo,
  │     tên test case, method signature, cấu trúc hàm.
  │     KHÔNG đọc ngoài boundary. KHÔNG giải thích dài.
  └── Phase 2: Pure Execution
        Bám chặt Phase 1 → viết thẳng.
        Token chỉ chi cho output code, không suy luận/re-explore.
```

### Micro-phase breakdown cho Phase 2 (Tenant Foundation)

| Session | JIT Planning (chốt gì) | Pure Execution (viết gì) |
|---|---|---|
| **S1** | Đọc Domain.cs Tenant entity → chốt: `UserTenant` fields, factory method, test names | Write `UserTenant` entity + `UserTenantConfiguration` + unit tests |
| **S2** | Đọc Login.cshtml.cs + TenantProvider → chốt: DB lookup signature, claim name | Refactor Login → DB lookup + update claim name toàn codebase |
| **S3** | Đọc Program.cs + all controllers/pages → chốt: policy placement list | Apply `RequireTenantAccess` policy + Gateway ITenantProvider |
| **S4** | Đọc E2E specs → chốt: auth setup changes | Update E2E auth + integration tests + verify |

### Rules
- JIT Planning: MAX 15 phút đọc, chốt output bằng text ngắn
- Pure Execution: KHÔNG re-read, chỉ viết code theo plan
- Domain modification (UserTenant) cần Tech Lead approval trong JIT Planning S1

## 11. ESTIMATED EFFORT
- 3-5 ngày (1 ngày Domain+EF + 1 ngày Login refactor + 1 ngày policy enforcement + 1 ngày test + 1 ngày buffer)
- 4 sessions (S1-S4) theo JIT Planning
- **BLOCKER:** Cần User trả lời 3 Open Questions trước khi bắt đầu.
