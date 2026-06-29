# Omnichannel E2E Test Suite for vanantech.io.vn

## Overview

Comprehensive End-to-End (E2E) automation test suite using Playwright (TypeScript) for the vanantech.io.vn platform, testing the full omnichannel order lifecycle across different actor perspectives.

## Architecture & Behavior

- **URL**: https://vanantech.io.vn
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
# Omnichannel Configuration
OMNICHANNEL_URL=https://vanantech.io.vn
GATEWAY_PUBLIC_URL=https://api.vanantech.io.vn

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
    baseURL: 'https://vanantech.io.vn',
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