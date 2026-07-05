# TASK CARD — SaaS W0.5: Stream D Completion (HKD Wave 8 Session 2 Cherry-pick)

> **Status:** NOT STARTED | INVESTIGATE → IMPLEMENT
> **Prerequisite:** VAS Stream F complete (W0-W9 merged)
> **Branch:** `feature/saas-w0p5-stream-d-completion`
> **Estimated sessions:** 1 (small wave — cherry-pick + verify)
> **Sprint:** 1 (Blockers)
> **Source:** Stream D Wave 8 Session 2 (commit `c387608` on `feature/hkd-fix-wave8-ui-docx-export-regression`)

## Objective
Cherry-pick 4 files từ Stream D Wave 8 Session 2 (commit `c387608`) vào main. Đóng Stream D (HKD Book Accounting Report Fix) — 12 waves hoàn tất.

## Context
Stream D Wave 8 Session 1 đã merged (`c8eb819` — UI + export + DI). Session 2 (commit `c387608`) trên branch `feature/hkd-fix-wave8-ui-docx-export-regression` **chưa merge** vì branch outdated so với main (main đã tiến xa với VAS W6-W9 + SaaS planning). Cần cherry-pick 4 files mới (không conflict) thay vì merge whole branch.

## Prerequisites (verify before code)
- [ ] VAS Stream F complete (1114/1114 tests PASS)
- [ ] Verify commit `c387608` tồn tại: `git show c387608 --stat`
- [ ] Verify 4 files chưa có trên main: `git ls-files 6_Testing/e2e-tests/hkd-books.spec.ts 6_Tests/VanAn.Architecture.Tests/HKDBookTemplateArchitectureTests.cs scripts/check-encoding.ps1 docs/UI_Platform_Implementation_Guide.md`
- [ ] Verify Stream D Wave 8 Session 1 đã merged (`c8eb819`): `git log --oneline c8eb819 -1`

## Files to Cherry-pick (4 new files from `c387608`)

| File | Lines | Purpose |
|------|-------|---------|
| `6_Testing/e2e-tests/hkd-books.spec.ts` | 135 | 6 E2E tests (list, detail, TT 152 layout, export buttons, nav) |
| `6_Tests/VanAn.Architecture.Tests/HKDBookTemplateArchitectureTests.cs` | 85 | 3 regression tests (Issue 1: all HKDBookTemplate extend BaseHKDBookTemplate, 7 templates present, CalculateAsync not abstract) |
| `scripts/check-encoding.ps1` | 90 | SC7 mojibake encoding lint (fixed false-positive detection — scans 2-char lead+continuation sequences, 923 files, 0 mojibake) |
| `docs/UI_Platform_Implementation_Guide.md` | +52 | Wave 8 HKD Book module reference section |

## Detailed Task List

### W0.5-T1: Create feature branch
```bash
git checkout main
git pull origin main  # ensure latest
git checkout -b feature/saas-w0p5-stream-d-completion
```

### W0.5-T2: Cherry-pick 4 files from `c387608`
```bash
# Cherry-pick only the 4 new files (no project_state.md — outdated)
git checkout c387608 -- 6_Testing/e2e-tests/hkd-books.spec.ts
git checkout c387608 -- 6_Tests/VanAn.Architecture.Tests/HKDBookTemplateArchitectureTests.cs
git checkout c387608 -- scripts/check-encoding.ps1
git checkout c387608 -- docs/UI_Platform_Implementation_Guide.md
git add 6_Testing/e2e-tests/hkd-books.spec.ts 6_Tests/VanAn.Architecture.Tests/HKDBookTemplateArchitectureTests.cs scripts/check-encoding.ps1 docs/UI_Platform_Implementation_Guide.md
```

### W0.5-T3: Verify build + guard + tests
- `dotnet build VanAn.sln --configuration Release` — 0 errors
- `guard-check.ps1` — ALL CHECKS PASSED
- `dotnet test VanAn.sln --filter "Category!=Performance"` — all tests pass
- Verify new arch tests: `dotnet test 6_Tests/VanAn.Architecture.Tests --filter "HKDBookTemplate"` — 3/3 PASS
- Verify E2E spec lists: `npx playwright test --list 6_Testing/e2e-tests/hkd-books.spec.ts` — 6 tests listed
- Run encoding lint: `pwsh scripts/check-encoding.ps1` — 0 mojibake

### W0.5-T4: Commit
```
[SAAS W0.5] Stream D Completion — cherry-pick HKD Wave 8 S2 (4 files)

Cherry-pick from c387608 (Stream D Wave 8 Session 2):
- hkd-books.spec.ts: 6 E2E tests (TT 152 layout + export buttons)
- HKDBookTemplateArchitectureTests.cs: 3 regression tests (Issue 1)
- check-encoding.ps1: SC7 mojibake lint (923 files, 0 mojibake)
- UI_Platform_Implementation_Guide.md: Wave 8 HKD Book module reference

Stream D (HKD Book Accounting Report Fix) COMPLETE — 12 waves done.
```

### W0.5-T5: Merge to main
- Merge `feature/saas-w0p5-stream-d-completion` to main
- Delete feature branch
- Stream D officially closed

### W0.5-T6: Update project_state.md
- Mark Stream D Wave 8 COMPLETE & MERGED
- Move Stream D to "Parked / Completed Streams" section
- Update Stream G W0.5 status

## Verification
- [ ] 4 files exist on main after merge
- [ ] `dotnet build` — 0 errors
- [ ] `guard-check.ps1` — ALL CHECKS PASSED
- [ ] All existing 1114 tests PASS
- [ ] 3 new arch tests PASS (HKDBookTemplateArchitectureTests)
- [ ] 6 E2E tests listed (Playwright --list)
- [ ] Encoding lint: 0 mojibake across 923 files
- [ ] Stream D marked COMPLETE in project_state.md

## Rollback
- Git revert (remove 4 files)
- If arch tests fail: investigate HKDBookTemplate subclasses, may need fix
- If E2E spec has import errors: fix imports (env-config, test-reporter paths)

## Open Questions
- Q1: `docs/UI_Platform_Implementation_Guide.md` — file đã có trên main? (Verify — nếu có thì merge content thay vì overwrite)
- Q2: E2E spec imports (`../utils/env-config`, `../utils/test-reporter`) — paths đúng với main? (Verify utils dir exists)
- Q3: Stream D branch `feature/hkd-fix-wave8-ui-docx-export-regression` — delete sau khi W0.5 merged? (Yes — Stream D closed)
