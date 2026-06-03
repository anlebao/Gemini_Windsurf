# ADR-005: Playwright Isolation

## Status

**Approved** (2026-06-01)

## Context

Playwright E2E tests có xu hướng drive development → fragile tests, AI loops, cost inflation. Cần governance để Playwright là validation tool, không phải development driver.

## Decision

**Playwright DISABLED during IMPLEMENT mode. Chỉ ENABLED trong VALIDATE và FIX_PLAYWRIGHT modes.**

### Mode Matrix

| Mode | Playwright Status |
|------|-------------------|
| ANALYZE | DISABLED |
| IMPLEMENT | DISABLED |
| FIX_ONLY | LIMITED - 1 spec, max 1 run |
| REVIEW_ONLY | DISABLED |
| VALIDATE_ONLY | ENABLED |
| TRIAGE_ONLY | ENABLED |
| FIX_PLAYWRIGHT | ENABLED |

### Core Principles

1. **Test as Contract**: Tests verify acceptance criteria
2. **User owns tests**: AI là proposer, user là approver  
3. **No auto-rewrite**: Không tự động sửa tests khi fail
4. **Root cause first**: Phân tích nguyên nhân trước khi fix

### Execution Rules

- **Pre-requisites**: Build pass + guard-check pass
- **Retry**: Max 1 rerun per spec
- **Cost tiers**: 1 spec (Low), 2-5 specs (Medium), >5 specs (High)

## Consequences

### Positive
- [x] Clear separation code/test
- [x] AI không loop
- [x] Cost control
- [x] Stable tests

### Negative
- [ ] Manual validation trong IMPLEMENT
- [ ] Delayed feedback

## Implementation

- [x] `playwright.rules.md` - Governance
- [x] `playwright_triage.md` - Triage workflow
- [x] `playwright_fix.md` - Fix workflow
- [x] `playwright_validation.md` - Validation workflow
- [x] `playwright_guard.md` - Mode guard
- [x] `playwright-ledger.md` - Execution tracking

## References

- `.windsurf/rules/playwright.rules.md`
- `6_Testing/reports/playwright-ledger.md`

## Notes

- **Date**: 2026-06-01
- **Hard stop**: Never modify `.spec.ts` without approval
