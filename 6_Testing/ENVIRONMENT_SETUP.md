# Environment Configuration for E2E Tests

## Overview

E2E tests now use centralized environment configuration through `loadEnvConfig()` in `utils/env-config.ts`. This ensures consistent behavior across different environments (local, staging, production).

## Environment Files

### Local Development (Default)
- **File**: `.env.test`
- **Purpose**: Local development testing
- **URLs**: All services use `localhost` ports
  - Gateway: `http://localhost:5001`
  - KhachLink: `http://localhost:5002`
  - ShopERP: `http://localhost:5003`

### Production Testing
- **Template**: `.env.production.example`
- **Setup**: Copy to `.env.test` and set `VANAN_DOMAIN` to your production domain
- **URLs**: Derived from `VANAN_DOMAIN` env var
  - Gateway: `https://api.${VANAN_DOMAIN}`
  - KhachLink: `https://${VANAN_DOMAIN}`
  - ShopERP: `https://app.${VANAN_DOMAIN}`

## Configuration Structure

### Environment Variables in `.env.test`

```bash
# Service URLs
GATEWAY_URL=http://localhost:5001
KHACHLINK_URL=http://localhost:5002
SHOPERP_URL=http://localhost:5003
OMNICHANNEL_URL=http://localhost:5002

# Test Credentials
ADMIN_USERNAME=admin
ADMIN_PASSWORD=admin123
KITCHEN_USERNAME=kitchen
KITCHEN_PASSWORD=kitchen123
TEST_EMAIL=admin@vanan.vn
TEST_PASSWORD=dev123
```

### Usage in Test Files

**✅ CORRECT (New approach):**
```typescript
import { loadEnvConfig } from '../utils/env-config';

const config = loadEnvConfig();

// Use config values
const shopErpUrl = config.SHOPERP_URL;
await page.goto(`${shopErpUrl}/dashboard`);
```

**❌ INCORRECT (Old approach):**
```typescript
// Don't use process.env directly
const shopErpUrl = process.env.SHOPERP_URL || 'http://localhost:5003';
```

## Switching Environments

### To test locally:
1. Use default `.env.test` (already configured for localhost)
2. Run: `npx playwright test`

### To test production:
1. Copy `.env.production.example` to `.env.test`
2. Update URLs and credentials for production
3. Run: `npx playwright test`

### To test specific environment:
1. Create `.env.staging` with staging URLs
2. Update `env-config.ts` to load staging file if needed
3. Or override via command line:
   ```bash
   OMNICHANNEL_URL=https://staging.example.com npx playwright test
   ```

## Playwright Configuration

The `playwright.config.ts` uses environment-aware configuration:

```typescript
const config = loadEnvConfig();

projects: [
  {
    name: 'e2e-tests',
    use: { baseURL: config.SHOPERP_URL }
  },
  {
    name: 'omnichannel-e2e',
    use: { baseURL: config.OMNICHANNEL_URL }
  }
]
```

## Benefits

1. **Consistency**: All tests use the same configuration source
2. **Flexibility**: Easy to switch between environments
3. **Maintainability**: Single source of truth for URLs and credentials
4. **Security**: Credentials not hardcoded in test files
5. **CI/CD Ready**: Environment variables can be set in CI/CD pipelines

## Files Modified

1. ✅ `utils/env-config.ts` - Updated fallback values for local development
2. ✅ `playwright.config.ts` - Already using config (no changes needed)
3. ✅ `.env.test` - Updated with clear comments
4. ✅ `omnichannel-order-lifecycle.spec.ts` - Using config instead of process.env
5. ✅ `accounting-entry-flow.spec.ts` - Using config instead of process.env
6. ✅ `provider-management.spec.ts` - Using config instead of process.env
7. ✅ `invoice-management.spec.ts` - Using config instead of process.env
8. ✅ `einvoice-dashboard.spec.ts` - Using config instead of process.env
9. ✅ `.env.production.example` - Created as production template
10. ✅ `ENVIRONMENT_SETUP.md` - This documentation file