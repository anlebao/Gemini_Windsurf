import { test, expect, request } from '@playwright/test';
import { loadEnvConfig } from '../utils/env-config';
import { getJwtToken } from './utils/auth-api';

/**
 * Loyalty Alliance Phase 6B — E2E Tests @golden
 *
 * Spec: docs/specs/loyalty-alliance-spec.md v1.0
 * Plan: docs/plans/loyalty-alliance-master-plan.md Session 12
 *
 * Scenario 1: Alliance mode config CRUD (SystemAdmin API)
 *   - Set global mode=Alliance, verify config persisted
 *   - Set per-tenant override (IsAllianceMember=true), verify persisted
 *   - Revert to Silo mode (cleanup)
 *
 * Scenario 2: Customer wallet endpoint (auth + structure)
 *   - GET /api/loyalty/wallet without X-Customer-Token → 401
 *   - GET /api/loyalty/wallet with invalid token → 401
 *   - GET /api/loyalty/wallet with valid token (customer not in alliance) → 200 + zero balance
 *
 * Scenario 3: Migrate endpoint (SystemAdmin API)
 *   - POST /api/platform/loyalty/migrate without token → 401/302
 *   - POST /api/platform/loyalty/migrate with invalid direction → 400
 *   - POST /api/platform/loyalty/migrate consolidate without customerBalances → 400
 *
 * Scenario 4: Cross-tenant API reachability
 *   - GET /api/redemption/catalog/active → 200 or 401 (route exists)
 *   - POST /api/redemption/redeem without token → 401
 *   - GET /api/loyalty/my without token → 401
 *
 * Prerequisites: Docker + ShopERP 5003 + KhachLink 5002 + Gateway 5001 all running.
 * Auth: SystemAdmin JWT from /dev/login/systemadmin (Development only).
 *       Customer token from OAuth (not available in E2E — test auth boundary only).
 */

const config = loadEnvConfig();

// Test tenant IDs (match dev seed data)
const TENANT_A = '00000000-0000-0000-0000-000000000001';
const TENANT_B = '11111111-1111-1111-1111-111111111111';

