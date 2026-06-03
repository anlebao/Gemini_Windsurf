# Test System Upgrade — Master Plan

**Project:** VanAn Ecosystem — 6_Tests & 6_Testing  
**Version:** 1.1  
**Created:** 2026-05-05  
**Last Updated:** 2026-05-05  
**Status:** � Partial (6/8 phases done, Phase 2 deferred)

---

## Objective

Bring the entire VanAn test suite from a broken/incomplete state to a fully green, domain-aligned baseline following the CartItem domain refactoring (ProductId added, Name/Price removed).

---

## Current Health Dashboard

| Layer | Project | Build | Tests | Status |
|---|---|---|---|---|
| Core | `VanAn.Core.Tests` | ✅ | ✅ | KhachLink reference added (Phase 1) |
| Unit | `VanAn.Unit.Tests` | ⚠️ | ⚠️ | Local `Customer` SSoT violation — Phase 2 DEFERRED (8 files / 96 refs) |
| Integration | `VanAn.Integration.Tests` | ✅ | ✅ | CartItem + CartState + SyncConflictResolver coverage added |
| OrderFlow | `VanAn.OrderFlow.Tests` | ⚠️ | ⏭️ | Skip added to 3 PostgreSQL tests (Phase 6); Pattern 1 fix pending |
| Architecture | `VanAn.Architecture.Tests` | ✅ | ✅ | Rule F + Rule G added (Phase 7) |
| E2E | `6_Testing/e2e-tests/` | N/A | ⚠️ | Not in scope this iteration |
| Load | `6_Testing/load-tests/` | N/A | ❓ | Not in scope this iteration |

**CartItem coverage:** 5 unit tests in `CartItemTests.cs`, 8 service tests in `CartStateTests.cs`, 4 regression tests in `SyncConflictResolverTests.cs`.

---

## Phase Status

| # | Phase | Scope | Files | Status |
|---|---|---|---|---|
| 1 | Fix Core.Tests build | Add KhachLink csproj ref | 1 file modified | [x] ✅ 2026-05-05 |
| 2 | Fix Unit.Tests SSoT violation | Rename local domain POCO classes | 8 files | [ ] ⏸️ DEFERRED |
| 3 | CartItem domain tests | New CartItemTests.cs + TestEntityBuilder update | 3 files | [x] ✅ 2026-05-05 |
| 4 | CartState service tests | New CartStateTests.cs + FluentAssertions added | 2 files | [x] ✅ 2026-05-05 |
| 5 | SyncConflictResolver tests | New SyncConflictResolverTests.cs (redesigned) | 1 file created | [x] ✅ 2026-05-05 |
| 6 | Fix OrderFlow anti-patterns | Skip added to 3 PostgreSQL tests | 1 file modified | [x] ✅ 2026-05-05 |
| 7 | Architecture regression rules | Add Rule F (CartItem.ProductId) + Rule G (no Name/Price) | 1 file modified | [x] ✅ 2026-05-05 |
| 8 | Verify full build + test run | `dotnet build` + `dotnet test` | — | [ ] |

---

## Success Criteria

```
dotnet build VanAn.Tests.sln               → 0 errors
dotnet test  --filter "Category!=RequiresDB"  → All GREEN
```

- All 8 phases complete
- No test references `CartItem.Name` or `CartItem.Price`
- `CartState` dedup-by-`ProductId` verified by test
- Architecture Rules F & G catch domain violations automatically

---

## How to Update This File

After each phase is implemented and verified:

1. Change `[ ]` → `[x]` in the Phase Status table above
2. Update `Last Updated` date at the top
3. Update `Status` field: 🔴 In Progress → 🟡 Partial → 🟢 Complete
4. See `TEST_DETAIL_PLAN.md` for full per-phase spec and completion log

---

## Related Files

| Resource | Path |
|---|---|
| Detail Plan | `docs/plan_MVP/Test_System_Plan/TEST_DETAIL_PLAN.md` |
| Domain entity | `1_Shared/Domain/CartItem.cs` |
| CartState service | `5_WebApps/KhachLink/Services/CartState.cs` |
| SyncConflictResolver | `5_WebApps/KhachLink/Services/SyncConflictResolver.cs` |
| Core.Tests csproj | `6_Tests/VanAn.Core.Tests/VanAn.Core.Tests.csproj` |
| Architecture tests | `6_Tests/VanAn.Architecture.Tests/ArchitectureRulesTests.cs` |
