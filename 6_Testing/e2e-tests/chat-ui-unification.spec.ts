import { test, expect, type Page } from '@playwright/test';
import { loadEnvConfig, isTierEnabled } from '../utils/env-config';
import { TestReporter } from '../utils/test-reporter';

// Chat UI Unification P0 (Gate 4 — UI layout change → E2E required).
// Task card: docs/AI/tasks/chat_ui_unification/task_card.md
//
// Verifies the unified chat design system inside AiChatWidget (1 bubble, 2 modes):
//   - AI mode: oldest-first message order (latest at bottom), typing indicator last
//   - Human mode (Chủ quán): widget header shows mode name; RealtimeChatPanel
//     renders SLIM toolbar (ShowHeader=false → no inner title h6)
//   - Unified input: send button is icon-only in BOTH modes (no "Gửi" label)
//   - Unified bubbles: human messages use .chat-bubble--own / .chat-bubble--peer + avatar
//
// Self-gating: needs AI_CHAT_TEST_STORE_URL (tenant có AIChat + chat identity).
// Skips gracefully khi widget/flags không khả dụng.

const config = loadEnvConfig();
const reporter = new TestReporter('Chat UI Unification P0 E2E');

const STORE_URL = process.env.AI_CHAT_TEST_STORE_URL ?? '';
const QUESTION = process.env.AI_CHAT_TEST_QUESTION ?? 'Quán có món gì?';

test.describe.configure({ mode: isTierEnabled('e2e') ? 'parallel' : 'skip' });

