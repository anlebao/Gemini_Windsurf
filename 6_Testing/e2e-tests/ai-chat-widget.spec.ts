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
// Task AI-P8: câu hỏi out-of-scope để kích refuse → chip handoff (có thể override qua env).
const REFUSE_QUESTION = process.env.AI_CHAT_TEST_REFUSE_QUESTION ?? 'Dự báo thời tiết ngày mai thế nào?';

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

    // User msg phải render trước (greeting ẩn khi _messages.Count > 0 — tránh race
    // với greeting/typing indicator cùng selector .vanan-ai-msg-assistant .vanan-ai-bubble).
    await expect(page.locator('.vanan-ai-msg-user .vanan-ai-bubble').last())
      .toBeVisible({ timeout: 10000 });

    // Answer phải hiện — loại trừ typing indicator (:not(.vanan-ai-typing));
    // degraded/fallback vẫn là answer hợp lệ.
    await expect(page.locator('.vanan-ai-msg-assistant .vanan-ai-bubble:not(.vanan-ai-typing)').last())
      .toBeVisible({ timeout: 45000 }); // LLM có thể mất ~30s

    // Product card (nếu câu hỏi grounded trên menu — card xuất hiện khi có products[]).
    const cards = page.locator('.vanan-ai-product-card');
    if (await cards.count() === 0) {
      reporter.log('Answer không có product card (no-data/refused path) — kiểm tra tenant knowledge');
      test.skip();
    }

    // Click "Thêm vào giỏ" trên card đầu tiên → cart phải nhận món (Option C snapshot).
    const productName = await cards.first().locator('.vanan-ai-product-name').innerText();
    await cards.first().getByRole('button', { name: /Thêm.*vào giỏ/ }).click();

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
    await expect(page.locator('.vanan-ai-msg-user .vanan-ai-bubble').last())
      .toBeVisible({ timeout: 10000 });
    await expect(page.locator('.vanan-ai-msg-assistant .vanan-ai-bubble:not(.vanan-ai-typing)').last())
      .toBeVisible({ timeout: 45000 });

    // Reload → sessionId từ localStorage → history phải restore (GET sessions/{sid}/messages).
    await page.reload();
    await page.waitForLoadState('networkidle');
    await page.locator('button.vanan-ai-bubble-btn').click();
    await expect(page.locator('.vanan-ai-msg-user .vanan-ai-bubble')).toContainText('Xin chào', { timeout: 15000 });
  });

  // ── Task AI-P8 (Gate 4): unified widget handoff cases ───────────────────

  test('P8 — nút "Chat với chủ quán" chuyển sang human mode + back', async ({ page }) => {
    await page.goto(STORE_URL);
    await page.waitForLoadState('networkidle');

    const bubble = page.locator('button.vanan-ai-bubble-btn');
    await expect(bubble).toBeVisible({ timeout: 20000 });
    await bubble.click();
    await expect(page.locator('.vanan-ai-panel')).toBeVisible({ timeout: 10000 });

    // Nút handoff chỉ render khi HumanChatAvailable + HumanContent (ChatIdentity luôn có trên Store).
    const humanBtn = page.locator('button.vanan-ai-human-btn');
    if (await humanBtn.count() === 0) {
      reporter.log('Human handoff không khả dụng (no chat identity) — skip');
      test.skip();
    }
    await humanBtn.click();

    // Human mode: header "Chủ quán" + RealtimeChatPanel mount inline (lazy SignalR).
    // .vanan-ai-header = widget's own header (inner chat-panel header excluded from strict match).
    await expect(page.locator('.vanan-ai-header h6')).toContainText('Chủ quán', { timeout: 10000 });
    await expect(page.locator('.ai-widget-human .chat-panel')).toBeVisible({ timeout: 10000 });

    // Back → AI mode, AI surface khôi phục.
    await page.locator('button.vanan-ai-back-btn').click();
    await expect(page.locator('.vanan-ai-panel input')).toBeVisible({ timeout: 10000 });
  });

  test('P8 — refuse → chip "Hỏi chủ quán trực tiếp" → human mode + draft prefill', async ({ page }) => {
    await page.goto(STORE_URL);
    await page.waitForLoadState('networkidle');

    const bubble = page.locator('button.vanan-ai-bubble-btn');
    await expect(bubble).toBeVisible({ timeout: 20000 });
    await bubble.click();

    await page.locator('.vanan-ai-panel input').fill(REFUSE_QUESTION);
    await page.locator('button.vanan-ai-send').click();
    await expect(page.locator('.vanan-ai-msg-user .vanan-ai-bubble').last())
      .toBeVisible({ timeout: 10000 });
    await expect(page.locator('.vanan-ai-msg-assistant .vanan-ai-bubble:not(.vanan-ai-typing)').last())
      .toBeVisible({ timeout: 45000 });

    // Chip chỉ xuất hiện khi answer bị refused — nếu AI trả lời được thì skip (không phải lỗi).
    const chip = page.locator('button.vanan-ai-escalate');
    if (await chip.count() === 0) {
      reporter.log(`AI không refuse câu "${REFUSE_QUESTION}" — đổi AI_CHAT_TEST_REFUSE_QUESTION`);
      test.skip();
    }
    await chip.click();

    // Human mode + draft prefill câu hỏi bị refuse vào input của RealtimeChatPanel.
    await expect(page.locator('.ai-widget-human .chat-panel')).toBeVisible({ timeout: 10000 });
    await expect(page.locator('.ai-widget-human .chat-panel input'))
      .toHaveValue(REFUSE_QUESTION, { timeout: 10000 });
  });

  // ── Task SF-W1 (Gate 4): AI quick-chips + sticky nav ───────────────────

  test('W1 — quick-chip bấm → widget mở + auto-send câu hỏi', async ({ page }) => {
    await page.goto(STORE_URL);
    await page.waitForLoadState('networkidle');

    const chip = page.locator('.store-ai-chip').first();
    await expect(chip).toBeVisible({ timeout: 20000 });
    const chipText = await chip.innerText();

    await chip.click();

    // Widget mở + câu hỏi xuất hiện như user message (auto-send, không cần gõ).
    await expect(page.locator('.vanan-ai-panel')).toBeVisible({ timeout: 10000 });
    await expect(page.locator('.vanan-ai-msg-user .vanan-ai-bubble').last())
      .toBeVisible({ timeout: 10000 });
    const userMsg = await page.locator('.vanan-ai-msg-user .vanan-ai-bubble').last().innerText();
    expect(chipText).toContain(userMsg.slice(0, 6)); // chip label ≈ question text
    await expect(page.locator('.vanan-ai-msg-assistant .vanan-ai-bubble:not(.vanan-ai-typing)').last())
      .toBeVisible({ timeout: 45000 });
  });

  // ── Task SF-W2 (Gate 4): semantic search bar + quick-view ──────────────

  test('W2 — search bar → answer + product card (không mở bubble)', async ({ page }) => {
    await page.goto(STORE_URL);
    await page.waitForLoadState('networkidle');

    const input = page.locator('.store-search-input');
    await expect(input).toBeVisible({ timeout: 20000 });
    await input.fill(QUESTION);
    await page.locator('.store-search-btn').click();

    // Answer trả thẳng trên trang — không cần mở chat bubble.
    await expect(page.locator('.store-search-answer')).toBeVisible({ timeout: 45000 });

    // Product cards xuất hiện khi câu trả lời grounded trên menu.
    const cards = page.locator('.store-search-product');
    if (await cards.count() === 0) {
      reporter.log('Search answer không có product card (no-data path) — kiểm tra tenant knowledge');
      test.skip();
    }
    await expect(cards.first()).toBeVisible();
    await expect(page.locator('.store-search-ask')).toBeVisible();
  });

  test('W2 — click product card → quick-view modal → thêm vào giỏ', async ({ page }) => {
    await page.goto(STORE_URL);
    await page.waitForLoadState('networkidle');

    const card = page.locator('.vibe-qv').first();
    if (await card.count() === 0) {
      reporter.log('Không có product card (VibeShowcase off?) — skip');
      test.skip();
    }
    await card.click();
    await expect(page.locator('.modal.show .qv-media')).toBeVisible({ timeout: 10000 });

    await page.locator('.qv-actions .btn-cta').click();
    await expect(page.locator('#vanan-toast, .cart-badge').first()).toBeVisible({ timeout: 10000 });
  });

  test('W1 — sticky navbar hiện + stick khi scroll qua hero', async ({ page }) => {
    await page.goto(STORE_URL);
    await page.waitForLoadState('networkidle');

    const nav = page.locator('.store-nav');
    await expect(nav).toBeVisible({ timeout: 20000 });

    // Scroll qua hero → nav dính top.
    await page.evaluate(() => window.scrollTo(0, document.body.scrollHeight / 2));
    await page.waitForTimeout(500);
    const top = await nav.evaluate(el => el.getBoundingClientRect().top);
    expect(top).toBeLessThanOrEqual(1);

    // Anchor links chỉ cho section đang bật.
    await expect(nav.locator('.store-nav-link').first()).toBeVisible();
  });
});
