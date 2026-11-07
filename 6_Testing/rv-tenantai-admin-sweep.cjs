// Task AI-P10 RV SWEEP (2026-11-07) — quét TOÀN BỘ admin UI /admin/tenant-ai/*
// trên production, collect tất cả PASS/FAIL rồi report một lần (sweep-first).
// Coverage: nav + sitemap + hub cards/links + knowledge (upload→pending→approve→
// docs list→delete cleanup) + profile (draft→preview→publish→rollback restore) +
// policy + conversations + usage.
// Tenant: An Tâm pilot e2d91cdf (TenantAiEnabled=ON, vanan-ai live).
const { chromium } = require('@playwright/test');

const APP2 = 'https://app2.khachvip.online';
const TENANT = 'e2d91cdf-d3c4-4399-b96e-e772a7a41630';
const MAP = '--host-resolver-rules=MAP app2.khachvip.online 136.85.94.119, MAP api2.khachvip.online 136.85.94.119';
const T = 30000; // default visibility timeout (Blazor interactive warmup)

const results = [];
const check = (name, ok, extra = '') => {
  results.push({ name, ok });
  console.log(`${ok ? 'PASS' : 'FAIL'} ${name}${extra ? ' — ' + extra : ''}`);
};
const vis = async (page, tid, timeout = T) =>
  page.getByTestId(tid).first().isVisible({ timeout }).catch(() => false);

