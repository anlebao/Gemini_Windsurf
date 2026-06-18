# TASK CARD: [TENANTID REMEDIATION] - [PHASE 1] - STOP THE BLEEDING

## 1. GOAL & CONTEXT
- **Mục tiêu cốt lõi:** Loại bỏ 3 lỗ hổng nghiêm trọng nhất của hệ thống TenantId: (1) client có thể spoof tenant qua request body, (2) orders đi tới tenant ngẫu nhiên, (3) VanAnDbContext silent empty results do claim name mismatch.
- **Nghiệp vụ áp dụng:** Multi-tenancy enforcement — mọi ghi dữ liệu phải gắn với tenant của user đã xác thực, không chấp nhận tenant từ client.

## 2. ACTIVE WORKFLOW ROUTING
- **Target Workflow:** `.devin/workflows/newfeaturebuild.md`
- **Execution Mode:** ANALYZE → IMPLEMENT (đã ANALYZE xong, chờ approval IMPLEMENT)

## 3. RELEVANT FILES (CONTEXT BOUNDARY)
- **Files được phép đọc/sửa:**
  - `docs/AI/project_state.md` (Bắt buộc đọc đầu phiên)
  - `5_WebApps/ShopERP/Services/TenantProvider.cs` — fix claim name
  - `2_Gateway/Controllers/OrdersController.cs` — replace `Guid.NewGuid()` + `GetTenantId()` fallback
  - `2_Gateway/Controllers/AccountingEntriesController.cs` — remove `request.TenantId`, remove `X-Tenant-Id` header, use JWT claim
  - `2_Gateway/Controllers/ProviderController.cs` — remove `tenantId` from query string, use JWT claim (DECISION Q2)
  - `3_CoreHub/Infrastructure/VanAnDbContext.cs` — throw if TenantId == Empty (query filter safety)
  - `5_WebApps/ShopERP/Program.cs` — verify ITenantProvider registration
  - `5_WebApps/ShopERP/Pages/Login.cshtml.cs` — verify claim name emitted (read-only for Phase 1)
- **Boundary Rules (Nghiêm cấm):**
  - CẤM tạo User-Tenant mapping trong Phase 1 (để Phase 2)
  - CẤM refactor 6 Razor pages sang ITenantProvider (để Phase 4)
  - CẤM sửa Domain.cs (không có modeling defect)
  - CẤM xóa hardcoded fallback trong 3 Razor pages (để Phase 4 — cần User-Tenant mapping trước)

## 4. TECHNICAL & REGULATORY CONSTRAINTS (HARDENING GATES)
- [ ] **Domain Purity:** Không sửa Domain layer.
- [ ] **Immutability:** AccountingEntry append-only — không liên quan Phase 1.
- [ ] **Multi-tenancy HARDENING:** Mọi endpoint ghi dữ liệu MUST lấy tenant từ JWT claim, KHÔNG từ request body/header. Throw `UnauthorizedAccessException` nếu claim thiếu hoặc rỗng.
- [ ] **Fail-fast principle:** VanAnDbContext MUST throw `InvalidOperationException("TenantId is empty — cannot query tenant-scoped data")` nếu `ITenantProvider.TenantId == Guid.Empty` (trừ design-time/migrations).
- [ ] **Legal Standards:** TT 152/2025/TT-BTC — dữ liệu kế toán phải cách ly theo từng HKD/tenant. Spoofable tenant = vi phạm dữ liệu tài chính.

## 5. SUCCESS CRITERIA (ĐO LƯỜNG ĐƯỢC)
- [ ] **SC1:** `HttpContextTenantProvider` đọc claim `"TenantId"` (PascalCase) — khớp với Login.cshtml.cs.
- [ ] **SC2:** `Gateway/OrdersController.CreateOrder` lấy tenant từ JWT claim, throw nếu thiếu — KHÔNG còn `Guid.NewGuid()`.
- [ ] **SC3:** `Gateway/AccountingEntriesController` — `CreateRevenueEntryRequest.TenantId` và `CreateExpenseEntryRequest.TenantId` bị REMOVE; tenant lấy từ JWT claim.
- [ ] **SC4:** `Gateway/AccountingEntriesController` — `ExtractTenantIdFromRequest()` bị REMOVE; thay bằng JWT claim extraction.
- [ ] **SC4b:** `Gateway/ProviderController` — `tenantId` query parameter bị REMOVE; tenant lấy từ JWT claim (DECISION Q2).
- [ ] **SC5:** `VanAnDbContext.ApplyMultiTenancyFilters` — throw nếu `_tenantProvider.TenantId == Guid.Empty` (runtime only, skip design-time).
- [ ] **SC6:** `dotnet build VanAn.sln` — 0 errors.
- [ ] **SC7:** `guard-check.ps1` — PASS.
- [ ] **SC8:** Architecture tests — PASS (không break VA1001-VA1005).
- [ ] **SC9:** Unit tests liên quan tenant — PASS (cần update test mocks nếu signature đổi).
- [ ] **SC10:** Security test: gọi `POST /api/accountingentries/revenue` với body có `TenantId` → request bị reject hoặc TenantId bị ignore (không ghi vào DB).

## 6. ACTIVE SKILLS (MAX 3)
- `domain-integrity-validation` — verify không break Domain
- `system-refactor-safety` — refactor API layer an toàn
- `pattern-based-fixing` — fix theo pattern, không one-by-one

