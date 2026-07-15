import { test, expect } from '@playwright/test';

const BASE_URL = 'https://khachvip.online';

test('auth-fix — login persists after 10s', async ({ browser }) => {
  const context = await browser.newContext({ baseURL: BASE_URL, ignoreHTTPSErrors: true });
  const page = await context.newPage();

  const allLogs: string[] = [];
  page.on('console', msg => allLogs.push(`[${msg.type()}] ${msg.text()}`));
  page.on('framenavigated', frame => allLogs.push(`[nav] ${frame.url()}`));

  try {
    // Step 1: Go to login page
    await page.goto(`${BASE_URL}/Login`);
    await page.waitForLoadState('networkidle');
    await page.waitForTimeout(2000);

    // Step 2: Login via API
    const loginResp = await context.request.post(`${BASE_URL}/api/platform/login`, {
      data: { Username: 'sysadmin@vanan.vn', Password: '2026@vanan' },
      headers: { 'Content-Type': 'application/json' },
    });
    console.log('Login status:', loginResp.status());

    // Step 3: Navigate to /products (should NOT redirect to /Login)
    await page.goto(`${BASE_URL}/products`);
    await page.waitForLoadState('networkidle');
    await page.waitForTimeout(3000);

    const url1 = page.url();
    const title1 = await page.title();
    console.log('After login — URL:', url1, 'Title:', title1);

    // Should be on /products, NOT /Login
    expect(url1).not.toContain('/Login');

    // Step 4: Wait 10 seconds (the 3-5s issue window)
    await page.waitForTimeout(10000);

    const url2 = page.url();
    console.log('After 10s — URL:', url2);

    // Should still be on /products, NOT redirected to /Login
    expect(url2).not.toContain('/Login');

    // Step 5: Check cookies
    const cookies = await context.cookies();
    const authCookie = cookies.find(c => c.name === '.VanAn.Auth');
    console.log('Auth cookie present:', !!authCookie);
    console.log('Auth cookie domain:', authCookie?.domain);
    console.log('All cookies:', JSON.stringify(cookies.map(c => ({ name: c.name, domain: c.domain }))));

    // Step 6: Reload page — should still be authenticated
    await page.reload();
    await page.waitForLoadState('networkidle');
    await page.waitForTimeout(3000);

    const url3 = page.url();
    console.log('After reload — URL:', url3);
    expect(url3).not.toContain('/Login');

    console.log('Navigations:', JSON.stringify(allLogs.filter(l => l.includes('[nav]')).slice(0, 10)));
  } finally {
    await context.close();
  }
});