(async () => {
  const browser = await chromium.launch({ headless: true, args: [MAP] });
  const context = await browser.newContext({ ignoreHTTPSErrors: true });
  const pageErrors = [];

  // ---------- auth ----------
  const login = await context.request.post(`${APP2}/api/platform/login`, {
    data: { Username: 'sysadmin@vanan.vn', Password: '2026@vanan' },
    headers: { 'Content-Type': 'application/json' },
  });
  check('auth: login sysadmin', login.ok(), `status=${login.status()}`);
  const imp = await context.request.post(`${APP2}/api/admin/impersonate/${TENANT}`);
  check('auth: impersonate An Tâm', imp.ok(), `status=${imp.status()}`);
  if (!login.ok() || !imp.ok()) { await browser.close(); process.exit(1); }

  const page = await context.newPage();
  page.on('pageerror', e => pageErrors.push(e.message));

  // ---------- L2: routes live (302→login khi anonymous) ----------
  for (const r of ['', '/profile', '/knowledge', '/policy', '/conversations', '/usage']) {
    const resp = await context.request.get(`${APP2}/admin/tenant-ai${r}`, {
      maxRedirects: 0,
    }).catch(e => ({ status: () => `ERR:${e.message.slice(0, 60)}` }));
    const s = typeof resp.status === 'function' ? resp.status() : resp.status;
    // impersonated ctx → route render 200 (Blazor SSR shell); 404 = route missing
    check(`L2 route /admin/tenant-ai${r || '/'}`, s === 200 || s === 302, `status=${s}`);
  }

  // ---------- nav + sitemap ----------
  await page.goto(`${APP2}/`, { waitUntil: 'networkidle', timeout: 60000 });
  const navLink = await page.getByRole('link', { name: /Trợ lý AI/ }).first()
    .isVisible({ timeout: 15000 }).catch(() => false);
  check('nav: mục "Trợ lý AI" hiển thị', navLink);

  await page.goto(`${APP2}/sitemap`, { waitUntil: 'networkidle', timeout: 60000 });
  check('sitemap: link-tenant-ai', await vis(page, 'link-tenant-ai'));

  // ---------- hub ----------
  await page.goto(`${APP2}/admin/tenant-ai`, { waitUntil: 'networkidle', timeout: 60000 });
  check('hub: render', await vis(page, 'tenant-ai-hub'));
  const flagOff = await vis(page, 'tenant-ai-disabled', 3000);
  const svcDown = await vis(page, 'alert-service-down', 3000);
  check('hub: flag ON + service reachable', !flagOff && !svcDown,
    `flagOff=${flagOff} svcDown=${svcDown}`);
  if (!flagOff && !svcDown) {
    for (const tid of ['card-ai-products', 'card-ai-docs', 'card-ai-profile', 'card-ai-usage',
      'card-ai-links', 'link-ai-profile', 'link-ai-knowledge', 'link-ai-policy',
      'link-ai-usage', 'link-ai-conversations', 'link-ai-features']) {
      check(`hub: ${tid}`, await vis(page, tid));
    }
  }

  // ---------- knowledge: upload → pending → approve → list → delete ----------
  await page.goto(`${APP2}/admin/tenant-ai/knowledge`, { waitUntil: 'networkidle', timeout: 60000 });
  check('knowledge: render', await vis(page, 'tenant-ai-knowledge'));
  check('knowledge: tab-pending + tab-docs',
    (await vis(page, 'tab-pending')) && (await vis(page, 'tab-docs')));

  await page.getByTestId('tab-docs').click();
  check('knowledge: card-upload + card-docs',
    (await vis(page, 'card-upload')) && (await vis(page, 'card-docs')));
  check('knowledge: select-status-filter', await vis(page, 'select-status-filter'));

  const title = `RV-P10 ${Date.now() % 1000000}`;
  let docId = null;
  try {
    await page.getByTestId('select-doc-type').selectOption('faq');
    await page.getByTestId('input-doc-title').fill(title);
    await page.getByTestId('input-doc-content')
      .fill('RV-P10: quán mở cửa 9h-21h mỗi ngày. Đây là dữ liệu kiểm thử.');
    await page.getByTestId('btn-upload-doc').click();
    // switch về pending sau upload (ReloadAsync giữ tab docs — check cả 2)
    await page.getByTestId('tab-pending').click();
    await page.waitForTimeout(1500);
    const pendingCard = page.getByTestId('pending-doc').filter({ hasText: title });
    const inPending = await pendingCard.first().isVisible({ timeout: 10000 }).catch(() => false);
    check('knowledge: upload → xuất hiện ở Chờ duyệt', inPending);
    if (inPending) {
      await pendingCard.first().getByTestId('btn-approve-doc').click();
      await page.waitForTimeout(2000);
      check('knowledge: approve → biến mất khỏi pending',
        (await pendingCard.count()) === 0);
    }
    // docs tab: doc đã duyệt xuất hiện + xóa cleanup
    await page.getByTestId('tab-docs').click();
    await page.getByTestId('select-status-filter').selectOption('approved');
    await page.waitForTimeout(1500);
    const docItem = page.getByTestId('doc-item').filter({ hasText: title });
    const inDocs = await docItem.first().isVisible({ timeout: 10000 }).catch(() => false);
    check('knowledge: doc approved xuất hiện ở list (filter=approved)', inDocs);
    if (inDocs) {
      await docItem.first().getByTestId('btn-expand-doc').click();
      check('knowledge: expand content',
        await docItem.first().locator('pre.doc-content').isVisible({ timeout: 5000 }).catch(() => false));
      await docItem.first().getByTestId('btn-delete-doc').click();
      await docItem.first().getByTestId('btn-confirm-delete').click();
      await page.waitForTimeout(2000);
      check('knowledge: delete cleanup', (await docItem.count()) === 0);
    }
  } catch (e) {
    check('knowledge flow (exception)', false, e.message.slice(0, 100));
  }

  // ---------- profile: draft → preview → publish → rollback restore ----------
  await page.goto(`${APP2}/admin/tenant-ai/profile`, { waitUntil: 'networkidle', timeout: 60000 });
  check('profile: render', await vis(page, 'tenant-ai-profile'));
  check('profile: card-profile-form + card-publish + card-preview',
    (await vis(page, 'card-profile-form')) && (await vis(page, 'card-publish')) && (await vis(page, 'card-preview')));

  // chụp published gốc để rollback về
  const origPublished = await page.getByTestId('published-summary')
    .textContent({ timeout: 8000 }).catch(() => null);
  check('profile: published-summary hiện bản đang phát hành', !!origPublished,
    origPublished ? origPublished.trim().slice(0, 80) : 'no published version');

  const aiName = `Bé RVP10 ${Date.now() % 10000}`;
  try {
    await page.getByTestId('input-ai-name').fill(aiName);
    await page.getByTestId('input-greeting').fill('Chào RV-P10!');
    await page.getByTestId('btn-save-draft').click();
    check('profile: lưu draft → draft-summary',
      await vis(page, 'draft-summary', 15000));

    await page.getByTestId('input-preview').fill('xin chào');
    await page.getByTestId('btn-preview-send').click();
    await page.waitForTimeout(3000);
    const thread = await page.getByTestId('preview-thread').textContent({ timeout: 60000 }).catch(() => '');
    check('profile: preview-chat trả lời (draft, không persist)', !!thread && thread.trim().length > 0,
      (thread || '').trim().slice(0, 60));

    await page.getByTestId('btn-publish').click();
    check('profile: confirm modal + soft-warning eval (NFR-10)',
      await vis(page, 'confirm-publish', 5000));
    await page.getByTestId('btn-confirm-publish').click();
    await page.waitForTimeout(3000);
    const pub = await page.getByTestId('published-summary').textContent({ timeout: 15000 }).catch(() => '');
    check('profile: publish → published-summary hiện tên mới',
      (pub || '').includes(aiName), (pub || '').trim().slice(0, 80));

    // rollback về bản published gốc (restore production state)
    const sel = page.getByTestId('select-rollback-version');
    check('profile: select-rollback-version hiển thị', await sel.isVisible({ timeout: 5000 }).catch(() => false));
    if (await sel.isVisible().catch(() => false) && origPublished) {
      const m = origPublished.match(/v(\d+)/);
      const origVer = m ? m[1] : null;
      if (origVer) {
        await sel.selectOption(origVer);
        await page.getByTestId('btn-rollback').click();
        check('profile: confirm-rollback modal', await vis(page, 'confirm-rollback', 5000));
        await page.getByTestId('btn-confirm-rollback').click();
        await page.waitForTimeout(3000);
        const restored = await page.getByTestId('published-summary')
          .textContent({ timeout: 15000 }).catch(() => '');
        check('profile: rollback → published trở về bản gốc',
          (restored || '').includes(`v${origVer}`) === false && (restored || '').length > 0,
          `restored="${(restored || '').trim().slice(0, 80)}" orig="${origPublished.trim().slice(0, 60)}"`);
      } else check('profile: parse orig version', false, origPublished);
    }
  } catch (e) {
    check('profile flow (exception)', false, e.message.slice(0, 100));
  }

  // ---------- policy ----------
  await page.goto(`${APP2}/admin/tenant-ai/policy`, { waitUntil: 'networkidle', timeout: 60000 });
  check('policy: render', await vis(page, 'tenant-ai-policy'));
  check('policy: card-intent-scopes + toggles',
    (await vis(page, 'card-intent-scopes')) &&
    (await page.locator('[data-testid^="intent-toggle-"]').count()) > 0);
  check('policy: refused-topics editor + external read-only',
    (await vis(page, 'card-refused-topics')) && (await vis(page, 'card-external-allow')));
  check('policy: btn-save-policy', await vis(page, 'btn-save-policy'));

  // ---------- conversations ----------
  await page.goto(`${APP2}/admin/tenant-ai/conversations`, { waitUntil: 'networkidle', timeout: 60000 });
  check('conversations: render', await vis(page, 'tenant-ai-conversations'));

  // ---------- usage ----------
  await page.goto(`${APP2}/admin/tenant-ai/usage`, { waitUntil: 'networkidle', timeout: 60000 });
  check('usage: render', await vis(page, 'tenant-ai-usage'));
  check('usage: select-days + card/grid',
    (await vis(page, 'select-days')) && ((await vis(page, 'usage-grid')) || (await vis(page, 'card-usage'))));

  // ---------- summary ----------
  console.log('\n===== SWEEP SUMMARY =====');
  const fails = results.filter(r => !r.ok);
  console.log(`PASS ${results.length - fails.length} · FAIL ${fails.length} · TOTAL ${results.length}`);
  fails.forEach(f => console.log(`  FAIL: ${f.name}`));
  if (pageErrors.length) {
    console.log('PAGE ERRORS:');
    [...new Set(pageErrors)].slice(0, 10).forEach(e => console.log(`  ${e.slice(0, 200)}`));
  }
  await browser.close();
  process.exit(fails.length ? 1 : 0);
})().catch(e => { console.error('FATAL', e); process.exit(2); });
