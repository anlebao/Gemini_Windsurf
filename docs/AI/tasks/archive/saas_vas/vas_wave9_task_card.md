# TASK CARD — VAS Wave 9: Regression + Merge (REVIEW ONLY)

> **Status:** NOT STARTED | REVIEW
> **Prerequisite:** W8 merged (feature flag complete)
> **Branch:** `feature/vas-wave9-regression-merge`
> **Estimated sessions:** 1

## Objective
Full regression, merge to main, update project state.

## Prerequisites (verify before review)
- [ ] W0-W8 all merged to main
- [ ] All task cards completed

## Detailed Task List

### W9-T1: guard-check.ps1 pass
### W9-T2: dotnet build VanAn.sln Release pass, 0 errors
### W9-T3: All tests pass
- Core.Tests (including W0 OrderServiceTests + W7 numeric tests)
- Architecture.Tests (28+ including W8 TenantType isolation)
- Integration.Tests (including W5 endpoint tests + W6 E2E)

### W9-T4: Manual smoke test
- 4 BCTC render đúng với seed data
- Verify BS: TotalAssets == TotalLiabilities + TotalEquity
- Verify IS: NetProfit calculated
- Verify CF: ClosingCash == OpeningCash + NetChange
- Verify TB: TotalDebit == TotalCredit

### W9-T5: HKD reports regression check
- S1a-S3a vẫn hoạt động
- HKD tenant không thấy VAS menu
- HKD tenant không truy cập VAS API

### W9-T6: Order→Payment→Accounting flow check (W0 fix)
- Order confirm payment tạo JE có VAT tách (511 net + 3331)
- PaymentMethod truyền đúng → 111 vs 112
- COGS Path A == Path B
- Period dùng OrderDate

### W9-T7: Update project_state.md
- Mark VAS stream complete
- Move to Section 6 (history)
- Update Section 11 maintenance log

### W9-T8: Merge to main
- Final merge of feature/vas-wave9-regression-merge
- Tag release if needed

## Verification
- [ ] All W0-W8 deliverables in main
- [ ] Build 0 errors
- [ ] All tests pass
- [ ] Manual smoke test pass
- [ ] HKD no regression
- [ ] project_state.md updated

## Rollback
- N/A (final merge)
- Nếu phát hiện issue: tạo hotfix branch, không revert toàn bộ stream

## Open Questions
- Q1: Có cần tag release?
- Q2: Có cần update README hay docs khác?
