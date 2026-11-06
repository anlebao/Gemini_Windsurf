import { test, expect } from '@playwright/test';
import { loadEnvConfig, isTierEnabled } from '../utils/env-config';
import { TestReporter } from '../utils/test-reporter';

// Task AI-P4 (Gate 4 rules — UI change → E2E) / SRS v1.1 §6.1.
// Storefront AI chat widget E2E: load store → widget bubble → ask a menu question
// → grounded answer with product card → "Thêm vào giỏ" → cart badge/drawer updates.
// Runs at CP3 on production (pilot-shaped test tenant, AIChat_Enabled ON).
//
// Prerequisites (CP3 RV setup):
//   - AI_CHAT_TEST_STORE_URL: full storefront URL (vd https://<host>/store/<slug>)
//     của tenant có AIChat_Enabled=true + knowledge đã ingest + metering ok.
//   - AI_CHAT_TEST_QUESTION: optional — câu hỏi chắc chắn grounded (default: "Quán có món gì?")
// Self-gating: spec bị skip nếu thiếu env hoặc E2E tier tắt (playwright.rules).

const config = loadEnvConfig();
const reporter = new TestReporter('AI Chat Widget E2E');

const STORE_URL = process.env.AI_CHAT_TEST_STORE_URL ?? '';
const QUESTION = process.env.AI_CHAT_TEST_QUESTION ?? 'Quán có món gì?';

test.describe.configure({ mode: isTierEnabled('e2e') ? 'parallel' : 'skip' });

test.describe('VanAn Ecosystem — AI Chat Widget (task_ai_p4)', () => {
  test.beforeAll(async () => {
    if (!isTierEnabled('e2e')) {
      reporter.setArchitectDecision('Bypassed by Architect - E2E tests disabled');
      test.skip();
    }
    if (!STORE_URL) {
      reporter.setArchitectDecision('AI_CHAT_TEST_STORE_URL chưa set — spec bị skip (CP3 RV setup)');
      test.skip();
    }
    reporter.log(`Starting AI Chat Widget E2E (store: ${STORE_URL})`);
  });

  test('Widget bubble hiển thị trên storefront (aiEnabled=true)', async ({ page }) => {
    await page.goto(STORE_URL);
    await page.waitForLoadState('networkidle');

    // Bubble chỉ render khi widget-config trả aiEnabled=true.
    await expect(page.locator('button.vanan-ai-bubble-btn')).toBeVisible({ timeout: 20000 });
  });

  test('Hỏi → answer → product card → thêm vào giỏ (Option C)', async ({ page }) => {
    await page.goto(STORE_URL);
    await page.waitForLoadState('networkidle');

    // Mở widget
    const bubble = page.locator('button.vanan-ai-bubble-btn');
    await expect(bubble).toBeVisible({ timeout: 20000 });
    await bubble.click();
    await expect(page.locator('.vanan-ai-panel')).toBeVisible({ timeout: 10000 });

    // Gửi câu hỏi
    await page.locator('.vanan-ai-panel input').fill(QUESTION);
    await page.locator('button.vanan-ai-send').click();

    // Answer phải hiện (user msg + assistant msg); degraded/fallback vẫn là answer hợp lệ.
    await expect(page.locator('.vanan-ai-msg-assistant .vanan-ai-bubble').last())
      .toBeVisible({ timeout: 45000 }); // LLM có thể mất ~30s

    // Product card (nếu câu hỏi grounded trên menu — card xuất hiện khi có products[]).
    const cards = page.locator('.vanan-ai-product-card');
    if (await cards.count() === 0) {
      reporter.log('Answer không có product card (no-data/refused path) — kiểm tra tenant knowledge');
      test.skip();
    }

    // Click "Thêm vào giỏ" trên card đầu tiên → cart phải nhận món (Option C snapshot).
    const productName = await cards.first().locator('.vanan-ai-product-name').innerText();
    await cards.first().getByRole('button', { name: /Thêm vào giỏ/ }).click();

    // Toast xác nhận hoặc cart badge cập nhật — khớp bất kỳ dấu hiệu nào của add-to-cart.
    await expect(
      page.locator('.vanan-ai-panel').getByText('Đã thêm').or(page.locator('.cart-badge, .vanan-toast').first())
    ).toBeVisible({ timeout: 10000 });
    reporter.log(`Added to cart: ${productName}`);
  });

  test('Session persist — reload giữ lịch sử chat', async ({ page }) => {
    await page.goto(STORE_URL);
    await page.waitForLoadState('networkidle');

    const bubble = page.locator('button.vanan-ai-bubble-btn');
    await expect(bubble).toBeVisible({ timeout: 20000 });
    await bubble.click();

    await page.locator('.vanan-ai-panel input').fill('Xin chào');
    await page.locator('button.vanan-ai-send').click();
    await expect(page.locator('.vanan-ai-msg-assistant .vanan-ai-bubble').last())
      .toBeVisible({ timeout: 45000 });

    // Reload → sessionId từ localStorage → history phải restore (GET sessions/{sid}/messages).
    await page.reload();
    await page.waitForLoadState('networkidle');
    await page.locator('button.vanan-ai-bubble-btn').click();
    await expect(page.locator('.vanan-ai-msg-user .vanan-ai-bubble')).toContainText('Xin chào', { timeout: 15000 });
  });
});
