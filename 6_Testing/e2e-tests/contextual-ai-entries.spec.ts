import { test, expect } from '@playwright/test';
import { loadEnvConfig } from '../utils/env-config';

// Task W3-1 (Gate 4 rules — UI layout change → E2E bắt buộc).
// Contextual AI/voice entries: member/store/product cards chỉ render
// "Hỏi AI" (?chat=ai) / "Gọi" (?chat=human) khi tenant bật flag (FR-V7/V8/V10).
// Tenant flag OFF → không nút nào (render rule fail-safe).
// Env: HOPTACXA_URL (default https://hoptacxa.timlathay.com).

const config = loadEnvConfig();
const PORTAL = process.env.HOPTACXA_URL || 'https://hoptacxa.timlathay.com';

interface CoopMember {
  tenantId: string;
  name: string;
  slug?: string | null;
  khachLinkDomain?: string | null;
  aiChatEnabled: boolean;
  voiceCallEnabled: boolean;
}

async function fetchJson<T>(request: any, url: string): Promise<T | null> {
  try {
    const res = await request.get(url);
    if (!res.ok()) return null;
    return (await res.json()) as T;
  } catch {
    return null;
  }
}

test.describe('W3-1 contextual AI/voice entries', () => {
  test('portal member actions: href đúng deep-link ?chat=ai|human, target _blank', async ({ page }) => {
    await page.goto(`${PORTAL}/`, { waitUntil: 'networkidle' });
    await expect(page.locator('.hoptacxa-portal')).toBeVisible({ timeout: 30000 });

    // Đợi cooperatives API resolve (member data load async sau portal shell)
    await page.waitForResponse(
      (r) => r.url().includes('/api/cooperatives/') && r.url().includes('/members'),
      { timeout: 30000 }
    ).catch(() => null);

    const aiLinks = page.locator('.htx-member-action-ai');
    const callLinks = page.locator('.htx-member-action-call');
    const aiCount = await aiLinks.count();
    const callCount = await callLinks.count();

    // Mọi action link phải tuân contract deep-link (không link chết / sai format).
    // Member không có KhachLinkDomain → relative /store/by-id trên portal domain
    // (apex khachvip.online đã chết — không được fallback tới đó).
    for (let i = 0; i < aiCount; i++) {
      const href = await aiLinks.nth(i).getAttribute('href');
      expect(href).toContain('?chat=ai');
      expect(href).toMatch(/^(https:\/\/|\/store\/)/);
      expect(await aiLinks.nth(i).getAttribute('target')).toBe('_blank');
    }
    for (let i = 0; i < callCount; i++) {
      const href = await callLinks.nth(i).getAttribute('href');
      expect(href).toContain('?chat=human');
      expect(href).toMatch(/^(https:\/\/|\/store\/)/);
    }

    // Render rule: action chỉ tồn tại trên card có flag ON — không có nút mồ côi
    // (không assert count cụ thể vì flags phụ thuộc data prod).
    const cards = await page.locator('.htx-member-card').count();
    expect(aiCount + callCount).toBeLessThanOrEqual(cards * 2);
  });

  test('?chat=ai deep-link mở AI widget trên storefront của member bật flag', async ({ page, request }) => {
    // Tìm member đầu tiên có AiChatEnabled qua public API (anonymous-safe).
    const coops = await fetchJson<Array<{ tenantId: string }>>(request, `${PORTAL}/api/cooperatives`);
    if (!coops || coops.length === 0) {
      test.skip(true, 'Chưa có HTX data trên prod');
      return;
    }

    let target: CoopMember | null = null;
    for (const coop of coops) {
      const members = await fetchJson<CoopMember[]>(request, `${PORTAL}/api/cooperatives/${coop.tenantId}/members`);
      // Chỉ member có storefront domain riêng — cross-domain deep-link test.
      target = members?.find((m) => m.aiChatEnabled && m.khachLinkDomain) ?? null;
      if (target) break;
    }
    if (!target) {
      test.skip(true, 'Không member nào bật AIChat_Enabled + có KhachLinkDomain trên prod');
      return;
    }

    const domain = target.khachLinkDomain!;
    const storePath = target.slug ? `/store/${target.slug}` : `/store/by-id/${target.tenantId}`;
    await page.goto(`https://${domain}${storePath}?chat=ai`, { waitUntil: 'domcontentloaded' });

    // Store page load xong → AI widget phải mở panel (OpenPanel consumed once)
    await expect(page.locator('.vanan-ai-panel')).toBeVisible({ timeout: 60000 });
  });

  test('?chat=human deep-link mở human chat panel', async ({ page, request }) => {
    const coops = await fetchJson<Array<{ tenantId: string }>>(request, `${PORTAL}/api/cooperatives`);
    if (!coops || coops.length === 0) {
      test.skip(true, 'Chưa có HTX data trên prod');
      return;
    }

    let target: CoopMember | null = null;
    for (const coop of coops) {
      const members = await fetchJson<CoopMember[]>(request, `${PORTAL}/api/cooperatives/${coop.tenantId}/members`);
      target = members?.find((m) => m.voiceCallEnabled && m.khachLinkDomain)
            ?? members?.find((m) => m.khachLinkDomain) ?? null;
      if (target) break;
    }
    if (!target) {
      test.skip(true, 'Chưa có member data + storefront domain trên prod');
      return;
    }

    const domain = target.khachLinkDomain!;
    const storePath = target.slug ? `/store/${target.slug}` : `/store/by-id/${target.tenantId}`;
    await page.goto(`https://${domain}${storePath}?chat=human`, { waitUntil: 'domcontentloaded' });

    // Human mode: panel mở + header "Chủ quán" (hoặc human surface). WASM chậm → timeout rộng.
    await expect(page.locator('.vanan-ai-panel')).toBeVisible({ timeout: 60000 });
    await expect(page.locator('.vanan-ai-panel')).toContainText('Chủ quán', { timeout: 30000 });
  });
});
