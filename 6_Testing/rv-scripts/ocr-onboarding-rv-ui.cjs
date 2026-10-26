#!/usr/bin/env node
// ─────────────────────────────────────────────────────────────────────────────
// RV UI — OCR Tenant Onboarding Wizard (L3/L4) — PRODUCTION (2026-10-26 v2)
// Bao phủ yêu cầu bổ sung: nav root item + sitemap link + camera scan mode
// + responsive mobile (375×667) + full Case B create + cleanup.
//
// Chạy:  node rv-scripts/ocr-onboarding-rv-ui.cjs
// Screenshots: rv-scripts/ocr-onboarding-*.png
// ─────────────────────────────────────────────────────────────────────────────
const { chromium } = require('@playwright/test');
const { writeFileSync, mkdirSync } = require('fs');
const { join } = require('path');

const APP2 = process.env.APP2_URL ?? 'https://app2.khachvip.online';
const GW = process.env.GW_URL ?? 'https://api2.khachvip.online';
const ADMIN_USER = process.env.RV_ADMIN_USER ?? 'sysadmin@vanan.vn';
const ADMIN_PASS = process.env.RV_ADMIN_PASS ?? '2026@vanan';
const GATEWAY_IP = process.env.GATEWAY_IP ?? '136.85.94.119';

const results = [];
function check(name, pass, detail = '') {
  results.push({ name, pass: !!pass, detail });
  console.log(`${pass ? '[PASS]' : '[FAIL]'} ${name}${detail ? ` — ${detail}` : ''}`);
}

const gpsMock = `
  Object.defineProperty(navigator, 'geolocation', {
    value: {
      getCurrentPosition: (success) => success({ coords: { latitude: 10.7705, longitude: 106.7031, accuracy: 15 } }),
      watchPosition: () => 0, clearWatch: () => {}
    },
    configurable: true
  });
`;

const tsName = new Date().toISOString().slice(0, 19).replace(/[:T]/g, '');

const LAUNCH_ARGS = [
  '--host-resolver-rules=' +
    `MAP app2.khachvip.online ${GATEWAY_IP}, MAP api2.khachvip.online ${GATEWAY_IP}, ` +
    `MAP api.khachvip.online ${GATEWAY_IP}, MAP khachvip.online ${GATEWAY_IP}, MAP www2.khachvip.online ${GATEWAY_IP}`,
  // Fake camera cho mode "Chụp trực tiếp" (getUserMedia headless)
  '--use-fake-device-for-media-stream',
  '--use-fake-ui-for-media-stream',
];

