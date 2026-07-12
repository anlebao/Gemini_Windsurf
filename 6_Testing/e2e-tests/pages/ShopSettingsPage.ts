import { APIRequestContext, expect } from '@playwright/test';
import { TEST_TENANT_ID } from '../utils/test-data-cleaner';

/**
 * W4-T5: Page Object for Shop Feature Settings toggle management.
 * Uses ShopERP API (PUT /api/shop/settings/features) to set toggles.
 * Requires admin auth — call login() first or pass authenticated APIRequestContext.
 */
export interface ShopFeatureToggles {
  qR_TableNumber_Enabled?: boolean;
  kitchen_Workflow_Enabled?: boolean;
  voice_Note_Enabled?: boolean;
  loyalty_Program_Enabled?: boolean;
  accounting_Sync_Enabled?: boolean;
  eInvoice_Auto_Export_Enabled?: boolean;
}

export class ShopSettingsPage {
  private readonly apiContext: APIRequestContext;
  private readonly baseURL: string;
  private readonly tenantId: string;

  constructor(apiContext: APIRequestContext, baseURL: string, tenantId?: string) {
    this.apiContext = apiContext;
    this.baseURL = baseURL;
    this.tenantId = tenantId || TEST_TENANT_ID;
  }

  /**
   * Login as owner via DevLogin endpoint to get auth cookie.
   * Call this before setToggles/getToggles if APIRequestContext is not authenticated.
   */
  async login(): Promise<void> {
    const resp = await this.apiContext.post(`${this.baseURL}/dev/login/owner`);
    expect(resp.ok(), `DevLogin owner should return 200, got ${resp.status()}`).toBeTruthy();
  }

  /**
   * Get current toggle settings via GET /api/shop/settings/features.
   */
  async getToggles(): Promise<ShopFeatureToggles> {
    const resp = await this.apiContext.get(
      `${this.baseURL}/api/shop/settings/features?tenantId=${this.tenantId}`
    );
    expect(resp.ok(), `GET features should return 200, got ${resp.status()}`).toBeTruthy();
    return await resp.json() as ShopFeatureToggles;
  }

  /**
   * Set toggle values via PUT /api/shop/settings/features.
   * Only includes fields provided in the toggles parameter.
   */
  async setToggles(toggles: ShopFeatureToggles): Promise<void> {
    // First get current toggles to merge (PUT requires all fields)
    const current = await this.getToggles();
    const merged: Required<ShopFeatureToggles> = {
      qR_TableNumber_Enabled: toggles.qR_TableNumber_Enabled ?? current.qR_TableNumber_Enabled ?? false,
      kitchen_Workflow_Enabled: toggles.kitchen_Workflow_Enabled ?? current.kitchen_Workflow_Enabled ?? false,
      voice_Note_Enabled: toggles.voice_Note_Enabled ?? current.voice_Note_Enabled ?? false,
      loyalty_Program_Enabled: toggles.loyalty_Program_Enabled ?? current.loyalty_Program_Enabled ?? false,
      accounting_Sync_Enabled: toggles.accounting_Sync_Enabled ?? current.accounting_Sync_Enabled ?? false,
      eInvoice_Auto_Export_Enabled: toggles.eInvoice_Auto_Export_Enabled ?? current.eInvoice_Auto_Export_Enabled ?? false,
    };
    const body = JSON.stringify(merged);
    const resp = await this.apiContext.put(
      `${this.baseURL}/api/shop/settings/features?tenantId=${this.tenantId}`,
      { data: merged, headers: { 'Content-Type': 'application/json' } }
    );
    expect(resp.ok(), `PUT features should return 200, got ${resp.status()}`).toBeTruthy();
  }

  /**
   * Enable all toggles — for Scenario 1 (full flow).
   */
  async enableAll(): Promise<void> {
    await this.setToggles({
      qR_TableNumber_Enabled: true,
      kitchen_Workflow_Enabled: true,
      voice_Note_Enabled: true,
      loyalty_Program_Enabled: true,
      accounting_Sync_Enabled: true,
      eInvoice_Auto_Export_Enabled: false,
    });
  }

  /**
   * Disable kitchen + loyalty + accounting + QR table + voice — for Scenario 2 (minimal flow).
   */
  async disableKitchenLoyaltyAccounting(): Promise<void> {
    await this.setToggles({
      qR_TableNumber_Enabled: false,
      kitchen_Workflow_Enabled: false,
      voice_Note_Enabled: false,
      loyalty_Program_Enabled: false,
      accounting_Sync_Enabled: false,
      eInvoice_Auto_Export_Enabled: false,
    });
  }
}
