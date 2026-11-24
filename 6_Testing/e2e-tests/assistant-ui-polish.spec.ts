import { test, expect } from '@playwright/test';

// AI Assistant UI polish — Gate 4 E2E (UI layout change → test required).
// Covers the 4 reported issues on production surfaces:
//   1. User bubble wraps naturally (overflow-wrap: break-word, NOT anywhere)
//   2. Expand toggle bung ~100% chiều cao viewport
//   3. Send button visible + clickable inside panel bounds (mọi viewport)
//   4. No document-level horizontal overflow @390px
// Profile "Việc cần làm" overflow → profile-contextual.spec.ts (auth-gated).
//
// Env: KHACHLINK_URL (default https://shopai.timlathay.com).

const BASE = process.env.KHACHLINK_URL || 'https://shopai.timlathay.com';
const STORE_AI = `${BASE}/store/an-tam?chat=ai`;

const LONG_VN_MESSAGE =
  'Xin chào, mình muốn hỏi về liệu quán có nhận đặt lịch trước cho liệu trình chăm sóc da không ạ, ' +
  'và nếu có thì mình nên đặt trước bao lâu thì hợp lý nhất vậy shop?';

async function sendMessage(page: import('@playwright/test').Page, text: string) {
  const input = page
    .locator('.vanan-ai-widget input[type="text"], .vanan-ai-widget textarea, .vanan-ai-widget input:not([type])')
    .first();
  await input.fill(text);
  const sendBtn = page
    .locator('.vanan-ai-widget button[aria-label*="Gửi"], .vanan-ai-widget .vanan-ai-send, .vanan-ai-widget button:has(i.bi-send)')
    .first();
  await sendBtn.click();
}

test.describe('AI widget UI polish — mobile 390px', () => {
  test.use({ viewport: { width: 390, height: 844 } });

  test('send button visible in-bounds + user bubble wraps naturally + no overflow', async ({ page }) => {
    await page.goto(STORE_AI, { waitUntil: 'domcontentloaded' });
    await expect(page.locator('.vanan-ai-widget')).toBeVisible({ timeout: 30000 });
    await expect(page.locator('.vanan-ai-panel')).toBeVisible({ timeout: 30000 });

    // Issue 3: send button exists, enabled, fully inside panel bounds.
    const sendBtn = page
      .locator('.vanan-ai-widget button[aria-label*="Gửi"], .vanan-ai-widget .vanan-ai-send, .vanan-ai-widget button:has(i.bi-send)')
      .first();
    await expect(sendBtn).toBeVisible({ timeout: 15000 });
    await expect(sendBtn).toBeEnabled();
    const bounds = await page.evaluate(() => {
      const panel = document.querySelector('.vanan-ai-panel')!.getBoundingClientRect();
      const btns = Array.from(document.querySelectorAll('.vanan-ai-inputbar button'));
      const send = btns.find((b) => (b as HTMLElement).getBoundingClientRect().width > 0 &&
        (b.getAttribute('aria-label') || '').includes('Gửi')) as HTMLElement | undefined;
      if (!send) return { missing: true };
      const r = send.getBoundingClientRect();
      return {
        missing: false,
        insidePanel: r.left >= panel.left - 1 && r.right <= panel.right + 1 &&
                    r.top >= panel.top - 1 && r.bottom <= panel.bottom + 1,
        insideViewport: r.left >= 0 && r.right <= window.innerWidth,
      };
    });
    expect(bounds.missing, 'send button missing from DOM').toBe(false);
    expect((bounds as any).insidePanel, 'send button clipped by panel').toBe(true);
    expect((bounds as any).insideViewport, 'send button clipped by viewport').toBe(true);

    // Issue 1: send long VN message → bubble wraps at word boundaries.
    await sendMessage(page, LONG_VN_MESSAGE);
    const userBubble = page.locator('.vanan-ai-msg-user .vanan-ai-bubble').last();
    await expect(userBubble).toBeVisible({ timeout: 15000 });

    const wrap = await userBubble.evaluate((el) => {
      const cs = getComputedStyle(el);
      const r = el.getBoundingClientRect();
      return {
        overflowWrap: cs.overflowWrap,
        wordBreak: cs.wordBreak,
        width: r.width,
        height: r.height,
        text: el.textContent || '',
      };
    });
    expect(wrap.overflowWrap).toBe('break-word');
    expect(['normal', 'break-word']).toContain(wrap.wordBreak);
    // Natural wrap → multi-line bubble, not shrunk to a few chars per line.
    expect(wrap.height).toBeGreaterThan(40);

    // Issue 4: no document-level horizontal overflow.
    const docOverflow = await page.evaluate(() => ({
      scrollW: document.documentElement.scrollWidth,
      innerW: window.innerWidth,
    }));
    expect(docOverflow.scrollW).toBeLessThanOrEqual(docOverflow.innerW + 1);
  });
});

