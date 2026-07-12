# Omnichannel E2E Test Suite

## Overview

Comprehensive End-to-End (E2E) automation test suite using Playwright (TypeScript) for the VanAn platform, testing the full omnichannel order lifecycle across different actor perspectives.

## Architecture & Behavior

- **URL**: configured via `VANAN_DOMAIN` env var (e.g. `https://khachvip.online`)
- **Architecture**: Hybrid Online/Offline-First with NATS Sync Workers
- **Synchronization**: 1-second polling interval for NATS sync from edge storage to cloud PostgreSQL
- **Critical Timing**: Tests use fluent polling assertions instead of hardcoded sleep to handle async NATS synchronization

## Test Scenarios

### SCENARIO 1: First-Time Guest Omnichannel Order Flow (Guest Web to Handover)

**Actors:**
1. **First-Time Customer (Guest Web User)**: Browse menu, add items to cart, checkout as guest
2. **Shop Owner / Receptionist (Admin Dashboard)**: Accept and process order
3. **Kitchen / Barista Station (KDS)**: Mark order as Preparing → Ready
4. **Customer Handover & Payment**: View status updates, QR code payment, complete order

**Key Validations:**
- Order creation and ID generation
- NATS sync timing between stations (5s timeout)
- Status propagation across Customer → Admin → Kitchen → Customer
- QR code display and payment completion

### SCENARIO 2: Returning Loyalty Customer Flow (PWA / Installed App Simulator)

**Actors:**
1. **Returning Customer**: Login with existing profile, view loyalty points, apply points, submit order
2. **Cross-Station Processing**: Receptionist → Kitchen processing chain
3. **Completion & Points Update**: Handover, payment, verify points balance update

**Key Validations:**
- Loyalty points balance display
- Points redemption during checkout
- Points calculation (earned - spent)
- Data propagation across omnichannel

### SCENARIO 3: Network Interruption / Edge Offline Resiliency

**Actors:**
1. **Customer**: Place order during network failure (offline mode)
2. **Network Restoration**: Restore network, verify NATS sync worker flushes Outbox
3. **Admin**: Verify order appears on dashboard after sync

**Key Validations:**
- UI responsiveness during offline mode
- Outbox pattern local persistence
- NATS background sync worker automatic flush
- Order appears on admin dashboard within 10s of network restoration

## Technical Implementation

### Page Object Model (POM)

```
e2e-tests/
├── pages/
│   ├── CustomerPage.ts      # Customer/Guest UI interactions
│   ├── AdminPage.ts         # Admin Dashboard interactions
│   └── KitchenPage.ts       # Kitchen Display System interactions
├── utils/
│   └── test-data-cleaner.ts # Data isolation and cleanup utilities
└── omnichannel-order-lifecycle.spec.ts  # Test scenarios
```

### Key Features

1. **Fluent Polling for NATS Sync**: Uses `expect(...).toBeVisible({ timeout: 5000 })` instead of `page.waitForTimeout()`
2. **Data Isolation**: Unique test phone numbers/email per test to prevent pollution
3. **Automatic Cleanup**: Test data cleanup after each test execution
4. **Video/Screenshot on Failure**: Audit trails for debugging
5. **HTTP Basic Auth Support**: Configurable for staging environments

## Configuration

### Environment Variables (.env.test)

```bash
# Omnichannel Configuration (derived from VANAN_DOMAIN)
VANAN_DOMAIN=khachvip.online
OMNICHANNEL_URL=https://${VANAN_DOMAIN}
GATEWAY_PUBLIC_URL=https://api.${VANAN_DOMAIN}

# Test Credentials
ADMIN_USERNAME=admin
ADMIN_PASSWORD=admin123
KITCHEN_USERNAME=kitchen
KITCHEN_PASSWORD=kitchen123

# HTTP Basic Auth (if staging protected)
# STAGING_USERNAME=
# STAGING_PASSWORD=
```

### Playwright Configuration (playwright.config.ts)

```typescript
{
  name: 'omnichannel-e2e',
  testMatch: 'e2e-tests/omnichannel*.spec.ts',
  use: {
    baseURL: process.env.OMNICHANNEL_URL || 'http://localhost:5002',
    trace: 'on-first-retry',
    screenshot: 'only-on-failure',
    video: 'retain-on-failure',
    httpCredentials: {
      username: process.env.STAGING_USERNAME,
      password: process.env.STAGING_PASSWORD
    }
  }
}
```

## Running Tests

### Install Dependencies

```bash
cd 6_Testing
npm install
```

### Install Playwright Browsers

```bash
npx playwright install
```

### Run All Omnichannel Tests

```bash
npx playwright test --project=omnichannel-e2e
```

### Run Specific Scenario

```bash
# Scenario 1: Guest Order Flow
npx playwright test --project=omnichannel-e2e -g "SCENARIO 1"

# Scenario 2: Loyalty Customer Flow
npx playwright test --project=omnichannel-e2e -g "SCENARIO 2"

# Scenario 3: Network Resiliency
npx playwright test --project=omnichannel-e2e -g "SCENARIO 3"
```

