# E2E Testing Infrastructure

This directory contains Playwright E2E tests for the VanAn ecosystem.

## Prerequisites

- Docker Desktop installed and running
- .NET 8 SDK
- Node.js 18+ with npm

## Service Startup for E2E Tests

### Option 1: Using docker-compose.test.yml (Recommended for E2E tests)

```bash
cd 6_Testing
docker-compose -f docker-compose.test.yml up -d
```

This will start:
- Gateway (port 5001)
- KhachLink (port 5002)
- ShopERP (port 5003)

### Option 2: Using full ecosystem

```bash
# From project root
docker-compose -f docker-compose.testing.yml up -d
```

### Verify Services are Healthy

```bash
# Check Gateway
curl http://localhost:5001/health

# Check KhachLink
curl http://localhost:5002/health

# Check ShopERP
curl http://localhost:5003/health
```

## Running E2E Tests

### Install Dependencies

```bash
cd 6_Testing
npm install
```

### Run All E2E Tests

```bash
npm run test:e2e
```

### Run Specific Test File

```bash
npx playwright test accounting-entry-flow.spec.ts
```

### Run with UI Mode (Debug)

```bash
npx playwright test --ui
```

## Test Configuration

Edit `.env.test` to configure:

- Service URLs (GATEWAY_URL, SHOPERP_URL, etc.)
- Test timeouts (E2E_TEST_TIMEOUT)
- Test credentials (TEST_EMAIL, TEST_PASSWORD)

## Authentication

E2E tests use the dev login endpoint (`/dev/login`) which is only available in Development mode. This endpoint:

- Issues a real Cookie auth session with a fixed TenantId
- Returns a JWT token for API calls
- Bypasses OIDC (no external identity server needed)

## Cleanup

```bash
# Stop services
docker-compose -f docker-compose.test.yml down

# Remove volumes (if needed)
docker-compose -f docker-compose.test.yml down -v
```

## Troubleshooting

### Services not starting

Check Docker Desktop is running and ports 5001-5003 are not in use.

### Tests failing with connection refused

Ensure all services are healthy before running tests:
```bash
docker-compose -f docker-compose.test.yml ps
```

### Auth failures

Verify ShopERP is running in Development mode (ASPNETCORE_ENVIRONMENT=Development).

## Reports

Test reports are generated in:
- HTML: `reports/playwright-html-report/index.html`
- JSON: `reports/playwright-report.json`
- JUnit: `reports/playwright-junit.xml`
