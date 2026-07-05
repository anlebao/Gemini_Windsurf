# MASTER PLAN — SaaS Production Hardening (Multi-Tenant Deploy)

> **Status:** 🟡 SPRINT 2 IN PROGRESS (W0-W4, 5/9 waves) — Sprint 1 complete, W4 complete pending merge
> **Created:** 2026-07-05 · **Last Updated:** 2026-07-05 (Sprint 2 W4 complete — 44 new bUnit tests for 3 missing Accounting pages)
> **Workflow:** `newfeaturebuild.md` (ANALYZE → IMPLEMENT) · **Branch:** per-wave feature branch, always-green main
> **Prerequisite:** VAS Stream F complete (W0-W9 merged, 1114/1114 tests PASS)
> **Source:** Production readiness review 2026-07-05 (3 subagent audit + manual verify)

---

## 0. JIT PLANNING STRATEGY (NON-NEGOTIABLE)

**Nguyên tắc cốt lõi:** KHÔNG code mò mẫm — **Investigate trước, Implement sau**. Áp dụng cho mỗi wave.

### 3-Phase per wave
```
Phase 1 (INVESTIGATE): Đọc task card wave + verify codebase hiện tại
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
- **Mỗi wave có 1 task card** tại `docs/AI/tasks/saas_w{N}_task_card.md`
- Task card chứa: objective, prerequisites, exact file changes, code snippets, verification, rollback
- **Task card phải được đọc TRƯỚC khi code** (Phase 1)
- **Task card có thể update** nếu INVESTIGATE phát hiện drift
- **Task card KHÔNG thay thế master plan** — master plan là chiến lược, task card là chiến thuật

### Anti-Guessing Gate (Gate 1 từ .windsurfrules)
- Assumptions ≥ Verified Facts → CẤM code, chuyển Investigate
- Mỗi wave phải có ≥ 3 verified facts trước khi implement:
  1. File path tồn tại (verify bằng read/glob)
  2. Method signature đúng (verify bằng grep)
  3. Dependency chain đúng (verify bằng trace)

---

## 1. EXECUTION RULES

### Dependency chain
```
W0 (Gateway Fix) ─┐
W1 (Secrets)     ─┤
W2 (.NET Upgrade) ─┼→ W3 (CI Restore) → W7 (Tech Debt) → W8 (Regression + Tag)
W4 (UI Tests)    ─┤
W5 (Period+Auth) ─┤
W6 (E-Invoice)   ─┘
```
- W0-W2: Blockers (Sprint 1) — có thể song song trong nhiều session
- W3: CI Restore — phụ thuộc W0-W2 clean
- W4-W6: Hardening (Sprint 2) — có thể song song
- W7: Tech Debt — phụ thuộc W0-W6
- W8: Final Regression — phụ thuộc tất cả
- **W0.5 CANCELLED:** Stream D (HKD Book Fix) đã merged vào main qua `68580bc` (2026-07-04). Verify `git merge-base --is-ancestor c387608 main` = 0. Không cần cherry-pick.
- Mỗi wave xong: `dotnet build VanAn.sln` Release pass + `guard-check.ps1` pass + commit

### Session protocol
1. Mỗi session làm 1 wave (W4 có thể 2-3 session do 10 trang UI)
2. Bắt đầu session: đọc `project_state.md` + task card wave
3. Trước session end: build pass + commit
4. Commit format: `[SAAS W{N}] <short description>`

### Branch protocol
```
main ← feature/saas-w0-gateway-architecture-fix
main ← feature/saas-w1-secrets-config-hardening
main ← feature/saas-w2-dotnet-upgrade-package-security
main ← feature/saas-w3-ci-pipeline-restore
main ← feature/saas-w4-ui-test-coverage
main ← feature/saas-w5-period-persist-auth-hardening
main ← feature/saas-w6-einvoice-real-verification
main ← feature/saas-w7-tech-debt-cleanup
main ← feature/saas-w8-regression-production-tag
```

---

## 2. AUDIT FINDINGS SUMMARY (2026-07-05)

### 2.1. Blockers (🔴 Must fix before production)

| # | Blocker | File | Severity | Wave fix |
|---|---------|------|----------|----------|
| B1 | Gateway registers DbContext (vi phạm pure proxy) | `2_Gateway/Program.cs:54-58` | 🔴 | W0 |
| B2 | Secrets hardcoded: JWT placeholder, default password, connection string | `ShopERP/Program.cs:261,341` + `appsettings.Production.json` | 🔴 | W1 |
| B3 | .NET 8.0.100 outdated (CVEs: DoS, RCE, Info Disclosure) + old auth packages 2.3.0 | `global.json` + `Directory.Packages.props:54-56` | 🔴 | W2 |
| B4 | E2E tests + Integration tests disabled in CI | `.github/workflows/e2e.yml:115` + `ci.yml:198` | 🔴 | W3 |

### 2.2. High Priority (⚠️ Should fix)

| # | Issue | File | Wave fix |
|---|-------|------|----------|
| H1 | 10/14 Accounting pages + 5/5 Admin + 6/6 EInvoice pages thiếu bUnit tests | `6_Tests/VanAn.ShopERP.Tests/` | W4 |
| H2 | PeriodClosing status store in-memory (mất khi restart) | `3_CoreHub/Services/PeriodClosingService.cs` | W5 |
| H3 | DevLoginController không guard kỹ (lộ dev login nếu env misconfigured) | `5_WebApps/ShopERP/Controllers/DevLoginController.cs` | W5 |
| H4 | Cookie JWT thiếu HttpOnly (XSS risk) | `5_WebApps/ShopERP/Pages/Login.cshtml.cs:94-100` | W5 |
| H5 | E-Invoice providers unverified với credentials thật | `3_CoreHub/Services/Providers/EInvoice/` | W6 |

### 2.3. Tech Debt (📋 Medium priority)

| # | Debt | File | Wave fix |
|---|------|------|----------|
| M1 | Tenant fallback hardcode (Tier 1) | `TransactionHistory.razor:187`, `ExpenseEntry.razor:211` | W7 |
| M2 | JS interop workaround cho @bind | `ExpenseEntry.razor:222`, `App.razor:18` | W7 |
| M3 | 5 flaky performance tests | `ProductionDataTests.cs` | W7 |
| M4 | HKDBookService obsolete methods (0 callers) | `HKDBookService.cs:356,709` | W7 |
| M5 | NotImplemented trong sync/audit services | `SyncStrategyService`, `DataVersioningService` | W7 |
| M6 | Docker: no resource limits, no SQLite volume | `docker-compose.yml` | W7 |
| M7 | Docker: tests disabled in Dockerfile | `ShopERP/Dockerfile:33` | W7 |

### 2.4. What's Already Good (✅ No action needed)

| Khu vực | Trạng thái |
|---------|-----------|
| HKD Books (7 sổ) | ✅ Hoàn thiện, production ready |
| VAS 4 BCTC | ✅ Hoàn thiện, 1114 tests PASS |
| Domain Layer | ✅ Sạch, immutable, pure |
| Multi-tenancy (logic) | ✅ Enforced, query filters |
| Feature Flag W8 | ✅ Hoàn thiện, HKD→403 |
| UI Platform (26 components) | ✅ Đầy đủ |
| Order→Payment→Accounting | ✅ Hoạt động, VAT split |
| Database migrations (5) | ✅ Chain complete |
| Error handling controllers | ✅ Consistent try/catch |

---

## 3. SCOPE DECISIONS (APPROVED 2026-07-05)

| # | Quyết định | Lựa chọn |
|---|-------------|----------|
| D1 | Deploy target | SaaS multi-tenant độc lập |
| D2 | Gateway fix approach | Remove DbContext, chuyển services sang ShopERP (monolithic mode) |
| D3 | Secrets management | Environment variables + appsettings.Production.json (no Key Vault cho V1) |
| D4 | .NET version | Upgrade 8.0.100 → 8.0.22 (stay on .NET 8 LTS, không nhảy .NET 9) |
| D5 | CI strategy | Enable E2E + Integration trong CI, fix auth files cho multi-role |
| D6 | UI test priority | Accounting pages trước (10 trang), Admin + EInvoice sau |
| D7 | Period closing persist | Persist ra DB (PeriodClosingStatus table) |
| D8 | DevLogin guard | `#if DEBUG` conditional compilation + environment check |
| D9 | E-Invoice verification | Staging environment test với real Viettel/MISA credentials |
| D10 | Tech debt | Tier 1 (M1) before production, Tier 2-3 (M2-M7) can defer |

