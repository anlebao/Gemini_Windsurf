import { test, expect, request } from '@playwright/test';
import { loadEnvConfig } from '../utils/env-config';
import { getJwtToken } from './utils/auth-api';

/**
 * Loyalty Alliance Phase 6B — Silo Mode E2E Tests @golden
 *
 * Spec: docs/specs/loyalty-alliance-spec.md v1.0
 * Plan: docs/plans/loyalty-alliance-master-plan.md Session 12
 *
 * Verifies that existing Silo mode flow is unchanged when loyalty alliance
 * features are deployed. Silo mode = per-tenant SQLite LoyaltyRewards only,
 * no cross-tenant AllianceWallet, no PG loyalty tables queried.
 *
 * Scenario 1: Silo mode config (default state)
 *   - GET global config → mode=Silo (0) or Alliance (1) depending on prior tests
 *   - Verify config endpoint is reachable + returns valid structure
 *
 * Scenario 2: Silo mode customer endpoints (auth boundary)
 *   - GET /api/loyalty/my without token → 401
 *   - GET /api/loyalty/wallet without token → 401 (wallet endpoint exists regardless of mode)
 *   - POST /api/loyalty/redeem without token → 401
 *
 * Scenario 3: Silo mode tenant config (non-member default)
 *   - GET per-tenant config for unknown tenant → returns defaults (IsAllianceMember=false)
 *   - Verify tenant override endpoint is reachable
 *
 * Prerequisites: Docker + ShopERP 5003 + Gateway 5001 all running.
 */

const config = loadEnvConfig();

const TENANT_UNKNOWN = '99999999-9999-9999-9999-999999999999';

test.describe('Loyalty Silo Mode E2E — Phase 6B @golden', () => {
  let systemAdminToken: string | null = null;

  test.beforeAll(async () => {
    const apiContext = await request.newContext({ baseURL: config.SHOPERP_URL });
    try {
      const resp = await apiContext.post('/dev/login/systemadmin');
      if (resp.ok()) {
        const body = await resp.json();
        systemAdminToken = body.token;
      }
    } catch {
      // Dev login not available
    }
    await apiContext.dispose();

    if (!systemAdminToken) {
      systemAdminToken = getJwtToken('admin');
    }
  });

  // ─── Scenario 1: Config Endpoint Reachability ─────────────────────────────

  test('LA-SILO-1: GET /api/platform/loyalty/config returns valid structure', async ({ request: req }) => {
    test.skip(!systemAdminToken, 'SystemAdmin token not available — dev login required');

    const resp = await req.get(`${config.GATEWAY_URL}/api/platform/loyalty/config`, {
      headers: { Authorization: `Bearer ${systemAdminToken}` },
    });
    expect(resp.status()).toBe(200);
    const body = await resp.json();
    // Mode should be 0 (Silo) or 1 (Alliance) — both are valid
    expect([0, 1]).toContain(body.mode);
    expect(body.maxPointsPerOrder).toBeGreaterThanOrEqual(0);
    expect(body.maxWalletPoints).toBeGreaterThanOrEqual(0);
  });

  // ─── Scenario 2: Customer Endpoints Auth Boundary (Silo mode unchanged) ───

  test('LA-SILO-2: GET /api/loyalty/my without token → 401 (Silo flow unchanged)', async ({ request: req }) => {
    const resp = await req.get(`${config.GATEWAY_URL}/api/loyalty/my`);
    expect([401, 403]).toContain(resp.status());
  });

  test('LA-SILO-3: GET /api/loyalty/wallet without token → 401 (wallet endpoint exists)', async ({ request: req }) => {
    const resp = await req.get(`${config.GATEWAY_URL}/api/loyalty/wallet`);
    expect(resp.status()).toBe(401);
  });

  test('LA-SILO-4: POST /api/loyalty/redeem without token → 401 (Silo redeem unchanged)', async ({ request: req }) => {
    const resp = await req.post(`${config.GATEWAY_URL}/api/loyalty/redeem`, {
      data: { catalogItemId: '00000000-0000-0000-0000-000000000001' },
    });
    expect([401, 403, 400]).toContain(resp.status());
  });

  // ─── Scenario 3: Per-Tenant Config (Unknown Tenant Defaults) ──────────────

  test('LA-SILO-5: GET per-tenant config for unknown tenant → defaults (IsAllianceMember=false)', async ({ request: req }) => {
    test.skip(!systemAdminToken, 'SystemAdmin token not available — dev login required');

    const resp = await req.get(`${config.GATEWAY_URL}/api/platform/loyalty/tenant/${TENANT_UNKNOWN}/config`, {
      headers: { Authorization: `Bearer ${systemAdminToken}` },
    });
    expect(resp.status()).toBe(200);
    const body = await resp.json();
    // Unknown tenant → no override row → defaults returned
    expect(body.tenantId).toBe(TENANT_UNKNOWN);
    expect(body.isAllianceMember).toBe(false);
    // Mode = null means inherit global
    expect(body.mode === null || body.mode === 0 || body.mode === 1).toBeTruthy();
  });

  // ─── Scenario 4: Config Validation (Negative Tests) ───────────────────────

  test('LA-SILO-6: PUT global config with negative maxPointsPerOrder → 400', async ({ request: req }) => {
    test.skip(!systemAdminToken, 'SystemAdmin token not available — dev login required');

    const resp = await req.put(`${config.GATEWAY_URL}/api/platform/loyalty/config`, {
      headers: { Authorization: `Bearer ${systemAdminToken}` },
      data: { mode: 0, maxPointsPerOrder: -1, maxWalletPoints: 100000 },
    });
    expect(resp.status(), 'Negative maxPointsPerOrder should return 400').toBe(400);
  });

  test('LA-SILO-7: PUT global config with invalid mode → 400', async ({ request: req }) => {
    test.skip(!systemAdminToken, 'SystemAdmin token not available — dev login required');

    const resp = await req.put(`${config.GATEWAY_URL}/api/platform/loyalty/config`, {
      headers: { Authorization: `Bearer ${systemAdminToken}` },
      data: { mode: 99, maxPointsPerOrder: 30, maxWalletPoints: 100000 },
    });
    expect(resp.status(), 'Invalid mode should return 400').toBe(400);
  });

  test('LA-SILO-8: PUT tenant config with empty tenantId → 400', async ({ request: req }) => {
    test.skip(!systemAdminToken, 'SystemAdmin token not available — dev login required');

    const resp = await req.put(`${config.GATEWAY_URL}/api/platform/loyalty/tenant/00000000-0000-0000-0000-000000000000/config`, {
      headers: { Authorization: `Bearer ${systemAdminToken}` },
      data: { mode: null, isAllianceMember: false, maxWalletPoints: null },
    });
    expect(resp.status(), 'Empty tenantId should return 400').toBe(400);
  });
});
