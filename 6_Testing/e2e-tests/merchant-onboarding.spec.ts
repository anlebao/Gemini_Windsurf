import { test, expect, chromium } from '@playwright/test';

/**
 * Task P5 — Self-Serve Onboarding RV production spec.
 * SRS: docs/requirements/van_an_self_serve_onboarding_srs_v1.md · task_p5_e2e_rv.md
 *
 * Flow: merchant self-serve (Case B + Case Rong) → Pending → sysadmin review
 * /admin/tenants → verify-signup → owner login app2 → reject path → regression
 * Track A (/onboard wizard + /admin/onboarding-ocr + flags API).
 *
 * DEVIATIONS (ghi nhận theo task card §5):
 * - Turnstile: production widget không solve được trong E2E → submit qua API
 *   request context (api2). P1 RV đã verify: prod sitekey rỗng → Gateway skip
 *   verify → POST không token vẫn 200. UI wizard chỉ test phần hiển thị/status.
 * - Rate limit "merchant-signup" = 5/24h/IP: spec submit 2 signup thật.
 *   Chạy lại trong 24h có thể 429 → phải đợi window reset.
 * - Cleanup tenant test: sysadmin deactivate thủ công sau RV (spec không xóa
 *   production data — theo precedent các RV trước).
 *
 * Auth: sysadmin login thật POST app2/api/platform/login (DevLogin #if DEBUG
 * không tồn tại trên Release) → storageState auth/rv-onboarding-admin.json.
 * Owner login test dùng context mới (username+password merchant tự đặt — PF1:
 * login = ShopERP host app2, slug chỉ storefront).
 */

const API2 = 'https://api2.khachvip.online';
const APP2 = 'https://app2.khachvip.online';
const KL = 'https://diemthuong2.khachvip.online';
const ADMIN_STATE = 'auth/rv-onboarding-admin.json';
const SYSLAB_PASSWORD = 'RvTest@2026!';

const ts = Date.now().toString(36);
const RUN = {
  shopName: `RV Onboard Shop ${ts}`,
  rongName: `RV Onboard Rong ${ts}`,
  phone: `09${Date.now().toString().slice(-8)}`,
  rongPhone: `08${(Date.now() + 1).toString().slice(-8)}`,
  username: `rvshop${ts}`,
  rongUsername: `rvrong${ts}`,
};

let signupId = '';
let rongSignupId = '';
let verifiedSlug = '';

test.describe.configure({ mode: 'serial' });
test.setTimeout(180000);

test.beforeAll(async () => {
  // Sysadmin storageState — pattern rv-metering-auth.cjs
  const browser = await chromium.launch({
    headless: true,
    args: [
      '--host-resolver-rules=' +
        'MAP app2.khachvip.online 136.85.94.119, MAP api2.khachvip.online 136.85.94.119, ' +
        'MAP api.khachvip.online 136.85.94.119, MAP diemthuong2.khachvip.online 136.85.94.119',
    ],
  });
  const context = await browser.newContext({ ignoreHTTPSErrors: true });
  const login = await context.request.post(`${APP2}/api/platform/login`, {
    data: { Username: 'sysadmin@vanan.vn', Password: '2026@vanan' },
    headers: { 'Content-Type': 'application/json' },
  });
  if (!login.ok()) {
    console.error(`sysadmin login fail: ${login.status()}`);
    process.exit(1);
  }
  const page = await context.newPage();
  await page.goto(`${APP2}/admin/tenants`, { waitUntil: 'domcontentloaded', timeout: 60000 });
  await context.storageState({ path: ADMIN_STATE });
  console.log(`saved ${ADMIN_STATE}`);
  await browser.close();
});

function signupPayload(caseType: number, name: string, phone: string, username: string, extra: object = {}) {
  return {
    case: caseType,               // 1 = SmallShopNoLicense · 2 = StreetVendorUnregistered
    name,
    address: '123 Duong RV Test, Phuong 1, Quan 1, TP.HCM',
    businessType: 2,              // HouseholdBusiness
    hkdGroup: caseType === 1 ? 1 : null,
    gpdkInfo: null,
    signName: null,
    latitude: null,
    longitude: null,
    photoUrls: null,
    productLines: [{ name: 'Ca phe sua da', price: 25000, lineNo: 1 }],
    contactName: 'RV Tester',
    contactPhone: phone,
    contactEmail: null,
    ownerUsername: username,
    ownerPassword: SYSLAB_PASSWORD,
    turnstileToken: null,
    honeypotWebsite: null,
    src: 'direct',
    ...extra,
  };
}

async function submitSignup(request: any, payload: object) {
  const resp = await request.post(`${API2}/api/v1/merchant-signups`, { data: payload });
  return resp;
}