### Run in Debug Mode

```bash
npx playwright test --project=omnichannel-e2e --debug
```

### Run with UI Mode

```bash
npx playwright test --project=omnichannel-e2e --ui
```

### View Test Reports

```bash
npx playwright show-report
```

## NATS Sync Timing Strategy

### Why Not Hardcoded Sleep?

The Hybrid Online/Offline architecture uses NATS Sync Workers with 1-second polling intervals. Hardcoded sleep (`page.waitForTimeout(5000)`) is unreliable because:

1. Network latency varies
2. Database load affects sync speed
3. Race conditions can cause flaky tests

### Fluent Polling Approach

```typescript
// ❌ DON'T DO THIS
await page.waitForTimeout(5000);
await expect(orderElement).toBeVisible();

// ✅ DO THIS INSTEAD
await expect(orderElement).toBeVisible({ timeout: 5000 });
```

This approach:
- Fails fast if element appears early
- Waits up to timeout if sync is slow
- Provides better test reliability
- Reduces overall test execution time

## Data Isolation Strategy

### Unique Test Data Generation

```typescript
// Phone numbers: TEST + timestamp + random
const phone = `TEST${Date.now()}${Math.floor(Math.random() * 1000)}`;

// Emails: test + timestamp + random @ vanantest.io.vn
const email = `test${Date.now()}${Math.floor(Math.random() * 1000)}@vanantest.io.vn`;
```

### Automatic Cleanup

Each test automatically cleans up:
- Orders created during test
- Customer accounts created for test
- Test data by phone number

## Troubleshooting

### Tests Fail Due to NATS Sync Timeout

**Symptom**: `waitForOrderToAppear` times out after 5s

**Solutions**:
1. Check NATS sync worker is running
2. Verify network connectivity to cloud PostgreSQL
3. Increase timeout in test (default: 5000ms)
4. Check Outbox table for queued messages

### Tests Fail Due to Selector Changes

**Symptom**: `locator.click` or `expect.toBeVisible` fails

**Solutions**:
1. Run test in debug mode: `npx playwright test --debug`
2. Use Playwright Inspector to update selectors
3. Check if UI platform components changed
4. Verify test environment matches expected UI

### Tests Fail Due to Authentication

**Symptom**: Login fails or 401 errors

**Solutions**:
1. Verify credentials in .env.test
2. Check if staging site requires HTTP Basic Auth
3. Update httpCredentials in playwright.config.ts
4. Verify user accounts exist in test environment

## Maintenance

### Updating Selectors

When UI changes:
1. Run affected test in debug mode
2. Use Playwright Inspector to find new selectors
3. Update corresponding Page Object Model file
4. Re-run test to verify fix

### Adding New Test Scenarios

1. Create new test in `omnichannel-order-lifecycle.spec.ts`
2. Follow existing pattern with `test.step()` for clarity
3. Use fluent polling for NATS sync timing
4. Add cleanup logic in `afterEach`
5. Update this README with scenario description

### Performance Optimization

- Use `test.step()` for better test reporting
- Reuse browser contexts where possible
- Minimize unnecessary page reloads
- Use network interception for offline tests (not full browser offline)

## Notes

- Tests are designed for staging/production-like environments
- Local development may require mock services
- Ensure test environment has NATS sync workers running
- Video recordings are stored in `reports/test-results/` on failure
- HTML reports are generated in `reports/playwright-html-report/`

## Support

For issues or questions:
1. Check Playwright documentation: https://playwright.dev
2. Review test execution logs in console
3. Examine video recordings for failure scenarios
4. Check network tab in browser for API failures

---

## Anti-Patterns (Stream B — E2E Test Cleanup)

> **Status:** 7 anti-patterns identified and fixed across Waves 1-7 (2026-07-02 to 2026-07-03).
> **Regression prevention:** `npm run lint:e2e` scans all `*.spec.ts` files for these patterns.
> **Helper:** `utils/strict-assert.ts` provides safe alternatives.

### Pattern A — OR-Tautology Assertions (Wave 7)

**Anti-pattern:** `expect(hasX || hasY || hasZ).toBeTruthy()` — passes with ANY state, tests nothing.

```typescript
// ❌ BAD — passes if page renders anything at all
const hasSuccess = await page.locator('[class*="success"]').count() > 0;
const hasError = await page.locator('[class*="error"]').count() > 0;
expect(hasSuccess || hasError).toBeTruthy();

// ✅ GOOD — scoped to validation card, specific alert class
await expect(
  validationCard.locator('.alert-success, .alert-danger')
).toBeVisible();
```

**Fix:** Read the Razor UI code to identify the canonical expected state. Assert that specific state. If two states are genuinely valid alternatives, use `assertOneOf(page, ['.x', '.y'])` from `strict-assert.ts`.

**Exception:** Security tests may use `isLoginPage || isForbidden || hasAccessDenied` — each condition represents a distinct, valid access-control response. This is NOT a tautology because each condition is specific and security-relevant. Add a comment explaining the intentional OR.

### Pattern B — Silent-Skip (Wave 6)