test.describe('Chat UI Unification P0 (chat_ui_unification)', () => {
  test.beforeAll(async () => {
    if (!isTierEnabled('e2e')) {
      reporter.setArchitectDecision('Bypassed by Architect - E2E tests disabled');
      test.skip();
    }
    if (!STORE_URL) {
      reporter.setArchitectDecision('AI_CHAT_TEST_STORE_URL chưa set — spec bị skip');
      test.skip();
    }
    reporter.log(`Starting Chat UI Unification P0 E2E (store: ${STORE_URL})`);
  });

  test('AI mode: oldest-first — user msg trước, assistant answer cuối list', async ({ page }) => {
    await page.goto(STORE_URL);
    await page.waitForLoadState('networkidle');

    const bubble = page.locator('button.vanan-ai-bubble-btn');
    await expect(bubble).toBeVisible({ timeout: 20000 });
    await bubble.click();
    await expect(page.locator('.vanan-ai-panel')).toBeVisible({ timeout: 10000 });

    await page.locator('.vanan-ai-panel input').fill(QUESTION);
    await page.locator('button.vanan-ai-send').click();

    // User message render trước.
    await expect(page.locator('.vanan-ai-msg-user .vanan-ai-bubble').first())
      .toBeVisible({ timeout: 10000 });
    await expect(page.locator('.vanan-ai-msg-assistant .vanan-ai-bubble:not(.vanan-ai-typing)').first())
      .toBeVisible({ timeout: 45000 });

    // Oldest-first: user msg (cũ hơn) phải đứng TRƯỚC assistant msg trong DOM.
    const userBox = await page.locator('.vanan-ai-msg-user').first().boundingBox();
    const assistantBox = await page.locator('.vanan-ai-msg-assistant:not(:has(.vanan-ai-typing))').last().boundingBox();
    expect(userBox).not.toBeNull();
    expect(assistantBox).not.toBeNull();
    // Tin mới nhất (assistant answer) nằm DƯỚI tin user → y lớn hơn.
    expect(assistantBox!.y).toBeGreaterThan(userBox!.y);

    // Scroll container phải ở gần bottom sau khi có answer.
    const scrolled = await page.locator('#vanan-ai-scroll, [id$="ai-scroll"], .vanan-ai-messages').first()
      .evaluate(el => el.scrollHeight - el.scrollTop - el.clientHeight < 80);
    expect(scrolled).toBeTruthy();
  });

  test('Mode tabs: switch AI → Chủ quán → header + slim toolbar + unified input', async ({ page }) => {
    await page.goto(STORE_URL);
    await page.waitForLoadState('networkidle');

    const bubble = page.locator('button.vanan-ai-bubble-btn');
    await expect(bubble).toBeVisible({ timeout: 20000 });
    await bubble.click();
    await expect(page.locator('.vanan-ai-panel')).toBeVisible({ timeout: 10000 });

    // Segmented tabs phải hiện khi cả 2 mode khả dụng.
    const humanTab = page.locator('.vanan-ai-tab-human');
    if (await humanTab.count() === 0) {
      reporter.log('Human tab không render (HumanChatAvailable=false) — skip');
      test.skip();
    }
    await humanTab.click();

    // Widget header đổi sang "Chủ quán".
    await expect(page.locator('.vanan-ai-header h6')).toContainText('Chủ quán', { timeout: 10000 });

    // RealtimeChatPanel mount trong human surface.
    const chatPanel = page.locator('.ai-widget-human .chat-panel');
    await expect(chatPanel).toBeVisible({ timeout: 10000 });

    // ShowHeader=false → KHÔNG còn title h6 trong chat panel (slim toolbar only).
    await expect(chatPanel.locator('h6')).toHaveCount(0);

    // Slim toolbar vẫn giữ actions: call btn (nếu VoiceCallEnabled) + refresh + conn badge.
    // Ít nhất refresh button phải còn.
    await expect(chatPanel.locator('button[aria-label="Làm mới tin nhắn"]')).toBeVisible();

    // Unified input: send button icon-only — không chứa text "Gửi".
    const sendBtn = chatPanel.locator('.chat-send-btn');
    await expect(sendBtn).toBeVisible();
    await expect(sendBtn).not.toContainText('Gửi');
    await expect(sendBtn.locator('i.bi-send')).toBeVisible();

    // Input row horizontal: input + send btn cùng hàng (y gần nhau).
    const inputBox = await chatPanel.locator('.chat-input-row input, .chat-input-row .vanan-input-field, .chat-input-row input').first().boundingBox();
    const sendBox = await sendBtn.boundingBox();
    expect(inputBox).not.toBeNull();
    expect(sendBox).not.toBeNull();
    expect(Math.abs(inputBox!.y - sendBox!.y)).toBeLessThan(30);
  });

  // ── P1: bubble polish + mode-aware icon + mini call-bar ──────────────

  test('P1 — bubble 60px + icon đổi theo mode (robot → person)', async ({ page }) => {
    await page.goto(STORE_URL);
    await page.waitForLoadState('networkidle');

    const bubble = page.locator('button.vanan-ai-bubble-btn');
    await expect(bubble).toBeVisible({ timeout: 20000 });

    // P1: bubble to hơn — 60px.
    const box = await bubble.boundingBox();
    expect(box).not.toBeNull();
    expect(Math.round(box!.width)).toBeGreaterThanOrEqual(58);

    // AI mode (default) → robot icon.
    await expect(bubble.locator('i.bi-robot')).toBeVisible();

    // Không có cuộc gọi → mini call-bar KHÔNG hiện.
    await expect(page.locator('[data-testid="ai-mini-callbar"]')).toHaveCount(0);

    // Switch sang human mode rồi đóng → icon đổi person.
    await bubble.click();
    await expect(page.locator('.vanan-ai-panel')).toBeVisible({ timeout: 10000 });
    const humanTab = page.locator('.vanan-ai-tab-human');
    if (await humanTab.count() === 0) {
      reporter.log('Human tab không render — skip icon check');
      test.skip();
    }
    await humanTab.click();
    await page.locator('button[aria-label="Đóng trợ lý AI"]').click();
    await expect(bubble.locator('i.bi-person-fill')).toBeVisible({ timeout: 10000 });
  });

  test('Human mode: gửi tin → bubble unified (.chat-bubble--own)', async ({ page }) => {
    await page.goto(STORE_URL);
    await page.waitForLoadState('networkidle');

    const bubble = page.locator('button.vanan-ai-bubble-btn');
    await expect(bubble).toBeVisible({ timeout: 20000 });
    await bubble.click();
    await expect(page.locator('.vanan-ai-panel')).toBeVisible({ timeout: 10000 });

    const humanTab = page.locator('.vanan-ai-tab-human');
    if (await humanTab.count() === 0) {
      reporter.log('Human tab không render — skip');
      test.skip();
    }
    await humanTab.click();

    const chatPanel = page.locator('.ai-widget-human .chat-panel');
    await expect(chatPanel).toBeVisible({ timeout: 10000 });

    // Gửi tin nhắn text qua human chat (guest device identity).
    const input = chatPanel.locator('.chat-input-row input').first();
    await expect(input).toBeVisible({ timeout: 10000 });
    await input.fill('Xin chào từ E2E');
    await chatPanel.locator('.chat-send-btn').click();

    // Own bubble phải dùng class unified mới.
    await expect(chatPanel.locator('.chat-bubble--own').first())
      .toContainText('Xin chào từ E2E', { timeout: 15000 });

    // P2 R11: date separator "Hôm nay" hiện trên message mới.
    await expect(chatPanel.locator('.chat-date-sep').first())
      .toContainText('Hôm nay', { timeout: 10000 });
  });
});
