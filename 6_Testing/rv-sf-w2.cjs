// SF-W2 RV (2026-11-07) — storefront smart UX đợt 2 production sweep:
//   L2 page · A2 semantic search bar (type → answer → product card → "Hỏi AI thêm"
//   opens widget) · B3 gallery quick-view (click card → modal → add cart → close)
//   · B5 scroll-reveal (armed → revealed on scroll, layout intact)
// Run: node rv-sf-w2.cjs   (needs playwright installed in 6_Testing)
const { chromium } = require('@playwright/test');

const STORE = 'https://diemthuong2.khachvip.online/store/by-id/e2d91cdf-d3c4-4399-b96e-e772a7a41630';
const MAP = 'MAP diemthuong2.khachvip.online 136.85.94.119';
const QUESTION = 'Quán có món gì?';

const results = [];
const check = (name, ok, extra = '') => {
  results.push({ name, ok });
  console.log(`${ok ? 'PASS' : 'FAIL'} ${name}${extra ? ' — ' + extra : ''}`);
};
const warn = (name, extra) => console.log(`WARN ${name} — ${extra}`);

(async () => {
  const browser = await chromium.launch({
    headless: true,
    args: ['--host-resolver-rules=' + MAP],
  });

  const ctx = await browser.newContext({ ignoreHTTPSErrors: true });
  const page = await ctx.newPage();
  page.on('pageerror', e => console.log('PAGEERROR:', e.message));

  const resp = await page.goto(STORE, { waitUntil: 'networkidle', timeout: 60000 });
  check('L2 storefront 200', resp?.status() === 200, `status=${resp?.status()}`);

  // WASM first-load can take >20s — wait for store body first.
  await page.locator('.store-body').waitFor({ timeout: 45000 }).catch(() => {});

  // ── A2: semantic search bar ────────────────────────────────────────────
  const searchInput = page.locator('.store-search-input');
  await searchInput.waitFor({ timeout: 20000 }).catch(() => {});
  check('A2 search bar rendered', (await searchInput.count()) > 0);
  if (await searchInput.count()) {
    await searchInput.fill(QUESTION);
    await page.locator('.store-search-btn').click();
    await page.locator('.store-search-answer').waitFor({ timeout: 60000 }).catch(() => {});
    const answer = page.locator('.store-search-answer');
    check('A2 search → answer shown', (await answer.count()) > 0,
      (await answer.count()) ? (await answer.innerText()).slice(0, 60) : 'no answer (AI down?)');

    const cards = await page.locator('.store-search-product').count();
    if (cards > 0) {
      check('A2 search → product cards', true, `${cards} card(s)`);
      const added = await page.locator('.store-search-add').count();
      check('A2 product card has add-to-cart', added > 0);
    } else {
      warn('A2 product cards', 'answer có 0 products (no-data path — kiểm tra knowledge)');
    }

    // "Hỏi AI thêm" → widget opens with the same question auto-sent.
    const askBtn = page.locator('.store-search-ask');
    if (await askBtn.count()) {
      await askBtn.click();
      await page.locator('.vanan-ai-panel').waitFor({ timeout: 10000 }).catch(() => {});
      check('A2 "Hỏi AI thêm" → widget opened', (await page.locator('.vanan-ai-panel').count()) > 0);
      await page.locator('.vanan-ai-msg-user .vanan-ai-bubble').first()
        .waitFor({ timeout: 10000 }).catch(() => {});
      const userMsg = await page.locator('.vanan-ai-msg-user .vanan-ai-bubble').first()
        .innerText().catch(() => '');
      check('A2 question forwarded to widget', userMsg.includes(QUESTION.slice(0, 6)),
        `"${userMsg.slice(0, 40)}"`);
      await page.locator('button[aria-label="Đóng trợ lý AI"]').click().catch(() => {});
    }
  }

  // ── B3: gallery quick-view ─────────────────────────────────────────────
  const qv = page.locator('.vibe-qv').first();
  if (await qv.count() === 0) {
    warn('B3 quick-view', 'no .vibe-qv cards on this store (VibeShowcase off/empty)');
  } else {
    await qv.scrollIntoViewIfNeeded().catch(() => {});
    await page.waitForTimeout(400); // let reveal finish animating
    await qv.click();
    await page.locator('.modal.show .qv-media').waitFor({ timeout: 10000 }).catch(() => {});
    check('B3 card click → quick-view modal', (await page.locator('.modal.show .qv-media').count()) > 0);
    if (await page.locator('.modal.show').count()) {
      check('B3 modal has price', (await page.locator('.qv-price').count()) > 0);
      const aiChip = await page.locator('.qv-actions .store-ai-chip').count();
      check('B3 "Hỏi AI" chip (AIChat on)', aiChip > 0);

      // Add to cart → toast/badge appears + modal closes.
      // (toast element is #vanan-toast by id — success variant auto-dismisses after 2s)
      await page.locator('.qv-actions .btn-cta').click();
      await page.waitForTimeout(800);
      const toast = await page.locator('#vanan-toast, .cart-badge').count();
      check('B3 add-to-cart feedback', toast > 0);
      check('B3 modal closed after add', (await page.locator('.modal.show').count()) === 0);

      // Re-open + close via × button.
      await qv.click();
      await page.locator('.modal.show').waitFor({ timeout: 10000 }).catch(() => {});
      await page.locator('.modal.show .btn-close').click();
      await page.waitForTimeout(400);
      check('B3 close button dismisses modal', (await page.locator('.modal.show').count()) === 0);
    }
  }

  // ── B5: scroll-reveal micro-motion ─────────────────────────────────────
  const revealCount = await page.locator('.reveal-on-scroll').count();
  check('B5 reveal-on-scroll elements present', revealCount > 0, `${revealCount} element(s)`);
  if (revealCount) {
    // At load, in-viewport items got armed+revealed; below-fold items are armed only.
    const armedOrRevealed = await page.evaluate(() =>
      document.querySelectorAll('.reveal-on-scroll.reveal-armed, .reveal-on-scroll.revealed').length);
    check('B5 vananReveal.scan armed elements', armedOrRevealed > 0, `${armedOrRevealed} armed/revealed`);

    // Scroll through the page — every element must end up revealed + visible.
    await page.evaluate(async () => {
      const step = window.innerHeight * 0.8;
      for (let y = 0; y <= document.body.scrollHeight; y += step) {
        window.scrollTo(0, y);
        await new Promise(r => setTimeout(r, 120));
      }
    });
    await page.waitForTimeout(900); // outlast the last IO dispatch + 0.3s transition
    const hidden = await page.evaluate(() =>
      [...document.querySelectorAll('.reveal-on-scroll')]
        .filter(el => !el.classList.contains('revealed')).length);
    check('B5 all elements revealed after scroll', hidden === 0, `${hidden} still hidden`);
    // Elements revealed during the scroll loop animate 0→1 over 0.3s; headless IO
    // dispatch can lag → poll until every revealed element settles at opacity 1.
    const settle = await page.evaluate(async () => {
      const els = [...document.querySelectorAll('.reveal-on-scroll.revealed')];
      const deadline = Date.now() + 5000;
      while (Date.now() < deadline) {
        const pending = els.filter(el => getComputedStyle(el).opacity !== '1').length;
        if (pending === 0) return { pending: 0 };
        await new Promise(r => setTimeout(r, 150));
      }
      return { pending: els.filter(el => getComputedStyle(el).opacity !== '1').length };
    });
    check('B5 no stuck-opacity layout break', settle.pending === 0,
      settle.pending ? `${settle.pending} still transitioning/stuck` : '');
  }

  const fails = results.filter(r => !r.ok).length;
  console.log(`\n==> SF-W2 RV: ${results.length - fails} PASS · ${fails} FAIL`);
  await browser.close();
  process.exit(fails ? 1 : 0);
})().catch(e => { console.error(e); process.exit(1); });