---

## 4. WAVE OVERVIEW (9 waves — W0-W8)

| Wave | Tên | Mode | Sprint | Task Card | Status |
|------|-----|------|--------|-----------|--------|
| W0 | Gateway Architecture Fix | IMPLEMENT | 1 | `saas_w0_task_card.md` | ✅ DONE — Option B (monolithic mode) approved & merged. Governance rule updated, arch test inverted. 1133/1133 tests PASS. |
| W1 | Secrets + Production Config Hardening | IMPLEMENT | 1 | `saas_w1_task_card.md` | ✅ DONE — fail-fast in Production (4 Program.cs locations) + mandatory script params (3 scripts) + config validation. **Gap fix:** appsettings.Production.json `__REPLACE_*` → `${VAR}` env var references + ValidateProductionConfig detects unresolved `${VAR}`. 1133/1133 tests PASS. |
| W2 | .NET SDK Upgrade + Package Security | IMPLEMENT | 1 | `saas_w2_task_card.md` | ✅ DONE — Removed 9 legacy 2.3.0 auth packages + FrameworkReference for .NET 8 shared framework. **Gap fix:** SDK 8.0.100 → 8.0.422 installed (CVEs patched). global.json `rollForward: latestFeature` auto-selects 8.0.422. 1133/1133 tests PASS. |
| W3 | CI Pipeline Restore (E2E + Integration) | IMPLEMENT | 1 | `saas_w3_task_card.md` | ✅ DONE — Multi-role DevLogin endpoints (Staff/StoreKeeper/Guard), global-setup generates 4 auth files, rbac-enforcement.spec.ts real tests (7 skip→0), ci.yml + e2e.yml + pr-check.yml all re-enabled. **Gap fix:** GoldenFlow 2 test failures fixed (ITenantProvider not registered in test DI → IgnoreQueryFilters added). 1133/1133 tests PASS. |
| W4 | UI Test Coverage (10 Accounting pages) | IMPLEMENT | 2 | `saas_w4_task_card.md` | ✅ DONE (pending merge) — 44 new bUnit tests for 3 missing pages (HKDBooks 10 + HKDBookDetail 15 + PeriodClosing 19). 7/10 pages already had 38 tests from VAS W6. bUnit + `@rendermode InteractiveServer` limitation documented (click tests → reflection/render assertions; full interaction → Playwright E2E). Build 0 errors, guard PASS, 44/44 new tests PASS. |
| W5 | Period Closing Persist + Auth Hardening | IMPLEMENT | 2 | `saas_w5_task_card.md` | ⏳ |
| W6 | E-Invoice Real Integration Verification | IMPLEMENT | 2 | `saas_w6_task_card.md` | ⏳ |
| W7 | Tech Debt Cleanup (Tier 1+2) | IMPLEMENT | 3 | `saas_w7_task_card.md` | ⏳ |
| W8 | Final Regression + Production Tag | REVIEW | 3 | `saas_w8_task_card.md` | ⏳ |