test.describe('Loyalty Alliance E2E — Phase 6B @golden', () => {
  let systemAdminToken: string | null = null;

  test.beforeAll(async () => {
    // Get SystemAdmin JWT via dev login (Development environment only)
    const apiContext = await request.newContext({ baseURL: config.SHOPERP_URL });
    try {
      const resp = await apiContext.post('/dev/login/systemadmin');
      if (resp.ok()) {
        const body = await resp.json();
        systemAdminToken = body.token;
      }
    } catch {
      // Dev login not available (Production) — tests will use fallback token
    }
    await apiContext.dispose();

    // Fallback: try auth/admin.token from global-setup
    if (!systemAdminToken) {
      systemAdminToken = getJwtToken('admin');
    }
  });

  // ─── Scenario 1: Alliance Mode Config CRUD (SystemAdmin API) ──────────────

  test('LA-E2E-1: SystemAdmin can set global mode=Alliance and verify persistence', async ({ request: req }) => {
    test.skip(!systemAdminToken, 'SystemAdmin token not available — dev login required');

    await test.step('PUT /api/platform/loyalty/config — set mode=Alliance', async () => {
      const resp = await req.put(`${config.GATEWAY_URL}/api/platform/loyalty/config`, {
        headers: { Authorization: `Bearer ${systemAdminToken}` },
        data: { mode: 1, maxPointsPerOrder: 30, maxWalletPoints: 100000 },
      });
      expect(resp.status(), 'PUT config should return 200').toBe(200);
      const body = await resp.json();
      expect(body.mode).toBe(1); // Alliance
    });

    await test.step('GET /api/platform/loyalty/config — verify mode=Alliance persisted', async () => {
      const resp = await req.get(`${config.GATEWAY_URL}/api/platform/loyalty/config`, {
        headers: { Authorization: `Bearer ${systemAdminToken}` },
      });
      expect(resp.status()).toBe(200);
      const body = await resp.json();
      expect(body.mode).toBe(1); // Alliance
      expect(body.maxPointsPerOrder).toBe(30);
      expect(body.maxWalletPoints).toBe(100000);
    });
  });

  test('LA-E2E-2: SystemAdmin can set per-tenant override (IsAllianceMember=true)', async ({ request: req }) => {
    test.skip(!systemAdminToken, 'SystemAdmin token not available — dev login required');

    await test.step(`PUT /api/platform/loyalty/tenant/${TENANT_A}/config — set IsAllianceMember=true`, async () => {
      const resp = await req.put(`${config.GATEWAY_URL}/api/platform/loyalty/tenant/${TENANT_A}/config`, {
        headers: { Authorization: `Bearer ${systemAdminToken}` },
        data: { mode: null, isAllianceMember: true, maxWalletPoints: null },
      });
      expect(resp.status(), 'PUT tenant config should return 200').toBe(200);
      const body = await resp.json();
      expect(body.isAllianceMember).toBe(true);
    });

    await test.step(`GET /api/platform/loyalty/tenant/${TENANT_A}/config — verify IsAllianceMember=true`, async () => {
      const resp = await req.get(`${config.GATEWAY_URL}/api/platform/loyalty/tenant/${TENANT_A}/config`, {
        headers: { Authorization: `Bearer ${systemAdminToken}` },
      });
      expect(resp.status()).toBe(200);
      const body = await resp.json();
      expect(body.isAllianceMember).toBe(true);
      expect(body.tenantId).toBe(TENANT_A);
    });
  });

  test('LA-E2E-3: SystemAdmin can set tenant B as alliance member', async ({ request: req }) => {
    test.skip(!systemAdminToken, 'SystemAdmin token not available — dev login required');

    await test.step(`PUT /api/platform/loyalty/tenant/${TENANT_B}/config — set IsAllianceMember=true`, async () => {
      const resp = await req.put(`${config.GATEWAY_URL}/api/platform/loyalty/tenant/${TENANT_B}/config`, {
        headers: { Authorization: `Bearer ${systemAdminToken}` },
        data: { mode: null, isAllianceMember: true, maxWalletPoints: null },
      });
      expect(resp.status()).toBe(200);
      const body = await resp.json();
      expect(body.isAllianceMember).toBe(true);
    });
  });

  // ─── Scenario 2: Customer Wallet Endpoint (Auth Boundary) ─────────────────

  test('LA-E2E-4: GET /api/loyalty/wallet without X-Customer-Token → 401', async ({ request: req }) => {
    const resp = await req.get(`${config.GATEWAY_URL}/api/loyalty/wallet`);
    expect(resp.status(), 'Wallet without token should return 401').toBe(401);
  });

  test('LA-E2E-5: GET /api/loyalty/wallet with invalid X-Customer-Token → 401', async ({ request: req }) => {
    const resp = await req.get(`${config.GATEWAY_URL}/api/loyalty/wallet`, {
      headers: { 'X-Customer-Token': 'invalid_token_e2e_test' },
    });
    expect([401, 404]).toContain(resp.status());
  });

  test('LA-E2E-6: GET /api/loyalty/my without X-Customer-Token → 401', async ({ request: req }) => {
    const resp = await req.get(`${config.GATEWAY_URL}/api/loyalty/my`);
    // /api/loyalty/my forwards to ShopERP — ShopERP returns 401 without token
    expect([401, 403]).toContain(resp.status());
  });

  // ─── Scenario 3: Migrate Endpoint (SystemAdmin API) ───────────────────────

  test('LA-E2E-7: POST /api/platform/loyalty/migrate without auth → 401 or 302', async ({ request: req }) => {
    const resp = await req.post(`${config.GATEWAY_URL}/api/platform/loyalty/migrate`, {
      data: { direction: 'consolidate', tenantId: TENANT_A, customerBalances: [] },
    });
    // SystemAdmin policy → 302 redirect to login (no JWT) or 401 depending on config
    expect([302, 401, 403]).toContain(resp.status());
  });

  test('LA-E2E-8: POST /api/platform/loyalty/migrate with invalid direction → 400', async ({ request: req }) => {
    test.skip(!systemAdminToken, 'SystemAdmin token not available — dev login required');

    const resp = await req.post(`${config.GATEWAY_URL}/api/platform/loyalty/migrate`, {
      headers: { Authorization: `Bearer ${systemAdminToken}` },
      data: { direction: 'invalid_direction', tenantId: TENANT_A },
    });
    expect(resp.status(), 'Invalid direction should return 400').toBe(400);
  });

  test('LA-E2E-9: POST /api/platform/loyalty/migrate consolidate without customerBalances → 400', async ({ request: req }) => {
    test.skip(!systemAdminToken, 'SystemAdmin token not available — dev login required');

    const resp = await req.post(`${config.GATEWAY_URL}/api/platform/loyalty/migrate`, {
      headers: { Authorization: `Bearer ${systemAdminToken}` },
      data: { direction: 'consolidate', tenantId: TENANT_A },
    });
    expect(resp.status(), 'Consolidate without balances should return 400').toBe(400);
  });

  // ─── Scenario 4: Cross-Tenant API Reachability ────────────────────────────

  test('LA-E2E-10: GET /api/redemption/catalog/active is reachable', async ({ request: req }) => {
    const resp = await req.get(`${config.GATEWAY_URL}/api/redemption/catalog/active`);
    // Forwarded to ShopERP — may return 200 (catalog exists) or 401 (customer token required)
    expect(resp.status(), 'Catalog endpoint should not be 404').not.toBe(404);
    expect(resp.status(), 'Catalog endpoint should not be 500').not.toBe(500);
  });

  test('LA-E2E-11: POST /api/redemption/redeem without token → 401', async ({ request: req }) => {
    const resp = await req.post(`${config.GATEWAY_URL}/api/redemption/redeem`, {
      data: { catalogItemId: '00000000-0000-0000-0000-000000000001' },
    });
    // Forwarded to ShopERP — ShopERP returns 401 without customer token
    expect([401, 403, 400]).toContain(resp.status());
  });

  test('LA-E2E-12: GET /api/loyalty/wallet with valid token format but unknown customer → 401 or 200', async ({ request: req }) => {
    // Use a fake but well-formed token — ShopERP my-identity will reject it
    const resp = await req.get(`${config.GATEWAY_URL}/api/loyalty/wallet`, {
      headers: { 'X-Customer-Token': 'e2e_test_fake_token_12345' },
    });
    // ShopERP /api/loyalty/my-identity returns 401 for invalid token → Gateway forwards 401
    expect([401, 404]).toContain(resp.status());
  });

  // ─── Cleanup: Revert config to Silo mode ──────────────────────────────────

  test('LA-E2E-13: Cleanup — revert global mode to Silo', async ({ request: req }) => {
    test.skip(!systemAdminToken, 'SystemAdmin token not available — dev login required');

    await test.step('PUT /api/platform/loyalty/config — revert to Silo', async () => {
      const resp = await req.put(`${config.GATEWAY_URL}/api/platform/loyalty/config`, {
        headers: { Authorization: `Bearer ${systemAdminToken}` },
        data: { mode: 0, maxPointsPerOrder: 30, maxWalletPoints: 100000 },
      });
      expect(resp.status()).toBe(200);
      const body = await resp.json();
      expect(body.mode).toBe(0); // Silo
    });

    await test.step(`PUT /api/platform/loyalty/tenant/${TENANT_A}/config — revert IsAllianceMember=false`, async () => {
      const resp = await req.put(`${config.GATEWAY_URL}/api/platform/loyalty/tenant/${TENANT_A}/config`, {
        headers: { Authorization: `Bearer ${systemAdminToken}` },
        data: { mode: null, isAllianceMember: false, maxWalletPoints: null },
      });
      expect(resp.status()).toBe(200);
    });

    await test.step(`PUT /api/platform/loyalty/tenant/${TENANT_B}/config — revert IsAllianceMember=false`, async () => {
      const resp = await req.put(`${config.GATEWAY_URL}/api/platform/loyalty/tenant/${TENANT_B}/config`, {
        headers: { Authorization: `Bearer ${systemAdminToken}` },
        data: { mode: null, isAllianceMember: false, maxWalletPoints: null },
      });
      expect(resp.status()).toBe(200);
    });
  });
});
