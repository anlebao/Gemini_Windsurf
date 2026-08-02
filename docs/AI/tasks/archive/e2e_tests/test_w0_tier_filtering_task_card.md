# TASK CARD — Test System W0: E2E Tier Filtering (Smoke / Golden / Full)

> **Status:** 📋 PLANNING → ready for IMPLEMENT
> **Prerequisite:** Master plan approved · **Branch:** `feature/test-w0-tier-filtering`
> **Estimated sessions:** 1

## Objective

Split 704 E2E tests into 3 tiers with tag-based filtering. CI runs only relevant tiers per context (PR → Smoke+Golden, merge → Full, nightly → All×browsers).

## Tier Definition

| Tier | Tag | Count (est.) | Description | CI context |
|------|-----|-------------|-------------|------------|
| Tier 0 (Smoke) | `@smoke` | ~10 | Health checks (gateway, shoperp, khachlink, db, nats) | Every PR |
| Tier 1 (Golden) | `@golden` | ~30 | 3 critical business flows | Every PR |
| Tier 2 (Full) | (no tag) | ~700 | All remaining tests | Merge to main + nightly |

### Golden Path tests (Tier 1 — `@golden` tag)

**Flow A: Order placement**
- `order-flow.spec.ts`: "Customer can view product catalog"
- `order-flow.spec.ts`: "Customer can add items to cart"
- `order-flow.spec.ts`: "Customer can place order"
- `order-flow.spec.ts`: "Staff can view orders in ShopERP"
- `order-flow.spec.ts`: "Staff can update order status"
- `order-tracking.spec.ts`: all 6 tests (tracking page renders, timeline, checkout redirect)

**Flow B: Payment**
- `qr-payment-ui.spec.ts`: "Checkout page has Thanh toán QR trigger button"
- `qr-payment-ui.spec.ts`: "Clicking Thanh toán QR opens #qrPaymentModal"
- `qr-payment.spec.ts`: "TC_QR_Generation - Gateway returns valid response"
- `qr-payment.spec.ts`: "TC_QR_Validation - Gateway validates correctly"

**Flow C: Kitchen → Dashboard**
- `omnichannel-order-lifecycle.spec.ts`: "SCENARIO 1: First-Time Guest Omnichannel Order Flow"

**Flow D: Smoke (already exists)**
- `gateway-smoke.spec.ts`: all tests (already in smoke project)

## Files to Modify

| File | Action | Changes |
|------|--------|---------|
| `6_Testing/playwright.config.ts` | UPDATE | Add 3 projects: `tier-smoke`, `tier-golden`, `tier-full` with grep filters |
| `6_Testing/utils/env-config.ts` | UPDATE | Add `E2E_TIER` config option (smoke/golden/full) |
| `.github/workflows/e2e.yml` | UPDATE | Split e2e-tests job into 2: `e2e-pr` (Tier 0+1) + `e2e-full` (all) |
| `.github/workflows/pr-check.yml` | UPDATE | Add E2E Tier 0+1 step (if not already) |

## Implementation Plan

### W0-T1: Tag golden path tests

Add `@golden` tag to test.describe or test() titles in:
- `order-flow.spec.ts` — 5 tests
- `order-tracking.spec.ts` — 6 tests
- `qr-payment-ui.spec.ts` — 2 tests (modal open/close)
- `qr-payment.spec.ts` — 2 tests (API)
- `omnichannel-order-lifecycle.spec.ts` — 1 test (SCENARIO 1)

**Pattern:**
```typescript
// Before:
test('Customer can place order', async ({ page }) => { ... })
// After:
test('Customer can place order @golden', async ({ page }) => { ... })
```

### W0-T2: Update playwright.config.ts

Add 3 project entries with grep filtering:
```typescript
{
  name: 'tier-smoke',
  testMatch: 'smoke-tests/**/*.spec.ts',
  use: { baseURL: config.COREHUB_URL, ... }
},
{
  name: 'tier-golden',
  testMatch: 'e2e-tests/**/*.spec.ts',
  grep: '@golden',
  use: { baseURL: config.SHOPERP_URL, storageState: 'auth/admin.json', ... }
},
{
  name: 'tier-full',
  testMatch: 'e2e-tests/**/*.spec.ts',
  grepInvert: '@slow',  // keep existing slow filter
  use: { baseURL: config.SHOPERP_URL, storageState: 'auth/admin.json', ... }
},
```

### W0-T3: Update env-config.ts

```typescript
// Add to TestConfig interface:
E2E_TIER: 'smoke' | 'golden' | 'full' | 'all';

// Add to loadEnvConfig defaults:
E2E_TIER: (process.env.E2E_TIER as any) || 'all',
```

### W0-T4: Update CI workflows

**e2e.yml:** Split `e2e-tests` job:
- `e2e-pr`: runs `--project=tier-smoke --project=tier-golden` (on PR + push to feature)
- `e2e-full`: runs `--project=tier-full` (on push to main + nightly)

**pr-check.yml:** Add E2E smoke step (Tier 0 only, ~30s).

### W0-T5: Verify

- `npx playwright test --list --project=tier-smoke` → ~10 tests
- `npx playwright test --list --project=tier-golden` → ~16-20 tests
- `npx playwright test --list --project=tier-full` → ~700 tests
- `dotnet build VanAn.sln` → 0 errors
- Commit

## Verification Checklist

- [ ] `@golden` tag added to 16+ tests across 5 spec files
- [ ] `playwright.config.ts` has 3 tier projects
- [ ] `env-config.ts` has `E2E_TIER` option
- [ ] CI e2e.yml split into PR (Tier 0+1) + Full (Tier 2)
- [ ] `npx playwright test --list` parse OK for all 3 tiers
- [ ] `dotnet build` 0 errors