// ── 1. API guards + flags (L2) ────────────────────────────────────────────
test('L2: flags public + admin guards', async ({ request }) => {
  const flags = await request.get(`${API2}/api/v1/platform/onboarding-flags`);
  expect(flags.ok()).toBeTruthy();
  const body = await flags.json();
  expect(body.selfServeEnabled).toBe(true);
  expect(body.directoryCta).toBe(true);
  expect(body.qrEntry).toBe(true);
  expect(body.deeplinkPrefill).toBe(true);

  // maxRedirects:0 — raw auth challenge (cookie flow → 302; bearer → 401/403)
  const putNoAuth = await request.put(`${API2}/api/v1/platform/onboarding-flags`, {
    data: { selfServeEnabled: false, directoryCta: true, qrEntry: true, deeplinkPrefill: true },
    maxRedirects: 0,
  });
  expect([401, 403, 302]).toContain(putNoAuth.status());

  const detailNoAuth = await request.get(`${API2}/api/v1/tenants/${crypto.randomUUID()}/signup-detail`, { maxRedirects: 0 });
  expect([401, 403, 302]).toContain(detailNoAuth.status());

  const otp = await request.post(`${API2}/api/v1/merchant-signups/otp/send`);
  expect(otp.status()).toBe(501); // P4 stub
});

// ── 2. Self-serve submit (Case B) + status Pending ────────────────────────
test('submit Case B via API → status page Pending', async ({ request, page }) => {
  const resp = await submitSignup(request, signupPayload(1, RUN.shopName, RUN.phone, RUN.username));
  expect(resp.ok(), `submit failed: ${resp.status()} ${await resp.text()}`).toBeTruthy();
  const body = await resp.json();
  expect(body.signupId).toBeTruthy();
  signupId = body.signupId;
  console.log(`signupId=${signupId} tenant=${RUN.shopName}`);

  await page.goto(`${KL}/onboard/status?signupId=${signupId}&phone=${encodeURIComponent(RUN.phone)}`);
  await page.getByRole('button', { name: /Tra cứu/ }).click();
  await expect(page.getByRole('heading', { name: 'Đang chờ đội ngũ duyệt' })).toBeVisible({ timeout: 30000 });
});

// ── 3. Sysadmin review + verify-signup ────────────────────────────────────
test('sysadmin review hồ sơ → Duyệt & Kích hoạt → status Active', async ({ browser }) => {
  test.skip(!signupId, 'no signupId from previous test');
  const context = await browser.newContext({ ignoreHTTPSErrors: true, storageState: ADMIN_STATE });
  const page = await context.newPage();

  await page.goto(`${APP2}/admin/tenants`, { waitUntil: 'networkidle', timeout: 90000 });
  await page.locator('button', { hasText: 'Pending' }).first().click();
  await page.waitForLoadState('networkidle');

  const row = page.locator('tr', { hasText: RUN.shopName }).first();
  await expect(row).toBeVisible({ timeout: 30000 });
  await expect(row.getByText('Self-serve')).toBeVisible();
  await row.getByRole('button', { name: /Hồ sơ/ }).click();

  // Detail modal — assert hồ sơ đầy đủ, KHÔNG lộ password
  await expect(page.getByText('Hồ sơ self-serve').first()).toBeVisible({ timeout: 30000 });
  await expect(page.getByText(RUN.shopName).last()).toBeVisible();
  await expect(page.locator('code').filter({ hasText: RUN.username }).first()).toBeVisible();
  await expect(page.locator('td', { hasText: 'RV Tester' }).first()).toBeVisible();
  await expect(page.locator('td', { hasText: 'Ca phe sua da' }).first()).toBeVisible(); // menu OCR line
  await expect(page.getByText(/mật khẩu được bảo vệ/i).first()).toBeVisible();

  // Mở form verify
  await page.getByRole('button', { name: /Duyệt & Kích hoạt/ }).first().click();
  await expect(page.locator('#signup-displayname')).toBeVisible();
  // Chọn ShopERP instance đầu tiên có value
  const instSelect = page.locator('#signup-shopinstance');
  const options = await instSelect.locator('option').all();
  let picked = '';
  for (const opt of options) {
    const v = await opt.getAttribute('value');
    if (v) { picked = v; break; }
  }
  expect(picked, 'no ShopERP instance option').toBeTruthy();
  await instSelect.selectOption(picked);
  // "Dùng mật khẩu merchant" mặc định checked (_useMerchantPassword=true)

  await page.getByRole('button', { name: /Duyệt & Kích hoạt/ }).last().click();
  await expect(page.getByText('Đã duyệt — tenant chuyển Active.')).toBeVisible({ timeout: 60000 });
  await expect(page.getByText('Slug cửa hàng:')).toBeVisible();
  await expect(page.getByText('Owner username:')).toBeVisible();
  await expect(page.locator('.verify-credentials code').filter({ hasText: RUN.username })).toBeVisible();

  const slugText = await page.locator('.verify-credentials code').first().textContent();
  verifiedSlug = (slugText ?? '').trim();
  console.log(`verified slug=${verifiedSlug} username=${RUN.username}`);

  // Status page → Active
  const statusPage = await context.newPage();
  await statusPage.goto(`${KL}/onboard/status?signupId=${signupId}&phone=${encodeURIComponent(RUN.phone)}`);
  await statusPage.getByRole('button', { name: /Tra cứu/ }).click();
  await expect(statusPage.getByRole('heading', { name: 'Đăng ký đã được duyệt' })).toBeVisible({ timeout: 30000 });
  if (verifiedSlug) {
    await expect(statusPage.locator('code').filter({ hasText: verifiedSlug }).first()).toBeVisible();
  }
  await context.close();
});

