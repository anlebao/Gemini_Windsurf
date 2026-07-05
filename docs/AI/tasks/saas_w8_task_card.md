# TASK CARD — SaaS W8: Final Regression + Production Tag

> **Status:** NOT STARTED | REVIEW
> **Prerequisite:** W0-W7 all merged
> **Branch:** `feature/saas-w8-regression-production-tag`
> **Estimated sessions:** 1
> **Sprint:** 3 (Cleanup)

## Objective
Full regression test, verify all blockers resolved, tag production release.

## Prerequisites (verify before review)
- [ ] W0-W7 all merged to main
- [ ] All task cards completed
- [ ] No open blockers

## Detailed Task List

### W8-T1: Build Release + guard-check pass
- `dotnet build VanAn.sln --configuration Release` — 0 errors
- `guard-check.ps1` — ALL CHECKS PASSED

### W8-T2: Full test suite pass
- Core.Tests — all PASS (baseline 910 + new W4/W5 tests)
- Architecture.Tests — all PASS (31 + W0 Gateway fix test)
- Integration.Tests — all PASS (173 + W3 CI restore + W6 EInvoice staging)
- ShopERP.Tests — all PASS (49 + W4 bUnit tests)
- E2E.Tests — all PASS (W3 CI restore, 21 specs)
- Total: target 1200+ tests PASS

### W8-T3: Blocker verification checklist
- [ ] B1: Gateway — no `AddDbContext` in `2_Gateway/Program.cs`
- [ ] B2: Secrets — no `__REPLACE_*` in appsettings, no hardcoded passwords
- [ ] B3: .NET SDK 8.0.22+, no 2.3.0 auth packages
- [ ] B4: CI pipeline — E2E + Integration enabled and PASS

### W8-T4: Hardening verification checklist
- [ ] H1: 10 Accounting pages have bUnit tests
- [ ] H2: PeriodClosing status persisted to DB
- [ ] H3: DevLoginController guarded
- [ ] H4: JWT cookie HttpOnly=true
- [ ] H5: E-Invoice staging tests PASS (local, gated)

### W8-T5: Tech debt verification checklist
- [ ] M1+M2: No tenant fallback hardcode
- [ ] M3+M4: No JS interop workaround (if fixable)
- [ ] M5-M7: Obsolete methods removed
- [ ] M8-M11: Docker hardening (limits, volume, security headers)

### W8-T6: Smoke test (manual)
- [ ] App starts without errors
- [ ] Login works (admin + staff + storekeeper)
- [ ] Create order → confirm payment → accounting entry created
- [ ] Generate 4 BCTC reports — correct data
- [ ] HKD books (S1a-S3a) still work
- [ ] Feature flag: HKD tenant → 403 on VAS endpoints
- [ ] Period close → restart app → status persists
- [ ] E-Invoice create → submit (staging)

### W8-T7: Update project_state.md
- Mark SaaS hardening stream complete
- Move to Section 6 (history)
- Update Section 11 maintenance log
- Update branch info

### W8-T8: Tag release
```bash
git tag saas-production-v1.0 -m "SaaS Production v1.0 — Multi-tenant ready (2026-07-XX)"
```

### W8-T9: Merge to main
- Final merge of `feature/saas-w8-regression-production-tag`
- Verify tag on main

## Verification
- [ ] All W0-W7 deliverables in main
- [ ] Build 0 errors
- [ ] All tests pass (1200+ target)
- [ ] Manual smoke test pass
- [ ] All 4 blockers resolved
- [ ] All 5 high-priority issues resolved
- [ ] Tech debt Tier 1+2 resolved
- [ ] `project_state.md` updated
- [ ] Tag `saas-production-v1.0` created

## Rollback
- N/A (final merge)
- Nếu phát hiện issue: tạo hotfix branch, không revert toàn bộ stream

## Open Questions
- Q1: Tag name — `saas-production-v1.0` or `v1.0.0-saas`?
- Q2: Release notes — generate from git log or manual?
- Q3: Post-production monitoring — Seq dashboards configured?