**Anti-pattern:** `if (await element.isVisible()) { ...assert... }` with no `else` branch — passes vacuously when the element is absent.

```typescript
// ❌ BAD — passes without testing anything if button is absent
if (await btn.isVisible()) {
  await btn.click();
  await expect(page.locator('.result')).toBeVisible();
}

// ✅ GOOD — explicit skip with reason
const visible = await btn.isVisible();
if (!visible) test.skip(true, 'Button not present on this page variant');
await btn.click();
await expect(page.locator('.result')).toBeVisible();
```

**Fix:** Add `test.skip(condition, reason)` or a hard `expect().toBeVisible()`. Use `assertVisibleOrSkip(page, selector, reason)` from `strict-assert.ts`.

### Pattern C — Reachability Smoke Tests (Wave 5)

**Anti-pattern:** Multiple tests that only check `response.status() !== 404` — low value, clutter the suite.

**Fix:** Consolidate into `test.step()` within a single `gateway-smoke.spec.ts` file. Each step asserts `status !== 404 && status !== 500`, preserving strictness while reducing test count.

### Pattern D — Wrong Auth Pattern (Wave 2)

**Anti-pattern:** Filling a login form (`#username`/`#email`/`#password`) that doesn't exist, or overriding `storageState` with empty cookies in authenticated tests.

```typescript
// ❌ BAD — /login form doesn't exist in ShopERP (uses /dev/login)
await page.fill('#username', 'admin');
await page.fill('#password', 'VanAn@2026');
await page.click('button[type="submit"]');
await page.waitForURL('/dashboard');

// ✅ GOOD — global storageState from auth/admin.json (playwright.config.ts L34, L56)
// No explicit login needed — global-setup.ts handles it via /dev/login
```

**Fix:** Rely on global `storageState` (`auth/admin.json`) applied by `playwright.config.ts`. For unauthenticated tests, use `test.use({ storageState: { cookies: [], origins: [] } })` inside a named `test.describe('Unauthenticated access')` block with an explanatory comment.

### Pattern F — Decorative `reporter.pass()` (Wave 1)

**Anti-pattern:** Calling `reporter.pass()` after a conditional check — creates the illusion of verification without an actual `expect()` assertion.

```typescript
// ❌ BAD — reporter.pass() is decorative, not a real assertion
if (await page.locator('.alert').isVisible()) {
  reporter.pass('Alert displayed');
}

// ✅ GOOD — use expect() for real assertions
await expect(page.locator('.alert')).toBeVisible();
```

**Fix:** Remove all `reporter.pass()` calls. Use `expect()` for real assertions. `reporter.log()` is fine for informational logging.

### Pattern G1 — Anti-Schema Tests (Wave 3)

**Anti-pattern:** Asserting API response fields that don't exist in the actual controller return type — hallucinated schema.

```typescript
// ❌ BAD — VoiceCommandController returns { Success: bool }, not { Command, Executed }
expect(result.Command.CommandText).toBe('order');
expect(result.Executed).toBe(true);

// ✅ GOOD — verify against actual controller return type
expect(result.Success).toBe(true);
```

**Fix:** Read the actual C# controller to verify the response schema before asserting. Delete tests that assert hallucinated fields.

### Pattern G2 — Anti-UI Tests (Wave 4)

**Anti-pattern:** Asserting selectors for UI features that don't exist in the codebase.

```typescript
// ❌ BAD — .loyalty-points selector doesn't exist in KhachLink
await expect(page.locator('.loyalty-points')).toBeVisible();

// ✅ GOOD — verify selector exists in Razor before using it
// grep -r "loyalty-points" 5_WebApps/KhachLink/ → 0 matches → don't test it
```

**Fix:** Verify selectors exist in the Razor/UI code before writing tests. Delete tests for features that don't exist.

### Lint Script

Run `npm run lint:e2e` to scan all `*.spec.ts` files for these 7 anti-patterns:

```bash
npm run lint:e2e
# ✅ No anti-pattern violations found across 20 spec file(s).
```

The lint script (`utils/anti-pattern-lint.ts`) uses regex patterns to detect violations and exits with code 1 if any are found. It is context-aware — it skips comment lines and recognizes intentional patterns marked with explanatory comments (e.g., "unauthenticated", "AUTH_LIFECYCLE_TEST", "Removed redirectedAway").

### Strict Assert Helper

`utils/strict-assert.ts` provides three helper functions:

| Function | Replaces | Description |
|---|---|---|
| `assertOneOf(page, selectors, opts)` | Pattern A (OR-tautology) | Asserts at least one selector is visible; returns which one. Fails if NONE visible. |
| `assertVisibleOrSkip(page, selector, reason)` | Pattern B (silent-skip) | Asserts visible or explicitly `test.skip()` with reason. |
| `assertUrlMatches(page, pattern, message)` | Pattern A (URL OR-tautology) | Hard-asserts URL matches a regex. No fallback. |

These helpers are **optional** — existing tests are not required to migrate. New tests should prefer them over hand-rolled OR-tautology or silent-skip patterns.