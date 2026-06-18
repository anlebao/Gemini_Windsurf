# TASK CARD: [TENANTID REMEDIATION] - [PHASE 4] - CLEANUP & UNIFICATION

## 1. GOAL & CONTEXT
- **Mục tiêu cốt lõi:** Dọn dẹp technical debt còn sót sau Phase 1-3: xóa hardcoded fallbacks, refactor 6 Razor pages sang `ITenantProvider`, unify 5 patterns thành 1 (JWT claim → ITenantProvider).
- **Nghiệp vụ áp dụng:** Code quality + DRY — một pattern duy nhất cho tenant resolution toàn codebase.

## 2. ACTIVE WORKFLOW ROUTING
- **Target Workflow:** `.devin/workflows/newfeaturebuild.md`
- **Execution Mode:** ANALYZE → IMPLEMENT (cần Phase 2 + 3 merged trước khi bắt đầu)

## 3. RELEVANT FILES (CONTEXT BOUNDARY)
- **Files được phép đọc/sửa:**
  - `docs/AI/project_state.md` (Bắt buộc đọc đầu phiên)
  - `5_WebApps/ShopERP/Components/Pages/Accounting/TransactionHistory.razor` — remove fallback, use ITenantProvider
  - `5_WebApps/ShopERP/Components/Pages/Accounting/ExpenseEntry.razor` — remove fallback, use ITenantProvider
  - `5_WebApps/ShopERP/Components/Pages/Accounting/PeriodClosing.razor` — remove hardcoded GetTenantId(), use ITenantProvider
  - `5_WebApps/ShopERP/Components/Pages/Accounting/RevenueEntry.razor` — use ITenantProvider
  - `5_WebApps/ShopERP/Components/Pages/Accounting/AccountingIndex.razor` — use ITenantProvider
  - `5_WebApps/ShopERP/Components/Pages/Accounting/AccountBalance.razor` — use ITenantProvider
  - `UI.Platform/Services/TenantService.cs` — verify claim name consistency
- **Boundary Rules (Nghiêm cấm):**
  - CẤM sửa Domain.cs
  - CẤM thay đổi API signatures (chỉ refactor internal resolution)
  - CẤM break existing tests

## 4. TECHNICAL & REGULATORY CONSTRAINTS (HARDENING GATES)
- [ ] **DRY:** 1 pattern duy nhất: `@inject ITenantProvider TenantProvider` → `TenantProvider.TenantId`. Không manual `FindFirst`.
- [ ] **No fallback:** Nếu `ITenantProvider.TenantId == Guid.Empty` → throw, không hardcode.
- [ ] **UI Compliance:** Không thay đổi UI, chỉ thay đổi code-behind tenant resolution.
- [ ] **Test stability:** Tests phải vẫn pass sau refactor.

## 5. SUCCESS CRITERIA (ĐO LƯỜNG ĐƯỢC)
- [ ] **SC1:** 0 occurrences của `00000000-0000-0000-0000-000000000001` trong production code (chỉ allowed trong test code).
- [ ] **SC2:** 0 occurrences của `user.FindFirst("TenantId")` hoặc `user.FindFirst("tenant_id")` trong Razor pages — tất cả dùng `ITenantProvider`.
- [ ] **SC3:** 0 occurrences của `Guid.NewGuid()` cho tenant trong production code.
- [ ] **SC4:** 6 Razor pages dùng `@inject ITenantProvider` thay vì manual claim lookup.
- [ ] **SC5:** `PeriodClosing.razor` — `GetTenantId()` method bị remove, dùng `ITenantProvider.TenantId`.
- [ ] **SC6:** `dotnet build VanAn.sln` — 0 errors.
- [ ] **SC7:** `guard-check.ps1` — PASS.
- [ ] **SC8:** Architecture tests — PASS.
- [ ] **SC9:** Tất cả existing tests — PASS (không regression).
- [ ] **SC10:** Grep verify: `grep -r "00000000-0000-0000-0000-000000000001" --include="*.razor"` → 0 results.