// ── 4. Owner login (PF1: app2 host, username+password) ────────────────────
test('owner login app2 bằng credentials merchant', async ({ browser }) => {
  test.skip(!signupId, 'no signupId');
  const context = await browser.newContext({ ignoreHTTPSErrors: true });
  const page = await context.newPage();
  await page.goto(`${APP2}/Login`, { waitUntil: 'networkidle', timeout: 60000 });
  await page.locator('#username').fill(RUN.username);
  await page.locator('#password').fill(SYSLAB_PASSWORD);
  await page.locator('button[type="submit"]').click();
  await page.waitForURL((url) => !url.pathname.toLowerCase().includes('login'), { timeout: 90000 })
    .catch(() => console.log(`owner login redirect: ${page.url()}`));
  expect(page.url().toLowerCase()).not.toContain('/login');
  await context.close();
});

// ── 5. Reject path (Case Rong) ────────────────────────────────────────────
test('reject path: submit Case Rong → sysadmin Từ chối → status Rejected', async ({ request, browser }) => {
  const resp = await submitSignup(request, signupPayload(2, RUN.rongName, RUN.rongPhone, RUN.rongUsername, {
    address: null, hkdGroup: null,
  }));
  expect(resp.ok(), `rong submit failed: ${resp.status()} ${await resp.text()}`).toBeTruthy();
  const body = await resp.json();
  rongSignupId = body.signupId;
  console.log(`rong signupId=${rongSignupId} tenant=${RUN.rongName}`);

  const context = await browser.newContext({ ignoreHTTPSErrors: true, storageState: ADMIN_STATE });
  const page = await context.newPage();
  await page.goto(`${APP2}/admin/tenants`, { waitUntil: 'networkidle', timeout: 90000 });
  await page.locator('button', { hasText: 'Pending' }).first().click();
  await page.waitForLoadState('networkidle');

  const row = page.locator('tr', { hasText: RUN.rongName }).first();
  await expect(row).toBeVisible({ timeout: 30000 });
  await row.getByRole('button', { name: /Hồ sơ/ }).click();
  await expect(page.getByText('Hồ sơ self-serve').first()).toBeVisible({ timeout: 30000 });
  // Flag "Chưa đăng ký kinh doanh" phải hiện trong modal
  await expect(page.getByText(/Chưa đăng ký kinh doanh/).first()).toBeVisible();

  await page.getByRole('button', { name: 'Từ chối', exact: true }).click();
  await expect(page.getByText('Từ chối tenant').first()).toBeVisible({ timeout: 15000 });
  await page.locator('textarea').last().fill('RV test reject — dữ liệu test tự động');
  await page.getByRole('button', { name: /Xác nhận từ chối/ }).click();
  await page.waitForTimeout(3000);
  await context.close();

  // Status page → Rejected
  const statusContext = await browser.newContext({ ignoreHTTPSErrors: true });
  const statusPage = await statusContext.newPage();
  await statusPage.goto(`${KL}/onboard/status?signupId=${rongSignupId}&phone=${encodeURIComponent(RUN.rongPhone)}`);
  await statusPage.getByRole('button', { name: /Tra cứu/ }).click();
  await expect(statusPage.getByRole('heading', { name: 'Đăng ký không được chấp nhận' })).toBeVisible({ timeout: 30000 });
  await statusContext.close();
});

// ── 6. Regression: /onboard wizard + Track A OCR wizard + flags UI ────────
test('regression: /onboard wizard render + /admin/onboarding-ocr + flags card', async ({ browser }) => {
  // KhachLink /onboard — 3 case cards
  const klContext = await browser.newContext({ ignoreHTTPSErrors: true });
  const klPage = await klContext.newPage();
  await klPage.goto(`${KL}/onboard`, { waitUntil: 'networkidle', timeout: 90000 });
  await expect(klPage.getByText('Tôi có giấy phép kinh doanh')).toBeVisible({ timeout: 30000 });
  await expect(klPage.getByText('Tôi có cửa hàng, chưa có giấy phép')).toBeVisible();
  await expect(klPage.getByText('Tôi bán hàng rong / chưa đăng ký kinh doanh')).toBeVisible();
  await klContext.close();

  // Sysadmin: Track A wizard vẫn load + flags card trên /admin/tenants
  const adminContext = await browser.newContext({ ignoreHTTPSErrors: true, storageState: ADMIN_STATE });
  const adminPage = await adminContext.newPage();
  await adminPage.goto(`${APP2}/admin/onboarding-ocr`, { waitUntil: 'networkidle', timeout: 90000 });
  await expect(adminPage.locator('body')).toContainText(/onboarding|giấy phép|biển hiệu/i, { timeout: 30000 });
  await adminPage.goto(`${APP2}/admin/tenants`, { waitUntil: 'networkidle', timeout: 90000 });
  await expect(adminPage.locator('body')).toContainText(/Onboarding|tự phục vụ|self-serve/i, { timeout: 30000 });
  await adminContext.close();
});
