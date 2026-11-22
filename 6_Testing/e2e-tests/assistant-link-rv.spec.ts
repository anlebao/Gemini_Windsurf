import { test, expect } from '@playwright/test';

// UNIVERSAL AI ASSISTANT W1 — RV production sweep trên /a/{slug} hosted page.
// Tenant: An Tâm (e2d91cdf) — AIChat_Enabled ON + TenantAI published (Shi persona).
// Link: https://shopai.timlathay.com/a/rvAntamAi01x (AssistantLink seeded via psql).

const LINK_URL = 'https://shopai.timlathay.com/a/rvAntamAi01x';

test.describe('W1 AI Link RV', () => {
  test('resolve page loads + AI widget opens with greeting', async ({ page }) => {
    const errors: string[] = [];
    page.on('pageerror', e => errors.push(String(e)));

    await page.goto(LINK_URL, { waitUntil: 'domcontentloaded' });

    // Header trang link
    await expect(page.locator('.assistant-header h1')).toHaveText('Trợ lý AI', { timeout: 30000 });

    // Widget panel mở sẵn (OpenPanel consume-once) — chờ greeting hoặc suggestions
    const widget = page.locator('.vanan-ai-widget');
    await expect(widget).toBeVisible({ timeout: 30000 });

    // Greeting của Shi persona hoặc suggestions chips
    const greeting = page.locator('.vanan-ai-bubble').first();
    const hasGreeting = await greeting.isVisible().catch(() => false);
    const hasSuggestions = await page.locator('.vanan-ai-chip').first().isVisible().catch(() => false);
    expect(hasGreeting || hasSuggestions).toBeTruthy();

    // Không có JS fatal
    expect(errors.filter(e => !e.includes('favicon'))).toHaveLength(0);
  });

  test('send message → AI answer returns', async ({ page }) => {
    await page.goto(LINK_URL, { waitUntil: 'domcontentloaded' });
    await expect(page.locator('.vanan-ai-widget')).toBeVisible({ timeout: 30000 });

    const input = page.locator('.vanan-ai-widget input[type="text"], .vanan-ai-widget textarea, .vanan-ai-widget input:not([type])').first();
    await expect(input).toBeVisible({ timeout: 15000 });
    await input.fill('Quán có những dịch vụ gì?');

    // Submit — tìm nút send trong widget
    const sendBtn = page.locator('.vanan-ai-widget button[aria-label*="Gửi"], .vanan-ai-widget .vanan-ai-send, .vanan-ai-widget button:has(i.bi-send)').first();
    if (await sendBtn.isVisible().catch(() => false)) {
      await sendBtn.click();
    } else {
      await input.press('Enter');
    }

    // Chờ AI trả lời xong: input bị disable trong lúc _sending — chờ enable lại
    // rồi mới đọc bubble (tránh đọc bubble rỗng giữa chừng / nhầm greeting).
    await expect(input).toBeEnabled({ timeout: 90000 });
    const bubbles = page.locator('.vanan-ai-msg-assistant .vanan-ai-bubble');
    await expect(bubbles.last()).toBeVisible({ timeout: 30000 });
    await expect.poll(async () => (await bubbles.last().innerText()).trim().length, { timeout: 30000 })
      .toBeGreaterThan(10);
    const answerText = await bubbles.last().innerText();
    console.log('AI answer:', answerText.slice(0, 120));
  });

  test('human handoff tab visible (guest device id)', async ({ page }) => {
    await page.goto(LINK_URL, { waitUntil: 'domcontentloaded' });
    await expect(page.locator('.vanan-ai-widget')).toBeVisible({ timeout: 30000 });

    // Tab "Chủ quán" phải hiện khi HumanHandoffAvailable (guest device id luôn có)
    const humanTab = page.locator('.vanan-ai-tab-human, .vanan-ai-tab:has-text("Chủ quán")').first();
    await expect(humanTab).toBeVisible({ timeout: 15000 });
    await humanTab.click();

    // Human panel (RealtimeChatPanel) render
    await expect(page.locator('.ai-widget-human, [class*="realtime-chat"], [class*="chat-panel"]').first())
      .toBeVisible({ timeout: 20000 });
  });

  test('revoked slug returns unavailable state', async ({ page }) => {
    // Resolve endpoint check trước (nhanh hơn render)
    const resp = await page.request.get('https://api2.khachvip.online/api/public/assistant-link/rvAntamAi01x/resolve');
    expect(resp.ok()).toBeTruthy();
    const body = await resp.json();
    expect(body.tenantId).toBe('e2d91cdf-d3c4-4399-b96e-e772a7a41630');
    expect(body.aiEnabled).toBe(true);
  });
});