## 7. AI HEALTH CHECK MATRIX (INITIAL)
- **Evidence Count:** 8 defects đã verify bằng grep + read code
- **Verified Facts:**
  - Fact 1: `Login.cshtml.cs:54` writes claim `"TenantId"`, `HttpContextTenantProvider:24-25` reads `"tenant_id"`/`"tenantId"` — MISMATCH confirmed
  - Fact 2: `OrdersController.cs:29` uses `Guid.NewGuid()` — confirmed
  - Fact 3: `AccountingEntriesController.cs:39` uses `request.TenantId` from body — confirmed spoofable
  - Fact 4: `AccountingEntriesController.cs:289` uses `X-Tenant-Id` header — confirmed spoofable
  - Fact 5: `VanAnDbContext.cs:187-190` skips filter if provider null — confirmed silent failure path
  - Fact 6: `Program.cs:165` defines `RequireTenantAccess` policy but ZERO usages in codebase — confirmed dead code
  - Fact 7: 3 Razor pages có hardcoded fallback `00000000-0000-0000-0000-000000000001` — confirmed masking
  - Fact 8: No User-Tenant mapping exists — `DemoUser` không có TenantId field
- **Assumptions:**
  - Login.cshtml.cs claim name `"TenantId"` là chuẩn (vì UI.Platform/TenantService và Gateway OrdersController đều đọc `"TenantId"`)
  - OIDC provider (nếu dùng production) sẽ emit claim `"tenant_id"` per scope — cần verify khi integrate OIDC thật
- **Open Questions:** (0 — RESOLVED 2026-06-18)
  - ~~Q1: Có nên统一 claim name sang `"tenant_id"` (snake_case, OIDC standard) hoặc `"TenantId"` (PascalCase, hiện tại)?~~ → **DECISION: Giữ `"TenantId"` cho Phase 1, chuẩn hóa sang `"tenant_id"` ở Phase 2 khi integrate OIDC thật. Lý do: Phase 1 là stop-the-bleeding, không change claim name để tránh break existing tokens.**
  - ~~Q2: `ProviderController.cs` cũng nhận `tenantId` từ query string — có cần fix trong Phase 1 không?~~ → **DECISION: YES, fix trong Phase 1, cùng pattern với AccountingEntriesController. Lý do: `ProviderController` cũng là Gateway endpoint tenant-scoped, cùng lỗ hổng spoofable.**
- **Recommended Action:** **Continue** — Verified Facts (8) >> Assumptions (2), Open Questions (0). Đủ evidence để implement Phase 1.

## 8. REVERSE IMPACT ANALYSIS
| File thay đổi | Reverse impact | Mitigation |
|---|---|---|
| `HttpContextTenantProvider` claim name | AuditTrailService, ShopService, VanAnDbContext sẽ nhận real TenantId thay vì Empty | ✅ Positive — fix silent failure |
| `OrdersController` bỏ `Guid.NewGuid()` | Orders tạo qua API sẽ cần JWT auth | ⚠️ E2E tests cần update auth setup |
| `AccountingEntriesController` bỏ body TenantId | E2E tests gửi body TenantId sẽ fail | ⚠️ Update E2E + integration tests |
| `AccountingEntriesController` bỏ `X-Tenant-Id` | Tests dùng header sẽ fail | ⚠️ Update tests |
| `VanAnDbContext` throw if Empty | Tests không set tenant sẽ throw | ⚠️ TestTenantProvider đã set tenant — nên OK, nhưng verify |

## 9. TDD & E2E TESTING STRATEGY
- **TDD khuyến khích (Retrofit TDD cho existing code):**
  - Trước khi fix mỗi defect, viết test FAIL trước (reproduce bug)
  - Fix → test PASS
  - Pattern: Red → Green → Refactor
- **E2E Playwright test (nếu có UI thay đổi):**
  - Phase 1 chủ yếu thay đổi API layer + TenantProvider — KHÔNG có UI change trực tiếp
  - NHƯNG: hardcoded fallback removal ở 3 Razor pages (deferred Phase 4) sẽ affect UI → E2E cần update
  - E2E test bắt buộc khi: thay đổi auth flow, tenant resolution visible trên UI
  - Spec files cần update: `accounting-flow.spec.ts`, `order-flow.spec.ts` (auth setup)
- **Test boundary:**
  - Unit tests: `HttpContextTenantProvider`, `VanAnDbContext` tenant filter
  - Integration tests: Gateway controllers (Orders, AccountingEntries) — verify tenant từ JWT
  - Security tests: spoof attempt → reject (body TenantId, X-Tenant-Id header, missing claim)

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

### Micro-phase breakdown cho Phase 1 (Stop the Bleeding)

| Session | JIT Planning (chốt gì) | Pure Execution (viết gì) |
|---|---|---|
| **S1** | Đọc 5 files boundary → chốt: claim name fix, test case names, VanAnDbContext throw point | Fix `HttpContextTenantProvider` + write unit test + fix `VanAnDbContext` throw |
| **S2** | Đọc 2 Gateway controllers → chốt: API signatures mới, test cases spoof | Fix `OrdersController` + `AccountingEntriesController` + write integration tests |
| **S3** | Đọc E2E specs → chốt: auth setup changes | Update E2E test auth + verify build + guard-check |

### Rules
- JIT Planning: MAX 15 phút đọc, chốt output bằng text ngắn (file list + signatures)
- Pure Execution: KHÔNG re-read files, KHÔNG explore thêm, chỉ viết code theo plan
- Nếu phát hiện cần đọc thêm → STOP, ghi vào Open Questions, sang session sau

## 11. ESTIMATED EFFORT
- 2-3 ngày (1 ngày fix + 1 ngày test + 0.5 ngày E2E update + 0.5 ngày buffer)
- 3 sessions (S1-S3) theo JIT Planning
