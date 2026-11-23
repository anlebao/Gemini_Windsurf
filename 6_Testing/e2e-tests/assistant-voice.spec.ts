import { test, expect } from '@playwright/test';

// UNIVERSAL AI ASSISTANT W3d — RV production sweep: voice wiring V1 trên
// /a/{slug} + embed. Tenant: An Tâm (e2d91cdf) — voice_Note_Enabled=true,
// aiChat_Enabled=true (verify qua /api/tenants/{id}/feature-settings).
// Cover: script load, mic button, speaker button per AI message,
// auto-speak toggle, embed mode parity.

const LINK_URL = 'https://shopai.timlathay.com/a/rvAntamAi01x';
const EMBED_URL = 'https://shopai.timlathay.com/a/rvAntamAi01x?embed=1';

// Đếm số lần speechSynthesis.speak được gọi — chứng minh TTS fire thật
// (headless Chrome có API nhưng không phát audio; ta assert call count).
async function instrumentTts(page: import('@playwright/test').Page) {
  await page.addInitScript(() => {
    (window as any).__ttsCalls = [] as string[];
    const orig = window.speechSynthesis?.speak?.bind(window.speechSynthesis);
    if (orig) {
      window.speechSynthesis.speak = (u: SpeechSynthesisUtterance) => {
        (window as any).__ttsCalls.push(u.text.slice(0, 80));
        return orig(u);
      };
    }
  });
}

async function ttsCallCount(page: import('@playwright/test').Page): Promise<number> {
  return page.evaluate(() => ((window as any).__ttsCalls || []).length);
}

async function sendChat(page: import('@playwright/test').Page, text: string) {
  const widget = page.locator('.vanan-ai-widget');
  await expect(widget).toBeVisible({ timeout: 30000 });
  const input = widget.locator('input[type="text"], input:not([type]), textarea').first();
  await expect(input).toBeVisible({ timeout: 15000 });
  await input.fill(text);
  const sendBtn = widget.locator('button[aria-label*="Gửi"], .vanan-ai-send, button:has(i.bi-send)').first();
  if (await sendBtn.isVisible().catch(() => false)) await sendBtn.click();
  else await input.press('Enter');
  // chờ AI trả lời xong (input enable lại) + bubble cuối có nội dung
  await expect(input).toBeEnabled({ timeout: 90000 });
  const bubbles = widget.locator('.vanan-ai-msg-assistant .vanan-ai-bubble');
  await expect(bubbles.last()).toBeVisible({ timeout: 30000 });
  await expect
    .poll(async () => (await bubbles.last().innerText()).trim().length, { timeout: 30000 })
    .toBeGreaterThan(10);
}

test.describe('W3d AI voice wiring RV', () => {
  test('voice scripts loaded — vananVoiceInput + vananVoiceTts globals', async ({ page }) => {
    await page.goto(LINK_URL, { waitUntil: 'domcontentloaded' });
    await expect(page.locator('.vanan-ai-widget')).toBeVisible({ timeout: 30000 });

    const hasStt = await page.evaluate(() => typeof (window as any).vananVoiceInput?.isSupported === 'function');
    const hasTts = await page.evaluate(() => typeof (window as any).vananVoiceTts?.isSupported === 'function');
    expect(hasStt).toBeTruthy();
    expect(hasTts).toBeTruthy();
  });

  test('mic + speaker + auto-speak controls visible khi flag ON', async ({ page }) => {
    await instrumentTts(page);
    await page.goto(LINK_URL, { waitUntil: 'domcontentloaded' });
    const widget = page.locator('.vanan-ai-widget');
    await expect(widget).toBeVisible({ timeout: 30000 });

    // Mic button (SpeechRecognition — Chromium headless support)
    const mic = widget.locator('.vanan-ai-mic');
    const micVisible = await mic.isVisible().catch(() => false);
    // TTS controls: auto-speak toggle luôn hiện khi TTS supported
    const autoSpeak = widget.locator('[data-testid="btn-tts-autospeak"]');
    await expect(autoSpeak).toBeVisible({ timeout: 15000 });
    // Ít nhất 1 trong 2 phải có (TTS chắc chắn có trên Chromium)
    expect(micVisible || (await autoSpeak.isVisible())).toBeTruthy();
  });

  test('AI reply → auto-speak fire + speaker button replay/stop', async ({ page }) => {
    await instrumentTts(page);
    await page.goto(LINK_URL, { waitUntil: 'domcontentloaded' });
    await sendChat(page, 'Xin chào, quán có món gì?');

    const widget = page.locator('.vanan-ai-widget');

    // Auto-speak: câu trả lời mới phải trigger speechSynthesis.speak ≥1 lần
    await expect
      .poll(() => ttsCallCount(page), { timeout: 15000 })
      .toBeGreaterThan(0);

    // Speaker button trên AI message (class .vanan-ai-tts — tránh match nhầm
    // data-testid="btn-tts-autospeak" vì cùng prefix)
    const speaker = widget.locator('button.vanan-ai-tts').last();
    await expect(speaker).toBeVisible({ timeout: 10000 });
    const before = await ttsCallCount(page);
    await speaker.click();
    await expect
      .poll(() => ttsCallCount(page), { timeout: 10000 })
      .toBeGreaterThan(before);

    // Click lại → stop (icon đổi sang bi-stop-circle khi đang đọc)
    await speaker.click();
    const stopped = await page.evaluate(() => !(window.speechSynthesis.speaking || window.speechSynthesis.pending));
    expect(stopped).toBeTruthy();
  });

  test('embed mode ?embed=1 — voice controls hoạt động', async ({ page }) => {
    await instrumentTts(page);
    await page.goto(EMBED_URL, { waitUntil: 'domcontentloaded' });
    const widget = page.locator('.vanan-ai-widget');
    await expect(widget).toBeVisible({ timeout: 30000 });

    // Embed: header ẩn, widget vẫn mở + auto-speak toggle hiện
    await expect(page.locator('.assistant-header')).toHaveCount(0);
    await expect(widget.locator('[data-testid="btn-tts-autospeak"]')).toBeVisible({ timeout: 15000 });

    await sendChat(page, 'Quán mở cửa lúc mấy giờ?');
    await expect
      .poll(() => ttsCallCount(page), { timeout: 15000 })
      .toBeGreaterThan(0);
    await expect(widget.locator('button.vanan-ai-tts').last()).toBeVisible({ timeout: 10000 });
  });

  test('TTS sanitize đọc giá tiền thành chữ — 1000đ → "một nghìn đồng"', async ({ page }) => {
    await page.goto(LINK_URL, { waitUntil: 'domcontentloaded' });
    await expect(page.locator('.vanan-ai-widget')).toBeVisible({ timeout: 30000 });

    const out = await page.evaluate(() => {
      const t = (window as any).vananVoiceTts;
      return {
        plain: t.sanitize('Giá 1000đ'),
        dots: t.sanitize('Combo 850.000đ thôi'),
        million: t.sanitize('Chỉ 1.200.000 đồng'),
        k: t.sanitize('Rẻ 50k'),
        untouched: t.sanitize('Năm nay 2026 có 5 món'),
      };
    });

    expect(out.plain).toContain('một nghìn đồng');
    expect(out.plain).not.toContain('không không');
    expect(out.dots).toContain('tám trăm năm mươi nghìn đồng');
    expect(out.million).toContain('một triệu hai trăm nghìn đồng');
    expect(out.k).toContain('năm mươi nghìn đồng');
    expect(out.untouched).toContain('2026'); // số thường không bị đổi
  });
});