(async () => {
  mkdirSync(join(__dirname, 'auth'), { recursive: true });
  const browser = await chromium.launch({ headless: true, args: LAUNCH_ARGS });

  // ── Login + save state ────────────────────────────────────────────────
  let token = null;
  try {
    const authCtx = await browser.newContext({ ignoreHTTPSErrors: true });
    const login = await authCtx.request.post(`${APP2}/api/platform/login`, {
      data: { Username: ADMIN_USER, Password: ADMIN_PASS },
      headers: { 'Content-Type': 'application/json' },
    });
    if (!login.ok()) throw new Error(`Login fail: ${login.status()}`);
    const loginBody = await login.json();
    token = loginBody?.token ?? loginBody?.accessToken ?? loginBody?.jwt;
    await authCtx.storageState({ path: join(__dirname, 'auth/rv-ocr-admin.json') });
    console.log('saved auth/rv-ocr-admin.json');
    await authCtx.close();
  } catch (e) {
    check('login + save state', false, e.message);
  }

  async function newWizardPage(viewport) {
    const context = await browser.newContext({
      ignoreHTTPSErrors: true,
      storageState: join(__dirname, 'auth/rv-ocr-admin.json'),
      viewport: viewport ?? { width: 1280, height: 800 },
    });
    await context.addInitScript(gpsMock);
    if (viewport) {
      await context.grantPermissions(['camera', 'geolocation'], { origin: APP2 });
    }
    const page = await context.newPage();
    const logs = [];
    page.on('console', (m) => { if (m.type() === 'error') logs.push(`[console.error] ${m.text()}`); });
    page.on('pageerror', (e) => logs.push(`[pageerror] ${e.message}`));
    return { context, page, logs };
  }

  // ═══ DESKTOP — nav + sitemap + wizard render ═════════════════════════
  try {
    const { context, page, logs } = await newWizardPage();

    // Nav menu: root item "Onboarding OCR" (2026-10-26 user directive).
    // AdminLayout (trang /admin/*) render ShopErpMenuService — /dashboard dùng
    // menu hardcoded GetDashboardMenu() nên KHÔNG có item này (đúng thiết kế).
    await page.goto(`${APP2}/admin/tenants`, { waitUntil: 'networkidle', timeout: 60000 });
    await page.waitForTimeout(2500);
    const navOcr = page.locator('a, button', { hasText: 'Onboarding OCR' }).first();
    const navVisible = await navOcr.isVisible().catch(() => false);
    check('L3 nav root item "Onboarding OCR" visible (AdminLayout)', navVisible);
    if (navVisible) {
      await navOcr.click();
      await page.waitForTimeout(2500);
      const url = page.url();
      const wizardActive = url.includes('/admin/onboarding-ocr') &&
        (await page.locator('.ocr-case-card', { hasText: 'Shop nhỏ' }).isVisible().catch(() => false));
      check('L3 nav item → page riêng /admin/onboarding-ocr + wizard step 0', wizardActive, url);
      await page.screenshot({ path: join(__dirname, 'ocr-onboarding-nav.png'), fullPage: true });
    }

    // Sitemap: link "Onboarding OCR" (2026-10-26 user directive)
    await page.goto(`${APP2}/sitemap`, { waitUntil: 'networkidle', timeout: 60000 });
    await page.waitForTimeout(2000);
    const sitemapLink = page.locator('[data-testid="link-admin-onboarding-ocr"]');
    const sitemapOk = await sitemapLink.isVisible().catch(() => false);
    check('L3 sitemap link "Onboarding OCR" visible', sitemapOk);
    if (sitemapOk) {
      await sitemapLink.click();
      await page.waitForTimeout(2500);
      check('L3 sitemap link → page riêng mở (wizard active)',
        page.url().includes('/admin/onboarding-ocr') &&
        (await page.locator('.ocr-wizard').isVisible().catch(() => false)));
    }

    // Camera mode (fake camera): GPKD modal → video → capture → không crash circuit
    await page.goto(`${APP2}/admin/onboarding-ocr`, { waitUntil: 'networkidle', timeout: 60000 });
    await page.waitForTimeout(2000);
    await page.locator('.ocr-case-card', { hasText: 'Công ty / Hộ kinh doanh' }).click();
    await page.getByRole('button', { name: 'Tiếp theo' }).click();
    await page.waitForTimeout(800);
    await page.getByRole('button', { name: 'Chụp trực tiếp' }).first().click();
    await page.waitForTimeout(2500); // getUserMedia fake + video play
    const videoVisible = await page.locator('#ocrCameraVideo').isVisible().catch(() => false);
    const videoPlaying = await page.evaluate(() => {
      const v = document.getElementById('ocrCameraVideo');
      return !!v && v.videoWidth > 0;
    }).catch(() => false);
    check('L3 camera modal: video hiển thị + đang chạy (fake device)', videoVisible && videoPlaying);
    await page.screenshot({ path: join(__dirname, 'ocr-onboarding-camera.png'), fullPage: true });
    // Chụp + OCR — fake frame → OCR chạy (text có thể rác) — yêu cầu: KHÔNG circuit chết.
    // Sau OCR thành công wizard TỰ ĐÓNG camera (OnOcrResult → CloseCameraAsync) —
    // chấp nhận modal đóng (auto) HOẶC còn mở (rồi đóng thủ công).
    await page.getByRole('button', { name: 'Chụp + Nhận diện' }).click();
    await page.waitForTimeout(6000); // Tesseract WASM load + recognize
    const camStillOpen = await page.locator('.ocr-camera-overlay').isVisible().catch(() => false);
    check('L3 capture+OCR không làm chết circuit (modal auto-close hoặc vẫn mở)',
      (await page.locator('.ocr-wizard').isVisible().catch(() => false)));
    if (camStillOpen) {
      await page.getByRole('button', { name: '✕ Đóng' }).click();
      await page.waitForTimeout(800);
    }
    check('L3 đóng camera OK', !(await page.locator('.ocr-camera-overlay').isVisible().catch(() => true)));
    check('L3 desktop không có console error/pageerror', logs.length === 0, logs.slice(0, 3).join(' | '));

    await context.close();
  } catch (e) {
    check('L3 desktop nav/sitemap/camera', false, e.message);
  }

  // ═══ MOBILE 375×667 — responsive + full Case B create ════════════════
  try {
    const { context, page, logs } = await newWizardPage({ width: 375, height: 667 });

    // Nav menu mobile: root item visible (AdminLayout — /dashboard dùng menu hardcoded)
    await page.goto(`${APP2}/admin/tenants`, { waitUntil: 'networkidle', timeout: 60000 });
    await page.waitForTimeout(2500);
    const navOcrM = page.locator('a, button', { hasText: 'Onboarding OCR' }).first();
    check('L4 mobile: nav root item visible', await navOcrM.isVisible().catch(() => false));
    await navOcrM.click();
    await page.waitForTimeout(2500);

    // Responsive: case cards xếp 1 cột (bounding box x bằng nhau, y tăng dần)
    const cards = page.locator('.ocr-case-card');
    const boxes = await cards.evaluateAll((els) => els.map((e) => {
      const r = e.getBoundingClientRect();
      return { x: r.x, w: r.width, y: r.y };
    })).catch(() => []);
    const oneColumn = boxes.length === 2 && Math.abs(boxes[0].x - boxes[1].x) < 2 && boxes[1].y > boxes[0].y;
    check('L4 mobile: case cards xếp 1 cột (responsive)', oneColumn,
      JSON.stringify(boxes.map((b) => ({ x: Math.round(b.x), w: Math.round(b.w), y: Math.round(b.y) }))));
    await page.screenshot({ path: join(__dirname, 'ocr-onboarding-mobile-step0.png'), fullPage: true });

    // Full Case B create trên mobile (GPS mock + fake camera không cần ở đây)
    await page.locator('.ocr-case-card', { hasText: 'Shop nhỏ' }).click();
    await page.getByRole('button', { name: 'Tiếp theo' }).click();
    await page.waitForTimeout(800);
    await page.locator('input[placeholder="VD: Cà Phê ABC"]').fill(`RV UI Mobile ${tsName}`);
    await page.locator('input[placeholder="Số nhà, đường, phường/xã, quận/huyện, tỉnh/thành"]').fill('12 Nguyễn Huệ, Q1, HCM');
    await page.locator('.ocr-form select').selectOption('2');
    await page.getByRole('button', { name: 'Tiếp theo' }).click();
    await page.waitForTimeout(800);
    // Nút "Chụp trực tiếp" cho biển hiệu + menu (yêu cầu bổ sung) hiện trên mobile
    const captureBtns = await page.locator('.ocr-capture-row button', { hasText: 'Chụp trực tiếp' }).count();
    check('L4 mobile: 2 nút "Chụp trực tiếp" (biển hiệu + menu)', captureBtns === 2, `count=${captureBtns}`);
    await page.getByRole('button', { name: 'Tiếp theo' }).click();
    await page.waitForTimeout(800);
    await page.getByRole('button', { name: 'Lấy vị trí hiện tại' }).click();
    await page.waitForTimeout(1500);
    await page.getByRole('button', { name: 'Tạo tenant + kích hoạt' }).click();
    await page.waitForTimeout(8000);
    const credModal = await page.locator('.ocr-credential-box').isVisible().catch(() => false);
    check('L4 mobile: full Case B create → credentials modal', credModal);
    await page.screenshot({ path: join(__dirname, 'ocr-onboarding-mobile-result.png'), fullPage: true });

    // Cleanup tenant vừa tạo
    if (token) {
      const list = await context.request.get(`${GW}/api/v1/tenants`, {
        headers: { Authorization: `Bearer ${token}` },
      });
      const tenants = await list.json();
      const created = tenants?.find((t) => t.name?.includes(`RV UI Mobile ${tsName}`));
      if (created) {
        const tid = created.tenantId ?? created.id;
        const deact = await context.request.post(`${GW}/api/v1/tenants/${tid}/deactivate`, {
          headers: { Authorization: `Bearer ${token}` },
          data: { reason: 'RV cleanup' },
        });
        check('L4 cleanup: deactivate tenant mobile tạo', deact.ok(), `status=${deact.status()}`);
      } else {
        check('L4 cleanup: deactivate tenant mobile tạo', false, 'không tìm thấy tenant');
      }
    }
    check('L4 mobile không có console error/pageerror', logs.length === 0, logs.slice(0, 3).join(' | '));

    await context.close();
  } catch (e) {
    check('L4 mobile flow', false, e.message);
  }

  await browser.close();

  const failed = results.filter((r) => !r.pass).length;
  const report = { ts: new Date().toISOString(), total: results.length, failed, results };
  writeFileSync(join(__dirname, 'ocr-onboarding-rv-ui-report.json'), JSON.stringify(report, null, 2));
  console.log(`\n=== UI RV DONE: ${results.length - failed}/${results.length} PASS, ${failed} FAIL ===`);
  process.exit(failed > 0 ? 1 : 0);
})();