**Chi tiết từng wave:** xem task card tương ứng. Master plan chỉ giữ overview.

### 4.1. Sprint Mapping

```
Sprint 1 (Blockers):    W0 → W1 → W2 → W3
Sprint 2 (Hardening):   W4 → W5 → W6
Sprint 3 (Cleanup):     W7 → W8
```

- Sprint 1: 4 waves, mỗi wave 1 session (~2-3 ngày)
- Sprint 2: 3 waves, W4 có thể 2-3 session (10 trang UI)
- Sprint 3: 2 waves, W7 cleanup + W8 regression
- **W0.5 CANCELLED:** Stream D đã merged (`68580bc`) — không cần cherry-pick

---

## 5. RISK REGISTER

| # | Risk | Mitigation | Wave |
|---|------|------------|------|
| R1 | Gateway fix break ApiKeyRepository (cần IVanAnDbContext) | W0: Move ApiKeyRepository sang ShopERP hoặc dùng HTTP call | W0 |
| R2 | .NET upgrade break build (API changes 8.0.8→8.0.22) | W2: Build sau upgrade, fix breaking changes, guard pass | W2 |
| R3 | E2E tests fail do service setup issues (lý do disable) | W3: Fix service setup, generate auth files, incremental enable | W3 |
| R4 | UI tests cho 10 trang = effort lớn | W4: Ưu tiên 4 BCTC pages + PeriodClosing, defer Admin/EInvoice | W4 |
| R5 | Period closing persist migration break existing data | W5: Migration with default Open status cho existing periods | W5 |
| R6 | E-Invoice real API test tốn tiền (Viettel/MISA) | W6: Dùng staging/test accounts, limit calls | W6 |
| R7 | Tech debt cleanup break working code | W7: Mỗi change có test, guard pass sau mỗi file | W7 |
| R8 | Gateway architecture test (Gateway_Architecture_No_DbContext) fail hiện tại | W0: Fix test + fix Gateway code | W0 |
| R9 | Auth packages 2.3.0 removal break cookie auth | W2: Replace với 8.0.x versions, test login flow | W2 |
| R10 | Docker SQLite no volume = data loss | W7: Add volume mount, document in deployment guide | W7 |

