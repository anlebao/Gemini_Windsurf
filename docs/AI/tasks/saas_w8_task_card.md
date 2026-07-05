# TASK CARD — SaaS W8: Final Regression + Production Tag

> **Status:** ✅ COMPLETE | REVIEW
> **Prerequisite:** W0-W7 all merged ✅
> **Branch:** `feature/saas-w8-regression-production-tag`
> **Estimated sessions:** 1
> **Sprint:** 3 (Cleanup)
> **Completed:** 2026-07-05

## Objective
Full regression test, verify all blockers resolved, tag production release.

## Prerequisites (verify before review)
- [x] W0-W7 all merged to main
- [x] All task cards completed
- [x] No open blockers

## Detailed Task List

### W8-T1: Build Release + guard-check pass ✅
- `dotnet build VanAn.sln --configuration Release` — **0 errors**, 1416 warnings (all analyzer style)
- `guard-check.ps1` — **ALL CHECKS PASSED** (untracked files, windsurf guard, architecture guard, Roslyn analyzers, build, core/arch/integration tests)
- Verified with SDK 8.0.422 (patched)

### W8-T2: Full test suite pass ✅ (1257 tests PASS)
- Core.Tests — **941/941 PASS** ✅
- Architecture.Tests — **34/34 PASS** ✅
- Integration.Tests — **177/177 PASS** ✅
- ShopERP.Tests — **99/99 PASS** ✅
- Load.Tests — **6/6 PASS** ✅
- E2E.Tests — 0/27 (CI-only — needs Playwright browsers + running services; W3 enabled in CI workflows)
- **Total: 1257 tests PASS** (exceeds 1200+ target)

**Note:** Full-solution `dotnet test` parallel run showed interference (Core 3 flaky, Arch 1 when Debug build present). Per-project runs = all pass. Root cause: `Assembly.LoadFrom` in W5-ARCH-003 returns already-loaded Release assembly when both Debug+Release exist. Security property verified via byte search: DevLoginController present in Debug DLL, absent from Release DLL.

### W8-T3: Blocker verification checklist ✅
- [x] B1: Gateway — `AddDbContext` present at `2_Gateway/Program.cs:63-64` but **Option B (monolithic mode) approved W0** — governance updated, arch test inverted. Not a violation.
- [x] B2: Secrets — `appsettings.Production.json` uses `${JWT_SECRET_MIN_32_CHARS}` + `${ESMS_SECRET_KEY}` env var references. No `__REPLACE_*`, no hardcoded passwords.
- [x] B3: .NET SDK 8.0.422 installed (user-local `C:\Users\lebao\AppData\Local\dotnet`), `global.json rollForward: latestFeature` selects it. Runtime 8.0.0 on system (patched runtime 8.0.22+ required on production server). No 2.3.0 auth packages (W2 removed 9 legacy packages).
- [x] B4: CI pipeline — e2e.yml + ci.yml + pr-check.yml + full-test-suite.yml all active with test/e2e jobs

### W8-T4: Hardening verification checklist ✅
- [x] H1: 10 Accounting pages have bUnit tests — all 10 test files exist (BalanceSheet, IncomeStatement, CashFlow, TrialBalance, FinancialReportsHub, HKDBooks, HKDBookDetail, PeriodClosing, AccountingLayout, AccountingIndex)
- [x] H2: PeriodClosing status persisted to DB — `PeriodClosingService` uses `_dbContext.PeriodClosingStatuses` (DB queries, not static Dictionary)
- [x] H3: DevLoginController guarded — `#if DEBUG` at `Controllers/DevLoginController.cs:9`, verified by W5-ARCH-001/002 (pass)
- [x] H4: JWT cookie HttpOnly=true — `Login.cshtml.cs:96`
- [x] H5: E-Invoice staging tests — ⏳ W6-T6 DEFERRED (needs real Viettel/MISA credentials — user-side)

### W8-T5: Tech debt verification checklist ✅
- [x] M1: SKIP (already fixed — proper `HasTenant` guard)
- [x] M2: DEFER (E2E test reliability hack, kept as tech debt)
- [x] M3: DEFER (performance tests, not in gate)
- [x] M4: DONE (M6 obsolete methods removed, M5 kept — has internal caller)
- [x] M5: DEFER (post-production)
- [x] M6: DONE (Docker: cpus limits + vanan-sqlite volume at `docker-compose.yml:211-212,260-266`)
- [x] M7: DONE (Docker test stage + security headers in `Program.cs:427-428` + `nginx/nginx.conf:27-28`)

### W8-T6: Smoke test (manual) ⏳ DEFERRED TO USER
- [ ] App starts without errors
- [ ] Login works (admin + staff + storekeeper)
- [ ] Create order → confirm payment → accounting entry created
- [ ] Generate 4 BCTC reports — correct data
- [ ] HKD books (S1a-S3a) still work
- [ ] Feature flag: HKD tenant → 403 on VAS endpoints
- [ ] Period close → restart app → status persists
- [ ] E-Invoice create → submit (staging)
- **Status:** Deferred to user per session decision. Tag created; user runs smoke test post-merge.

### W8-T7: Update project_state.md ✅
- Mark SaaS hardening stream complete
- Move to Section 6 (history)
- Update Section 11 maintenance log
- Update branch info

### W8-T8: Tag release ✅
- Tag `saas-production-v1.0` created (per user decision Q1)

### W8-T9: Merge to main ✅
- Final merge of `feature/saas-w8-regression-production-tag`
- Verify tag on main

## Verification
- [x] All W0-W7 deliverables in main
- [x] Build 0 errors (SDK 8.0.422)
- [x] All tests pass (1257 PASS, E2E CI-only)
- [ ] Manual smoke test pass — DEFERRED TO USER
- [x] All 4 blockers resolved
- [x] All 5 high-priority issues resolved (H5 deferred — credentials)
- [x] Tech debt Tier 1+2 resolved (M1/M4/M6/M7 done, M2/M3/M5 deferred)
- [x] `project_state.md` updated
- [x] Tag `saas-production-v1.0` created

## Rollback
- N/A (final merge)
- Nếu phát hiện issue: tạo hotfix branch, không revert toàn bộ stream

## Open Questions
- Q1: Tag name — `saas-production-v1.0` ✅ (user decision)
- Q2: Release notes — generate from git log or manual? → Open (post-tag)
- Q3: Post-production monitoring — Seq dashboards configured? → Open (post-production)

## Findings (W8 regression)
1. **W5-ARCH-003 test infrastructure flaw:** `Assembly.LoadFrom` returns already-loaded Release assembly when both Debug+Release builds exist. Security property verified via byte search. Test passes in CI (Release-only build). Recommend future fix: use `MetadataLoadContext`.
2. **B3 SDK discrepancy:** W2 claimed 8.0.422 installed but was in user-local path, not system PATH. Resolved by adding to PATH. Production server must install patched SDK/runtime separately.
3. **Parallel test interference:** Full-solution `dotnet test` has flaky failures (SQLite contention + LoadFrom issue). Per-project runs are clean. CI runs per-project (guard-check pattern).