test.describe('AI widget UI polish — expand (desktop)', () => {
  test.use({ viewport: { width: 1280, height: 800 } });

  test('expand toggle → panel ~100% viewport height, contract restores', async ({ page }) => {
    await page.goto(STORE_AI, { waitUntil: 'domcontentloaded' });
    await expect(page.locator('.vanan-ai-panel')).toBeVisible({ timeout: 30000 });

    const panel = page.locator('.vanan-ai-panel');
    const before = await panel.boundingBox();
    expect(before).not.toBeNull();
    expect(before!.height).toBeLessThan(700); // collapsed panel is ~560px max

    const expandBtn = page.locator('button[aria-label="Mở rộng khung chat"], button[title="Mở rộng"]').first();
    await expect(expandBtn).toBeVisible({ timeout: 15000 });
    await expandBtn.click();

    // Issue 2: expanded panel ≈ full viewport height (≥ 92% of innerHeight).
    await page.waitForTimeout(400);
    const after = await panel.boundingBox();
    expect(after).not.toBeNull();
    expect(after!.height).toBeGreaterThanOrEqual(800 * 0.92);
    expect(after!.width).toBeGreaterThanOrEqual(900);

    // Contract back.
    const contractBtn = page.locator('button[aria-label="Thu nhỏ khung chat"], button[title="Thu nhỏ"]').first();
    await contractBtn.click();
    await page.waitForTimeout(300);
    const restored = await panel.boundingBox();
    expect(restored!.height).toBeLessThan(after!.height - 100);
  });

  test('expanded: inputbar pinned to panel bottom + send button in-bounds', async ({ page }) => {
    await page.goto(STORE_AI, { waitUntil: 'domcontentloaded' });
    await expect(page.locator('.vanan-ai-panel')).toBeVisible({ timeout: 30000 });
    await page.locator('button[aria-label="Mở rộng khung chat"], button[title="Mở rộng"]').first().click();
    await page.waitForTimeout(400);

    // Regression (2026-11): messages max-height:360px cap → inputbar trôi
    // giữa panel ~250px; VanAInput scoped CSS dead → send bị clip mép phải.
    const m = await page.evaluate(() => {
      const panel = document.querySelector('.vanan-ai-panel')!.getBoundingClientRect();
      const bar = document.querySelector('.vanan-ai-inputbar')!.getBoundingClientRect();
      const send = document.querySelector('.vanan-ai-send')!.getBoundingClientRect();
      const input = document.querySelector('.vanan-ai-input') as HTMLElement;
      return {
        bottomGap: panel.bottom - bar.bottom,
        sendInside: send.left >= panel.left - 1 && send.right <= panel.right + 1,
        sendRightGap: panel.right - send.right,
        inputFlex: input ? getComputedStyle(input).flexGrow : null,
      };
    });
    expect(m.bottomGap).toBeLessThanOrEqual(20);   // inputbar sát đáy panel
    expect(m.sendInside).toBe(true);               // send không bị clip
    expect(m.sendRightGap).toBeLessThanOrEqual(30); // send sát mép phải (input flex:1)
    expect(m.inputFlex).toBe('1');
  });
});
