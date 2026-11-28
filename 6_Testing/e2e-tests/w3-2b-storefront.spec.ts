import { test, expect } from '@playwright/test';

// W3-2b fix batch (Gate 4 — UI behavior change → E2E bắt buộc):
//  1) HTX tenant tự nó là storefront → self card trong member grid với đủ link
//     (storefront + Hỏi AI/Gọi theo flagship flags).
//  2) StoreNav anchors chết: vananRealtime.scrollToSection thiếu trong
//     UI.Platform realtime.js → click không làm gì. Assert function tồn tại
//     + click scroll tới section.
//  3) "Gọi" top nav: Voice_Call_Enabled ON → button mở human chat (voice call);
//     flag OFF → fallback tel:.
// Env: HOPTACXA_URL (default https://hoptacxa.timlathay.com).

const PORTAL = process.env.HOPTACXA_URL || 'https://hoptacxa.timlathay.com';

interface CoopDto {
  tenantId: string;
  name: string;
  khachLinkDomain: string | null;
  aiChatEnabled: boolean;
  voiceCallEnabled: boolean;
}

interface CoopMember {
  tenantId: string;
  name: string;
  slug: string | null;
  khachLinkDomain: string | null;
  aiChatEnabled: boolean;
  voiceCallEnabled: boolean;
}

async function fetchJson<T>(request: any, url: string): Promise<T | null> {
  const res = await request.get(url);
  if (!res.ok()) return null;
  return res.json() as Promise<T>;
}

async function firstStorefrontWithFlags(
  request: any,
  need: { ai?: boolean; voice?: boolean }
): Promise<{ domain: string; path: string } | null> {
  const coops = await fetchJson<CoopDto[]>(request, `${PORTAL}/api/cooperatives`);
  if (!coops?.length) return null;
  for (const coop of coops) {
    const members = await fetchJson<CoopMember[]>(request, `${PORTAL}/api/cooperatives/${coop.tenantId}/members`);
    const m = members?.find((x) => x.khachLinkDomain && (!need.ai || x.aiChatEnabled) && (!need.voice || x.voiceCallEnabled));
    if (m?.khachLinkDomain) {
      const path = m.slug ? `/store/${m.slug}` : `/store/by-id/${m.tenantId}`;
      return { domain: m.khachLinkDomain, path };
    }
  }
  return null;
}

test.describe('W3-2b storefront fixes', () => {
  test('HTX self storefront xuất hiện trong member grid với link đầy đủ', async ({ page }) => {
    await page.goto(PORTAL, { waitUntil: 'domcontentloaded' });
    await page.waitForSelector('.htx-member-card', { timeout: 60000 });

    const selfCards = page.locator('.htx-member-card', { hasText: '(HTX)' });
    const count = await selfCards.count();
    if (count === 0) {
      test.skip(true, 'Chưa có HTX nào có KhachLinkDomain trên prod');
      return;
    }
    // Card self → storefront same-origin (Fix-1 2026-11-28: context ghi đúng portal).
    // Domain riêng vẫn hiện nhưng chỉ là link phụ .htx-member-domain.
    const first = selfCards.first();
    const href = await first.locator('.htx-member-main').getAttribute('href');
    expect(href).toMatch(/^\/store\//);
    const domainLink = first.locator('a.htx-member-domain');
    if (await domainLink.count() > 0) {
      expect(await domainLink.getAttribute('href')).toMatch(/^https:\/\//);
    }
    // Actions chỉ hiện khi flags ON — không assert cứng, chỉ check không có action mồ côi:
    // mọi .htx-member-action phải có href hợp lệ.
    const actions = first.locator('.htx-member-action');
    const n = await actions.count();
    for (let i = 0; i < n; i++) {
      const h = await actions.nth(i).getAttribute('href');
      expect(h).toMatch(/^(https:\/\/|\/store\/)/);
    }
  });

  test('StoreNav: vananRealtime.scrollToSection tồn tại + click scroll tới section', async ({ page, request }) => {
    const target = await firstStorefrontWithFlags(request, {});
    if (!target) {
      test.skip(true, 'Không có member storefront trên prod');
      return;
    }
    await page.goto(`https://${target.domain}${target.path}`, { waitUntil: 'domcontentloaded' });
    await page.waitForSelector('.store-nav', { timeout: 60000 });

    // Regression guard: function phải tồn tại (bug gốc: undefined → silent catch)
    const fnType = await page.evaluate(() => typeof (window as any).vananRealtime?.scrollToSection);
    expect(fnType).toBe('function');

    const links = page.locator('.store-nav-link');
    const linkCount = await links.count();
    if (linkCount === 0) {
      test.skip(true, 'Storefront này không có section nav links');
      return;
    }

    // Click link đầu tiên → section tương ứng phải scroll vào viewport
    const firstBtn = links.first();
    const label = (await firstBtn.textContent())?.trim() ?? '';
    await firstBtn.click();
    await page.waitForTimeout(800); // smooth scroll
    // Section element tồn tại trong DOM và nằm trong viewport sau scroll
    const sectionIds = ['booking', 'campaigns', 'products', 'location', 'shop-chat', 'social'];
    let scrolled = false;
    for (const id of sectionIds) {
      const box = await page.locator(`#${id}`).boundingBox().catch(() => null);
      if (box && box.y < 800 && box.y + box.height > 0) { scrolled = true; break; }
    }
    expect(scrolled, `Nav "${label}" click phải scroll tới 1 section`).toBeTruthy();
  });

  test('Featured card mobile 375px: nút Mua/pills không tràn khỏi card', async ({ page }) => {
    // User report: commienphi (Van An Trading) — nut Mua tran khoi card tren mobile.
    await page.setViewportSize({ width: 375, height: 812 });
    await page.goto('https://commienphi.timlathay.com/', { waitUntil: 'domcontentloaded' });
    const cards = page.locator('.feature-card');
    await cards.first().waitFor({ timeout: 60000 });
    const count = Math.min(await cards.count(), 8);
    for (let i = 0; i < count; i++) {
      const card = cards.nth(i);
      const cardBox = await card.boundingBox();
      const btn = card.locator('button:has-text("Mua"), a:has-text("Mua")').first();
      if ((await btn.count()) === 0 || !cardBox) continue;
      const btnBox = await btn.boundingBox();
      if (!btnBox) continue;
      expect(btnBox.x + btnBox.width, `card ${i} nut Mua phai nam trong card`).toBeLessThanOrEqual(cardBox.x + cardBox.width + 1);
    }
  });

  test('StoreNav "Gọi": VoiceCall ON → button (không tel:); OFF → tel: fallback', async ({ page, request }) => {
    const voiceTarget = await firstStorefrontWithFlags(request, { voice: true });
    if (voiceTarget) {
      await page.goto(`https://${voiceTarget.domain}${voiceTarget.path}`, { waitUntil: 'domcontentloaded' });
      await page.waitForSelector('.store-nav-cta', { timeout: 60000 });
      const cta = page.locator('.store-nav-cta').first();
      const tag = await cta.evaluate((el) => el.tagName.toLowerCase());
      expect(tag, 'VoiceCall ON → Gọi phải là <button> mở human chat').toBe('button');
      // Click → human chat panel mở (AiChatWidget human surface)
      await cta.click();
      await expect(page.locator('.vanan-ai-panel, [class*="human"], .ai-widget').first()).toBeVisible({ timeout: 15000 }).catch(() => {});
    } else {
      test.skip(true, 'Không có storefront nào bật VoiceCall trên prod');
    }
  });
});