## 6. ACTIVE SKILLS (MAX 3)
- `system-refactor-safety`
- `pattern-based-fixing`
- `test-system-upgrade` (nếu cần retrofit tests)

## 7. AI HEALTH CHECK MATRIX (INITIAL)
- **Evidence Count:** 3 cleanup items đã verify
- **Verified Facts:**
  - Fact 1: 3 Razor pages có hardcoded `00000000-0000-0000-0000-000000000001` fallback
  - Fact 2: 6 Razor pages dùng `user.FindFirst("TenantId")` manual thay vì `ITenantProvider`
  - Fact 3: `PeriodClosing.razor:180` có method `GetTenantId()` return hardcoded tenant
- **Assumptions:**
  - Sau Phase 2, `ITenantProvider` sẽ return real tenant (không Empty) — Phase 4 chỉ việc swap
  - Blazor components có thể `@inject ITenantProvider` (scoped service)
- **Open Questions:** (0 — Phase 4 là mechanical refactor)
- **Recommended Action:** **Continue** —纯 refactor, low risk.

## 8. REVERSE IMPACT ANALYSIS
| File thay đổi | Reverse impact | Mitigation |
|---|---|---|
| 6 Razor pages → ITenantProvider | Components cần inject thêm dependency | Verify DI scope (Scoped OK cho Blazor Server) |
| Remove fallback | Nếu ITenantProvider return Empty → crash | Phase 2 đảm bảo không Empty |
| Remove `GetTenantId()` method | Callers cần update | Chỉ PeriodClosing internal |

## 9. TDD & E2E TESTING STRATEGY
- **TDD (Retrofit TDD — refactor existing code):**
  - Trước khi refactor mỗi Razor page, viết test FAIL: page với `ITenantProvider.TenantId == Empty` → should throw
  - Refactor → test PASS
  - Verify: 0 hardcoded fallbacks sau refactor
- **E2E Playwright test (verify không regression):**
  - Phase 4 là refactor internal — UI không thay đổi visible
  - NHƯNG: cần chạy lại toàn bộ E2E specs để verify không break
  - Spec files: `accounting-flow.spec.ts`, `period-closing-flow.spec.ts`, `balance-dashboard-flow.spec.ts`, `audit-trail-flow.spec.ts`
  - Test case: login → access Accounting pages → data hiển thị đúng (không empty, không crash)
  - Test case: ITenantProvider return Empty → page hiển thị error rõ ràng (không silent empty)
- **Test boundary:**
  - Unit tests: mỗi Razor page @code block với ITenantProvider mock
  - Integration tests: Blazor component rendering với tenant context
  - E2E tests: full Accounting flow sau refactor

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

### Micro-phase breakdown cho Phase 4 (Cleanup)

| Session | JIT Planning (chốt gì) | Pure Execution (viết gì) |
|---|---|---|
| **S1** | Đọc 3 pages có fallback → chốt: ITenantProvider inject syntax, throw point | Refactor TransactionHistory + ExpenseEntry + PeriodClosing |
| **S2** | Đọc 3 pages còn lại → chốt: FindFirst → ITenantProvider swap | Refactor RevenueEntry + AccountingIndex + AccountBalance |
| **S3** | Grep verify → chốt: 0 occurrences hardcoded, 0 manual FindFirst | Run E2E + verify build + guard-check |

### Rules
- JIT Planning: MAX 15 phút đọc, chốt output bằng text ngắn
- Pure Execution: KHÔNG re-read, chỉ viết code theo plan
- Mechanical refactor — low risk, nhưng verify grep sau mỗi session

## 11. ESTIMATED EFFORT
- 1-2 ngày (0.5 ngày refactor + 0.5 ngày test + 0.5 ngày verify grep + 0.5 ngày buffer)
- 3 sessions (S1-S3) theo JIT Planning