---

## 6. SUCCESS CRITERIA

### Sprint 1 (Blockers) — MUST PASS:
- [ ] Gateway KHÔNG register DbContext (pure proxy)
- [ ] `Gateway_Architecture_No_DbContext_Registered` test PASS (check đúng type `IVanAnDbContext`)
- [ ] Zero hardcoded secrets trong production code
- [ ] `appsettings.Production.json` không có placeholder `__REPLACE_*`
- [ ] .NET SDK 8.0.22+, zero CVEs
- [ ] Auth packages 8.0.x (không 2.3.0)
- [ ] CI pipeline: E2E + Integration tests ENABLED và PASS
- [ ] Build 0 errors, guard pass, all tests pass

### Sprint 2 (Hardening) — SHOULD PASS:
- [x] 10 Accounting pages có bUnit tests (BS, IS, CF, TB, HKDBooks, HKDBookDetail, PeriodClosing, FinancialReports, AccountingLayout, AccountingIndex) — **W4 DONE** (38 existing from W6 + 44 new = 82 total bUnit tests)
- [ ] PeriodClosing status persisted to DB (survive restart)
- [ ] DevLoginController guarded (`#if DEBUG` + env check)
- [ ] JWT cookie HttpOnly=true
- [ ] E-Invoice: Viettel + MISA verified với real credentials (staging)

### Sprint 3 (Cleanup + Tag) — NICE TO HAVE:
- [ ] Tier 1 tech debt resolved (tenant fallback hardcode)
- [ ] Tier 2 tech debt resolved (JS interop workaround)
- [ ] HKDBookService obsolete methods removed
- [ ] Docker: resource limits + SQLite volume mount
- [ ] Full regression: 1114+ tests PASS (baseline + new tests)
- [ ] Tag: `saas-production-v1.0`

---

## 7. REFERENCES

- **Source review:** Production readiness review 2026-07-05 (3 subagent audit)
- **VAS stream (complete):** `docs/AI/tasks/vas_enterprise_reports_master_plan.md`
- **Tech debt ledger:** `5_WebApps/ShopERP/TECHNICAL_DEBT_LEDGER.md`
- **Governance:** `.devin/rules/governance.md`
- **Workflow:** `.devin/workflows/newfeaturebuild.md`
- **Architecture rules:** `.windsurfrules` (CRITICAL ARCHITECTURAL BOUNDARIES section)
- **Task cards:** `docs/AI/tasks/saas_w{0-8}_task_card.md`
